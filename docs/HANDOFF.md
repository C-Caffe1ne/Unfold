# Unfold 작업 인계

2026년 10월 14일 · 한국 시간 기준. 다른 세션·IDE에서 작업을 재개할 때 먼저 읽는다.
이 문서는 확인 당시의 상태다. 다음 작업에서는 브랜치·HEAD·변경·버전·배포·실행 경로를 다시 확인한다.

## 2026-10-15 v1.2.0 정식 배포 준비

사용자가 최신 배포 이후 변경 전체를 커밋하고 베타가 아닌 v1.2.0으로 배포하도록 요청했다.
프로젝트는 `1.2.0`으로 올렸고 정식 패키징·베타 업데이트 전환 피드를 추가했다.
[릴리스 노트](releases/v1.2.0.md)와 [배포 검증](validation/2026-10-15-v1.2.0-release.md)을 현재 기준으로 읽는다.
아래 10월 14일 표와 후속 수정의 미커밋·미배포 문구는 각 작업 당시 기록이다.
배포 완료 후 이 절에 제품 SHA·공개 상태·검증 결과를 갱신한다.

## 2026-10-15 후속 수정 · GLB 요소별 외곽선

이번 작업에서 다시 확인한 최신 제품 기준은 `codex/pet-overlay-hover`,
`58d629949860ecac68e1ed1a48154710ec1693ca`, `1.1.4-beta`다. 실제 저장소는
`/Users/hwanghyeonseong/Developer/GitHub/Unfold/.worktrees/pet-overlay-hover`에 있다.
아래 10월 14일 표의 SHA·배포 상태는 당시 기록이다.

GLB 외곽선을 최초 10% 구현 후 사용자 요청에 따라 내부 고정값 **1%**로 줄이고,
보이는 메시 노드·재질 요소 사이에도 선을 추가했다. 새 GLB의 첫 미리보기, 기존 GLB 재생,
저장·재열기·팩 내보내기에 공통 적용하며 조절 UI는 없다. 192px 출력 기준 약 0.1px이고
해상도에 비례한다. GLB 원본과 manifest 형식은 바꾸지 않는다.
최신 두께·검사는 [1% 외곽선 검증](validation/2026-10-15-glb-one-percent-outline.md),
요소별 구현·비교 이미지는 [요소별 외곽선 검증](validation/2026-10-15-glb-element-outline.md),
최초 10% 구현 이력은 [기존 외곽선 검증](validation/2026-10-15-glb-outline.md)에 기록한다.
이번 수정은 미커밋이며 기존 문서 변경을 보존했다. 배포·설치 앱 교체·실제 OS 입력 검사는 수행하지 않았다.

## 2026-10-15 후속 수정 · 말풍선 접합부

본체 Border 뒤에 꼬리를 따로 그리던 구조를 하나의 합집합 도형으로 바꾸어, 겹치는 부분의
수직 보더를 제거했다. 채움·외곽선을 한 번 그리고 내용과 같은 부모에서 불투명도를 적용한다.
말풍선 크기·내용 배치·버튼·펫/타이머 창 분리는 유지한다. GLB의 1% 요소별 외곽선도 유지한다.
[말풍선 보더 검증](validation/2026-10-15-bubble-border-join.md)에 수정 전후 픽셀·이미지,
관련 45개 검사와 Release 빌드 결과를 기록한다. 미커밋이며 설치 앱 교체·배포는 수행하지 않았다.

## 1. 이어받을 소스

| 구분 | 확인한 상태 |
|---|---|
| 최신 개발 작업트리 | `/Users/hwanghyeonseong/Developer/GitHub/Unfold/.worktrees/pet-overlay-hover` |
| 브랜치 | `codex/pet-overlay-hover` |
| 제품 HEAD | `f9953a060deb76ea089e9442b9cf94ea12a78c73` |
| 프로젝트 버전 | `src/Unfold.Desktop/Unfold.Desktop.csproj`의 `1.1.4-beta` |
| 원격 저장소 | [C-Caffe1ne/Unfold](https://github.com/C-Caffe1ne/Unfold) |
| 확인한 원격 브랜치 HEAD | `4a668fdd0e54155037b88d5c34167c7f8019d76f` |
| 최신 공개 태그·제품 소스 | [v1.1.4-beta](https://github.com/C-Caffe1ne/Unfold/releases/tag/v1.1.4-beta), `4c64bfc5eacd9835454e4d26179aca0f46c55cf9` |
| 공개 시각 | 2026-10-14 15:24 KST, prerelease, 공개 자산 10개 |
| 작업 시작 때의 Git 상태 | 최신 작업트리 clean. 문서 갱신 후의 Markdown·검증 자료는 미커밋 상태 |

**`f9953a0`는 아직 로컬에만 있다.** 원격을 새로 clone하면 이 휴식 타이머 수정을 받지 못한다.
`4a668fd`는 배포 문서 커밋, `f9953a0`는 이후 제품 수정이다. 공개 소스보다 2커밋,
확인한 원격보다 1커밋 앞선다. 개발·배포가 모두 `1.1.4-beta`여도 내용은 다르다.
이번 문서 작업에서 제품 코드 수정·커밋·푸시·태그·배포·서버 변경은 수행하지 않았다.

### 다른 작업트리

아래 경로·변경은 작업 시작 때 확인한 값이며 기존 작업을 보존한다.
`A...B`는 각 브랜치에만 있는 커밋 수다. 기준 B는 `codex/pet-overlay-hover`다.

| 작업트리 | 브랜치 / HEAD / 버전 | A만 / B만 | 기존 변경 |
|---|---|---:|---|
| 저장소 루트 `/Users/hwanghyeonseong/Developer/GitHub/Unfold` | `release/mvp` / `ce0ccda` / `0.2.2` | 5 / 52 | `AGENTS.md`, 미추적 `.playwright-cli/`, `output/` |
| `~/.codex/worktrees/6bca/Unfold` | `codex/beta-v1.0.4` / `95f7a21` / `1.0.4-beta` | 1 / 40 | 없음 |
| `~/.codex/worktrees/beta-v1-1-0/Unfold` | `codex/glb-import-compat` / `bbdb4a8` / `1.1.1-beta` | 0 / 32 | README·AGENTS와 기능/검증 문서 등 12개 수정 |
| `~/.codex/worktrees/d33f/Unfold` | detached / `ce0ccda` / `0.2.2` | 5 / 52 | 루트와 같은 변경 표시 |
| `~/.codex/worktrees/gif-glb-guidance/Unfold` | `codex/gif-glb-import-guidance` / `09853c7` / `1.1.3-beta` | 0 / 5 | 없음 |
| `.worktrees/remove-unused-features` | `codex/remove-unused-features` / `6f0a3dd` / `1.1.3-beta` | 0 / 10 | 없음 |
| `.worktrees/pet-pack-ux` | `feat/pet-pack-ux` / `5dc4f7e` / `1.0.3-beta` | 10 / 50 | 없음 |

루트의 커밋 제목 `Beta_v1.1.1`은 실제 csproj `0.2.2`와 다르다. 제목·수정 시각만으로
기준을 고르지 않는다. 이전 작업트리의 문서 변경은 최신 소스에 자동 적용하지 않는다.

## 2. 지금까지 반영한 작업

| 단계 | 반영 내용 | 근거 |
|---|---|---|
| 1.1.1 | 루틴·프로필 설정과 픽셀 편집 진입점 제거, 로그인 복원, 캐시·미디어 메모리 개선 | [제거](validation/2026-10-07-unused-features.md), [메모리](validation/2026-10-07-memory-budget.md) |
| 1.1.2 | 설치한 커스텀 펫 편집·삭제, OGG와 음량 %, 테마 색상, 시작 업데이트 안내, 폐기 소스 정리 | [1.1.2 배포](validation/2026-10-08-beta-v1.1.2-release.md) |
| 1.1.3 | 홈 시간 자동 저장·중지 복귀, GLB 회전과 모션 복귀, 불필요한 미리보기 제거 범위 정정 | [1.1.3 배포](validation/2026-10-12-beta-v1.1.3-release.md), [미리보기 범위](validation/2026-10-09-pack-preview-correction.md) |
| 별도 서버 | 운영 Live 결제의 세금 포함·환산 검증 수정, 웹훅 v21, 기존 이벤트 재전송·중복 방지·권한 active 확인 | [Live 복구](validation/2026-10-12-live-payment-pending.md) |
| 1.1.4 공개 | GLB 전체 폭·확대 미리보기와 회전 화살표, 펫 하단 시간·타이머 호버, 별도 창 분리, 들기·알림·스트레칭 수명, 중지 상태의 홈 시간 편집, 핵심 업데이트 노트 | [릴리스 노트](releases/v1.1.4-beta.md), [배포 검증](validation/2026-10-14-beta-v1.1.4-release.md) |
| 배포 이후 로컬 | 휴식 말풍선도 공통 `AnimatedCountdown`으로 연결해 바뀐 숫자만 380ms 전환. 숨김·분리 시 종료, 남은 초 올림·초과 초 내림 유지 | [f9953a0 검증](validation/2026-10-14-rest-timer-motion.md) |

공개 웹 변경 이력과 Notion 1.1.4 반영은 배포 기록의 당시 확인 결과다. 이번에는
GitHub 릴리스·원격 브랜치만 다시 조회했으며 웹·Notion·운영 DB의 현재 상태는 다시 조회하지 않았다.
Google 로그인 브랜딩과 추가 3D 메모리 최적화는 기존 요청에 따라 보류 상태다.

## 3. 현재 앱 동작과 코드 지도

현재 앱은 **C#/.NET 10, Avalonia 12.1.2, Velopack 1.2.0**이다.
빌드 입력은 `Unfold.slnx`, `Directory.Build.props`, `src/`, `Tests/`, `Assets/`,
`Art/Brand/`, `web/assets/pet-rest.png`와 각 프로젝트 잠금 파일이다.
원본 자산을 함께 옮긴다. `bin/`, `obj/`, 배포물의 복사본은 편집 원본이 아니다.

| 책임 | 읽을 파일 |
|---|---|
| 시작·버전·진단·데이터 경로·단일 인스턴스 | [Program.cs](../src/Unfold.Desktop/Program.cs), [App.cs](../src/Unfold.Desktop/App.cs) |
| 타이머·알림·완료 기록의 연결 | [AppRuntime.cs](../src/Unfold.Desktop/AppRuntime.cs), [StretchClock.cs](../src/Unfold.Core/StretchClock.cs), [BreakSession.cs](../src/Unfold.Core/BreakSession.cs), [PetReminder.cs](../src/Unfold.Core/PetReminder.cs) |
| 홈 시간 자동 저장·설정 화면 | [SettingsWindow.cs](../src/Unfold.Desktop/SettingsWindow.cs), [SettingsWindow.Layout.cs](../src/Unfold.Desktop/SettingsWindow.Layout.cs), [SettingsWindow.Preferences.cs](../src/Unfold.Desktop/SettingsWindow.Preferences.cs) |
| 펫 렌더링·입력·행동 수명 | [PetWindow.cs](../src/Unfold.Desktop/PetWindow.cs), [PetWindow.Companion.cs](../src/Unfold.Desktop/PetWindow.Companion.cs), [PetWindow.PointerArt.cs](../src/Unfold.Desktop/PetWindow.PointerArt.cs) |
| 말풍선·별도 타이머 창 | [PetOverlayWindow.cs](../src/Unfold.Desktop/PetOverlayWindow.cs), [PetTimerWindow.cs](../src/Unfold.Desktop/PetTimerWindow.cs), [PetTimerReveal.cs](../src/Unfold.Desktop/PetTimerReveal.cs) |
| 최신 휴식 숫자 전환 | [PetSpeechBubble.cs](../src/Unfold.Desktop/PetSpeechBubble.cs), [AnimatedCountdown.cs](../src/Unfold.Desktop/AnimatedCountdown.cs), [TimerRefinementTests.cs](../Tests/Unfold.Tests/TimerRefinementTests.cs), [PetOverlayWindowTests.cs](../Tests/Unfold.Tests/PetOverlayWindowTests.cs) |
| 펫 편집·팩·호환 | [PetManagementView.cs](../src/Unfold.Desktop/PetManagementView.cs), [GlbPetView.cs](../src/Unfold.Desktop/GlbPetView.cs), [CharacterLibrary.cs](../src/Unfold.Core/CharacterLibrary.cs), [CharacterPack.cs](../src/Unfold.Core/CharacterPack.cs), `src/Unfold.Core/Compatibility/` |
| 계정 복원·권한 | [AppRuntime.Account.cs](../src/Unfold.Desktop/AppRuntime.Account.cs), [AccountSessionVault.cs](../src/Unfold.Desktop/AccountSessionVault.cs), [NativeAccountCredentialStore.cs](../src/Unfold.Desktop/NativeAccountCredentialStore.cs) |
| 업데이트 | [AppRuntime.Updates.cs](../src/Unfold.Desktop/AppRuntime.Updates.cs), [AppUpdates.cs](../src/Unfold.Desktop/AppUpdates.cs), `Scripts/package-updates.py` |

일반 시작은 Google 로그인 또는 저장 세션을 복원하고 Live 구매/무료 권한을 확인한 뒤
홈·펫·타이머를 시작한다. 오프라인 권한은 제공하지 않는다. 진단 모드의 우회는 일반 실행과 구분한다.
홈의 알림 간격 1~240분·휴식 1~10분은 **중지 상태이며 활성 휴식이 없을 때** 자동 저장한다.
일시정지는 시간 편집을 허용하지 않는다. 중지는 확인 창 없이 전체 간격으로 돌아가며
열린 알림·휴식을 완료 기록 없이 취소한다. 종료 확인과 펫 초안 보호는 별도다.

펫·알림 말풍선·타이머 바는 서로 다른 투명 창이다. 표시 버튼만 입력을 받고 텍스트와
투명 여백은 뒤 창으로 입력을 통과시킨다. 알림 행동은 버튼/자동 미루기까지, 스트레칭은
완료/중지까지 반복 또는 끝 자세를 유지한다. 완료는 말풍선 만료와 별개로 전체 재생 후 idle로 복귀한다.
GLB는 X/Y 회전 화살표와 전체 폭 미리보기를 사용한다. 이전 Z 값은 저장 호환 계약을 따른다.
별도 팩 재생 미리보기는 제거했지만 **홈 미리보기와 편집 미리보기는 유지한다**.

## 4. 저장 계약과 실행 중인 앱

| 항목 | 계약 |
|---|---|
| macOS 기본 데이터 | `~/Library/Application Support/Unfold/` |
| Windows 기본 데이터 | `%LOCALAPPDATA%\Unfold\` |
| 격리 프로필 | `UNFOLD_DATA_DIR`로 지정. 진단에는 새 빈 폴더 |
| 설정·기록 | `settings.json`, `break-history.json`; 완료 기록 최대 2,000개·90일, 선택적 `actualSeconds` 호환 |
| 펫·소리·오류 | `Characters/`, `Sounds/`, `unfold.log`; 기본 번들 자산은 앱의 `Assets/Characters/` |
| 로그인 보관 | macOS Keychain / Windows 자격 증명 관리자. 프로젝트·환경·데이터 프로필별 분리 |
| manifest | `character.json` version 1, `pack.json` formatVersion 1·contentVersion·SHA-256 목록. 앱 버전과 독립 |

이번 확인에서 실행 프로세스는 `/Applications/Unfold.app/Contents/MacOS/Unfold`였고,
설치 앱의 plist는 `1.1.4`, build `100100400`이었다. 설치된 DLL과 작업트리의 기존
Release DLL은 해시가 서로 달랐다. 기존 Release DLL의 수정 시각도 마지막 제품 수정 이전이다.
버전 표시와 실행 중이라는 사실만으로 최신 소스의 실행을 증명할 수 없다.
이번에는 앱을 재시작하거나 사용자 데이터·로그인·설정을 변경하지 않았다.
GUI 읽기는 일반 `Window`만 노출해 실제 화면·입력 동작의 새 검증 근거로 사용하지 않았다.
세부 확인 값은 [문서 최신화 기록](validation/2026-10-14-handoff-refresh.md)에 있다.

## 5. 다른 세션·IDE에서 시작하기

같은 Mac에서는 **위 최신 작업트리 자체를 IDE 폴더로 연다**. 저장소 루트는 `0.2.2` 소스다.
IDE의 터미널·실행 구성·작업 디렉터리가 같은 작업트리인지 확인한다.
이 문서 → `AGENTS.md` → [문서 색인](README.md) → 필요한 기능 문서 순서로 읽는다.
Claude의 진입점은 `CLAUDE.md`이며 공통 규칙은 `AGENTS.md`에서 가져온다.
에이전트/플러그인 설치 유무와 관계없이 같은 솔루션·명령·격리 프로필을 사용한다.

macOS / zsh:

```sh
cd /Users/hwanghyeonseong/Developer/GitHub/Unfold/.worktrees/pet-overlay-hover
git worktree list
git branch --show-current
git rev-parse HEAD
git status --short
git rev-list --left-right --count HEAD...origin/codex/pet-overlay-hover
git rev-parse 'v1.1.4-beta^{commit}'
dotnet --version
```

현재 호스트 SDK는 `10.0.401`이다. .NET SDK 10이 필요하며 NuGet 잠금 파일을 유지한다.
온라인으로 기준을 새로 확인할 때 원격 ref가 최신인지 확인한다. 빌드·일반 실행 예시:

```sh
export AVALONIA_TELEMETRY_OPTOUT=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet restore Unfold.slnx --locked-mode
dotnet build Unfold.slnx -c Release --no-restore
export UNFOLD_DATA_DIR="$(mktemp -d /tmp/unfold-dev.XXXXXX)"
dotnet run --project src/Unfold.Desktop -c Release --no-build
```

Windows / PowerShell에서는 해당 최신 소스를 옮긴 **실제 경로**로 이동한 뒤 실행한다:

```powershell
git branch --show-current
git rev-parse HEAD
git status --short
dotnet --version
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet restore Unfold.slnx --locked-mode
dotnet build Unfold.slnx -c Release --no-restore
$env:UNFOLD_DATA_DIR = Join-Path ([IO.Path]::GetTempPath()) ('unfold-dev-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $env:UNFOLD_DATA_DIR | Out-Null
dotnet run --project src/Unfold.Desktop -c Release --no-build
```

실제 미디어 변환 검사에 필요한 FFmpeg 준비는 [검증 안내](verification.md)를 따른다.
`dotnet run --project tools/Unfold.MediaSetup -- osx-arm64 .`의 RID는 실제 호스트에 맞춘다.
`UNFOLD_FFMPEG_PATH`를 쓰면 로컬 실행 파일의 절대 경로를 지정한다.
배포 스크립트의 Git 경로는 **`Scripts/`**다. 현재 브랜치에는 `Scripts/run-desktop.sh`가 없다.
소스 실행은 설치된 Velopack 업데이트 대상이 아니므로 앱 업데이트 검사는 패키지 앱으로 수행한다.

### 다른 컴퓨터로 옮길 때

원격에는 `f9953a0`가 없고 이번 문서·새 증거도 아직 미커밋이다. 현재 작업트리의
로컬 커밋과 모든 문서 변경을 함께 보존한 뒤 옮긴다. Git bundle은 커밋만 보존하므로
미커밋·미추적 문서는 따로 포함하거나 검토 후 커밋해야 한다. `.worktrees/` 경로명은
이 Mac의 배치이며 새 컴퓨터에서는 브랜치·제품 SHA·빌드 입력으로 동일 소스를 식별한다.
OS 로그인 보관·인증서·notary 프로필·토큰은 문서나 저장소에 복사하지 않는다.

## 6. 검증 상태

| 대상 | 확인된 결과 | 한계 |
|---|---|---|
| 최신 `f9953a0` 수정 | 수정 당시 고유 집중 16개 통과, Release 프로젝트 빌드·diff 검사 통과. 수정 파일 7개가 격리 검사 소스와 SHA-256 일치 | 첫 실행 15통과·1실패 후 fixture 수정·1개 재검사. 전체·OS 진단·실기·재실행·배포 미실행 |
| 공개 `4c64bfc` | 로컬·Windows CI·macOS CI 집중 각 63개, Mac ARM64/Intel 빌드·서명·공증, 공개 자산 10개 | 전체·앱 OS 진단·설치·제거·업데이트 재시작은 사용자 요청으로 제외 |
| 10월 13일 행동 수명 수정 | 당시 전체 Release 979개, smoke 110장, 기본 펫 진단 120장 | 현재 HEAD 전체 검사로 재사용하지 않음. 기본 펫 진단 `offScreen:true`, `physicalInput:false` |
| 이번 문서 갱신 | 후보 소스·공개 GitHub·원격·설치 경로·기존 TRX 확인, 링크·공백·변경 범위 검사 | 앱 빌드·테스트·smoke·물리 입력은 다시 실행하지 않음 |

새 근거 없이 통과한 검사를 반복하지 않는다. 문서 변경은 문서 검사를 한다.
제품 수정 시 해당 회귀 검사부터 실행하고 실행한 소스·명령·종료 코드·OS·결과 경로를 기록한다.
휴식 타이머를 더 수정할 경우 `TimerRefinementTests`, `AnimatedCountdownTests`,
`PetOverlayWindowTests`와 직접 영향받는 테스트를 우선한다. 전체/진단 여부는 요청 범위에 맞춘다.
진단은 새 빈 프로필로 실행하며 결과 `<프로필>/verification/smoke.json`과 오류 로그를 확인한다.

## 7. 남은 확인과 다음 작업

아래는 인계 항목이며 자동 실행할 새 기능·배포 목록이 아니다.

1. `f9953a0`와 이번 문서·증거를 유지한다. 다른 머신/원격에서 재개하기 전 로컬 전용 상태를 먼저 해소한다.
2. 휴식 숫자 전환의 macOS 실제 표시·입력·숨김/완료/중지·0초/초과 경계는 아직 실기 결과가 없다.
   관련 제품 수정 요청이 생기면 최신 소스를 새로 빌드하고 새 프로필에서 확인한다.
3. 1.1.4 설치/업데이트 재시작 검사는 해당 배포에서 의도적으로 제외했다.
   이전 1.1.3의 Mac/Windows 업데이트 통과를 1.1.4 결과로 표시하지 않는다.
4. 물리 Windows·Intel, 다중 모니터/혼합 DPI·Spaces·장시간 사용·긴 GLB 자연 재생은
   실제 장비와 새 증거가 필요하다. [Windows 양식](windows-dogfooding-log.md)은 빈 항목을 미검증으로 유지한다.
5. 400MB 상한·전체 30분 반복 사용은 완료되지 않았다. 추가 3D 최적화와 Google 브랜딩은 보류를 유지한다.
6. Live 서버의 권한 복구와 실제 고객 앱의 로그인·구매 복원·환불을 구분한다.
   후속 운영 작업은 [보안 운영](operations/backend-security.md)의 절차와 요청 범위를 따른다.
7. Lemon Live 가격이 US$2.99로 바뀌어 현재 앱·웹 구매가 막혀 있다. [가격 전환 준비](validation/2026-10-14-lemon-usd-price.md)의
   마이그레이션·앱·웹 수정은 미커밋·미적용이다. 운영 DB 적용과 앱·웹 배포는 기록된 순서와 사용자 승인을 따른다.

작업 종료 때 이 문서의 날짜·개발 SHA·원격/태그·남은 로컬 변경·새 검증·보류 항목을
함께 갱신한다. 날짜별 검증 기록은 당시 사실을 보존하고 현재 상태는 이 인계와 문서 색인에 연결한다.

## 새 세션에 전달할 문장

> Unfold 작업을 이어받아 주세요. 먼저 `docs/HANDOFF.md`, `AGENTS.md`, `docs/README.md`를 읽고 모든 작업트리의 브랜치·HEAD·변경·csproj 버전을 확인하세요. 인계 시점 최신 개발본은 `codex/pet-overlay-hover`의 `f9953a0` (`1.1.4-beta`)이며 공개 `v1.1.4-beta`는 `4c64bfc`, 원격은 `4a668fd`입니다. 로컬 전용 휴식 숫자 수정과 미커밋 문서·증거를 보존하고 설치 앱을 최신 소스 실행으로 간주하지 마세요. 새 요청 범위만 진행하며 격리 데이터, 자동 검사와 실제 OS 검증을 구분해 보고하세요.
