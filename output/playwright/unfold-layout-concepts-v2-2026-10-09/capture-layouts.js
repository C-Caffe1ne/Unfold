async (page) => {
 const root='/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-layout-concepts-v2-2026-10-09';const errors=[];const metrics=[];
 page.on('pageerror',error=>errors.push(String(error)));
 await page.emulateMedia({reducedMotion:'reduce'});
 for(const slug of ['magazine','canvas','journey','catalog'])for(const width of [320,390,768,1440]){
  await page.setViewportSize({width,height:width===390?844:1000});await page.goto(`http://127.0.0.1:4189/${slug}.html`);
  await page.evaluate(()=>document.fonts.ready);await page.waitForFunction(()=>window.__layoutMotion);
  await page.locator('img').evaluateAll(images=>Promise.all(images.map(img=>img.decode().catch(()=>{}))));
  metrics.push(await page.evaluate(({slug,width})=>{
   const h1=document.querySelector('h1');const ys=new Set();const rects=[];const walker=document.createTreeWalker(h1,NodeFilter.SHOW_TEXT);let node;
   while(node=walker.nextNode())for(let i=0;i<node.length;i++){if(!node.textContent[i].trim())continue;const range=document.createRange();range.setStart(node,i);range.setEnd(node,i+1);for(const r of range.getClientRects())if(r.width){ys.add(Math.round(r.top));rects.push({left:r.left,right:r.right})}}
   return {slug,width,overflow:document.documentElement.scrollWidth-window.innerWidth,headingLines:ys.size,headingLeft:Math.min(...rects.map(r=>r.left)),headingRight:Math.max(...rects.map(r=>r.right)),missingImages:[...document.images].filter(img=>!img.complete||!img.naturalWidth).map(img=>img.src),motionTriggers:window.__layoutMotion.triggers()};
  },{slug,width}));
  if(width===1440||width===390){await page.screenshot({path:`${root}/${slug}-${width===1440?'desktop':'mobile'}.png`});if(width===1440)await page.screenshot({path:`${root}/${slug}-full.png`,fullPage:true})}
 }
 return {metrics,errors};
}
