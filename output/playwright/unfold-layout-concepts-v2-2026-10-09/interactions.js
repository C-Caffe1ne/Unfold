import {gsap,ScrollTrigger} from './vendor/gsap.js';
gsap.registerPlugin(ScrollTrigger);
const toggle=document.querySelector('.menu-toggle');
const nav=document.querySelector('#nav-links');
function closeMenu(){toggle?.setAttribute('aria-expanded','false');toggle?.setAttribute('aria-label','메뉴 열기');nav?.classList.remove('is-open')}
toggle?.addEventListener('click',()=>{const open=toggle.getAttribute('aria-expanded')!=='true';toggle.setAttribute('aria-expanded',String(open));toggle.setAttribute('aria-label',open?'메뉴 닫기':'메뉴 열기');nav.classList.toggle('is-open',open)});
nav?.addEventListener('click',e=>{if(e.target.closest('a,button'))closeMenu()});
document.addEventListener('keydown',e=>{if(e.key==='Escape'&&nav?.classList.contains('is-open')){closeMenu();toggle.focus()}});
const dialog=document.querySelector('.purchase-dialog');let opener;
document.querySelectorAll('[data-buy]').forEach(button=>button.addEventListener('click',()=>{opener=button;dialog.showModal()}));
dialog.querySelector('.dialog-close').addEventListener('click',()=>dialog.close());
dialog.addEventListener('close',()=>{const target=opener?.getClientRects().length?opener:toggle;target?.focus({preventScroll:true})});
dialog.addEventListener('click',event=>{const r=dialog.getBoundingClientRect();if(event.target===dialog&&(event.clientX<r.left||event.clientX>r.right||event.clientY<r.top||event.clientY>r.bottom))dialog.close()});
const shots=['home','review','speech'];const names=['타이머','휴식 기록','휴식 알림'];
document.querySelectorAll('[data-gallery]').forEach(gallery=>{
 let index=0;const image=gallery.querySelector('[data-screen]');
 function show(next){index=(next+shots.length)%shots.length;gallery.dataset.current=String(index);image.src=`./assets/${gallery.dataset.theme}-${shots[index]}.png`;image.alt=`Unfold ${names[index]} 실제 앱 화면`;gallery.querySelector('[data-shot-title]').textContent=names[index];gallery.querySelectorAll('[data-shot]').forEach(button=>button.setAttribute('aria-pressed',String(Number(button.dataset.shot)===index)))}
 gallery.querySelectorAll('[data-shot]').forEach(button=>button.addEventListener('click',()=>show(Number(button.dataset.shot))));
 gallery.querySelector('[data-prev]').addEventListener('click',()=>show(index-1));gallery.querySelector('[data-next]').addEventListener('click',()=>show(index+1));
 image.addEventListener('load',()=>ScrollTrigger.refresh());
});
const filters=document.querySelectorAll('[data-filter]');
if(filters.length){
 const grid=document.querySelector('.catalog-grid');const label={all:'이미지·영상·3D(GLB)',image:'이미지',motion:'GIF · MP4',glb:'3D(GLB)'};
 filters.forEach(button=>button.addEventListener('click',()=>{
  const category=button.dataset.filter;filters.forEach(item=>item.setAttribute('aria-pressed',String(item===button)));grid.dataset.active=category;grid.classList.toggle('is-filtered',category!=='all');
  grid.querySelectorAll('[data-category]').forEach(tile=>{tile.hidden=category!=='all'&&tile.dataset.category!==category});
  document.querySelector('[data-filter-status]').textContent=category==='all'?'원하는 형식을 살펴보세요.':`${label[category]} 형식을 보고 있어요.`;document.querySelector('[data-selected-format]').textContent=label[category];ScrollTrigger.refresh();
 }));
}
const journey=document.querySelector('#journey');let currentScene=0;let horizontalMotion;
function syncScene(index){currentScene=Math.max(0,Math.min(3,index));document.querySelectorAll('[data-scene]').forEach(button=>button.setAttribute('aria-pressed',String(Number(button.dataset.scene)===currentScene)))}
function goScene(index){
 if(!journey)return;index=Math.max(0,Math.min(3,index));const panel=journey.querySelectorAll('.journey-panel')[index];
 if(horizontalMotion?.scrollTrigger){const trigger=horizontalMotion.scrollTrigger;const track=journey.querySelector('.journey-track');const viewport=journey.querySelector('.journey-window');const distance=track.scrollWidth-viewport.clientWidth;const target=Math.min(panel.offsetLeft,distance);window.scrollTo({top:trigger.start+target/distance*(trigger.end-trigger.start),behavior:'smooth'})}
 else{const viewport=journey.querySelector('.journey-window');viewport.scrollTo({left:panel.offsetLeft,behavior:matchMedia('(prefers-reduced-motion:reduce)').matches?'instant':'smooth'})}
 syncScene(index);
}
document.querySelectorAll('[data-scene]').forEach(button=>button.addEventListener('click',()=>goScene(Number(button.dataset.scene))));
document.querySelectorAll('[data-journey-next]').forEach(button=>button.addEventListener('click',()=>goScene(currentScene+1)));
document.querySelector('[data-journey-prev]')?.addEventListener('click',()=>goScene(currentScene-1));
journey?.querySelector('.journey-window').addEventListener('scroll',e=>{if(horizontalMotion)return;const width=journey.querySelector('.journey-panel').getBoundingClientRect().width;syncScene(Math.round(e.target.scrollLeft/width))},{passive:true});
const media=gsap.matchMedia();
media.add({desktop:'(min-width:1000px)',reduced:'(prefers-reduced-motion:reduce)'},context=>{
 const {desktop,reduced}=context.conditions;
 if(journey){
  const track=journey.querySelector('.journey-track');const viewport=journey.querySelector('.journey-window');
  if(desktop&&!reduced){
   viewport.scrollLeft=0;viewport.style.overflowX='hidden';viewport.style.scrollSnapType='none';
   horizontalMotion=gsap.to(track,{x:()=>-(track.scrollWidth-viewport.clientWidth),ease:'none',scrollTrigger:{trigger:journey,pin:journey.querySelector('.journey-stage'),start:'top top+=90',end:()=>`+=${track.scrollWidth-viewport.clientWidth}`,scrub:.5,invalidateOnRefresh:true,onUpdate:self=>syncScene(Math.round(self.progress*(track.scrollWidth-viewport.clientWidth)/journey.querySelector('.journey-panel').offsetWidth))}});
   journey.querySelectorAll('[data-scale]').forEach(image=>gsap.fromTo(image,{scale:.8,opacity:.4},{scale:1,opacity:1,ease:'none',scrollTrigger:{trigger:image,containerAnimation:horizontalMotion,start:'left 90%',end:'left 45%',scrub:.5}}));
  }
 }
 if(!reduced){
  document.querySelectorAll('[data-scrub]').forEach(el=>{
   if(!el.querySelector('[data-word]')){
    const walker=document.createTreeWalker(el,NodeFilter.SHOW_TEXT);const nodes=[];let node;while(node=walker.nextNode())nodes.push(node);
    nodes.forEach(text=>{const fragment=document.createDocumentFragment();text.textContent.split(/(\s+)/).forEach(word=>{if(!word.trim())fragment.append(document.createTextNode(word));else{const span=document.createElement('span');span.dataset.word='';span.textContent=word;fragment.append(span)}});text.replaceWith(fragment)});
   }
   gsap.fromTo(el.querySelectorAll('[data-word]'),{opacity:.2},{opacity:1,stagger:.15,ease:'none',scrollTrigger:{trigger:el,start:'top 85%',end:'top 40%',scrub:.6}});
  });
  if(desktop)document.querySelectorAll('[data-pin]').forEach(el=>ScrollTrigger.create({trigger:el,pin:el,start:'top top+=120',endTrigger:el.parentElement,end:'bottom top+=420',pinSpacing:false}));
 }
 return ()=>{if(journey){horizontalMotion=undefined;const viewport=journey.querySelector('.journey-window');viewport.style.overflowX='auto';viewport.style.scrollSnapType='x mandatory';syncScene(0)}};
});
window.__layoutMotion={version:gsap.version,triggers:()=>ScrollTrigger.getAll().length};
window.addEventListener('load',()=>ScrollTrigger.refresh());document.fonts.ready.then(()=>ScrollTrigger.refresh());
