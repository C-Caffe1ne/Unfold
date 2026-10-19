async (page) => {
  const failures = [];
  page.on('pageerror', error => failures.push(String(error)));
  const results = [];
  const cases = [];
  for (const width of [320, 375, 390, 768, 1050, 1100, 1440]) {
    for (const view of ['home', 'pricing', 'install']) cases.push({width, view});
  }
  for (const width of [320, 390, 1440]) {
    for (const view of ['dashboard', 'privacy', 'terms', 'refund', 'changelog', 'not-found']) cases.push({width, view});
  }
  await page.reload();
  await page.emulateMedia({reducedMotion: 'reduce'});
  for (const {width, view} of cases) {
    await page.setViewportSize({width, height: 1000});
    await page.goto('http://127.0.0.1:4174/#' + view);
    await page.locator('#' + view).waitFor({state:'visible'});
    await page.evaluate(() => document.fonts.ready);
    await page.evaluate(() => window.scrollTo({top:0, behavior:'instant'}));
    await page.waitForTimeout(150);
    results.push(await page.evaluate(({width, view}) => {
      const panel = document.getElementById(view);
      const heading = panel.querySelector('h1');
      const lines = new Set();
      const walker = document.createTreeWalker(heading, NodeFilter.SHOW_TEXT);
      let text;
      while (text = walker.nextNode()) {
        for (let n=0; n<text.textContent.length; n++) {
          if (!text.textContent[n].trim()) continue;
          const range=document.createRange();
          range.setStart(text,n); range.setEnd(text,n+1);
          for (const box of range.getClientRects()) if(box.width>0) lines.add(Math.round(box.top));
        }
      }
      return {
        width, view, title:document.title,
        overflow:Math.max(document.body.scrollWidth,document.documentElement.scrollWidth)-window.innerWidth,
        heading:heading.textContent, headingLines:lines.size,
        extraFeatureDescriptions:panel.querySelectorAll('#features .feature-desc, #features .check-list, #themes .section-desc').length,
        removedFeatureReferences:document.querySelectorAll('[href*="custom-pet-result"], [src*="custom-pet-result"], .feature-custom-result, .feature-custom-panels').length
      };
    }, {width, view}));
  }
  await page.setViewportSize({width:1440,height:1000});
  await page.goto('http://127.0.0.1:4174/');
  await page.evaluate(() => document.fonts.ready);
  const themes = [];
  for (const theme of ['oat','sage','midnight','plum']) {
    await page.locator('[data-theme-value="'+theme+'"]').click();
    await page.locator('#home .hero-shot img').evaluate(image => image.decode());
    themes.push(await page.evaluate(theme => ({theme, current:document.documentElement.dataset.theme, mismatched:[...document.querySelectorAll('[data-themed-asset]')].filter(el => !(el.src || el.href).includes('/themes/'+theme+'/')).length}),theme));
  }
  await page.evaluate(() => window.scrollTo({top:0,behavior:'instant'}));
  await page.screenshot({path:'/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/home-desktop.png'});
  await page.locator('#features').evaluate(el => window.scrollTo({top:el.getBoundingClientRect().top + window.scrollY - 88,behavior:'instant'}));
  await page.screenshot({path:'/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/features-desktop.png'});
  for (const img of await page.locator('#home img').all()) {
    if (await img.isVisible()) { await img.scrollIntoViewIfNeeded(); await img.evaluate(el => el.decode().catch(() => {})); }
  }
  await page.evaluate(() => window.scrollTo({top:0,behavior:'instant'}));
  await page.screenshot({path:'/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/home-full.png',fullPage:true});
  await page.setViewportSize({width:390,height:844});
  await page.goto('http://127.0.0.1:4174/');
  await page.evaluate(() => document.fonts.ready);
  await page.locator('#home .hero-shot img').evaluate(image => image.decode());
  await page.screenshot({path:'/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/home-mobile.png'});
  await page.locator('#features').evaluate(el => window.scrollTo({top:el.getBoundingClientRect().top + window.scrollY - 88,behavior:'instant'}));
  await page.locator('.feature-custom img').evaluate(image => image.decode());
  await page.screenshot({path:'/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/features-mobile.png'});
  await page.evaluate(payload => {window.__copyAudit = payload;}, {results, themes, failures});
}
