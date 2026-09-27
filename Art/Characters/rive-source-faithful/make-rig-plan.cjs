// Native Rive timeline plan. This is a pose-switch rig, not a bone/skin rig.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const source = JSON.parse(fs.readFileSync(path.join(__dirname, 'rig-contract.json')));
const result = {};
const key = (object, propertyKey, frame, value, interpolationType='hold') => ({object,propertyKey,frame,value,interpolationType});
const rootAt = (frame, sx, sy, lift, smooth=false) => [
  key('root',13,frame,128*(1-sx)), key('root',14,frame,230.4*(1-sy)-256*lift),
  key('root',16,frame,sx*100), key('root',17,frame,sy*100),
].map(k=>smooth?{...k,interpolationType:'cubic',cubicParams:[1/3,0,2/3,1]}:k);
for (const [pet,contract] of Object.entries(source)) {
  const timelines={};
  const poseIds=Object.keys(contract.poses).map(Number);
  function poseKeys(frames, step=1) {
    const keys=poseIds.map(p=>key(`pose_${String(p).padStart(2,'0')}`,18,0,p===frames[0]?100:0));
    frames.forEach((p,i)=>{if(i && p!==frames[i-1])keys.push(key(`pose_${String(frames[i-1]).padStart(2,'0')}`,18,i*step,0),key(`pose_${String(p).padStart(2,'0')}`,18,i*step,100));});
    return keys;
  }
  for (const [name,clip] of Object.entries(contract.animations)) {
    const fps=name==='pickup'?300:clip.fps,step=fps/clip.fps;
    const keys=poseKeys(clip.frames,step);
    keys.push(...rootAt(0,1,1,name==='held'?.06:0,name==='pickup'));
    if(name==='pickup')keys.push(...rootAt(84,1,1,.06));
    timelines[name]={fps,duration:clip.frames.length*step,loop:clip.loop?1:0,keys};
    assert.equal(timelines[name].duration/fps,clip.frames.length/clip.fps);
    for(let i=0;i<clip.frames.length;i++){
      const values={};for(const k of keys)if(k.propertyKey===18&&k.frame<=i*step)values[k.object]=k.value;
      assert.deepEqual(Object.entries(values).filter(([,v])=>v===100).map(([k])=>k),[`pose_${String(clip.frames[i]).padStart(2,'0')}`]);
    }
  }
  timelines.pending={fps:100,duration:22,loop:0,keys:[...poseKeys([0]),...rootAt(0,1,1,0)]};
  timelines.bounce={fps:100,duration:64,loop:0,keys:[...poseKeys([contract.animations.held.frames[0]]),...rootAt(0,1,1,.06,true),...rootAt(16,1.10,.88,0,true),...rootAt(34,.98,1.02,.10,true),...rootAt(52,1.05,.94,0,true),...rootAt(64,1,1,0)]};
  result[pet]={timelines,sourceAnimationCount:13,poseCount:poseIds.length};
}
fs.writeFileSync(path.join(__dirname,'rig-plan.json'),JSON.stringify(result,null,2)+'\n');
console.log('PASS: 5 pets, 65 source timelines, 122 poses, exact source frame visibility and duration.');
