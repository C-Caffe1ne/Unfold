# 로그인 유지 구현·검증

2026-10-01 · macOS arm64 / .NET 10 / 현재 C#·Avalonia 작업본.
기존 사용자 변경을 보존하고 로그인 저장·복원과 관련된 파일만 수정했다.

## 원인

`AppRuntime.accountSession`은 메모리에만 있었고 종료 때 삭제됐다.
`Start`는 저장된 자격 증명 복원 없이 계정 화면을 열었다.
서버에 남아 있는 구매·무료 이용 권한과 앱의 로그인 세션은 서로 다른 상태이므로,
무료 코드 등록이 유지되어도 앱을 다시 실행하면 Google 로그인이 필요했다.

## 변경

- Google 로그인 성공 직후 계정 ID·갱신 토큰만 OS 보안 저장소에 보관한다.
  macOS는 Keychain의 generic password 항목, Windows는 Credential Manager의 generic credential을 사용한다.
  Windows의 보존 범위는 `CRED_PERSIST_LOCAL_MACHINE`으로 같은 컴퓨터·OS 사용자의 다음 로그인에도 유지된다.
  일반 JSON 파일·설정·로그에 토큰, 이메일 또는 이용 권한을 저장하지 않는다.
- 재실행 시 갱신 토큰을 서버와 교환해 새 로그인 세션을 얻고 `get-app-access`를 확인한다.
  유효한 구매·무료 권한이면 Google 재로그인과 무료 코드 재입력 없이 시작한다.
  자동실행 복원 성공은 설정 창을 열지 않는다. 진단 실행은 복원을 생략한다.
- 서버가 교체한 토큰은 이용 권한 조회 전에 바로 저장한다.
  저장·복원·갱신·삭제를 프로젝트·환경·데이터 프로필별 공유 잠금으로 직렬화한다.
  이전 토큰을 가진 갱신도 최신 저장 토큰을 사용하고, 로그아웃으로 삭제된 세션을 다시 만들지 않는다.
- 로그아웃·계정 변경은 저장된 로그인 정보를 삭제한다.
  서버 로그아웃의 통신 실패는 로컬 삭제를 되돌리지 않는다.
  토큰 갱신 응답과 로그아웃이 겹쳐도 삭제가 최종 상태로 남는다.
- 통신 장애는 저장 정보를 남기고 복원 재시도를 제공한다.
  인증 무효 응답은 로그인 정보를 삭제한다.
  OS 저장소가 잠기거나 접근이 거부되면 별도 오류를 표시하며 평문 저장으로 대체하지 않는다.
  서버 이용 권한 확인 전에는 실행 기능이 계속 잠겨 있다.

## 소유 파일

- `src/Unfold.Desktop/AccountSessionVault.cs`, `NativeAccountCredentialStore.cs`: 새 보안 저장 계층.
- `src/Unfold.Desktop/DesktopAccountService.cs`: 저장·복원·갱신·로그아웃 연결.
- `src/Unfold.Desktop/AccountScreenModel.cs`, `AccountScreenContent.cs`, `AccountWindow.axaml.cs`: 복원 상태·재시도·계정 변경·오류 처리.
- `src/Unfold.Desktop/AppRuntime.cs`, `AppRuntime.Account.cs`: 시작·이용 권한·자동실행·로그아웃 연결.
- `src/Unfold.Core/SupabaseAccountClient.cs`: 보안 저장소 오류 종류와 세션 설명 갱신.
- `Tests/Unfold.Tests/AccountSessionPersistenceTests.cs`, `AccountScreenTests.cs`: 복원·보존·갱신·삭제와 앱 런타임 회귀 검사, 기존 fake 확장.
- `docs/account-screen.md`, `docs/README.md`, 이 문서: 현재 동작과 검증 근거.

## 검증

자동 검사는 새 `UNFOLD_DATA_DIR`, HTTP fake와 메모리 자격 증명 저장소를 사용했다.
실제 사용자 세션·무료 코드·결제 데이터는 사용하거나 변경하지 않았다.

| 항목 | 결과·근거 |
|---|---|
| Release 빌드 | 컴파일 경고·오류 0. 전체 검사 실행이 최종 소스를 다시 빌드한다. |
| 로그인 관련 집중 검사 | 최초 집중 실행 97개 통과. `artifacts/validation/2026-10-01-session-persistence/account-focused.trx` |
| 전체 Release 검사 | **523개 통과, 실패·건너뜀 0**. `release-confirmed.trx` 및 `evidence.json`에 기록했다. 이전 496개에 이번 회귀 검사 27개가 추가됐다. |
| 앱 재실행 상태 | 서로 다른 런타임·계정 서비스·저장 계층에서 두 번 복원, 서버 권한 재확인, 로그아웃 뒤 세 번째 시작의 로그인 요구를 자동 확인했다. |
| 실패·경합 | 통신 장애 보존, 인증 무효 삭제, 손상 항목 복구, 저장소 실패, 계정 ID 불일치, 늦은 응답, 동시 갱신, 로그아웃 삭제를 자동 확인했다. |
| macOS 실제 Keychain | 고유 테스트 키에 더미 토큰을 저장한 후 **별도 프로세스**에서 복원·교체·로그아웃·삭제 확인을 실행했다. 모든 프로세스 종료 코드 0. 정리도 통과. `macos-keychain-processes-final.json` |
| 변경 형식 | `git diff --check` 통과. |

macOS Keychain 검사는 실제 OS API를 사용하되 HTTP 응답은 fake다.
앱 UI 자동 검사는 Avalonia headless로 수행했으므로 실제 OAuth 브라우저 조작이나 설치 앱의 OS 권한 검사를 대신하지 않는다.
처음 제한된 빌드는 Avalonia의 작업공간 밖 빌드 로그 쓰기 제한으로 중단됐고,
해당 빌드·테스트 접근을 허용한 실행에서 정상 빌드했다.

## 미검증·위험

- 실제 Google 계정 로그인 → 설치된 앱 종료 → 재실행의 전 과정은 미검증이다.
- 컴퓨터의 물리적 재부팅, macOS 로그인 Keychain 잠금·서명 변경 시 접근, Windows 실제 Credential Manager와 재부팅은 미검증이다.
- 서버에서 세션을 취소하거나 세션 수명 정책으로 만료시키면 다시 로그인해야 한다.
  통신 장애 때 로그인 자격 증명은 보존하지만 오프라인 사용 권한을 부여하지 않는다.
- 이미 웹사이트에서 제공하는 Beta v1.0.0 배포 파일은 이번 코드 수정으로 교체되지 않았다.
  수정 빌드에서 한 번 Google 로그인해야 안전한 저장이 시작된다. 이전 버전의 메모리 세션은 종료 뒤 복구할 수 없다.

## API 근거

- [Supabase 사용자 세션](https://supabase.com/docs/guides/auth/sessions): 갱신 토큰 교체와 세션 정책.
- [Apple Keychain 조회](https://developer.apple.com/documentation/security/secitemcopymatching(_:_:)),
  [Keychain 항목 갱신·삭제](https://developer.apple.com/documentation/security/updating-and-deleting-keychain-items): native 저장 계약과 스레드 처리.
- [Microsoft CREDENTIALW](https://learn.microsoft.com/en-us/windows/win32/api/wincred/ns-wincred-credentialw),
  [CredWriteW](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credwritew): generic credential과 컴퓨터·OS 사용자 단위 보존.
