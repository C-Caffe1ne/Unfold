/* ==========================================================================
   공통 동작 — 모바일 내비게이션, 설치 안내 탭, OS 감지, 인증 목업
   Google OAuth와 외부 네트워크 요청은 아직 연결하지 않는다.
   ========================================================================== */

/* --- 1. 모바일 내비게이션 ------------------------------------------------- */
function initNav() {
  const toggle = document.querySelector('.nav-toggle');
  const nav = document.querySelector('.site-nav');
  if (!toggle || !nav) return;

  function closeNav() {
    nav.classList.remove('is-open');
    toggle.setAttribute('aria-expanded', 'false');
    toggle.setAttribute('aria-label', '메뉴 열기');
    document.removeEventListener('keydown', handleNavEscape);
  }

  function openNav() {
    nav.classList.add('is-open');
    toggle.setAttribute('aria-expanded', 'true');
    toggle.setAttribute('aria-label', '메뉴 닫기');
    document.addEventListener('keydown', handleNavEscape);
  }

  function handleNavEscape(event) {
    if (event.key === 'Escape') {
      closeNav();
      toggle.focus();
    }
  }

  function handleNavToggle() {
    const isOpen = nav.classList.contains('is-open');
    if (isOpen) closeNav();
    else openNav();
  }

  function handleNavClick(event) {
    if (event.target.tagName === 'A') closeNav();
  }

  toggle.addEventListener('click', handleNavToggle);
  nav.addEventListener('click', handleNavClick);
}

/* --- 2. 설치 안내 탭 ------------------------------------------------------ */
function initTabs() {
  const tablist = document.querySelector('[role="tablist"]');
  if (!tablist) return;

  const tabs = Array.from(tablist.querySelectorAll('[role="tab"]'));

  function selectTab(tab, updateHash = false) {
    tabs.forEach((item) => {
      const isSelected = item === tab;
      item.setAttribute('aria-selected', String(isSelected));
      item.tabIndex = isSelected ? 0 : -1;
      const panel = document.getElementById(item.getAttribute('aria-controls'));
      if (panel) panel.hidden = !isSelected;
    });

    if (updateHash && tab.dataset.os) {
      history.replaceState(null, '', `#${tab.dataset.os}`);
    }
  }

  tablist.addEventListener('click', (event) => {
    const tab = event.target.closest('[role="tab"]');
    if (tab) selectTab(tab, true);
  });

  tablist.addEventListener('keydown', (event) => {
    const current = tabs.indexOf(document.activeElement);
    if (current === -1) return;

    let next = -1;
    if (event.key === 'ArrowRight') next = (current + 1) % tabs.length;
    if (event.key === 'ArrowLeft') next = (current - 1 + tabs.length) % tabs.length;
    if (event.key === 'Home') next = 0;
    if (event.key === 'End') next = tabs.length - 1;

    if (next !== -1) {
      event.preventDefault();
      tabs[next].focus();
      selectTab(tabs[next], true);
    }
  });

  // 해시로 진입했을 때 해당 탭을 연다 (예: install.html#windows)
  const hash = window.location.hash.replace('#', '');
  const target = tabs.find((tab) => tab.dataset.os === hash);
  if (target) selectTab(target);
}

/* --- 3. OS 감지 — 다운로드 행 강조 ---------------------------------------- */
function detectOs() {
  const ua = navigator.userAgent;
  if (/Windows/i.test(ua)) return 'windows';
  if (/Mac OS X|Macintosh/i.test(ua)) return 'mac';
  return null;
}

function initOsHint() {
  const os = detectOs();
  if (!os) return;

  const rows = document.querySelectorAll('.dl-row[data-os]');
  rows.forEach((row) => {
    if (row.dataset.os !== os) return;
    row.classList.add('is-detected');
    const hint = row.querySelector('[data-os-hint]');
    if (hint) hint.hidden = false;
  });
}

/* --- 4. Google 인증 목업 -------------------------------------------------- */
function initGoogleAuth() {
  const buttons = document.querySelectorAll('[data-google-auth]');

  buttons.forEach((button) => {
    function showIntegrationNotice() {
      const scope = button.closest('.auth-card') || document;
      const result = scope.querySelector('[data-mock-result]');
      if (!result) return;

      result.hidden = false;
      result.focus();
    }

    button.addEventListener('click', showIntegrationNotice);
  });
}

/* --- 5. 초기화 ------------------------------------------------------------ */
initNav();
initTabs();
initOsHint();
initGoogleAuth();
