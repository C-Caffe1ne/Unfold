# 계정별 무료 이용 코드 구현·검증

2026-09-30 · 현재 C#/.NET 10·Avalonia 체크아웃 · macOS arm64 호스트

## 결과

Google 로그인 후 우측 하단의 **코드 입력**에서 `admin`을 등록하면 결제 없이 이용한다.
Supabase Auth의 Google 계정 ID·이메일과 연결해 무료 이용 권한과 등록 시각을 서버에 저장한다.
이후 같은 계정으로 로그인하면 권한을 복원하므로 코드를 다시 입력하지 않는다.
원격 마이그레이션과 두 Edge Function 배포를 완료했고, 새 Beta v1.0.0 ZIP 3종을 생성했다.
실제 Google 계정의 브라우저 로그인부터 코드 등록까지 전체 흐름은 사람이 확인해야 한다.

## 변경과 경계

- 로컬 문자열 검사로 이용 잠금을 해제하지 않는다. 코드 등록과 권한 재조회가 모두 성공해야 홈·펫·타이머를 시작한다.
- `get-app-access`는 Google identity를 확인하고 현재 계정의 Live 구매 또는 무료 이용 권한을 조회한다. 결제 서버가 Test 모드인 상태에서도 무료 이용은 가능하다. Test 구매는 접근을 허용하지 않는다.
- `redeem-access-code`는 클라이언트에서 계정 ID·이메일·환경·권한을 받지 않는다. Auth가 반환한 사용자 ID만 서비스 전용 RPC에 전달한다.
- `202609300002_access_codes.sql`은 무료 권한을 결제 권한과 별도 저장한다. 원문 코드는 SHA-256 해시만 저장하고, 등록 인원 제한·등록 기한·권한 만료일·중단·회수를 지원한다.
- 계정별 10분에 5회 제한과 DB 잠금으로 중복 등록·인원 제한을 처리한다. 만료·회수한 권한은 같은 코드로 복구할 수 없다.
- `admin`은 요청한 무료 이용 코드이며 관리자 역할을 부여하지 않는다. 현재 인원·기간 제한은 없다.
- 모달은 비동기 등록, 중복 제출 방지, 오류 재시도, 취소·창 종료 시 늦은 응답 무시를 처리한다. 이메일·인증 토큰을 로컬 파일에 저장하지 않는다.
- 기존 로그아웃·5분 재조회·서버 오류 시 잠금·미저장 초안 보호를 유지한다.
- 향후 별도 앱 배포 없이 무작위 코드를 발급하는 운영자 스크립트와 [운영 안내](../access-codes.md)를 추가했다.

## 소유 파일

Core 클라이언트, Desktop 계정 서비스·모델·창·코드 모달·표시 문구, 관련 C# 테스트를 수정했다.
서버에는 새 migration, 공유 access-codes 모듈, 두 함수, config 항목, Node 통합 검사와 운영자 스크립트를 추가했다.
계정·서버·릴리스 안내를 갱신했고 기존 미커밋 작업을 보존했다. 코드·자산·서버·검사·패키징 입력 253개의 SHA-256을 기록했다.
기준 커밋은 `37c6de354ceb390500d4bcb586bde1a74d16e205`이며 미커밋 변경을 포함한 빌드다.

## 자동 검사

| 검사 | 결과 |
|---|---|
| 계정 클라이언트·모달·구매 잠금 집중 검사 | 74/74 통과 |
| 전체 Release 회귀 검사 | 496/496 통과, 실패·건너뜀 0 |
| Node 서버 전체 검사 | 43/43 통과, 실패·건너뜀 0 |
| 새 코드 등록 HTTP → Auth 모의 응답 → 실제 PostgreSQL 통합 | 9개 사례 통과, 위 43개에 포함 |
| Apple Silicon·Intel Mac·Windows x64 자체 포함 게시 | 성공 |
| ZIP CRC·5종 펫·폰트·아이콘·라이선스·설정·컴파일된 새 API·현재 README 검사 | 181개 통과 |
| 빌드 입력 비교 | 253개 변경 없음 |
| `git diff --check` | 통과 |

집중 검사 최초 모달 테스트의 컨트롤 검색 실패는 Avalonia 이름 범위가 없는 프로그램 생성 컨트롤을 테스트가 이름 범위로 찾았기 때문이다. 시각 트리 탐색으로 고쳐 실제 모달 오류·재시도·성공 닫기를 확인했다.
초기 NuGet 네트워크·MSBuild 로컬 소켓 제한은 필요한 권한으로 재실행해 해결했다. 제품 오류로 보고하지 않는다.

## 실제 Supabase

프로젝트 `xrelgkdawkogrwxmwkcx`에서 기존 관리 연결을 사용했다. 새 비밀키를 요청하거나 앱에 포함하지 않았다.

- `202609300002` 적용·이력 기록, `beta-admin` 활성화 확인.
- `get-app-access`, `redeem-access-code`: ACTIVE, version 1, `verify_jwt=true` 확인.
- 실제 PostgreSQL에서 임시 계정의 저장 → 같은 코드 재시도 → 권한 복원 → 회수 → 재사용 거부를 검증했다.
- 위 DB 검사는 트랜잭션을 롤백해 임시 사용자·권한을 남기지 않았다. 실제 Google OAuth 로그인 검증은 아니다.
- 두 배포 API는 공개키만 사용한 미로그인 요청에 HTTP 401을 반환했다.
- 기존 결제 함수·결제 Secret·판매 활성화는 변경하지 않았다. Live 결제 마이그레이션 `202609300001`을 배포했다는 의미가 아니다.

## 실제 macOS·미검증

새 Apple Silicon ZIP을 추출해 새 `UNFOLD_DATA_DIR`에서 네이티브 진단을 실행했다.
종료 코드 0, `success=true`, 약 22.9초, 렌더 캡처 93장이다. 기존 앱과 사용자 데이터는 변경하지 않았다.
진단은 자동 UI 이벤트와 격리된 모의 실행이며 실제 Google 로그인·결제·코드 등록 인증은 수행하지 않는다.

아래 항목은 미검증이다.

- 실제 테스터 Google 로그인 → `admin` 등록 → 홈 진입 → 종료·재로그인 복원.
- Intel Mac·Windows에서 실제 실행·물리 입력·브라우저 콜백.
- 실제 Live 결제·환불. 현재 결제는 Test 설정이다.
- macOS Developer ID 서명·공증, Windows 서명. Mac 후보는 ad hoc, Windows는 unsigned다.

## 배포물과 재현 자료

루트 `artifacts/`의 아래 ZIP은 무료 이용 코드가 포함된 현재 Beta v1.0.0이다.
추가 전 파일은 `artifacts/releases/v1.0.0-beta/before-access-codes/`에 보존했다. 외부 사이트에 업로드하지 않았다.

| 대상 | 파일 | 크기 | SHA-256 |
|---|---|---|---|
| osx-arm64 | `Unfold-v1.0.0-beta-osx-arm64.zip` | 71.5 MiB | `242b3976f9e5826c73dc16f89eaad14fd06371357f78946b8a00697ac6583bff` |
| osx-x64 | `Unfold-v1.0.0-beta-osx-x64.zip` | 75.4 MiB | `00134daa4002666163fa7c9125c2e84e0ab7636c863c3aeab4dac4495a47339e` |
| win-x64 | `Unfold-v1.0.0-beta-win-x64.zip` | 101.4 MiB | `2b25a01186317c15a55b251efbde8db34ee37f09852ad93b1040bd340a54f544` |

검증 자료는 `artifacts/access-code-qa/`에 있다: 집중·전체 TRX, server-tests.log, 원격 DB probe SQL, remote-verification.json, remote-http.json, 3종 publish 로그, native-smoke.json, artifact-audit.json, source-inputs.json, SHA256SUMS.txt.

테스터 확인 순서: 새 ZIP으로 실행 → Google 로그인 → 우측 하단 코드 입력 → `admin` → 확인 → 홈 진입 → 앱 종료 → 같은 Google 계정으로 로그인 → 코드 없이 홈 진입.
