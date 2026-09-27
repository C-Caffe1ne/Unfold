const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = __dirname;
const repo = path.resolve(root, '../../..');
const contract = JSON.parse(fs.readFileSync(path.join(root, 'rig-contract.json')));
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');

(async () => {
  const report = { note: 'Geometry and palette comparison, not a claim of pixel-identical vector reconstruction. No Rive export.', pets: {} };
  const overview = [], motion = [];
  for (const [row, [pet, definition]] of Object.entries(contract).entries()) {
    const manifest = JSON.parse(fs.readFileSync(path.join(repo, 'Assets/Characters', pet, 'character.json')));
    if (JSON.stringify(manifest.animations) !== JSON.stringify(definition.animations)) throw Error(`${pet}: changed animations`);
    for (const [name, expected] of Object.entries(definition.sourceHashes)) {
      if (hash(path.join(repo, 'Assets/Characters', pet, name)) !== expected) throw Error(`${pet}: source changed: ${name}`);
    }
    const records = [];
    for (const [index, pose] of Object.entries(definition.poses)) {
      const svgFile = path.join(root, pet, pose.svg);
      const svg = fs.readFileSync(svgFile, 'utf8');
      if (/<image|data:image|https?:\/\/(?!www.w3.org)/.test(svg)) throw Error(`${svgFile}: raster/external reference`);
      const pngFile = path.join(root, pet, 'reference', `pose-${index.padStart(2, '0')}.png`);
      const a = await sharp(pngFile).ensureAlpha().raw().toBuffer();
      const b = await sharp(svgFile).ensureAlpha().raw().toBuffer();
      let intersection = 0, union = 0, error = 0;
      for (let i = 0; i < a.length; i += 4) {
        const aa = a[i + 3] / 255, ab = b[i + 3] / 255;
        const sa = aa >= 0.5, sb = ab >= 0.5;
        if (sa && sb) intersection++;
        if (sa || sb) {
          union++;
          for (let c = 0; c < 3; c++) error += Math.abs(a[i+c]*aa + 255*(1-aa) - b[i+c]*ab - 255*(1-ab));
        }
      }
      records.push({pose:Number(index), silhouetteIoU:intersection/union, subjectRgbMae:error/(union*3), compoundPaths:pose.paths});
    }
    for (const [column, file] of [`reference/pose-00.png`, 'poses/pose-00.svg'].entries()) {
      overview.push({input:await sharp(path.join(root, pet, file)).resize(320,320).png().toBuffer(),left:column*336,top:row*356+32});
    }
    const indices = [0, ...new Set([...definition.animations.pickup.frames.slice(1), ...definition.animations.held.frames, ...definition.animations.land.frames.filter(x=>x!==0)])];
    for (const [column,index] of indices.entries()) {
      for (let version=0;version<2;version++) {
        const file = `${version?'poses':'reference'}/pose-${String(index).padStart(2,'0')}.${version?'svg':'png'}`;
        motion.push({input:await sharp(path.join(root,pet,file)).resize(160,160).png().toBuffer(),left:column*160,top:row*336+version*160+16});
      }
    }
    report.pets[pet] = {poseCount:records.length, animationCount:Object.keys(definition.animations).length, sourceHashesMatch:true, records};
    console.log(pet,records.length,'poses',Math.min(...records.map(x=>x.silhouetteIoU)).toFixed(4),'minimum silhouette IoU');
  }
  const label = Buffer.from('<svg width="672" height="32"><text x="12" y="23" font-family="sans-serif" font-size="17">SERVICE PNG</text><text x="348" y="23" font-family="sans-serif" font-size="17">SOURCE-DERIVED SVG</text></svg>');
  await sharp({create:{width:672,height:1780,channels:4,background:'#f3ece0'}}).composite([...overview,{input:label,top:0,left:0}]).png().toFile(path.join(root,'comparison-idle.png'));
  await sharp({create:{width:1440,height:1680,channels:4,background:'#f3ece0'}}).composite(motion).png().toFile(path.join(root,'comparison-pointer.png'));
  fs.writeFileSync(path.join(root,'verification.json'),JSON.stringify(report,null,2)+'\n');
})().catch(error=>{console.error(error);process.exitCode=1;});
