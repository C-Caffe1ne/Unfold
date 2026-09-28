# Lemon Squeezy 전환 구현·검증

2026년 09월 28일. Paddle보다 유리한 판매 조건을 사용하기 위해 결제 공급자를
Lemon Squeezy로 전환했다. 앱 잠금과 운영 결제는 활성화하지 않았다.

## 변경

- `create-checkout`을 Lemon Squeezy Checkouts API 방식으로 교체했다.
- 서버가 KR/Global 시장을 내부 가격과 Store/Product/Variant에 매핑한다.
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
| 마이그레이션 | `202609280002 lemon_squeezy_sandbox`, `202609280003 lemon_squeezy_hardening` 기록 존재 |
| 서버 테이블 | `lemon_prices`, `lemon_events` RLS 활성 |
| 클라이언트 권한 | `anon` 예약 RPC, `authenticated` 결제 반영 RPC 실행 권한 없음 |
| 상품 매핑 | 0건. 외부 식별자를 받기 전 결제 생성 불가 |
| 판매 가격 | KR/Global 모두 `live_enabled=false` |
| Edge Function | `create-checkout` 버전 2, `verify_jwt=true`, Active |
| 기존 Paddle 함수 | `paddle-webhook` 제거 |
| 인증 없는 결제 생성 | HTTP 401 |
| Lemon 웹훅 | Secret 미등록으로 아직 배포하지 않음 |

## 자동 검사

`supabase` 폴더에서 Node.js 24.16으로 `npm test`를 실행했다.

- 27개 통과, 실패·건너뜀 0.
- PGlite에서 네 마이그레이션, RLS와 PL/pgSQL 함수를 실제 실행했다.
- KRW 4,900원과 USD 3.99 원금, 세금 별도 합계, 서명 검증, 중복 결제를 검사했다.
- 부분 환불 누적, 전체 환불, 환불 후 늦은 결제, 복수 주문의 권한 보존을 검사했다.
- 상품·통화·원금·세금 모드 변조와 클라이언트의 서버 RPC 실행을 거절했다.
- `node --check`와 `git diff --check`가 통과했다.

이 자동 검사는 Lemon Squeezy 실제 API 응답, 호스팅 결제 화면, Google 로그인 사용자,
실제 환불 재전송과 운영 승인 상태를 대신하지 않는다.

## 사용자가 준비할 항목

1. Test mode에서 KRW 스토어와 USD 스토어를 준비한다.
2. 각 스토어에 Unfold의 Single payment Product/Variant를 만든다.
3. 무료 체험과 Pay what you want를 끄고 tax-inclusive pricing을 끈다.
4. 두 스토어의 Store ID, Product ID, Variant ID와 `*.lemonsqueezy.com` 결제 호스트를 확인한다.
5. 테스트 API 키와 6자 이상의 웹훅 Secret을 Supabase Secrets에 직접 등록한다.
   Secret 이름은 각각 `LEMONSQUEEZY_TEST_API_KEY`, `LEMONSQUEEZY_WEBHOOK_SECRET`이다.
6. `order_created`, `order_refunded` 웹훅을 아래 주소로 등록한다.

```text
https://xrelgkdawkogrwxmwkcx.supabase.co/functions/v1/lemon-webhook
```

비밀키는 채팅, 앱 설정, Git 파일에 넣지 않는다. 식별자 네 종류는 비밀이 아니므로 전달해도 된다.

기존 Paddle 알림 목적지는 새 엔드포인트 검증이 끝날 때까지 결제 이력 확인용으로 남겨도 된다.
현재 `paddle-webhook` 함수는 제거됐으므로 Paddle Dashboard의 기존 알림 목적지는 비활성화해
불필요한 재시도를 막는다. Lemon 테스트 결제가 통과하면 Supabase의 이전 Paddle API Secret과
사용하지 않는 Checkout URL 설정도 제거한다.

## 다음 검증

외부 설정이 준비되면 `lemon_prices` 두 매핑을 등록하고 `lemon-webhook`을 배포한다.
그 뒤 Google 로그인 계정으로 KR/Global 테스트 결제, 중복 클릭, 결제 중 앱 종료,
부분·전체 환불, 웹훅 재전송과 재로그인 복원을 확인한다. 모두 통과한 뒤에만
결제 생성을 켜고, 앱 사용 잠금은 별도 단계에서 적용한다.

## 공식 계약

- [Checkout 생성](https://docs.lemonsqueezy.com/api/checkouts/create-checkout)
- [Custom data 전달](https://docs.lemonsqueezy.com/help/checkout/passing-custom-data)
- [웹훅 서명](https://docs.lemonsqueezy.com/help/webhooks/signing-requests)
- [웹훅 이벤트](https://docs.lemonsqueezy.com/help/webhooks/event-types)
- [통화](https://docs.lemonsqueezy.com/help/payments/currencies)
- [세금 별도 설정](https://docs.lemonsqueezy.com/help/payments/sales-tax-vat)
