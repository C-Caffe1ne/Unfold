# Lemon Squeezy 전환 구현·검증

2026년 09월 28일. Paddle보다 유리한 판매 조건을 사용하기 위해 결제 공급자를
Lemon Squeezy로 전환했다. 한국 Test mode Checkout만 활성화했으며 앱 잠금과 운영 결제는 활성화하지 않았다.

## 변경

- `create-checkout`을 Lemon Squeezy Checkouts API 방식으로 교체했다.
- 서버가 KR 시장을 내부 가격과 Store/Product/Variant에 매핑한다. Global 매핑은 후속 작업으로 남겼다.
- Lemon API의 KRW 금액은 원화 표시 금액보다 100배 큰 정수로 전달되므로, 서버 경계에서만 변환하고
  내부 주문·권한 DB는 원 단위를 유지한다.
- 한 번 결제, 수량 1, 무료 체험 없음, 할인 입력 없음, 테스트 모드를 서버에서 강제한다.
- `lemon-webhook`은 원문 HMAC SHA-256 `X-Signature`와 `X-Event-Name`을 검증한다.
- `order_created`는 이용권을 활성화하고 `order_refunded`의 누적 환불액이 결제 합계에
  도달할 때만 해당 주문을 전체 환불로 처리한다.
- 재전송, 이벤트 키 충돌, 늦은 결제 알림, 여러 유효 주문과 환불 순서를 DB 트랜잭션으로 처리한다.
- 한 Checkout ID를 둘 이상의 내부 주문에 연결하지 못하며, URL 바인딩이 끝난 주문만 웹훅을 반영한다.
- Paddle 전용 런타임·테스트 도구를 제거했다. 이미 적용된 Paddle DB 이력은 삭제하지 않았다.

## 원격 적용

Supabase 프로젝트 `xrelgkdawkogrwxmwkcx`에 다음 상태를 적용하고 다시 조회했다.

| 항목 | 확인 결과 |
|---|---|
| 마이그레이션 | `202609280002`~`202609280004` 로컬·원격 이력 일치 |
| 서버 테이블 | `lemon_prices`, `lemon_events` RLS 활성 |
| 클라이언트 권한 | `anon` 예약 RPC, `authenticated` 결제 반영 RPC 실행 권한 없음 |
| 상품 매핑 | `unfold-kr` 1건. Store `485125`, Product `1393777`, Variant `2176689` |
| 판매 가격 | KR/Global 모두 `live_enabled=false` |
| Edge Function | `create-checkout` 버전 7/JWT 필수, `lemon-webhook` 버전 3/HMAC 필수, Active |
| 기존 Paddle 함수 | `paddle-webhook` 제거 |
| 인증 없는 결제 생성 | HTTP 401 |
| 서명 없는 Lemon 웹훅 | HTTP 401 |
| Checkout 설정 | 한국 Test mode 활성, Global 미매핑으로 생성 불가 |

## 자동 검사

`supabase` 폴더에서 Node.js 24.16으로 `npm test`를 실행했다.

- 29개 통과, 실패·건너뜀 0.
- PGlite에서 다섯 마이그레이션, RLS와 PL/pgSQL 함수를 실제 실행했다.
- KRW 4,900원과 USD 3.99 원금, 세금 별도 합계, 서명 검증, 중복 결제를 검사했다.
- 부분 환불 누적, 전체 환불, 환불 후 늦은 결제, 복수 주문의 권한 보존을 검사했다.
- 상품·통화·원금·세금 모드 변조와 클라이언트의 서버 RPC 실행을 거절했다.
- `node --check`와 `git diff --check`가 통과했다.

등록된 테스트 API 키로 실제 Store/Product/Variant를 조회했고, Lemon API가 만든 Checkout 미리보기에서
`₩4,900`, 할인 0, 합계 `₩4,900`, Test mode를 확인했다. 위치 정보가 없는 미리보기여서 세금은 0이었다.
실제 Google 로그인 사용자의 결제 화면 완료, 카드 승인, 웹훅 수신, 환불 재전송과 운영 승인 상태는 미검증이다.

## 완료된 외부 설정

1. KRW Store와 Single payment Product/Variant를 만들었다.
2. 무료 체험과 Pay what you want를 끄고 KRW 4,900원을 설정했다.
3. `LEMONSQUEEZY_TEST_API_KEY`, `LEMONSQUEEZY_WEBHOOK_SECRET`을 Supabase Secrets에 등록했다.
4. `order_created`, `order_refunded` 웹훅을 아래 주소로 등록했다.

```text
https://xrelgkdawkogrwxmwkcx.supabase.co/functions/v1/lemon-webhook
```

5. Paddle Dashboard의 기존 알림 목적지를 비활성화했다.

Product는 현재 Test mode `draft`이고 단일 기본 Variant는 `pending`이다. API custom Checkout은
정상 생성되지만 공개 Share URL은 404다. 운영 전 Product를 게시하고 Live mode로 복사해야 한다.
Global Store/Product/Variant는 사용자가 준비할 때 별도 마이그레이션으로 추가한다.

## 다음 검증

앱의 구매 버튼은 아직 Checkout API를 호출하지 않는다. 다음 단계에서 한국 시장으로 고정한 앱 요청을
연결하고, 실제 Google 로그인 계정으로 테스트 결제, 중복 클릭, 결제 중 앱 종료, 부분·전체 환불,
웹훅 재전송과 재로그인 복원을 확인한다. 앱 사용 잠금은 이 검증 이후 별도 단계에서 적용한다.

## 공식 계약

- [Checkout 생성](https://docs.lemonsqueezy.com/api/checkouts/create-checkout)
- [Custom data 전달](https://docs.lemonsqueezy.com/help/checkout/passing-custom-data)
- [웹훅 서명](https://docs.lemonsqueezy.com/help/webhooks/signing-requests)
- [웹훅 이벤트](https://docs.lemonsqueezy.com/help/webhooks/event-types)
- [통화](https://docs.lemonsqueezy.com/help/payments/currencies)
- [세금 별도 설정](https://docs.lemonsqueezy.com/help/payments/sales-tax-vat)
