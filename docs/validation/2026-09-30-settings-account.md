# 설정 이동 확인과 계정 표시·로그아웃

2026-09-30 · C# / .NET 10 / Avalonia 현재 체크아웃.

## 원인

설정 탭의 초안은 다른 탭으로 이동해도 그대로 유지했지만 이동 전에 저장 여부를 묻지 않았다.
로그인 세션은 계정 창을 닫으면 폐기했고, 설정의 계정 카드에는 로그인·구매 창을 여는 버튼만 있었다.

## 변경

- 홈·펫 추가·기록으로 이동할 때 설정 초안이 있으면 `변경된 설정이 있어요. 저장하시겠어요?` 모달을 연다.
  `저장`, `저장 안 함`, `취소`는 하단 중앙에 배치했다. 저장은 성공 후 이동하고,
  저장 안 함은 저장값 복원 후 이동하며, 취소·Esc는 탭과 초안을 유지한다.
- 입력 오류·저장 실패 시 이동하지 않는다. 모달 중복을 막고, 효과음 가져오기·저장 중에는 이동을 보류한다.
  버린 초안에서만 쓰는 효과음은 정리한다. 기존 저장 파일은 보존한다.
- 계정 제목 아래에 Google SVG와 인증에서 받은 이메일을 표시하고 기존 버튼을 `로그아웃`으로 교체했다.
  긴 이메일은 말줄임하고 툴팁에서 확인한다.
- 계정 창이 닫혀도 로그인 세션을 런타임 메모리에 유지한다. 계정 창을 다시 열면 구매 권한을 다시 확인하고,
  만료된 세션은 갱신한 뒤 요청한다. 이메일·토큰은 설정 파일에 저장하지 않는다.
- 로그아웃은 로컬 세션을 지우고 로그인 화면을 다시 연다. 서버에는 `POST /auth/v1/logout?scope=local`을 요청한다.
  현재 세션 범위를 사용한다. [Supabase 로그아웃 규칙](https://supabase.com/docs/reference/javascript/auth-signout)
  서버 통신 실패 시 로컬 상태는 지우고 서버 종료 미확인 오류를 표시한다.
  `admin`으로 진입한 경우도 로그아웃으로 로그인 화면에 돌아갈 수 있고, 사용자 이메일을 임의로 만들지 않는다.

## 소유 파일

- 설정 이동·저장·계정 카드: `SettingsWindow.Layout.cs`, `SettingsWindow.Preferences.cs`, `SettingsWindow.cs`, `Ui.cs`.
- 계정 상태·HTTP: `AppRuntime.cs`, `AccountScreenModel.cs`, `DesktopAccountService.cs`, `SupabaseAccountClient.cs`.
- 문구: `AccountScreenContent.cs`, `Assets/Account/entry-screen.json`의 `signOutButton`, `signedOutLabel`, `signOutUnavailable`.
- 회귀 검사: `SettingsNavigationGuardTests.cs`, `SettingsAccountTests.cs`, `AccountClientTests.cs`.
  기존 `ResponsiveLayoutTests.cs`, `SoundCleanupTests.cs`의 이동 전제를 새 확인 흐름에 맞췄다.
  `AccountScreenTests.cs`의 테스트 서비스에 세션 갱신·로그아웃을 추가했다.

## 검증

모든 검사는 새 `UNFOLD_DATA_DIR`을 사용했다.

- 집중 검사 65개 통과. 저장·버리기·취소, 모든 페이지 이동, 입력 오류·저장 실패, 중복 클릭·Esc,
  가져오기 대기·파일 정리, 이메일 유지, 구매 재조회, 만료 세션 갱신, 로그아웃 실패·취소를 검사했다.
- Release 전체: 437개 통과, 실패 0, 환경 의존 MP4 변환 2개 건너뜀(전체 439개).
- 이후 `admin` 진입 시 로그아웃 복귀 경로 보완: 관련 집중 검사 46개 통과.
- macOS 네이티브 진단: 네 가지 테마의 계정 카드·모달, 실제 640×560 창,
  버튼 중앙 정렬과 가로 넘침 없음 확인. PNG 9장. 입력·인증은 코드로 실행하고 테스트 계정 서비스를 사용했다.
- `git diff --check` 통과.

```sh
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
UNFOLD_DATA_DIR=$(mktemp -d /tmp/unfold-settings-account.XXXXXX) \
dotnet test Unfold.slnx -c Release --no-restore -p:UseSharedCompilation=false -nodeReuse:false
```

## 미검증·범위

실제 Google OAuth 로그인·원격 Supabase 세션 종료, 실제 마우스·키보드 입력과 Windows 실기는 미검증이다.
HTTP 검사는 대체 전송 계층으로 204·401·503 응답과 인증 헤더·현재 세션 범위를 확인한 것이다.
앱 재실행 시 자동 로그인, OS 보안 저장소와 전역 구매 권한 잠금은 이 작업에 포함하지 않았다.
구매 권한·사용자의 타이머·기록은 로그아웃으로 삭제하지 않는다.
