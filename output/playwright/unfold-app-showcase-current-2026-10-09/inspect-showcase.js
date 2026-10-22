async(page)=>{
  const checks=[],errors=[],failed=[];
  const url='http://127.0.0.1:4190/app-showcase.html';
  const root='/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-app-showcase-current-2026-10-09';
  const check=(name,pass,detail)=>checks.push({name,pass:Boolean(pass),detail});
  page.on('pageerror',error=>errors.push(String(error)));
  page.on('response',response=>{if(response.status()>=400)failed.push({url:response.url(),status:response.status()})});
  await page.emulateMedia({reducedMotion:'reduce'});
  await page.setViewportSize({width:1440,height:1000});
  await page.goto(url);
  await page.evaluate(()=>document.fonts.ready);

  for(const theme of ['plum','oat','sage','midnight']){
    await page.locator(`[data-theme="${theme}"]`).click();
    const state=await page.evaluate(theme=>({pressed:[...document.querySelectorAll('[data-theme]')].filter(b=>b.getAttribute('aria-pressed')==='true').map(b=>b.dataset.theme),visible:[...document.querySelectorAll('.theme-preview')].filter(e=>!e.hidden).map(e=>e.id),synchronized:[...document.querySelectorAll('[data-asset]')].every(e=>(e.src||e.href).includes(`/themes/${theme}/`))}),theme);
    check(`theme ${theme} selection and feature capture sync`,state.pressed.join()==theme&&state.visible.join()==`theme-${theme}`&&state.synchronized,state);
    for(const screen of ['home','custom-pet','speech','review']){
      await page.locator(`[data-screen="${screen}"]`).click();
      await page.locator('#hero-screen').evaluate(img=>img.decode());
      const shot=await page.evaluate(({theme,screen})=>({pressed:[...document.querySelectorAll('[data-screen]')].filter(b=>b.getAttribute('aria-pressed')==='true').map(b=>b.dataset.screen),src:document.querySelector('#hero-screen').src,href:document.querySelector('.screen-link').href,complete:document.querySelector('#hero-screen').naturalWidth>0}),{theme,screen});
      check(`${theme} / ${screen} real capture`,shot.pressed.join()==screen&&shot.src.endsWith(`/themes/${theme}/${screen}.png`)&&shot.href===shot.src&&shot.complete,shot);
    }
  }
  await page.getByRole('button',{name:'다음 앱 화면',exact:true}).click();
  check('next gallery wraps to timer',await page.locator('[data-screen="home"]').getAttribute('aria-pressed')==='true');
  await page.getByRole('button',{name:'이전 앱 화면',exact:true}).click();
  check('previous gallery wraps to records',await page.locator('[data-screen="review"]').getAttribute('aria-pressed')==='true');

  await page.locator('.screen-link').click();
  check('lightbox opens with selected capture',await page.locator('dialog').evaluate(d=>d.open)&&await page.locator('#dialog-image').getAttribute('src')===await page.locator('#hero-screen').getAttribute('src').then(s=>new URL(s,url).href));
  await page.getByRole('button',{name:'닫기',exact:true}).click();
  await page.waitForFunction(()=>!document.querySelector('dialog').open&&!document.body.classList.contains('dialog-open')&&document.activeElement.classList.contains('screen-link'));
  check('lightbox close restores focus and scroll',true);
  await page.locator('.screen-link').click();
  await page.keyboard.press('Escape');
  await page.waitForFunction(()=>!document.querySelector('dialog').open&&!document.body.classList.contains('dialog-open'));
  check('lightbox Escape closes',true);
  await page.locator('.screen-link').click();
  await page.mouse.click(5,5);
  await page.waitForFunction(()=>!document.querySelector('dialog').open);
  check('lightbox backdrop closes',true);

  await page.getByRole('tab',{name:'Windows',exact:true}).click();
  check('Windows panel selected',await page.locator('#download-windows').isVisible()&&!(await page.locator('#download-mac').isVisible()));
  await page.getByRole('tab',{name:'Windows',exact:true}).press('ArrowLeft');
  check('OS arrow key activates and focuses macOS',await page.locator('#download-mac').isVisible()&&await page.locator('#tab-mac').evaluate(e=>e===document.activeElement));
  await page.locator('#tab-mac').press('End');
  check('OS End selects Windows',await page.locator('#tab-windows').getAttribute('aria-selected')==='true');
  await page.locator('#tab-windows').press('Home');
  check('OS Home selects macOS',await page.locator('#tab-mac').getAttribute('aria-selected')==='true');
  const downloads=await page.locator('.download-panel .btn').evaluateAll(links=>links.map(a=>a.href));
  check('download links preserve the 3 current v1.1.2 release assets',downloads.length===3&&downloads.every(href=>href.startsWith('https://github.com/C-Caffe1ne/Unfold/releases/download/v1.1.2-beta/')),downloads);
  await page.locator('.faq-list summary').first().click();
  check('FAQ opens with current Beta conditions',await page.locator('.faq-list details').first().evaluate(d=>d.open&&d.textContent.includes('발급 코드를')));
  await page.locator('.faq-list summary').first().press('Enter');
  check('FAQ keyboard closes',!(await page.locator('.faq-list details').first().evaluate(d=>d.open)));

  await page.setViewportSize({width:390,height:844});
  await page.goto(url);
  await page.getByRole('button',{name:'메뉴 열기',exact:true}).click();
  check('mobile navigation opens',await page.locator('.menu-toggle').getAttribute('aria-expanded')==='true'&&await page.locator('.rail').isVisible());
  await page.keyboard.press('Escape');
  await page.waitForFunction(()=>document.activeElement.classList.contains('menu-toggle'));
  check('mobile menu Escape restores toggle focus',await page.locator('.menu-toggle').getAttribute('aria-expanded')==='false');
  await page.getByRole('button',{name:'메뉴 열기',exact:true}).click();
  await page.locator('.rail-link[href="#pricing-overview"]').click();
  await page.waitForFunction(()=>document.activeElement.id==='pricing-overview');
  check('mobile section navigation closes menu and focuses target',!(await page.locator('.rail').isVisible())&&await page.locator('.menu-toggle').getAttribute('aria-expanded')==='false'&&page.url().endsWith('#pricing-overview'));
  await page.getByRole('button',{name:'메뉴 열기',exact:true}).click();
  await page.mouse.click(350,750);
  await page.waitForFunction(()=>document.querySelector('.menu-toggle').getAttribute('aria-expanded')==='false');
  check('mobile outside click closes menu',await page.locator('.menu-toggle').getAttribute('aria-expanded')==='false');

  for(const theme of ['plum','oat','sage','midnight']){
    await page.locator(`[data-theme="${theme}"]`).click();
    await page.locator(`#theme-${theme} img`).evaluate(img=>img.decode());
    const mobileTheme=await page.locator(`#theme-${theme}`).evaluate(e=>{const r=e.getBoundingClientRect();return {left:r.left,right:r.right,width:r.width,height:r.height,overflow:Math.max(0,document.documentElement.scrollWidth-document.documentElement.clientWidth)}});
    check(`mobile ${theme} theme occupies full row`,mobileTheme.width>300&&mobileTheme.height>200&&mobileTheme.left>=0&&mobileTheme.right<=390&&mobileTheme.overflow===0,mobileTheme);
  }
  await page.goto(`${url}#install`);
  await page.reload();
  await page.waitForFunction(()=>scrollY===0&&!location.hash);
  check('reload restores page top',true);

  await page.setViewportSize({width:1440,height:1000});
  await page.emulateMedia({reducedMotion:'no-preference'});
  await page.goto(url);
  await page.evaluate(()=>document.fonts.ready);
  const motion=await page.evaluate(async()=>{const {ScrollTrigger}=await import('./vendor/gsap.js');return ScrollTrigger.getAll().map(t=>({pin:Boolean(t.pin),scrub:Boolean(t.vars.scrub)}))});
  check('desktop runs pinning and text scrubbing',motion.some(t=>t.pin)&&motion.some(t=>t.scrub),motion);
  await page.locator('.features').evaluate(e=>scrollTo({top:e.offsetTop+250,behavior:'instant'}));
  await page.waitForFunction(()=>getComputedStyle(document.querySelector('[data-pin-title]')).position==='fixed');
  check('desktop feature heading actually pins',true);
  await page.screenshot({path:`${root}/showcase-features-motion.png`});
  await page.emulateMedia({reducedMotion:'reduce'});
  await page.waitForFunction(()=>!document.querySelector('.pin-spacer'));
  const reducedTriggers=await page.evaluate(async()=>{const {ScrollTrigger}=await import('./vendor/gsap.js');return ScrollTrigger.getAll().length});
  check('reduced motion removes scroll pinning and scrubbing',reducedTriggers===0&&await page.locator('.format-band>div').evaluate(e=>getComputedStyle(e).animationName==='none'),reducedTriggers);
  const sourceClaims=await page.evaluate(()=>({features:document.querySelectorAll('.feature-card').length,featureBodyCopy:[...document.querySelectorAll('.feature-copy p')].every(p=>p.classList.contains('eyebrow')),removed:document.body.textContent.includes('나만의 펫 미리보기'),beta:document.querySelector('.beta-version').textContent,testing:document.querySelector('.price-card').textContent.includes('앱 이용 권한을 제공하지 않습니다')}));
  check('source feature and sale state constraints',sourceClaims.features===3&&sourceClaims.featureBodyCopy&&!sourceClaims.removed&&sourceClaims.beta==='Beta v1.1.2'&&sourceClaims.testing,sourceClaims);
  check('browser has no JavaScript errors or failed resources',errors.length===0&&failed.length===0,{errors,failed});
  return {checks,errors,failed,passed:checks.filter(c=>c.pass).length,total:checks.length};
}
