/* ==========================================================================
   테마 전환 — 앱의 네 가지 브랜드 팔레트를 웹에서도 그대로 사용한다.
   선택값은 localStorage에 저장하고 모든 테마 컨트롤을 동기화한다.
   ========================================================================== */

const THEMES = ['oat', 'sage', 'midnight', 'plum'];
const STORAGE_KEY = 'unfold-theme';
const DEFAULT_THEME = 'oat';

const root = document.documentElement;

/** 저장된 테마를 읽는다. 저장소 접근이 막히면 기본값으로 되돌린다. */
function readStoredTheme() {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    return THEMES.includes(saved) ? saved : DEFAULT_THEME;
  } catch {
    return DEFAULT_THEME;
  }
}

/** 테마를 저장한다. 실패해도 화면 적용은 막지 않는다. */
function storeTheme(theme) {
  try {
    localStorage.setItem(STORAGE_KEY, theme);
  } catch {
    /* 사생활 보호 모드 등에서 저장이 막힐 수 있다. 무시한다. */
  }
}

/** 테마 컨트롤(스와치·목록)의 선택 상태를 맞춘다. */
function syncControls(theme) {
  const controls = document.querySelectorAll('[data-theme-value]');
  controls.forEach((control) => {
    const isActive = control.dataset.themeValue === theme;
    control.setAttribute('aria-pressed', String(isActive));
  });
}

/** 테마 미리보기 이미지를 현재 테마 것만 보이게 한다. */
function syncPreview(theme) {
  const shots = document.querySelectorAll('[data-theme-shot]');
  shots.forEach((shot) => {
    shot.hidden = shot.dataset.themeShot !== theme;
    if (!shot.hidden) {
      const link = shot.closest('[data-theme-lightbox]');
      if (link) link.href = shot.src;
    }
  });
}

/** 테마를 적용한다. */
function applyTheme(theme, persist = true) {
  const next = THEMES.includes(theme) ? theme : DEFAULT_THEME;
  root.setAttribute('data-theme', next);
  syncControls(next);
  syncPreview(next);
  if (persist) storeTheme(next);
}

/** 테마 컨트롤 클릭을 처리한다. */
function handleThemeClick(event) {
  const control = event.target.closest('[data-theme-value]');
  if (!control) return;
  applyTheme(control.dataset.themeValue);
}

function initTheme() {
  applyTheme(readStoredTheme(), false);
  document.addEventListener('click', handleThemeClick);
}

initTheme();
