async (page) => {
  const errors=[];
  page.on('pageerror',error=>errors.push(String(error)));
  const metrics=[];
  await page.emulateMedia({reducedMotion:'reduce'});
  for(const slug of ['studio','night','club','daylight']){
    for(const width of [320,390,768,1440]){
      await page.setViewportSize({width,height:width===390?844:1000});
      await page.goto(`http://127.0.0.1:4188/${slug}.html`);
      await page.evaluate(()=>document.fonts.ready);
      await page.locator('.hero img').evaluateAll(images=>Promise.all(images.map(img=>img.decode().catch(()=>{}))));
      metrics.push(await page.evaluate(({slug,width})=>{
        const h1=document.querySelector('h1');const positions=[];const lines=new Set();
        const walker=document.createTreeWalker(h1,NodeFilter.SHOW_TEXT);let node;
        while(node=walker.nextNode())for(let i=0;i<node.textContent.length;i++){
          if(!node.textContent[i].trim())continue;
          const range=document.createRange();range.setStart(node,i);range.setEnd(node,i+1);
          for(const r of range.getClientRects())if(r.width>0){positions.push({left:r.left,right:r.right});lines.add(Math.round(r.top))}
        }
        return {slug,width,overflow:document.documentElement.scrollWidth-width,lines:lines.size,headingLeft:Math.min(...positions.map(r=>r.left)),headingRight:Math.max(...positions.map(r=>r.right)),heroHeight:document.querySelector('.hero').getBoundingClientRect().height,missingImages:[...document.images].filter(img=>!img.complete||!img.naturalWidth).map(img=>img.src)};
      },{slug,width}));
      if(width===1440||width===390){
        const mode=width===1440?'desktop':'mobile';
        await page.screenshot({path:`/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-sales-concepts-2026-10-09/${slug}-${mode}.png`});
        if(width===1440){
          await page.locator('img').evaluateAll(images=>Promise.all(images.map(img=>img.decode().catch(()=>{}))));
          await page.screenshot({path:`/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-sales-concepts-2026-10-09/${slug}-full.png`,fullPage:true});
        }
      }
    }
  }
  await page.evaluate(data=>{window.__conceptCapture=data},{metrics,errors});
}
