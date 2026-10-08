# 계정별 무료 이용 코드

2026년 10월 8일 · 공개 Beta v1.1.2의 테스터 이용

테스터는 Google 로그인 → 우측 하단 **코드 입력** → `admin` → **확인** 순서로 진행한다.
Supabase Auth가 Google 계정 ID와 이메일을 보관하고, `free_access_grants`가 해당 계정 ID에 무료 이용 권한과 등록 시각을 연결한다.
앱은 이메일·접근 토큰·이용 권한을 일반 파일에 저장하지 않는다. 갱신 토큰과 계정 ID는 macOS Keychain 또는 Windows 자격 증명 관리자에 보관한다.
재실행 시 저장된 세션과 서버 권한을 먼저 복원하며 정상 복원 시 Google 로그인과 코드 재입력 없이 시작한다.
로그아웃·서버의 세션 무효 확인 후에는 다시 로그인한다. 통신 실패 시 저장 정보를 유지하고 재시도를 제공한다.
`admin`은 무료 이용 코드의 이름일 뿐 관리자 권한을 부여하지 않는다.
2026-09-30 최초 등록 설정에는 인원·기간 제한이 없었다. 현재 유효성은 서버 응답을 따르며 이번 문서 갱신에서 원격 코드 설정을 다시 조회하지 않았다.

## 권한 구조

- `get-app-access`: Auth에서 Google identity 확인 후 `get_app_access()` 호출. 현재 사용자에게 유효한 무료 권한 또는 Live 구매 권한이 있으면 `active`.
- `redeem-access-code`: 인증된 Google 사용자만 요청 가능. 입력은 `code` 한 항목이고 사용자 ID·이메일·환경은 받지 않는다. SHA-256 해시를 서비스 전용 RPC에 전달한다.
- `access_codes`: 해시·라벨·사용 가능 여부·등록 기한·인원 제한·권한 만료일. 클라이언트 읽기·쓰기는 불가.
- `free_access_grants`: 사용자 ID·코드 ID·상품·상태·권한 만료일·등록 시각. 사용자는 자기 권한만 읽을 수 있고 변경할 수 없다.
- `access_code_attempts`: 계정당 10분에 5회 제한. 행은 계정당 하나이며 원문 코드와 토큰은 보관하지 않는다.

사용자 및 코드 잠금으로 동일 계정 중복 등록과 인원 제한을 처리한다. 같은 계정의 재시도는 등록 인원에 추가되지 않는다.
등록 코드의 사용을 중단하거나 등록 기한이 끝나도 이미 발급된 권한은 유지한다. 권한 회수는 별도로 수행한다.
같은 코드로 회수·만료된 권한을 다시 활성화할 수 없다. 다른 유효한 코드나 Live 구매가 있다면 이용 가능하다.
결제 환불은 무료 이용 권한을 변경하지 않고, Test 결제는 앱 접근을 허용하지 않는다.

## 추후 코드 발급

별도 앱 빌드 없이 서버에 코드를 추가할 수 있다. 아래는 2026-09-30에 사용한 발급 도구의 예시다.
현재 체크아웃에는 `supabase/scripts/create-access-code.mjs`가 포함되지 않아 이 경로에서 실행할 수 없다.
예시는 100명에게 등록을 허용하고 등록 기한을 설정한다.
무작위 코드와 SQL을 출력하므로 **code는 테스터에게**, **sql은 Supabase SQL Editor에** 전달한다. 옵션을 생략하면 해당 제한이 없다.

```sh
node supabase/scripts/create-access-code.mjs --label beta-100 --max-uses 100 --redeem-until 2026-12-31T14:59:59Z
```

`--grant-until`은 발급된 이용 권한 자체의 만료일이다. 모든 날짜는 UTC를 사용한다.
코드는 영문·숫자·`-`·`_` 최대 64자, 대소문자 구분, 앞뒤 공백 제거 규칙이다.
원문 코드가 유출되면 그 코드를 아는 Google 계정도 등록할 수 있으므로 배포 대상과 인원·기간은 운영자가 정한다.

등록 중단:

```sql
update public.access_codes set enabled = false where label = 'beta-admin';
```

해당 코드로 발급된 모든 무료 권한 회수:

```sql
update public.free_access_grants set status = 'revoked'
where code_id = (select id from public.access_codes where label = 'beta-admin');
```

계정별 회수는 `user_id` 조건을 추가한다. 실행 중 앱은 기존 5분 권한 조회에서 회수를 반영한다.

## 서버 반영

이 절은 2026-09-30의 서버 반영 절차와 기록이다. 현재 체크아웃에는
`get-app-access`·`redeem-access-code` 함수 소스와 `202609300002_access_codes.sql` 파일이 포함되지 않는다.
현재 앱의 호출 계약은 `src/Unfold.Core/SupabaseAccountClient.cs`와
[계정 안내](account-screen.md), 원격 반영 당시 증거는 아래 구현 보고서를 따른다.

대상 프로젝트: `xrelgkdawkogrwxmwkcx`. 새로운 마이그레이션은 `202609300002_access_codes.sql`이다.
이미 반영한 SQL을 다시 실행하지 않는다. SQL Editor로 적용하면 기존 `supabase_migrations.schema_migrations` 형식에 맞춰 이력을 함께 기록한다.
CLI 배포는 기존 관리 로그인으로 수행하고 개인 토큰·서비스 비밀키를 앱이나 채팅에 넣지 않는다.

```sh
npx supabase functions deploy get-app-access --project-ref xrelgkdawkogrwxmwkcx --use-api
npx supabase functions deploy redeem-access-code --project-ref xrelgkdawkogrwxmwkcx --use-api
```

두 함수는 `verify_jwt = true`와 추가 Auth 사용자 검사를 사용한다.
서버 기본 Supabase 키를 사용하므로 새로운 결제 Secret이나 Lemon Live 설정은 필요하지 않다.
[Supabase 함수 인증 문서](https://supabase.com/docs/guides/functions/auth)를 참고한다.

검증 범위와 실제 서버 반영 결과는 [구현 보고서](validation/2026-09-30-access-codes.md)에 기록한다.
