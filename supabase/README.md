# Unfold 계정·구매 권한 기반

무료 다운로드, 무료 체험 없음, KRW 4,900 / USD 3.99 일회성 구매를 위한 서버 기반이다.
현재 운영 서버에 배포하지 않았고 실제 결제 공급자도 연결하지 않았다.

## 구현한 범위

- PostgreSQL 마이그레이션: 상품·독립 가격·주문·구매 권한·처리된 결제 이벤트.
- RLS: 공개 가격 읽기, 본인의 주문·권한만 읽기. 클라이언트의 쓰기와 서버 RPC 호출 금지.
- 서버 전용 `create_pending_order`: DB의 가격을 복사하며, 실제 판매는 기본 차단.
- 서버 전용 `apply_verified_payment`: 원자적 반영, 중복 처리, 위조 금액/통화/환경 거절,
  환불 뒤 늦은 결제 이벤트 차단, 다른 유효 주문 보존. **전체 환불**만 다룬다.
- `get-entitlement` Edge Function: Supabase Auth로 사용자 검증 후 같은 토큰의 RLS를 적용해 조회.
  요청이 지정한 사용자나 테스트/운영 구분을 신뢰하지 않는다.
- Node 테스트: PGlite의 실제 PostgreSQL SQL/RLS/함수 실행과 HTTP 핸들러 검사.

`apply_verified_payment`는 웹훅 엔드포인트가 아니다. 실제 결제 어댑터가 공급자 서명과 서버 주문을
검증한 **후** 호출해야 하는 내부 RPC다. 외부 요청을 그대로 이 함수에 전달하면 안 된다.
지금은 checkout·웹훅 URL을 노출하지 않는다. 결제사 선택 후 어댑터를 구현한다.

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

사용자가 제공한 프로젝트 주소를 아래에 기록했다. 공개키와 원격 설정은 아직 연결하지 않았으며,
앱의 기존 실행 흐름은 그대로다. 원격 DB 마이그레이션과 Edge Function 배포도 수행하지 않았다.

| 항목 | 값 |
|---|---|
| 프로젝트 URL | `https://xrelgkdawkogrwxmwkcx.supabase.co` |
| 프로젝트 참조 ID | `xrelgkdawkogrwxmwkcx` |
| Google에 등록할 승인된 리디렉션 URI | `https://xrelgkdawkogrwxmwkcx.supabase.co/auth/v1/callback` |
| Supabase Auth에 허용할 앱 반환 URL | `http://127.0.0.1:43821/auth/callback` |
| 앱 공개키 | 미입력. Settings → API Keys 또는 Connect의 Publishable key (`sb_publishable_...`) 사용 |

2026-09-27에 키 없이 `/auth/v1/health`를 조회해 `401 No API key found in request` 응답을 확인했다.
이는 프로젝트 API 게이트웨이 응답 확인이며 Auth 정상 동작, Google 설정, DB 적용의 증거는 아니다.
원격 프로젝트의 개발/운영 용도도 아직 확인하지 않았다. 아래 설정값은 개발 테스트용이다.
공개키는 앱용이며, Secret key와 `service_role` 키는 앱·웹 설정에 넣지 않는다.
[공개키 확인 안내](https://supabase.com/docs/guides/getting-started/api-keys).

다음 순서는 개발 프로젝트의 설정과 접근 권한이 준비된 뒤 수행한다.

1. Supabase CLI와 로컬 테스트용 Docker를 준비한다. 로컬만 쓰면 저장소 루트에서 `supabase start`로 새 개발 DB를 만든다.
2. `supabase/.env.example`을 `supabase/.env`로 복사해 로컬 설정만 입력한다. `.env`는 Git에서 제외된다.
3. 로컬 DB 초기화가 필요하면 테스트 데이터임을 확인한 후 `supabase db reset`을 실행한다. 이 명령은 로컬 DB를 지운다.
4. 원격 개발 프로젝트를 사용할 때는 프로젝트를 명시해 연결하고 마이그레이션 내용을 검토한 뒤 적용한다.
5. Google provider에 OAuth client ID/secret과 Supabase `/auth/v1/callback`을 등록한다. 앱 반환 주소는
   `http://127.0.0.1:43821/auth/callback`이다. Google의 콜백 URL과 앱 반환 URL을 혼동하지 않는다.
6. 로컬 Google provider는 `config.toml`에서 기본 비활성이다. 테스트 계정·client ID·환경변수 secret을 준비한 뒤 활성화한다.
7. Edge Function에는 `UNFOLD_ENVIRONMENT=test`, 정확한 `UNFOLD_ALLOWED_ORIGINS`를 설정한다.
   Supabase가 공급하는 `SUPABASE_URL`/`SUPABASE_ANON_KEY`로 Auth·PostgREST를 호출한다.
   HTTP는 loopback 개발 주소와 테스트 모드의 로컬 `kong:8000`만 허용한다. 운영 연결은 HTTPS다.

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
  "status": "active"
}
```

상태는 `active`, `unowned`, `revoked`. `401`은 재인증, `503`은 일시적 확인 실패다.
오류를 `unowned`로 바꾸지 않는다. 응답은 `Cache-Control: no-store`이고 오프라인 라이선스가 아니다.
앱 클라이언트는 사용자·상품·환경·스키마를 다시 대조한다. 테스트 권한을 운영 권한으로 쓰지 않는다.

## 앱 연결 준비

`src/Unfold.Core/SupabaseAccountClient.cs`는 다음 동작을 제공하되 AppRuntime에는 연결하지 않았다.

- PKCE 인증 시도 생성, Google 인증 URL, 코드 교환, 세션 갱신, 구매 권한 조회.
- 인증 시도 재사용/다른 콜백 차단, 토큰의 문자열 출력 방지, HTTP/응답 오류 구분.
- 최초 브라우저 실행·로컬 콜백 listener·macOS Keychain/Windows 자격 증명 저장소는 후속 Desktop 작업.

운영 UI·세션 보관·콜백 수신·오프라인 정책이 준비되기 전에 이 클라이언트만으로 앱을 잠그지 않는다.
계정 변경과 로그아웃에서 로컬 설정·휴식 기록·펫 파일을 삭제하지 않는다.

## 운영 전 조건

- 국내 원화 결제사 및 해외 결제사 선택, 세금 포함 가격, 국가 판정, 기기 수·업데이트·오프라인 정책 확정.
- 실제 Google 로그인, 결제·취소·환불·중복 알림·복원, PostgREST RLS와 테스트/운영 분리 검증.
- 결제 어댑터 구성 및 검증 후에만 필요한 가격의 `live_enabled`를 서버 관리자가 변경.
- 분리된 운영 프로젝트, 비밀키 등록, 운영 OAuth URL·정책 페이지·서명된 설치 파일 검증.

## 참고한 계약

- [Google 로그인](https://supabase.com/docs/guides/auth/social-login/auth-google)
- [PKCE](https://supabase.com/docs/guides/auth/sessions/pkce-flow)
- [Supabase Auth 공식 클라이언트](https://github.com/supabase/auth-js/blob/master/src/GoTrueClient.ts)
- [Edge Function 인증](https://supabase.com/docs/guides/functions/auth)
- [RLS](https://supabase.com/docs/guides/database/postgres/row-level-security)
- [PGlite](https://pglite.dev/docs/)
