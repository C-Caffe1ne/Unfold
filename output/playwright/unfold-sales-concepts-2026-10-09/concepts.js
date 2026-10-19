import {gsap,ScrollTrigger} from './vendor/gsap.js';
gsap.registerPlugin(ScrollTrigger);
const menu=document.querySelector('.menu-toggle');
const links=document.querySelector('.nav-links');
const closeMenu=()=>{menu.setAttribute('aria-expanded','false');menu.setAttribute('aria-label','메뉴 열기');links.classList.remove('is-open')};
menu?.addEventListener('click',()=>{const open=menu.getAttribute('aria-expanded')!=='true';menu.setAttribute('aria-expanded',String(open));menu.setAttribute('aria-label',open?'메뉴 닫기':'메뉴 열기');links.classList.toggle('is-open',open)});
links?.addEventListener('click',e=>{if(e.target.closest('a'))closeMenu()});
document.addEventListener('keydown',e=>{if(e.key==='Escape'&&links?.classList.contains('is-open')){closeMenu();menu.focus()}});

const dialog=document.querySelector('.purchase-dialog');
let opener;
document.querySelectorAll('[data-purchase]').forEach(button=>button.addEventListener('click',()=>{opener=button;dialog.showModal()}));
dialog.querySelector('.dialog-close').addEventListener('click',()=>dialog.close());
dialog.addEventListener('close',()=>opener?.focus({preventScroll:true}));
dialog.addEventListener('click',e=>{const r=dialog.getBoundingClientRect();if(e.target===dialog&&(e.clientX<r.left||e.clientX>r.right||e.clientY<r.top||e.clientY>r.bottom))dialog.close()});

const names=['타이머','휴식 기록','휴식 알림'];
const shots=['home','review','speech'];
document.querySelectorAll('[data-carousel]').forEach(carousel=>{
 const theme=carousel.dataset.theme;let index=0;
 const image=carousel.querySelector('[data-screen]');
 image.addEventListener('load',()=>ScrollTrigger.refresh());
 const label=carousel.querySelector('[data-screen-name]');
 const count=carousel.querySelector('[data-screen-count]');
 function show(next){index=(next+shots.length)%shots.length;image.src=`./assets/${theme}-${shots[index]}.png`;image.alt=`Unfold ${names[index]} 실제 앱 화면`;label.textContent=names[index];count.textContent=`${index+1} / ${shots.length}`;carousel.dataset.current=String(index);ScrollTrigger.refresh()}
 carousel.querySelector('[data-prev]').addEventListener('click',()=>show(index-1));
 carousel.querySelector('[data-next]').addEventListener('click',()=>show(index+1));
});

document.querySelectorAll('.accordion').forEach(group=>{
 const items=[...group.querySelectorAll('.accordion-item')];
 function activate(item){items.forEach(el=>{const selected=el===item;el.classList.toggle('is-active',selected);el.querySelector('button').setAttribute('aria-expanded',String(selected))});ScrollTrigger.refresh()}
 items.forEach(item=>{item.querySelector('button').addEventListener('click',()=>activate(item));item.addEventListener('pointerenter',e=>{if(e.pointerType==='mouse'&&window.innerWidth>600)activate(item)})});
});

const media=gsap.matchMedia();
media.add({desktop:'(min-width:1000px)',reduced:'(prefers-reduced-motion:reduce)'},context=>{
 if(context.conditions.reduced)return;
 document.querySelectorAll('[data-scale]').forEach(image=>{
  gsap.fromTo(image,{scale:.8},{scale:1,ease:'none',scrollTrigger:{trigger:image.parentElement,start:'top 95%',end:'top 35%',scrub:.8}});
  gsap.to(image,{opacity:.25,ease:'none',scrollTrigger:{trigger:image.parentElement,start:'bottom 35%',end:'bottom top',scrub:.8}});
 });
 if(context.conditions.desktop){
  document.querySelectorAll('[data-pin]').forEach(el=>ScrollTrigger.create({trigger:el.parentElement,pin:el,start:'top top+=120',end:'bottom bottom-=120',pinSpacing:false}));
  document.querySelectorAll('[data-stack]').forEach((el,index)=>{
   ScrollTrigger.create({trigger:el,pin:true,start:`top top+=${100+index*30}`,endTrigger:el.parentElement,end:'bottom top+=400',pinSpacing:false});
  });
 }
 document.querySelectorAll('[data-scrub]').forEach(el=>{
  const text=el.textContent;el.textContent='';
  const words=text.split(' ').map(word=>{const span=document.createElement('span');span.textContent=word+' ';el.append(span);return span});
  gsap.fromTo(words,{opacity:.18},{opacity:1,stagger:.15,ease:'none',scrollTrigger:{trigger:el,start:'top 80%',end:'top 35%',scrub:.7}});
 });
});
window.__conceptMotion={gsap:gsap.version,triggers:()=>ScrollTrigger.getAll().length};
window.addEventListener('load',()=>ScrollTrigger.refresh());
document.fonts.ready.then(()=>ScrollTrigger.refresh());
