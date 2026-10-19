# Unfold 앱 쇼케이스 HTML

미리보기: http://127.0.0.1:4190/app-showcase.html

`app-showcase.html`에 CSS와 상호작용 코드를 담았습니다. `assets/`, `fonts/`, `vendor/`는 HTML과 함께 보관합니다.

현재 웹 작업본 `codex/web-ui-ux-review`의 `public/index.html`과 실제 앱 캡처를 사용했습니다. 원본 public 파일 52개는 변경하지 않았습니다. 커밋·배포는 수행하지 않았습니다.

구성: 앱 화면 갤러리 → 기능 → 네 가지 테마 → 가격 → 구매·이용 FAQ → 다운로드 → 계정·약관·문의 링크.

앱 화면은 Beta v1.1.3 개발본, 다운로드는 공개 Beta v1.1.2입니다. 별도 펫 팩 미리보기 창은 포함하지 않았습니다. 펫 관리 화면은 현재 사이트에 있는 실제 관리 화면입니다.

로그인·내 계정·테스트 결제·약관·변경 이력은 공식 사이트로 연결됩니다. 다운로드 파일 주소는 원본 `_redirects`에 있는 GitHub Release URL을 사용합니다. 로그인 및 결제 API를 새로 구현하거나 실제 인증·주문을 실행하지 않았습니다.

브라우저 검증 기록: `capture-results.json`, `inspection-results.json`. Chromium에서 반응형과 상호작용을 확인했으며 실제 모바일 기기 검증은 수행하지 않았습니다.
