'use strict';
const screens = {
  login: { title: '반가워요', primary: 'Google로 계속하기', next: 'auth', step: '01 / 로그인' },
  auth: { title: '로그인 중이에요', primary: '로그인 대기 중', secondary: '취소', next: 'purchase', step: '01 / 로그인', status: '브라우저에서 Google 로그인을 완료해 주세요.' },
  purchase: { title: 'Unfold와 함께하기', primary: '구매하기', secondary: '다른 계정으로 로그인', next: 'pending', step: '02 / 구매' },
  pending: { title: '결제를 확인하고 있어요', primary: '구매 내역 새로고침', secondary: '돌아가기', next: 'ready', step: '02 / 구매' },
  ready: { title: '준비됐어요', primary: 'Unfold 시작하기', secondary: '다른 계정으로 로그인', next: 'ready', step: '03 / 시작', status: '구매 완료' },
  error: { title: '연결을 확인해 주세요', primary: '다시 시도', secondary: '돌아가기', next: 'pending', step: '02 / 구매', status: '구매 내역을 불러오지 못했어요.' },
};
const state = document.querySelector('#state');
const market = document.querySelector('#market');
const theme = document.querySelector('#theme');
const size = document.querySelector('#size');
const primary = document.querySelector('#primary');
const secondary = document.querySelector('#secondary');
function render() {
  const screen = screens[state.value];
  document.querySelector('#screen-title').textContent = screen.title;
  primary.textContent = screen.primary;
  primary.disabled = state.value === 'auth';
  secondary.hidden = !screen.secondary;
  secondary.textContent = screen.secondary ?? '';
  document.querySelector('#price').hidden = state.value !== 'purchase';
  document.querySelector('#amount').textContent = market.value === 'KR' ? '4,900원' : 'US$3.99';
  document.querySelector('#account').hidden = ['login', 'auth'].includes(state.value);
  document.querySelector('#price-foot').hidden = state.value !== 'login';
  const status = document.querySelector('#status');
  status.hidden = !screen.status;
  status.textContent = screen.status ?? '';
  document.querySelector('#step').textContent = screen.step;
  document.documentElement.dataset.theme = theme.value;
  document.querySelector('.app-frame').classList.toggle('compact', size.value === 'compact');
}
function advance() { state.value = screens[state.value].next; render(); }
function goBack() { state.value = ['pending', 'error'].includes(state.value) ? 'purchase' : 'login'; render(); }
for (const control of [state, market, theme, size]) control.addEventListener('change', render);
primary.addEventListener('click', advance);
secondary.addEventListener('click', goBack);
render();
