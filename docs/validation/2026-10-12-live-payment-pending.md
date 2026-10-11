# Live 결제 pending 진단

2026년 10월 12일 · 기준 앱 배포 `v1.1.3-beta` (`b1242ea`), 작업트리 `codex/remove-unused-features`의 `c37b586`.

## 확인한 상태

- 운영 Live 주문 1개는 `checkout_state=ready`, `status=pending`, 공급자 주문 ID 미등록이다. Live 결제 영수증·active 구매 권한은 없다. 구매자의 개인정보와 주문 식별자는 이 문서에 저장하지 않는다.
- 운영 로그에는 Live 전환 후 lemon-webhook v19의 HTTP 422 세 건이 있다. HTTP 422는 코드상 HMAC 서명 검사 이후에 반환된다. 실패 원문과의 연결은 판매자 콘솔에서 확인해야 하므로 이 세 건을 해당 Live 구매의 전송이라고 단정하지 않는다.
- 기존 `report` 콜백의 기본값은 빈 함수였고 Edge 진입점에 로그 콜백을 연결하지 않았다. 따라서 어느 검증 조건에서 거절됐는지 기존 로그만으로 확인할 수 없다.
- 운영 `apply_lemon_event`는 Test/Live를 분리하고, `get_app_access()`는 Live active 구매 또는 유효한 무료 권한만 허용한다. RLS·호출자 권한 실행·서버 전용 결제 반영 RPC 제한을 확인했다.

## 진단 보완

- 웹훅 검증 실패를 객체·환경·상품·통화·세금 방식·총액·상품 가격·할인·합계 오류 코드로 구분했다. 검증 조건 자체는 유지한다.
- Edge 진입점에 정적인 단계·오류 코드만 `console.warn`으로 남기도록 연결했다. 요청 본문, 메일, 주문 ID, 비밀값은 로그에 남기지 않는다.
- 운영 lemon-webhook v20을 배포하고 원격 파일 5개가 로컬 소스와 일치함을 확인했다. HMAC 인증을 사용하는 기존 `verify_jwt=false`를 유지했다.

## 검증

- 변경 전 서버 Node/PGlite 38개 통과, 진단 보완 후 39개 통과, 실패·건너뜀 0.
- 새 회귀 검사는 가격 불일치의 결제 반영 차단, 오류 원인 구분, 사용자 메일·내부 오류 비밀 문자열이 로그에 포함되지 않는 것을 확인한다.
- 공개 운영 함수의 GET은 405, 서명 없는 POST와 잘못된 서명의 POST는 401로 거절됐다. 이 요청들은 결제 데이터를 쓰지 않았다.
- `git diff --check` 통과. 앱 코드·설치본·무료 초대 코드는 변경하지 않았다.

## 남은 원인 확인·복구

현재 Lemon Squeezy 판매자 콘솔은 로그아웃 상태다. 사용자 로그인 후 실패한 Live `order_created`의 실제 JSON·응답·연결된 주문을 대조해야 한다. 부가세 포함 가격과 환산된 총액의 엄격 일치 조건은 원인 후보이며 실제 데이터 없이 완화하지 않았다.

원인이 확인되면 최소 수정과 회귀 검사를 적용·배포한 뒤 기존 서명 이벤트를 재전송한다. `pending→paid`, Live 권한 `active`, 실제 앱 접근 계약을 차례로 확인한다. 결제 증빙 없이 DB 상태나 이용 권한을 수동으로 활성화하지 않는다. 아직 Live 결제 복구 완료가 아니다.

참고: [Lemon Squeezy 웹훅과 재전송](https://docs.lemonsqueezy.com/guides/developer-guide/webhooks), [주문 통화·금액·세금 계약](https://docs.lemonsqueezy.com/api/orders/the-order-object).
