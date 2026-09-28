# A안 최초 실행 계정 화면

2026-09-28 · 사용자 승인 A안 적용. 화면을 먼저 적용하고 사용 잠금은 결제 연동 후 적용한다.

## 변경

- 최초 일반 실행 → A안 계정 창 → `앱 열기` → 기존 홈. 이후 설정의 계정 카드에서 재진입한다.
- 문구·표시 가격·펫 이미지 선택은 `Assets/Account/entry-screen.json`, 배치는 `AccountWindow.axaml`,
  색상·공통 버튼은 기존 `DesignSystem`에서 관리한다. 편집 방법은 [계정 화면 안내](../account-screen.md)에 있다.
- Google 버튼은 PKCE + 시스템 브라우저 + `127.0.0.1` 콜백을 사용한다. 5분 제한·취소·포트 충돌·오류를 처리한다.
- 구매 권한은 서버 응답만 표시한다. 서버 오류를 미구매로 바꾸지 않는다. 결제 API가 없는 현재 구매 버튼은 비활성이다.
- 세션은 창 수명 동안 메모리에만 유지한다. 안내 종료 기록에는 인증·구매 정보가 없으며, 로컬 타이머·펫 사용을 제한하지 않는다.
- 설정 탭에 계정 재진입 버튼을 추가했다. 버전·배포 파일·원격 Supabase 설정은 변경하지 않았다.

## 자동 검사

- Release 빌드: 경고 0, 오류 0.
- 계정 집중 검사: 31개 통과. 기존 PKCE·권한 계약 검사와 함께 JSON 편집·오류 복구, 통화 독립 표시,
  로그인 실패·취소·중복 클릭·늦은 응답, 서버 오류 재시도, 창 종료, 잘못된 접속 설정, HTTP 콜백·포트 해제를 검사했다.
- 최종 전체 Release 테스트: 402개 통과, 실패 0, 건너뜀 0. 잘못된 접속 설정·HTTP 연결 끊김 처리 보완까지 포함한 빌드에서 실행했다.
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
- 실제 결제, 원격 구매 함수 배포, 주문·웹훅, 세션 보안 저장·복원, 접근 제한: 미구현/후속 단계.
- Windows 실제 렌더링·기본 브라우저 복귀·배율·물리 입력: 미검증.
- 현재 `connection.json`의 환경은 `test`다. 현재 화면 가격 JSON은 서버 청구액이나 구매 권한의 근거가 아니다.
