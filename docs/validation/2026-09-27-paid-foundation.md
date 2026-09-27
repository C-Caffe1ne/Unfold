# 유료 앱 출시 기반 1차 구현·검증

2026-09-27 · `release/mvp` · C#/.NET 10 · 로컬 macOS 개발 환경

## 변경

사용자가 확정한 무료 체험 제외, 국내 4,900원 / 해외 US$3.99 일회성 구매, Supabase 기반을 반영했다.
도메인 없이 준비할 수 있는 정책·화면 시안·데이터·인증 클라이언트 기반을 구현했다.

| 영역 | 결과 |
|---|---|
| BM·웹 | 기존 체험·미확정 가격·가입 선행 다운로드 문구를 현재 결정으로 정리. 실제 인증/결제는 준비 중으로 유지 |
| 화면 시안 | 로그인·인증 대기·국내/해외 구매·확인 중·구매 완료·연결 오류, 4개 테마와 최소 창 비교 |
| Supabase DB | 고정 가격, 계정별 RLS, 서버 전용 주문·권한 변경, 원자적 이벤트 처리, 테스트/운영 분리 |
| 결제 상태 | 중복 이벤트 무시, 금액·통화·공급자·환경 검증, 환불 우선, 여러 구매 중 유효 구매 보존 |
| Edge Function | 서버가 검증한 사용자로 권한 조회. 사용자 입력의 계정·환경 무시. 401/503/미구매 구분 |
| C# 클라이언트 | Google PKCE URL·코드 교환·세션 갱신·권한 조회, 콜백/계정/환경 검증, 민감 응답 비노출 |

새 계정 화면과 인증 클라이언트는 `AppRuntime`에 연결하지 않았다. 기존 앱은 로그인 없이 계속 동작한다.
첫 계정 UI는 사용자가 요청한 시안 확인 후 적용 순서를 따른다.

## 자동 검사

| 검사 | 결과 | 한계 |
|---|---|---|
| `dotnet test … --filter FullyQualifiedName~AccountClientTests` | 20 통과, 실패·건너뜀 0 | 가짜 HTTP 응답 기반. 실제 Google 로그인이 아님 |
| `dotnet test Unfold.slnx -c Release --no-restore --no-build -m:1 -nodeReuse:false` | 391 통과, 실패·건너뜀 0 | 기존 371개 + 신규 20개. 인증 클라이언트를 포함한 Release 빌드 후 실행 |
| `cd supabase && npm test` | 13 통과, 실패·건너뜀 0 | DB 7, HTTP 5, 진입점 1. PGlite PostgreSQL + 모의 Auth/전송 |
| `node --test web/scripts/main.test.cjs` | 4 통과 | 기존 웹 스크립트 회귀 |
| `python3 web/scripts/verify-site.py` | 9페이지·161개 참조, 이미지·ARIA·미디어 검사 통과 | 실제 계정·결제 흐름 검증이 아님 |
| `node --check web/proposals/account.js` | 통과 | 문법 검사 |
| `git diff --check` 및 신규 파일 공백 검사 | 통과 | 코드 동작 검증과 별개 |

테스트에는 새 `UNFOLD_DATA_DIR`을 사용했다. 첫 C# 실행은 샌드박스의 로컬 소켓 제한으로 중단됐고,
테스트 취소 토큰에 관한 xUnit 규칙을 반영한 후 허용된 실행 환경에서 위 결과를 얻었다.
의존성은 `supabase/package-lock.json`에 고정했으며 설치 스크립트를 실행하지 않았다.

## 브라우저 관찰

Codex 내장 브라우저에서 다음을 확인했다. Avalonia 네이티브 창을 검증한 결과가 아니다.

- 로그인·구매·결제 확인 중·구매 완료·오류 상태 전환과 KRW/USD 가격 전환.
- 1120×800에서 콘텐츠 경계 확인. 가로 스크롤 없음.
- 640×560에서 최소 창 시안의 콘텐츠가 길어지는 문제를 보완해 프레임 높이 560px 확인.
  시안 조작 툴바는 앱 외부이므로 전체 웹페이지는 세로 스크롤될 수 있다.
- 375px 웹 폭에서 버튼이 프레임 안에 배치되고 가로 스크롤 없음.
- 확인한 브라우저 오류 로그 없음. 운영 인증·결제는 실제로 수행하지 않았다.

시안: `web/proposals/account.html`.
로컬 서버가 실행 중이면 [브라우저로 보기](http://127.0.0.1:8766/proposals/account.html).
재실행 명령: `python3 -m http.server 8766 --bind 127.0.0.1 --directory web`.

## 미검증·다음 연결

- 1차 구현 당시 이 환경에는 Supabase 프로젝트 URL/키, Google OAuth 설정, 결제사 테스트 키가 없었다.
  Docker·Supabase CLI·Deno도 없어 전체 Supabase 스택과 실제 Edge 배포는 실행하지 않았다.
- 후속으로 사용자가 제공한 Supabase URL을 연결 안내에 기록했다. 키 없는 `/auth/v1/health` 요청은
  `401 No API key found in request`를 반환했다. API 게이트웨이 응답만 확인했으며, 공개키 입력·실제 인증·원격 DB 적용은 아직 남았다.
- 실제 Google 테스트 사용자 로그인, 로컬 콜백 listener, macOS/Windows 보안 저장소 연결이 남았다.
- 원격 PostgREST·JWT 게이트웨이와 실제 결제사 서명 검증·승인·웹훅 전달·환불은 아직 미구현/미검증이다.
  DB의 `apply_verified_payment`를 실제 웹훅 수신 완료로 간주하면 안 된다.
- 오프라인 서명 권한, 앱 실행 잠금, 자동실행/트레이 연결은 후속 작업이다. 네트워크 장애 시 정책도 확정해야 한다.
- 결제사, 국내/해외 판정, 세금 포함 여부, 기기 수와 업데이트 범위는 미정이다. 모든 가격의 실제 판매는 DB에서 기본 비활성이다.
- 실제 Windows, 새 설치 파일의 로그인/구매, 공개 서버·실결제·도메인 연결은 수행하지 않았다.

## 파일 경계

신규: `supabase/`, `src/Unfold.Core/SupabaseAccountClient.cs`, `Tests/Unfold.Tests/AccountClientTests.cs`,
`web/proposals/account.*`, 이 검증 문서.
갱신: `.gitignore`, 현재 BM·제품·개발·MVP·문서 안내, 출시 계획 진행 상태, 웹 가격/가입/개인정보 문구와 검사 범위.
기존 펫 관련 수정과 `Art/Characters/rive-source-faithful/`는 이번 작업에서 변경하지 않았다.
