# A안 최초 실행 계정 화면

2026-09-28 · 사용자 승인 A안 적용. 화면을 먼저 적용하고 사용 잠금은 결제 연동 후 적용한다.

## 변경

- 최초 일반 실행 → A안 계정 창 → `앱 열기` → 기존 홈. 이후 설정의 계정 카드에서 재진입한다.
- 문구·표시 가격·펫 이미지 선택은 `Assets/Account/entry-screen.json`, 배치는 `AccountWindow.axaml`,
  색상·공통 버튼은 기존 `DesignSystem`에서 관리한다. 편집 방법은 [계정 화면 안내](../account-screen.md)에 있다.
- Google 버튼은 PKCE + 시스템 브라우저 + `127.0.0.1` 콜백을 사용한다. 5분 제한·취소·포트 충돌·오류를 처리한다.
- 구매 권한은 서버 응답만 표시하고 서버 오류를 미구매로 바꾸지 않는다. 한국 미구매 계정은
  인증된 Checkout을 열고 명시적인 `구매 확인`으로 권한을 다시 조회한다.
- Checkout URL은 Lemon Squeezy HTTPS 호스트·Checkout ID·서명 쿼리를 검증한 뒤에만 기본 브라우저로 연다.
- 세션은 창 수명 동안 메모리에만 유지한다. 안내 종료 기록에는 인증·구매 정보가 없으며, 로컬 타이머·펫 사용을 제한하지 않는다.
- 기존 최초 화면을 닫은 프로필에도 새 한국 결제 화면이 한 번 표시되도록 안내 종료 기록을 버전화했다.
  현재 화면을 닫은 뒤에는 같은 버전이 다시 자동으로 열리지 않는다.
- 설정 탭의 계정 재진입 버튼은 유지했다. 앱 사용 잠금과 배포 파일은 변경하지 않았다.

## 자동 검사

- Release 빌드: 경고 0, 오류 0.
- 계정 집중 검사: 37개 통과. 기존 PKCE·권한 계약과 함께 한국 Checkout 요청 본문, 호스트·ID·서명 URL 검증,
  결제창 1회 열기, 명시적 구매 재확인, 오류 재시도, 통화 설정, 콜백·포트 해제,
  이전 안내 종료 기록의 버전 갱신을 검사했다.
- 최종 전체 Release 테스트: 408개 통과, 실패 0, 건너뜀 0.
- 4개 테마 × 로그인·구매·640×560 최소 창: headless 렌더링 12장. 최소 창의 하단 버튼·가로 넘침 검사 통과.
  구매 화면의 이메일·구매 응답은 테스트 대역이다. 실제 구매 증거가 아니다.
- `git diff --check`: 통과.

## macOS 네이티브 진단

새 `UNFOLD_DATA_DIR`:
`/var/folders/r6/7hjm96zd53g3dfxkbbb9jbnh0000gn/T/Unfold-smoke-8171afac2aa946b191ace73803dab024`

`verification/smoke.json`: `success: true`, PNG 93장.
계정 화면의 파일·이미지 로딩, 중복 창 방지, 최소 너비 가로 넘침 방지, 앱 열기와 안내 종료 기록을 확인했다.
기존 타이머·설정 저장·펫·말풍선 진단도 통과했다. 인증·결제는 실행하지 않았다.

캡처와 보고서 사본:

- `artifacts/verification/account-entry/native-account-login.png`
- `artifacts/verification/account-entry/native-account-login-minimum.png`
- `artifacts/verification/account-entry/native-smoke.json`
- `artifacts/verification/account-entry/purchase-OatLatte.png` (headless, 테스트 응답)

위 진단은 macOS 렌더러의 화면 밖 캡처와 이벤트 호출이다. 사람이 마우스로 조작한 실기 검증과 구분한다.

## 남은 범위

- 실제 Google 계정의 동의 → 앱 복귀 → 코드 교환 → 구매 권한 조회 완료: 미검증.
- 원격 한국 Checkout과 서명 웹훅은 배포했다. 실제 카드 결제, 웹훅 수신·환불, 세션 보안 저장·복원,
  접근 제한은 미검증 또는 후속 단계다.
- Windows 실제 렌더링·기본 브라우저 복귀·배율·물리 입력: 미검증.
- 현재 `connection.json`의 환경은 `test`, `checkoutMarkets`는 `KR` 하나다. 화면 가격 JSON은
  표시 용도이며 서버의 4,900원 카탈로그와 구매 권한이 결제 기준이다.
