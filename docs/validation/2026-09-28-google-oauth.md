# Google OAuth 설정 확인

2026-09-28 12:18 KST · `release/mvp` · 프로젝트 `xrelgkdawkogrwxmwkcx`

## 환경과 범위

사용자의 Google 설정 완료 후 재검사 요청에 따라 실제 Supabase 및 Google HTTPS 응답을 확인했다.
로컬 `.env.client`의 공개키를 사용했다. 앱·관리 설정·상품 DB는 변경하지 않았다.
제품 코드는 수정하지 않고 이 검증 기록만 추가했다.

## 자동 네트워크 검사

| 검사 | 응답·결과 |
|---|---|
| Supabase `/auth/v1/health` | HTTP 200 |
| Supabase `/auth/v1/settings` | HTTP 200, `external.google=true` |
| Google 인증 시작 | PKCE S256 요청에 HTTP 302, `accounts.google.com/o/oauth2/v2/auth`로 연결 |
| Google 클라이언트 ID | 전달됨, `.apps.googleusercontent.com` 형식 확인. 원문은 기록하지 않음 |
| Google에 전달된 콜백 | `https://xrelgkdawkogrwxmwkcx.supabase.co/auth/v1/callback`과 일치 |
| 인증 취소 시 반환 | HTTP 302, `http://127.0.0.1:43821/auth/callback`으로 `access_denied` 반환 |
| Google 로그인 진입 | HTTP 200, `/v3/signin/identifier`, 제목 `Sign in - Google Accounts` |
| Google 오류 표식 | 응답에서 `redirect_uri_mismatch`, `invalid_client`, `deleted_client`, `org_internal`, `invalid_request` 발견되지 않음 |

새 임의 PKCE 검증값으로 Supabase 인증 시작 요청을 만들었다. 받은 OAuth state로 취소 응답만
전달했으며 Google 계정을 선택하지 않았다. Google 로그인 진입 검사는 state를 제거한 주소로
별도 수행했다. 두 검사를 이어진 로그인 성공 흐름으로 간주하지 않는다.
Google로 전달한 요청에는 Supabase 공개키나 사용자 토큰을 포함하지 않았다.
로컬 임시 URL 파일은 검사 후 삭제했고 인증 state·검증값·키는 결과에 기록하지 않았다.

기존 관찰에서 Google 비활성이었던 상태는 이번 응답에서 활성으로 변경되었다.
이메일 provider는 여전히 활성이고 익명 로그인은 비활성이다. 설정 변경은 수행하지 않았다.

## 미검증과 다음 단계

- 실제 계정 로그인·사용자 동의, Google 테스트 사용자 등록 및 공개 심사 상태는 미검증이다.
- Client Secret의 정확성은 실제 Google 인증 코드 교환 전까지 확정할 수 없다.
- 반환 URL은 취소 응답의 Location으로 확인했다. 앱의 포트 수신·복귀·세션 저장을 검증한 것은 아니다.
- `SupabaseAccountClient`의 PKCE 생성 규칙과 주소는 대조했지만 이번 요청은 앱 코드가 아닌 HTTP 검사로 수행했다.
- 데스크톱 콜백 수신·보안 세션 저장·계정 UI 연결, DB·권한 조회 함수 배포는 후속 작업이다.
- 실제 macOS/Windows 앱 로그인과 사람의 Google 로그인 검수는 수행하지 않았다.
- 제품 코드 변경이 없어 C# 빌드·기존 전체 회귀 검사는 반복하지 않았다. 신규 문서의 공백·링크만 확인했다.

다음은 실제 Google 로그인부터 Supabase 세션 발급까지 연결하고, 앱 반환·취소·재실행을 검사하는 단계다.

## 근거

- [앞선 공개키 연결 검사](2026-09-28-supabase-connection.md)
- [Supabase Google 설정 계약](https://supabase.com/docs/guides/auth/social-login/auth-google)
- [Supabase 반환 URL 계약](https://supabase.com/docs/guides/auth/redirect-urls)
