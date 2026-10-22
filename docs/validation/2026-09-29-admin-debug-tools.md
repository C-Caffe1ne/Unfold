# 관리자 전용 디버그 도구 구현·검증

날짜: 2026-09-29

## 변경 계약

- 관리자 지정은 앱의 이메일 문자열이나 로컬 설정이 아니라 Supabase Auth 사용자 ID를 참조하는
  `public.account_roles`에서 관리한다.
- `authenticated` 사용자는 RLS로 자신의 역할만 읽을 수 있고 추가·수정·삭제할 수 없다.
  역할 행이 없으면 서버는 `member`, 정확히 한 개의 `admin` 행이 있으면 `admin`을 반환한다.
- 구매 확인이 성공한 계정의 사용자 ID·이메일·역할만 앱 실행 중 메모리에 전달한다.
  access token과 refresh token은 계정 창을 닫을 때 기존처럼 폐기한다.
- 설정의 디버그 카드, 디버그 사용 설정 저장, 알림 미리보기 직접 실행을 모두 관리자 권한으로 제한한다.
  일반 계정으로 바꾸면 실행 중 미리보기를 닫고 저장된 `DebugToolsEnabled`도 false로 복구한다.
- `--smoke-test` 진단 모드는 자동 검증 전용 경로이므로 사용자 계정 역할과 별도로 동작한다.

## 자동 검증

- `npm test` (`supabase/`): 33개 통과.
  - 실제 PostgreSQL 호환 엔진에서 `account_roles` 권한, 본인 행 RLS, 익명 읽기 거절을 검사했다.
  - 역할 조회의 관리자·일반 사용자 응답과 중복·알 수 없는 역할 거절을 검사했다.
- 관리자 권한 관련 C# 집중 검사: 65개 통과.
  - 서버 역할 응답 검증, 계정 화면의 안전한 식별 정보 전달, 비관리자 UI 숨김과 직접 실행 거절,
    관리자 활성화, 계정 전환 시 설정 제거를 검사했다.
- `dotnet test Unfold.slnx -c Release --no-restore`: 415개 통과.
- `git diff --check`: 통과.

## 원격 배포 검증

개인 계정과 회사 계정을 확인한 뒤 `202609290003_assign_admin_accounts.sql`에 안정적인 Auth 사용자
ID 두 개를 지정했다.

- `supabase db push --dry-run`: 역할 테이블과 계정 할당 마이그레이션 두 건만 예정됨을 확인했다.
- `supabase db push`: `202609290002`, `202609290003` 적용 성공.
- `supabase functions deploy get-entitlement`: 배포 성공.
- 원격 함수 목록: `get-entitlement` 버전 11, `ACTIVE`, JWT 검증 활성.
- `202609290004_verify_admin_accounts.sql`: 두 확인 계정의 `admin` 행이 정확히 두 건인지 원격
  트랜잭션에서 검사했고 적용에 성공했다. 하나라도 누락되면 이 마이그레이션은 실패한다.
- 원격 마이그레이션 목록: 로컬과 원격이 `202609290004`까지 일치한다.

자동 검사는 RLS와 앱 동작 계약을 확인하지만 실제 Google 계정 두 개의 로그인·역할 응답과
macOS/Windows 화면 표시는 아직 검증하지 않았다.
