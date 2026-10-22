async(page)=>{
 const checks=[];const root='/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-layout-concepts-v2-2026-10-09';
 await page.emulateMedia({reducedMotion:'reduce'});await page.setViewportSize({width:390,height:844});
 for(const slug of ['magazine','canvas','journey','catalog']){
  await page.goto(`http://127.0.0.1:4189/${slug}.html`);await page.waitForFunction(()=>window.__layoutMotion);await page.getByRole('button',{name:'메뉴 열기',exact:true}).click();await page.locator('#nav-links [data-buy]').click();await page.getByRole('button',{name:'구매 안내 닫기',exact:true}).click();
  const pass=await page.waitForFunction(()=>document.activeElement.classList.contains('menu-toggle'),{},{timeout:2500}).then(()=>true).catch(()=>false);
  checks.push({slug,name:'mobile dialog returns focus to menu',pass,detail:await page.evaluate(()=>({focused:document.activeElement.getAttribute('aria-label'),dialogOpen:document.querySelector('dialog').open}))});
 }
 await page.setViewportSize({width:1600,height:1450});await page.goto('http://127.0.0.1:4189/');await page.evaluate(()=>document.fonts.ready);await page.locator('img').evaluateAll(images=>Promise.all(images.map(img=>img.decode())));await page.getByLabel('색상 표시',{exact:true}).check();
 const pass=await page.waitForFunction(()=>getComputedStyle(document.querySelector('[data-thumb]')).filter==='none',{},{timeout:2500}).then(()=>true).catch(()=>false);
 checks.push({slug:'board',name:'optional color comparison',pass});await page.screenshot({path:`${root}/four-layouts-color-board.png`,fullPage:true});await page.getByLabel('색상 표시',{exact:true}).uncheck();await page.waitForFunction(()=>getComputedStyle(document.querySelector('[data-thumb]')).filter==='grayscale(1)');
 return {checks};
}
