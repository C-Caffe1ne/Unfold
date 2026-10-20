# Lemon 스토어 직접 구매 → 권한 연결

2026-10-20 · 기준 `393e00d`(v1.2.1 제품 `09c9e72`), 운영 Supabase `xrelgkdawkogrwxmwkcx`.
사용자 승인 후 운영 마이그레이션 `20261020095342_lemon_store_purchases`를 적용하고 `lemon-webhook` v22를 배포했다.
실제 결제·환불·Lemon 대시보드 조작은 하지 않았다.

## 원인

`unfoldpet.lemonsqueezy.com`의 바로 구매 주문에는 Unfold 주문 ID(`custom_data`)가 없다.
웹훅은 이런 주문을 200으로 무시했기 때문에 결제해도 권한이 생기지 않았다. [이전 확인](2026-10-20-lemon-entitlement-check.md)

## 변경

| 파일 | 내용 |
|---|---|
| `supabase/migrations/20261020095342_lemon_store_purchases.sql` | 직접 구매 기록 `lemon_store_purchases`·중복 이벤트 `lemon_store_events`(RLS, 클라이언트 접근 없음), 카탈로그 조회·기록·연결 함수, Google 로그인 트리거, `get_app_access` 연결 단계 |
| `supabase/functions/_shared/lemon-webhook.mjs` | 내부 주문 ID가 없으면 Store/Product/Variant로 카탈로그 가격을 찾고 기존 결제 검사(`fact`)와 구매자 이메일 형식을 확인해 기록 |
| `supabase/functions/_shared/payment-store.mjs` | `lemon_store_price`·`record_lemon_store_purchase` RPC |
| `supabase/tests/lemon-store-purchase.test.mjs` 외 4개 검사 준비 코드 | 새 검사 8개, 다른 검사의 Supabase `auth.identities` 준비 |

## 동작

- 카탈로그에 없는 상품은 무시(200). 가격·통화·테스트 모드·할인·결제 상태·이메일이 다르면 422로 반영하지 않는다.
- 구매자 이메일(소문자·앞뒤 공백 제거)과 같은 **확인된 Google 계정이 정확히 하나**일 때만 연결한다.
  - 계정이 이미 있으면 웹훅 처리 중 바로 연결한다.
  - 없으면 그 계정이 웹·앱 어디서든 Google 로그인할 때(`auth.identities` 트리거) 연결한다. 실패해도 로그인은 막지 않는다.
  - 앱 `get-app-access`가 부르는 `get_app_access()`도 먼저 연결을 시도한다(예비 경로). 실패하면 경고만 남기고 기존 판정을 한다.
- 연결은 일반 Live 주문을 만들어 `apply_verified_payment`로 반영한다. 같은 주문 재전송은 중복 처리, 전액 환불은 권한 회수,
  로그인 전 전액 환불된 구매는 연결하지 않는다.
- `get_app_access()`의 판정 규칙은 운영 본문(MD5 `ccf1f74d…`, 저장소 `tests/fixtures/app-access.sql`과 동일)을 그대로 두고
  연결 단계만 앞에 넣었다. 쓰기를 하므로 `STABLE`에서 `VOLATILE`이 된다. 운영 `get-app-access` v7은 POST로 호출해 쓰기 트랜잭션에서 실행된다.

## 적용 전 검증

| 항목 | 결과 |
|---|---|
| 서버 자동 검사 | Node 24 + PGlite 52/52 통과(기존 44 + 새 8) |
| 새 검사 | 기존 계정 즉시 연결, 가입 전 구매 → 첫 Google 로그인 연결(웹이 읽는 권한 행 `active`), 재전송 1건, 전액 환불 `revoked`, 로그인 전 환불 미연결, 연결 실패 시 로그인 유지 후 앱 확인에서 연결, 미확인·다른 제공자·비슷한 이메일 거부, 다른 variant 무시와 변조 422, 클라이언트 읽기·호출 거부. Auth 테이블은 `supabase_auth_admin` 소유·쓰기로 재현 |
| 운영 상품 정보 모의 | 실제 HTTP 저장소 어댑터(이름 붙은 RPC 인자)를 거쳐 USD 세금 포함·별도·없음 모두 `active`, 재전송 `duplicate`, 전액 환불 `revoked`, 다른 variant 무시 |
| 운영 읽기 전용 조회 | Google identity 7개 모두 `email_verified` 불리언 true, 여러 계정이 공유한 이메일 0, 새 이름과 충돌하는 객체 없음, `postgres`의 `auth.identities` TRIGGER 권한 있음, 기존 auth 트리거 없음 |

## 운영 적용과 확인

| 항목 | 결과 |
|---|---|
| 배포 전 대조 | 웹훅 v21의 공유 파일(`payment-http`·`lemon-money`·`index.ts`)이 저장소와 같음. 바뀐 파일은 `lemon-webhook.mjs`·`payment-store.mjs`뿐 |
| 마이그레이션 | 운영 버전 `20261020095342`. 새 함수 8개(`get_app_access` 포함)의 휘발성·definer·본문 MD5가 저장소 마이그레이션으로 만든 DB와 모두 같음 |
| 권한 | 새 테이블 2개 RLS 켜짐, 권한은 `postgres`·`service_role`만. 함수 실행은 `claim_lemon_store_purchases`·`get_app_access`만 `authenticated`, 나머지는 서버 전용 |
| 트리거 | `link_lemon_store_purchases` 활성(`O`), `AFTER INSERT OR UPDATE ON auth.identities … WHEN provider = 'google'` |
| 웹훅 | v22 ACTIVE, `verify_jwt=false` 유지, 배포본 내용이 보낸 파일과 같음. 서명 없는 요청은 `invalid_signature` 401 |
| 기존 데이터 | 주문·권한·결제 이벤트·무료 권한·사용자 수가 적용 전과 같음. 새 테이블 0행 |
| 기존 판정 | 항상 되돌리는 트랜잭션에서 `authenticated` 역할로 호출: Live 구매자 `active`, 무료 권한 `active`, 권한 없음 `unowned`. `anon`의 연결 호출은 거부 |
| 보안 권고 | 새 WARN 1건: `claim_lemon_store_purchases`를 로그인 사용자가 호출 가능. 호출자 자신의 확인된 Google 이메일 구매만 연결하므로 의도한 노출이며 `get_app_access`(호출자 권한)가 이 실행 권한을 쓴다. 새 테이블의 "RLS 정책 없음" INFO는 다른 서버 테이블과 같음 |

## 미검증·위험

- 실제 Lemon 직접 구매 이벤트(이메일·세금 필드)와 실제 Google 로그인 시 트리거 실행은 보지 못했다. 첫 실제 구매가 최종 확인이다.
- Lemon 대시보드의 Variant 가격이 DB `prices`(USD 299)와 달라지면 직접 구매는 422로 반영되지 않는다.
- 이번 적용 전 직접 구매는 자동 반영되지 않는다. 10/13~10/20 로그에는 없었고, 그 이전 구매가 있으면 Lemon에서 웹훅을 다시 보내야 한다.
- 이메일이 다르거나 확인된 Google 계정이 없으면 지원에서 연결한다:
  `select public.link_lemon_store_purchase('live', '<Lemon 주문 번호>', '<사용자 UUID>');`
- 운영 `get-entitlement` v17은 저장소 소스와 다르다(관리자 역할 조회 포함). 이번 변경에서 다시 배포하지 않았다.
- 연결을 위해 구매자 이메일을 서버에 보관한다. 클라이언트는 읽을 수 없고 로그·응답에 남기지 않는다.
