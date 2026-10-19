
    import { gsap, ScrollTrigger } from './vendor/gsap.js';

    const themeNames = {plum:'유연한 라일락',oat:'다정한 오트',sage:'숨 고르는 숲',midnight:'밤의 버터'};
    const screens = [{key:'home',name:'타이머',alt:'현재 Unfold의 타이머와 함께하는 펫 화면'},{key:'custom-pet',name:'펫 관리',alt:'현재 펫 관리 화면의 이름 입력과 행동별 파일 선택'},{key:'speech',name:'휴식 알림',alt:'잠깐 쉬어 갈 시간을 알려주는 펫 말풍선'},{key:'review',name:'휴식 기록',alt:'최근 7일의 휴식 기록과 CSV 내보내기 화면'}];
    let currentTheme = 'plum';
    let currentScreen = 0;
    const heroImage = document.querySelector('#hero-screen');
    const heroLink = heroImage.closest('a');
    const menuButton = document.querySelector('.menu-toggle');
    const nav = document.querySelector('.rail');
    const mobile = matchMedia('(max-width:600px)');
    const reduced = matchMedia('(prefers-reduced-motion:reduce)');
    const assetPath = key => `./assets/themes/${currentTheme}/${key}.png`;

    function closeMenu(restore = false) {
      nav.classList.remove('is-open');
      menuButton.setAttribute('aria-expanded','false');
      menuButton.setAttribute('aria-label','메뉴 열기');
      if (restore && mobile.matches) menuButton.focus();
    }
    menuButton.addEventListener('click', () => {
      const open = menuButton.getAttribute('aria-expanded') !== 'true';
      nav.classList.toggle('is-open',open);
      menuButton.setAttribute('aria-expanded',String(open));
      menuButton.setAttribute('aria-label',open?'메뉴 닫기':'메뉴 열기');
    });
    document.addEventListener('keydown',event => {
      if(event.key==='Escape' && nav.classList.contains('is-open')) closeMenu(true);
    });
    document.addEventListener('click',event => {
      if(nav.classList.contains('is-open') && !nav.contains(event.target) && !menuButton.contains(event.target)) closeMenu();
    });
    mobile.addEventListener('change',()=>closeMenu());

    function updateScreen() {
      const shot = screens[currentScreen];
      heroImage.src = assetPath(shot.key);
      heroImage.alt = shot.alt;
      heroImage.width = shot.key === 'speech' ? 524 : 1120;
      heroImage.height = shot.key === 'speech' ? 196 : 800;
      heroLink.href = assetPath(shot.key);
      heroLink.dataset.caption = `${shot.name} · Beta v1.1.3 개발본`;
      heroLink.setAttribute('aria-label',`${shot.name} 앱 화면 확대`);
      heroLink.classList.toggle('is-speech',shot.key==='speech');
      document.querySelector('#screen-name').textContent = shot.name;
      document.querySelectorAll('[data-screen]').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.screen===shot.key)));
    }
    document.querySelectorAll('[data-screen]').forEach(button=>button.addEventListener('click',()=>{
      currentScreen=screens.findIndex(shot=>shot.key===button.dataset.screen); updateScreen();
    }));
    document.querySelectorAll('[data-screen-step]').forEach(button=>button.addEventListener('click',()=>{
      currentScreen=(currentScreen+Number(button.dataset.screenStep)+screens.length)%screens.length; updateScreen();
    }));

    function setTheme(theme) {
      if(!Object.hasOwn(themeNames,theme)) return;
      currentTheme = theme;
      document.querySelectorAll('[data-theme]').forEach(button=>{
        const selected=button.dataset.theme===theme;
        button.setAttribute('aria-pressed',String(selected));
        button.closest('.theme-pane').classList.toggle('is-active',selected);
        document.getElementById(button.getAttribute('aria-controls')).hidden=!selected;
        if(selected) document.querySelector('.theme-accordion').style.setProperty('--selected-theme-bg',button.closest('.theme-pane').style.getPropertyValue('--theme-bg'));
      });
      document.querySelectorAll('[data-asset]').forEach(element=>{
        if(element.tagName==='IMG') element.src=assetPath(element.dataset.asset);
        else element.href=assetPath(element.dataset.asset);
      });
      updateScreen();
      if(!reduced.matches) setTimeout(()=>ScrollTrigger.refresh(),520);
    }
    document.querySelectorAll('[data-theme]').forEach(button=>button.addEventListener('click',()=>setTheme(button.dataset.theme)));

    const dialog = document.querySelector('.image-dialog');
    let imageOpener;
    document.querySelectorAll('[data-lightbox]').forEach(link=>link.addEventListener('click',event=>{
      event.preventDefault();
      imageOpener=link;
      document.querySelector('#dialog-image').src=link.href;
      document.querySelector('#dialog-image').alt=link.querySelector('img').alt;
      document.querySelector('#dialog-caption').textContent=link.dataset.caption;
      document.querySelector('#dialog-original').href=link.href;
      dialog.showModal();
      document.body.classList.add('dialog-open');
    }));
    dialog.addEventListener('close',()=>{
      document.body.classList.remove('dialog-open');
      if(imageOpener?.isConnected && !imageOpener.closest('[hidden]')) imageOpener.focus({preventScroll:true});
    });
    dialog.addEventListener('click',event=>{
      if(event.target!==dialog) return;
      const rect=dialog.getBoundingClientRect();
      if(event.clientX<rect.left || event.clientX>rect.right || event.clientY<rect.top || event.clientY>rect.bottom) dialog.close();
    });

    const osTabs=[...document.querySelectorAll('[data-os]')];
    function selectOS(os,focus=false) {
      osTabs.forEach(tab=>{
        const selected=tab.dataset.os===os;
        tab.setAttribute('aria-selected',String(selected));
        tab.tabIndex=selected?0:-1;
        document.getElementById(tab.getAttribute('aria-controls')).hidden=!selected;
        if(selected && focus) tab.focus();
      });
    }
    osTabs.forEach(tab=>{
      tab.addEventListener('click',()=>selectOS(tab.dataset.os));
      tab.addEventListener('keydown',event=>{
        if(!['ArrowLeft','ArrowRight','Home','End'].includes(event.key)) return;
        event.preventDefault();
        const next=event.key==='Home'?osTabs[0]:event.key==='End'?osTabs.at(-1):osTabs[(osTabs.indexOf(tab)+1)%osTabs.length];
        selectOS(next.dataset.os,true);
      });
    });
    if(/Windows/i.test(navigator.userAgent)) selectOS('windows');

    document.querySelectorAll('a[href^="#"]').forEach(link=>link.addEventListener('click',event=>{
      if(event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      const target=document.querySelector(link.getAttribute('href'));
      if(!target) return;
      event.preventDefault();
      closeMenu();
      history.pushState(null,'',link.getAttribute('href'));
      if(!target.hasAttribute('tabindex')) target.tabIndex=-1;
      target.focus({preventScroll:true});
      target.scrollIntoView({behavior:reduced.matches?'instant':'smooth',block:'start'});
    }));
    const observer=new IntersectionObserver(entries=>{
      const visible=entries.filter(entry=>entry.isIntersecting).sort((a,b)=>a.boundingClientRect.top-b.boundingClientRect.top);
      if(!visible.length) return;
      document.querySelectorAll('.rail-link').forEach(link=>{
        if(link.hash===`#${visible[0].target.id}`) link.setAttribute('aria-current','location');
        else link.removeAttribute('aria-current');
      });
    },{rootMargin:'-10% 0px -60% 0px',threshold:0});
    document.querySelectorAll('main>section[id]').forEach(section=>observer.observe(section));

    history.scrollRestoration='manual';
    if(performance.getEntriesByType('navigation')[0]?.type==='reload'){
      history.replaceState(null,'',location.pathname+location.search);
      scrollTo({top:0,behavior:'instant'});
    }
    gsap.registerPlugin(ScrollTrigger);
    const motion=gsap.matchMedia();
    motion.add('(min-width:1100px) and (prefers-reduced-motion:no-preference)',()=>{
      ScrollTrigger.create({trigger:'.features',start:'top 130px',end:'bottom bottom',pin:'[data-pin-title]',pinSpacing:false});
    });
    motion.add('(prefers-reduced-motion:no-preference)',()=>{
      gsap.fromTo('[data-scrub] span',{opacity:.2},{opacity:1,stagger:.25,ease:'none',scrollTrigger:{trigger:'[data-scrub]',start:'top 88%',end:'top 58%',scrub:true}});
    });
    document.fonts.ready.then(()=>ScrollTrigger.refresh());
  