async (page) => {
  const checks=[];const errors=[];
  const check=(slug,name,pass,detail)=>checks.push({slug,name,pass:Boolean(pass),detail});
  page.on('pageerror',error=>errors.push(String(error)));
  for(const slug of ['studio','night','club','daylight']){
    await page.emulateMedia({reducedMotion:'reduce'});
    await page.setViewportSize({width:1440,height:1000});
    await page.goto(`http://127.0.0.1:4188/${slug}.html`);
    await page.evaluate(()=>document.fonts.ready);
    await page.waitForFunction(()=>window.__conceptMotion);
    check(slug,'reduced motion',await page.evaluate(()=>window.__conceptMotion.triggers()===0));
    check(slug,'GLB support present',await page.locator('body').innerText().then(text=>text.includes('3D(GLB)')));
    check(slug,'removed preview absent',await page.locator('body').innerText().then(text=>!text.includes('펫 미리보기')&&!text.includes('펫 팩 미리보기')));
    await page.locator('.hero a[href="#showcase"]').click();
    check(slug,'hero to app screens',new URL(page.url()).hash==='#showcase');
    await page.locator('[data-next]').click();
    check(slug,'carousel review',await page.locator('[data-screen-name]').innerText()==='휴식 기록');
    await page.locator('[data-next]').click();
    await page.locator('[data-screen]').evaluate(img=>img.decode());
    check(slug,'carousel reminder',await page.locator('[data-screen-name]').innerText()==='휴식 알림');
    const ratio=await page.locator('[data-screen]').evaluate(img=>{const r=img.getBoundingClientRect();return {rendered:r.width/r.height,natural:img.naturalWidth/img.naturalHeight}});
    check(slug,'reminder aspect ratio',Math.abs(ratio.rendered-ratio.natural)<.01,ratio);
    await page.locator('[data-next]').click();
    check(slug,'carousel wraps',await page.locator('[data-screen-name]').innerText()==='타이머');
    await page.locator('[data-prev]').focus();await page.keyboard.press('Enter');
    check(slug,'keyboard carousel',await page.locator('[data-screen-name]').innerText()==='휴식 알림');
    if(slug!=='studio'){
      await page.locator('.accordion-trigger').nth(2).click();
      check(slug,'desktop accordion',await page.locator('.accordion-trigger').nth(2).getAttribute('aria-expanded')==='true');
    }
    await page.locator('[data-purchase]').click();
    check(slug,'purchase dialog opens',await page.locator('dialog').evaluate(el=>el.open));
    check(slug,'no live purchase',await page.locator('dialog').innerText().then(text=>text.includes('이 페이지에서 결제를 진행하지 않습니다')));
    await page.keyboard.press('Escape');
    check(slug,'dialog Escape and focus restore',await page.evaluate(()=>!document.querySelector('dialog').open&&document.activeElement.hasAttribute('data-purchase')));
    await page.locator('[data-purchase]').click();await page.getByRole('button',{name:'구매 안내 닫기'}).click();
    check(slug,'dialog close button',await page.locator('dialog').evaluate(el=>!el.open));

    await page.setViewportSize({width:390,height:844});
    await page.goto(`http://127.0.0.1:4188/${slug}.html`);
    await page.waitForFunction(()=>window.__conceptMotion);
    await page.getByRole('button',{name:'메뉴 열기'}).click();
    check(slug,'mobile menu opens',await page.locator('.menu-toggle').getAttribute('aria-expanded')==='true'&&await page.locator('.nav-links').isVisible());
    await page.keyboard.press('Escape');
    check(slug,'mobile menu Escape',await page.locator('.menu-toggle').getAttribute('aria-expanded')==='false'&&await page.locator('.menu-toggle').getAttribute('aria-label')==='메뉴 열기');
    await page.getByRole('button',{name:'메뉴 열기'}).click();await page.locator('.nav-links a[href="#features"]').click();
    check(slug,'mobile navigation closes menu',new URL(page.url()).hash==='#features'&&await page.locator('.menu-toggle').getAttribute('aria-expanded')==='false'&&await page.locator('.menu-toggle').getAttribute('aria-label')==='메뉴 열기');
    if(slug!=='studio'){
      await page.locator('.accordion-trigger').nth(1).focus();await page.keyboard.press('Enter');
      check(slug,'mobile keyboard accordion',await page.locator('.accordion-trigger').nth(1).getAttribute('aria-expanded')==='true'&&await page.locator('.accordion-panel').nth(1).isVisible()&&!await page.locator('.accordion-panel').nth(0).isVisible());
    }
    await page.locator('[data-purchase]').click();
    const dialogFits=await page.locator('dialog').evaluate(el=>{const r=el.getBoundingClientRect();return r.left>=0&&r.right<=window.innerWidth&&el.scrollWidth<=el.clientWidth});
    check(slug,'mobile dialog fits',dialogFits);
    if(slug==='daylight')await page.screenshot({path:'/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-sales-concepts-2026-10-09/mobile-purchase-dialog.png'});
    await page.keyboard.press('Escape');

    await page.emulateMedia({reducedMotion:'no-preference'});
    await page.setViewportSize({width:1440,height:1000});
    await page.goto(`http://127.0.0.1:4188/${slug}.html`);
    await page.evaluate(()=>document.fonts.ready);
    await page.waitForFunction(()=>window.__conceptMotion?.triggers()>0);
    const motion=await page.evaluate(()=>({version:window.__conceptMotion.gsap,triggers:window.__conceptMotion.triggers(),pins:document.querySelectorAll('.pin-spacer').length}));
    check(slug,'GSAP motion initialized',motion.triggers>=2,motion);
    if(slug!=='daylight')check(slug,'scroll pins initialized',motion.pins>0,motion);
    await page.evaluate(()=>{const section=document.querySelector('#showcase');window.scrollTo({top:section.getBoundingClientRect().top+window.scrollY+220,behavior:'instant'})});
    await page.locator('img').evaluateAll(images=>Promise.all(images.map(img=>img.decode().catch(()=>{}))));
    await page.screenshot({path:`/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-sales-concepts-2026-10-09/${slug}-motion-showcase.png`});
  }
  await page.emulateMedia({reducedMotion:'reduce'});
  await page.setViewportSize({width:1600,height:1500});
  await page.goto('http://127.0.0.1:4188/');
  await page.evaluate(()=>document.fonts.ready);
  await page.locator('img').evaluateAll(images=>Promise.all(images.map(img=>img.decode())));
  check('board','four separate pages',await page.locator('.concept').count()===4);
  await page.screenshot({path:'/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-sales-concepts-2026-10-09/four-concepts-board.png',fullPage:true});
  await page.getByRole('button',{name:'모바일',exact:true}).click();
  await page.locator('[data-thumb]').evaluateAll(images=>Promise.all(images.map(img=>img.decode())));
  check('board','mobile comparison',await page.getByRole('button',{name:'모바일',exact:true}).getAttribute('aria-pressed')==='true'&&await page.locator('[data-thumb]').evaluateAll(images=>images.every(img=>img.src.endsWith('-mobile.png'))));
  await page.screenshot({path:'/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-sales-concepts-2026-10-09/four-concepts-mobile-board.png',fullPage:true});
  await page.getByRole('button',{name:'데스크톱',exact:true}).click();
  check('board','desktop comparison',await page.getByRole('button',{name:'데스크톱',exact:true}).getAttribute('aria-pressed')==='true');
  check('all','no JavaScript errors',errors.length===0,errors);
  await page.evaluate(data=>{window.__conceptInspection=data},{checks,errors,passed:checks.filter(check=>check.pass).length,total:checks.length});
}
