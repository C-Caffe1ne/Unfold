# Unfold 계정·구매 권한 기반

무료 다운로드, 무료 체험 없음, KRW 4,900 / USD 3.99 일회성 구매를 위한 서버 기반이다.
2026-09-28에 결제 공급자를 Lemon Squeezy로 전환했다. 가격은 **세금 별도**이며,
Supabase 테스트 프로젝트에는 한국 상품 매핑과 인증 필수 `create-checkout`, 서명 필수
`lemon-webhook`을 배포했다. 한국 Test mode Checkout만 활성화돼 있고 Global은 아직 사용할 수 없다.
[전환 구현·검증](../docs/validation/2026-09-28-lemon-squeezy-transition.md).

## 구현한 범위

- PostgreSQL 마이그레이션: 상품·독립 가격·주문·구매 권한·처리된 결제 이벤트.
- RLS: 공개 가격 읽기, 본인의 주문·권한만 읽기. 클라이언트의 쓰기와 서버 RPC 호출 금지.
- 서버 전용 Lemon Squeezy 주문 예약: 로그인 사용자와 서버 가격만 사용하고, 요청 ID로 중복 생성을 막는다.
- 서버 전용 `apply_verified_payment`: 원자적 반영, 중복 처리, 위조 금액/통화/환경 거절,
  환불 뒤 늦은 결제 이벤트 차단, 다른 유효 주문 보존. **전체 환불**만 다룬다.
- `get-entitlement` Edge Function: Supabase Auth로 사용자 검증 후 같은 토큰의 RLS를 적용해 조회.
  요청이 지정한 사용자나 테스트/운영 구분을 신뢰하지 않는다. 서버 관리 `account_roles`에서
  본인 역할만 함께 읽으며, 역할이 없으면 일반 사용자로 처리한다.
- `create-checkout` Edge Function: 테스트 Variant를 확인한 뒤 30분짜리 호스팅 결제 URL을 생성한다.
  할인·체험·수량 변경·클라이언트 금액을 허용하지 않는다.
- `lemon-webhook`: 원문 HMAC SHA-256 서명, 주문·상품·통화·원금·세금 모드를 확인하고
  결제와 누적 환불을 원자적으로 권한에 반영한다.
- Lemon API의 KRW 금액과 내부 원 단위를 서버 경계에서 변환한다. DB 가격은 4,900이고
  Lemon API `custom_price` 및 주문 항목 계약가는 490,000으로 검증한다. Lemon은 표시 통화를
  USD로 처리하면서 실제 주문 subtotal·tax·total에 환율 소수값을 반환할 수 있으므로, 서명된
  주문 합계는 계약가와 분리해 소수 단위까지 저장한다.
- Node 테스트: PGlite의 실제 PostgreSQL SQL/RLS/함수 실행과 HTTP 핸들러 검사.

`apply_verified_payment`는 공개 웹훅이 아니라 검증 완료 후 서버 어댑터만 호출하는 내부 RPC다.
외부 요청을 그대로 전달하지 않는다.

## 로컬 검사

Node.js 24 이상에서 이 폴더를 기준으로 실행한다. 잠금 파일을 사용한다.

```sh
npm ci --ignore-scripts
npm test
```

DB 검사는 일회용 메모리 DB에 최소 `auth.users`, `auth.uid()`와 Supabase 역할을 만들어
마이그레이션을 실행한다. Auth 인증만 모의하며 SQL·RLS·권한 검사는 실제 엔진에서 실행한다.
Supabase 전체 스택, PostgREST 실제 연결, JWT 게이트웨이, 운영 동시성·부하의 검증을 대체하지 않는다.

## Supabase 개발 프로젝트 연결

사용자가 제공한 프로젝트 주소와 공개키로 연결한 뒤 2026-09-28에 테스트 스키마를 적용했다.
공개키는 Git에서 제외되는 `supabase/.env.client`에 저장했다. 앱은 아직 이 파일을 읽지 않으며,
기존 실행 흐름과 사용 잠금은 그대로다.

| 항목 | 값 |
|---|---|
| 프로젝트 URL | `https://xrelgkdawkogrwxmwkcx.supabase.co` |
| 프로젝트 참조 ID | `xrelgkdawkogrwxmwkcx` |
| Google에 등록할 승인된 리디렉션 URI | `https://xrelgkdawkogrwxmwkcx.supabase.co/auth/v1/callback` |
| Supabase Auth에 허용할 앱 반환 URL | `http://127.0.0.1:43821/auth/callback` |
| 앱 공개키 | 로컬 `.env.client`에 저장. 공유 예시는 `client.env.example` 사용 |

원격에는 `202609280002`~`202609290004` 마이그레이션과 `create-checkout` 버전 27,
`get-entitlement` 버전 11, `lemon-webhook` 버전 11이 적용됐다. 개인·회사 관리자 Auth 사용자 ID
두 개는 `account_roles`에 할당됐으며 후속 검증 마이그레이션이 두 행의 존재를 확인했다.
`lemon_prices`에는 한국 매핑 1건만 있으며 Global 매핑은 없다.
공개 판매 가격의 `live_enabled`는 모두 false다. RLS가 켜져 있고 `anon`과
`authenticated`는 예약·결제 반영 RPC를 실행할 수 없다. 인증 없는 결제 생성 요청은 HTTP 401이다.
아래 설정값은 개발 테스트용이다.
공개키는 앱용이며, Secret key와 `service_role` 키는 앱·웹 설정에 넣지 않는다.
[공개키 확인 안내](https://supabase.com/docs/guides/getting-started/api-keys).

### Lemon Squeezy 테스트 연결 상태

한국 Store `485125`, Product `1393777`, Variant `2176689`, Host
`dokhustudio.lemonsqueezy.com`을 서버 카탈로그에 등록했다. 실제 Lemon API 미리보기는 `₩4,900`,
할인 0, Test mode로 생성됐다. `UNFOLD_CHECKOUT_ENABLED=true`이지만 한국 매핑만 있으므로
`market=GLOBAL` 요청은 `checkout_not_ready`로 실패한다. Product는 Test mode에서 게시됐고,
실제 macOS 앱의 Google 로그인 사용자가 만든 API custom Checkout이 브라우저의 `₩4,900`
Test mode 결제 폼으로 열리는 것을 확인했다. 승인된 실제 Test mode 주문을 웹훅으로 재전송해
HTTP 200, 이벤트 저장, `active` 권한 반영, 같은 구매 사용자의 앱 메인 화면 진입까지 확인했다.

공개 Share Checkout UUID는 `33a0b33e-7a8c-4022-9b79-2ab02113ede1`이다. 이 값은 수동 검증과
공유 주소에만 쓰며 Lemon API Variant ID가 아니다. 런타임 카탈로그와 웹훅 검증은 숫자 Variant
`2176689`를 사용한다.

테스트 API 키와 웹훅 Secret은 Supabase Secrets에 있고, 웹훅은
`https://xrelgkdawkogrwxmwkcx.supabase.co/functions/v1/lemon-webhook`에서
`order_created`, `order_refunded`를 받는다. Secret 값은 앱·문서·Git에 저장하지 않는다.
API 키는 서버에서 앞뒤 공백과 복사 시 붙은 줄바꿈만 제거한 뒤 사용하며, 값 내부의 공백이나
16 KiB를 넘는 비정상 HTTP 헤더는 거부한다. Lemon이 공개하지 않은 최소 길이나 512자 제한은
적용하지 않고 실제 키 유효성은 공급자가 판정한다. Variant 조회 시 연결된 Product가 같은 Store의
게시된 Test mode 상품인지도 확인해 미게시 상품의 깨진 Checkout URL을 앱에 전달하지 않는다.
Checkout은 Variant 확인과 생성 요청을 순서대로 수행하므로 Lemon 요청은 각각 최대 15초,
앱의 Checkout 전체 요청은 최대 35초를 허용한다. 일반 로그인·권한 조회는 기존 10초 제한을 유지한다.
Checkout 생성 응답은 금액·상품·환경·만료·호스트를 모두 검증한다. 이미 저장한 Checkout을 재개할 때는
Lemon 조회 응답에서 생성 시점 전용 `preview`가 생략될 수 있으므로 주문에 저장한 Checkout ID와 URL을
기준으로 상품·환경·만료·호스트를 다시 확인한다.

현재 원격 `paddle-webhook` 함수, Paddle API Secret과 사용하지 않는 고정 Checkout URL 설정은 제거됐다.
Paddle Dashboard의 기존 알림 목적지도 비활성화해 실패 재전송을 막았다.

### Google 로그인 확인 사항

1. Google Auth Platform에서 OAuth 클라이언트를 **웹 애플리케이션** 유형으로 생성한다.
   개발 중에는 테스트 사용자를 등록하고 승인된 리디렉션 URI에 위 Supabase 콜백 주소를 입력한다.
2. Supabase Dashboard → Authentication → Sign In / Providers → Google에 Client ID와
   Client Secret을 직접 입력하고 활성화한다. Secret은 채팅·앱·저장소에 복사하지 않는다.
3. Authentication → URL Configuration의 Redirect URLs에 위 앱 반환 URL을 등록한다.
   원격 설정은 로컬 `config.toml` 수정만으로 바뀌지 않는다. Google 로그인만 제공할 정책에 맞춰
   이메일 provider의 현재 활성 상태도 운영 전 정리한다.
4. 실제 테스트 Google 계정으로 앱의 브라우저 로그인, 로컬 콜백, 권한 조회까지 확인한다.

클라이언트 공개 연결 설정(`.env.client`)과 Edge Function/Google 서버 설정(`.env`)은 별도다.
`.env.client`에는 URL과 Publishable key만 있으며 Google Secret, 사용자 세션이나 관리 키를 넣지 않는다.

```sh
# 저장소 루트, 로컬 Supabase 실행 후
supabase functions serve get-entitlement --env-file supabase/.env
```

배포에는 프로젝트 기본 HTTPS 주소를 사용할 수 있어 자체 도메인이 필요 없다.
`verify_jwt=true`를 유지하고 실제 사용자 access token으로 호출한다. Auth 검증을 건너뛰는
진단 옵션이나 service-role 키를 앱/웹에 배포하지 않는다. 공개 key는 비밀 인증 수단이 아니다.

## API 계약

`GET /functions/v1/get-entitlement`

- `Authorization: Bearer <user access token>`
- `apikey: <Supabase public key>`
- 사용자/상품/환경 쿼리는 사용하지 않는다. 상품은 `unfold`, 환경은 서버 설정이다.

```json
{
  "schema_version": 1,
  "user_id": "11111111-1111-4111-8111-111111111111",
  "product_id": "unfold",
  "environment": "test",
  "status": "active",
  "role": "admin"
}
```

상태는 `active`, `unowned`, `revoked`, 역할은 `member`, `admin`이다. 역할 행은 서비스 역할만
추가·변경할 수 있고 로그인 사용자는 RLS로 자신의 행만 읽는다. 역할 행이 없으면 `member`다.
`401`은 재인증, `503`은 일시적 확인 실패다.
오류를 `unowned`로 바꾸지 않는다. 응답은 `Cache-Control: no-store`이고 오프라인 라이선스가 아니다.
앱 클라이언트는 사용자·상품·환경·스키마를 다시 대조한다. 테스트 권한을 운영 권한으로 쓰지 않는다.

`POST /functions/v1/create-checkout`

- 사용자 access token과 Supabase 공개키가 필요하다.
- 본문은 `{"market":"KR|GLOBAL","request_id":"UUID"}`만 허용한다.
- 응답의 `checkout_url`은 검증된 Lemon Squeezy 호스팅 결제 주소다.
- 한국 매핑은 활성화돼 있다. Global은 매핑이 없어 `503 checkout_not_ready`를 반환한다.
- 현재 Avalonia 구매 버튼은 한국 시장만 이 API로 호출한다. Global은 UI에서도 숨긴다.

`POST /functions/v1/lemon-webhook`

- 사용자 JWT 대신 Lemon Squeezy의 `X-Signature`를 원문 바이트로 검증한다.
- `order_created`와 `order_refunded`만 처리하며 다른 이벤트는 확인 응답 후 무시한다.
- 서버에서 만든 내부 주문과 Store/Product/Variant/통화/원금/세금 별도 조건이 모두 맞아야 반영한다.

## 앱 연결 준비

`src/Unfold.Core/SupabaseAccountClient.cs`는 Desktop의 A안 계정 화면에서 명시적인 로그인 버튼을 통해 사용한다.

- PKCE 인증 시도 생성, Google 인증 URL, 코드 교환, 세션 갱신, 구매 권한 조회.
- 인증 시도 재사용/다른 콜백 차단, 토큰의 문자열 출력 방지, HTTP/응답 오류 구분.
- Desktop에서 시스템 브라우저 실행과 IPv4 loopback 콜백을 연결했다. macOS에서 실제 Google 계정 인증,
  Test 권한 조회와 메인 화면 진입을 확인했다.
- 인증 토큰 세션은 계정 창이 열린 동안 메모리에만 둔다. macOS Keychain/Windows 자격 증명 저장소와 재실행 시 복원은 후속 작업이다.
- 구매 확인이 끝난 뒤 토큰은 폐기하고 사용자 ID·이메일·검증된 역할만 앱 종료까지 메모리에 둔다.
  디버그 설정 카드와 알림 미리보기 실행은 `admin` 역할에서만 허용한다. 로컬 `settings.json`의
  `DebugToolsEnabled`를 직접 바꿔도 일반 계정에서는 저장·실행할 수 없다.

사용자 결정에 따라 이번 단계에서는 앱 사용을 잠그지 않는다. 결제 연결·세션 보관·오프라인 정책을 갖춘 뒤 사용 잠금을 적용한다.
계정 변경과 로그아웃에서 로컬 설정·휴식 기록·펫 파일을 삭제하지 않는다.
화면 문구·표시 가격·접속 설정의 편집 위치는 [계정 화면 편집 안내](../docs/account-screen.md)에 있다.

## 운영 전 조건

- Lemon Squeezy 스토어 승인·국내외 통화 결제, 세금 별도 표시, 국가 판정, 기기 수·업데이트·오프라인 정책 확정.
- Windows 실제 Google 로그인·결제 확인, 운영 주문·취소·환불·복원, 테스트/운영 분리 검증.
- 결제 어댑터 구성 및 검증 후에만 필요한 가격의 `live_enabled`를 서버 관리자가 변경.
- 분리된 운영 프로젝트, 비밀키 등록, 운영 OAuth URL·정책 페이지·서명된 설치 파일 검증.

## 참고한 계약

- [Google 로그인](https://supabase.com/docs/guides/auth/social-login/auth-google)
- [PKCE](https://supabase.com/docs/guides/auth/sessions/pkce-flow)
- [Supabase Auth 공식 클라이언트](https://github.com/supabase/auth-js/blob/master/src/GoTrueClient.ts)
- [Edge Function 인증](https://supabase.com/docs/guides/functions/auth)
- [RLS](https://supabase.com/docs/guides/database/postgres/row-level-security)
- [PGlite](https://pglite.dev/docs/)
- [Lemon Squeezy 결제 생성](https://docs.lemonsqueezy.com/api/checkouts/create-checkout)
- [Lemon Squeezy Checkout 객체](https://docs.lemonsqueezy.com/api/checkouts/the-checkout-object)
- [Lemon Squeezy 상품 공유](https://docs.lemonsqueezy.com/help/products/sharing-products)
- [Lemon Squeezy 웹훅 서명](https://docs.lemonsqueezy.com/help/webhooks/signing-requests)
- [Lemon Squeezy 웹훅 이벤트](https://docs.lemonsqueezy.com/help/webhooks/event-types)
