# Lemon 구매 승인 복구 검증

## 원인

2026-09-29 한국 Test mode 주문은 Lemon Squeezy에서 결제 승인됐지만 `lemon-webhook` 호출이
모두 HTTP 422로 끝났다. Supabase 주문은 `pending`, `lemon_events`와 `entitlements`는 0건이라
앱의 **구매 확인**은 정상적으로 `unowned`를 받았고 계정 창을 닫지 않았다.

상품 항목의 계약가는 4,900원이었지만 영수증의 실제 subtotal·total은 4,894.16원이었다.
Lemon은 결제를 USD로 처리하고 표시 통화로 환산하므로 주문 금액에 환율 차이와 소수 단위가
생길 수 있다. 기존 핸들러는 실제 subtotal이 계약가 4,900원과 정확히 같은 정수여야 한다고
검사해 서명과 상품 식별자가 정상인 주문도 거절했다.

## 변경

- `first_order_item.price`를 서버가 생성한 4,900원 계약가와 대조한다.
- 서명된 주문의 subtotal·tax·total은 환율 적용 실제 결제 금액으로 별도 검증·저장한다.
- Lemon이 세금 0원 주문에 보내는 `tax_inclusive=true`는 허용하되, 양수 세금이 포함된 가격은
  계속 거절해 세금 별도 가격 계약을 유지한다.
- Lemon 금액은 최대 6자리 provider 소수와 KRW 변환 뒤 최대 8자리 소수를 보존한다.
- DB 주문 합계와 Lemon 이벤트 금액 컬럼을 `numeric(20,8)`로 확장했다.
- `apply_lemon_event`가 계약가와 실제 결제 합계를 각각 검증하도록 RPC 계약을 변경했다.
- 웹훅 실패 로그에는 payload나 개인정보 없이 `request`, `signature`, `event`, `order`,
  `payment` 단계와 공개 오류 코드만 기록한다.

소유 파일:

- `supabase/functions/_shared/lemon-money.mjs`
- `supabase/functions/_shared/lemon-webhook.mjs`
- `supabase/functions/_shared/payment-store.mjs`
- `supabase/functions/lemon-webhook/index.ts`
- `supabase/migrations/202609290001_lemon_currency_conversion.sql`
- `supabase/tests/lemon-handlers.test.mjs`
- `supabase/tests/lemon-integration.test.mjs`

## 검증

### 자동 검사

- 수정 전 실제 영수증 형태 회귀 검사: HTTP 422, 예상 200으로 실패 재현.
- 수정 후 집중 검사 3개 통과.
- `cd supabase && npm test`: 31개 통과, 실패·건너뜀 0.
- 중복 이벤트, 부분·전체 환불, 늦은 paid 이벤트, 계정 RLS, 상품·통화·계약가 변조 거절을 포함한다.
- 격리된 `UNFOLD_DATA_DIR`에서 계정 화면·창·loopback 집중 Release 검사 15개 통과.
- `git diff --check`: 통과.

최초 C# 실행은 제한된 샌드박스에서 MSBuild named pipe 생성이 거부돼 앱 테스트 전에 중단됐다.
같은 체크아웃을 허용된 로컬 실행으로 다시 검사해 15개가 통과했으며 제품 실패로 집계하지 않았다.

### 원격 적용

- `supabase db push --dry-run`: `202609290001_lemon_currency_conversion.sql` 1건만 예정됨을 확인.
- `supabase db push`: 위 마이그레이션 적용 성공.
- `supabase migration list`: 로컬·원격 `202609290001` 일치.
- `supabase functions deploy lemon-webhook`: 배포 성공.
- `supabase functions list`: `lemon-webhook` ACTIVE, 버전 11, JWT 검증 비활성(HMAC 전용).
- 배포 주소에 무서명 POST를 보내 HTTP 401 `invalid_signature`를 확인했다.

### 실제 주문 복구

- Lemon 관리자에서 실패한 `order_created`를 재전송했다.
- 배포된 웹훅은 HTTP 200, `{"received":true,"duplicate":false}`를 반환했다.
- 서명된 실제 payload에서 상품 항목 가격은 4,900원, subtotal·total은 4,894.16원,
  tax는 0원, `tax_inclusive=true`였다. 계약가와 실제 환산 결제액을 분리한 새 검증과 일치한다.
- Supabase `lemon_events`에 이벤트 1건이 저장됐고, 같은 사용자의 `entitlements`는
  Test 환경 `active`로 갱신됐다.

### macOS 실제 앱 종단 검사

다른 체크아웃에서 실행 중인 Unfold와 섞이지 않도록 현재 체크아웃의 Release 실행 파일을
고유한 임시 앱 번들과 새 `UNFOLD_DATA_DIR`로 실행했다. 실제 Google 계정 로그인 뒤
`get-entitlement`가 HTTP 200을 반환했고 계정 창이 닫힌 다음 메인 타이머 화면으로 전환됐다.
따라서 승인 메일을 받은 기존 구매 사용자가 앱의 구매 확인을 통과하지 못하던 문제는 현재
macOS 환경에서 복구됐다.

자동화가 연결된 Chrome은 callback 요청을 앱에 전달한 뒤 로컬 callback 탭에
`ERR_BLOCKED_BY_CLIENT`를 표시했지만, 앱은 같은 callback으로 토큰 교환과 권한 조회를 완료했다.
이 표시는 결제 권한 통과 실패가 아니며 일반 배포 브라우저에서의 재현 여부는 별도 확인 대상이다.

## 미검증·위험

- 실제 Windows에서 로그인·구매 확인은 이번 서버 수정 범위에서 미검증이다.
- 운영 Live 환경과 실제 과금 주문·환불은 Test mode 결과로 대체하지 않았다.
- 자동화 확장 없이 일반 Chrome·Safari·Edge에서 callback 안내 문구가 표시되는지는 미검증이다.

## 근거

- [Lemon Squeezy 통화 안내](https://docs.lemonsqueezy.com/help/payments/currencies)
- [Lemon Squeezy 웹훅 재시도](https://docs.lemonsqueezy.com/help/webhooks/webhook-requests)
- [Lemon Squeezy 주문 이벤트 예시](https://docs.lemonsqueezy.com/help/webhooks/example-payloads)
