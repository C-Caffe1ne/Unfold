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
| Edge Function | `create-checkout` 버전 27/JWT 필수, `get-entitlement` 버전 10/JWT 필수, `lemon-webhook` 버전 9/HMAC 필수, Active |
| 기존 Paddle 함수 | `paddle-webhook` 제거 |
| 인증 없는 결제 생성 | HTTP 401 |
| 서명 없는 Lemon 웹훅 | HTTP 401 |
| Checkout 설정 | 한국 Test mode 활성, Global 미매핑으로 생성 불가 |

## 자동 검사

`supabase` 폴더에서 Node.js 24.16으로 `npm test`를 실행했다.

- 30개 통과, 실패·건너뜀 0.
- PGlite에서 다섯 마이그레이션, RLS와 PL/pgSQL 함수를 실제 실행했다.
- KRW 4,900원과 USD 3.99 원금, 세금 별도 합계, 서명 검증, 중복 결제를 검사했다.
- 부분 환불 누적, 전체 환불, 환불 후 늦은 결제, 복수 주문의 권한 보존을 검사했다.
- 상품·통화·원금·세금 모드 변조와 클라이언트의 서버 RPC 실행을 거절했다.
- `node --check`와 `git diff --check`가 통과했다.

등록된 테스트 API 키로 실제 Store/Product/Variant를 조회했고, Lemon API가 만든 Checkout 미리보기에서
`₩4,900`, 할인 0, 합계 `₩4,900`, Test mode를 확인했다. 위치 정보가 없는 미리보기여서 세금은 0이었다.
실제 macOS 앱에서 Google 로그인 후 구매하기를 실행해 API custom Checkout이 기본 브라우저에 열리고,
게시된 `Unfold 이용권`, `₩4,900`, Test mode 결제 폼이 표시되는 것까지 확인했다. 카드 승인,
웹훅 수신, 환불 재전송과 운영 승인 상태는 미검증이다.

## 완료된 외부 설정

1. KRW Store와 Single payment Product/Variant를 만들었다.
2. 무료 체험과 Pay what you want를 끄고 KRW 4,900원을 설정했다.
3. `LEMONSQUEEZY_TEST_API_KEY`, `LEMONSQUEEZY_WEBHOOK_SECRET`을 Supabase Secrets에 등록했다.
4. `order_created`, `order_refunded` 웹훅을 아래 주소로 등록했다.

```text
https://xrelgkdawkogrwxmwkcx.supabase.co/functions/v1/lemon-webhook
```

5. Paddle Dashboard의 기존 알림 목적지를 비활성화하고 Supabase의 Paddle API Secret과
   이전 고정 Checkout URL 설정을 제거했다.

Product `1393777`은 Test mode에서 게시됐다. 공개 Checkout UUID
`33a0b33e-7a8c-4022-9b79-2ab02113ede1`로 열린 Share Checkout에서도 `Unfold 이용권`,
`₩4,900`, Test mode를 확인했다. 이 UUID는 공유 Checkout 주소의 식별자이며 Lemon API가 요구하는
숫자 Variant ID가 아니므로 서버 카탈로그에는 Variant `2176689`를 계속 사용한다.
Global Store/Product/Variant와 Live mode 상품은 사용자가 준비할 때 별도 마이그레이션으로 추가한다.

## 다음 검증

앱 구매 버튼은 한국 시장으로 고정한 Checkout API를 호출한다. 다음 단계에서 테스트 카드 결제,
중복 클릭, 결제 중 앱 종료, 부분·전체 환불, 웹훅 재전송과 재로그인 복원을 확인한다.
앱 사용 잠금은 이 검증 이후 별도 단계에서 적용한다.

실제 앱 호출에서 Supabase의 `LEMONSQUEEZY_TEST_API_KEY`가 서버 형식 검사를 통과하지 못해
`create-checkout`이 부팅 중 500으로 종료되는 문제를 확인했다. 원인은 Lemon Squeezy가 공개하지 않은
API 키 길이 계약을 서버가 임의의 512자로 제한한 것이었다. 앞뒤 공백은 제거하고 내부 공백과
16 KiB를 넘는 비정상 HTTP 헤더만 거부하며, 실제 키 유효성은 Lemon API가 판정하도록 수정했다.
1,024자 키가 손실 없이 Authorization 헤더에 전달되는 회귀 검사를 추가했다.

Secret 교체 뒤 실제 앱 요청은 기존 Lemon API 8초 제한까지 대기한 다음 실패했다. Variant 확인과
Checkout 생성이 순차 실행되는 계약에 맞춰 Lemon 요청 제한을 각각 15초로, 앱의 Checkout 전체
요청 제한을 35초로 조정했다. 로그인·권한 조회의 10초 제한은 유지한다.

긴 키 허용 후 처음 생성한 custom Checkout은 Product `1393777`이 `draft`였을 때 만들어져 404를
반환했다. `create-checkout`은 Variant 조회에 Product를 포함해 Store/Product/Test mode와
`published` 상태를 함께 검증하도록 바꿨다. 미게시 상품은 사용할 수 없는 URL을 앱에 반환하지 않고
`503 checkout_not_ready`로 차단한다.

상품 게시 뒤 기존 요청을 재사용하면 Lemon의 Checkout 조회 응답에 생성 시점 전용 `preview`가 없거나
부분 값만 들어와 `payment_mismatch`가 발생했다. 생성 응답에서는 금액·상품·환경·만료·호스트를 모두
검증하고, 재조회에서는 주문에 저장된 Checkout ID와 URL을 기준으로 상품·환경·만료·호스트를 검증하도록
분리했다. 결제 생성·재개 실패 로그는 비밀값 없이 `단계:공개 오류 코드`만 남긴다.

원격 `create-checkout` 버전 27에 이 수정이 적용됐고 Active 상태를 확인했다. 새 요청으로 실제 macOS
앱의 Google 로그인 → 구매하기를 실행해 서명된 `dokhustudio.lemonsqueezy.com/checkout/custom/...`
주소가 기본 브라우저에서 정상 결제 폼으로 열리는 것까지 검증했다. 결제는 제출하지 않았다.

## 공식 계약

- [Checkout 생성](https://docs.lemonsqueezy.com/api/checkouts/create-checkout)
- [Checkout 객체와 생성 시점 preview](https://docs.lemonsqueezy.com/api/checkouts/the-checkout-object)
- [Test mode 상품 게시 조건](https://docs.lemonsqueezy.com/help/getting-started/test-mode)
- [상품 공유 Checkout](https://docs.lemonsqueezy.com/help/products/sharing-products)
- [Custom data 전달](https://docs.lemonsqueezy.com/help/checkout/passing-custom-data)
- [웹훅 서명](https://docs.lemonsqueezy.com/help/webhooks/signing-requests)
- [웹훅 이벤트](https://docs.lemonsqueezy.com/help/webhooks/event-types)
- [통화](https://docs.lemonsqueezy.com/help/payments/currencies)
- [세금 별도 설정](https://docs.lemonsqueezy.com/help/payments/sales-tax-vat)
