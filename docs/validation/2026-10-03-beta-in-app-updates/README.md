# 구현 보고서

## 원인

이전 Beta v1.0.2에는 앱 내 업데이트와 업데이트용 설치 구조가 없었습니다. 이번 작업은 최신 Beta 작업 폴더 `/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold`의 실제 빌드 입력을 기준으로 진행했습니다. 기준 HEAD는 `37c6de354ceb390500d4bcb586bde1a74d16e205`이고, 기존 미커밋 변경을 포함한 버전은 `1.0.2-beta`였습니다. 이전 `release/mvp` 작업 폴더의 v0.2.2 제품 코드는 수정하지 않았습니다.

## 변경

- 프로젝트·앱·설치 메타데이터를 **Beta v1.0.3 / 1.0.3-beta**로 맞췄습니다.
- macOS 메뉴 막대와 Windows 트레이에 **업데이트 확인**을 추가했습니다. 설치된 관리 앱은 시작할 때 새 버전을 확인하고, 사용자가 다운로드 및 **재시작하여 적용**을 선택합니다. 다운로드 창을 닫아도 다운로드는 유지됩니다.
- 진행 중인 휴식과 저장하지 않은 펫/편집 작업을 확인한 후 적용합니다. 다운로드 실패·손상·중복 요청·적용 도구 실행 실패에 대한 복구 경로가 있습니다.
- OS·CPU·Beta/정식 채널을 구분하며, 다른 앱·다른 채널·구버전의 패키지를 차단합니다. 재시작 대기 캐시에도 동일한 앱·채널 검사를 적용합니다.
- 설정·기록·펫·계정 저장 경로와 Mac 번들 식별자를 유지했습니다. Windows 관리 설치는 사용자 데이터 및 이전 NSIS 제거 항목과 별도 위치를 사용합니다. 이미 선택한 자동 실행만 새 고정 실행 경로로 옮기고, 제거할 때 해당 설치본의 자동 실행 항목만 지웁니다.
- Velopack SDK와 도구를 `1.2.0`으로 고정하고, Windows x64 및 Apple Silicon/Intel Mac의 설치 파일·업데이트 목록·전체 업데이트 패키지를 생성하도록 구성했습니다. 기본 공개 저장소는 `C-Caffe1ne/Unfold`이며 앱에 GitHub 토큰을 넣지 않습니다.
- 기존 한국어 UI 문구·타이머·펫·설정 화면에는 추가 변경을 하지 않았습니다.

## 소유 파일

변경 20개 파일만 Beta 작업 폴더에 반영했습니다. 소유 범위 밖의 기존 빌드 입력 **1051개**는 작업 전 해시와 동일합니다. 원본 소유 파일은 `before/`, 전체 변경은 [implementation.patch](implementation.patch), 최종 해시는 [applied-source.json](applied-source.json)에 남겼습니다. Git 커밋·체크아웃·리셋은 수행하지 않았습니다.

- `.config/dotnet-tools.json`
- `.github/workflows/desktop.yml`
- `Scripts/package-updates.py`
- `Scripts/make-macos-bundle.sh`
- `Scripts/verify-windows-installer.ps1`
- `docs/cross-platform.md`
- `docs/releases/v1.0.3-beta.md`
- `src/Unfold.Desktop/Unfold.Desktop.csproj`
- `src/Unfold.Desktop/packages.lock.json`
- `src/Unfold.Desktop/AppUpdates.cs`
- `src/Unfold.Desktop/UpdateWindow.cs`
- `src/Unfold.Desktop/AppRuntime.Updates.cs`
- `src/Unfold.Desktop/AppRuntime.cs`
- `src/Unfold.Desktop/Program.cs`
- `src/Unfold.Desktop/PlatformServices.cs`
- `src/Unfold.Desktop/SmokeDiagnostics.cs`
- `Tests/Unfold.Tests/AppUpdateTests.cs`
- `Tests/Unfold.Tests/AppUpdateUiTests.cs`
- `Tests/Unfold.Tests/packages.lock.json`
- `Scripts/sign-macos-app.sh`

## 검증

| 검사 | 최종 결과 | 근거 |
|---|---|---|
| 실제 Beta 소스 Release 전체 테스트 | **571/571 통과**, 실패·건너뜀 0 | [TRX](beta-updates-final.trx), [로그](beta-source-tests-final.log) |
| 업데이트 집중 테스트 | **28/28 통과** | [TRX](updates-channel-guard.trx) |
| 실제 Beta 잠금 복원·Release 빌드 | 오류 0, 경고 0 | [로그](beta-source-build.log) |
| Windows x64·Mac ARM64·Mac x64 게시/패키지 생성 | 모두 종료 코드 0 | `*-frozen.log` |
| 패키지 해시·ZIP·Windows 실행 형식·Mac 서명/링크 | 통과 | [결과](packages-final.json), [재현 스크립트](verify-final-packages.py) |
| 실제 Apple Silicon Mac 업데이트 | SDK 다운로드 → 네이티브 앱 교체 → 자동 재시작 → 진단 통과 | [결과](mac-native-update-final.json), [로그](mac-native-update-final.log) |
| 업데이트 후 Mac 전체 진단 | 성공, 이미지 99개, 업데이트 창·설치 메타데이터 확인 | [smoke.json](mac-smoke-final/smoke.json), [업데이트 창](mac-smoke-final/app-updates.png) |
| Intel 패키지 Rosetta 실행 | 성공, 이미지 99개, 업데이트 설치 상태 확인 | [smoke.json](mac-rosetta-smoke/smoke.json) |
| 소스·작업 범위 보존 | 기준 HEAD 유지, 소유 밖 입력 해시 동일 | [최종 결과](final-result.json), `source-status-before.txt`, `source-status-after.txt` |

실제 SDK 테스트는 유효한 패키지 다운로드·체크섬 검증, 변조 파일 거부, 이전 버전 차단을 포함합니다. UI 테스트는 로그인/구매 접근 없이 업데이트 창 열기, 중복 창·중복 작업 방지, 휴식 중 적용 차단, 저장하지 않은 편집 취소, 다운로드 후 명시적 적용을 확인합니다.

Mac 교체 검증은 **임시 관리 앱의 이전 버전 메타데이터를 1.0.2-beta로 구성한 fixture**에서 수행했습니다. 기존 사용자 설치본을 업데이트한 시험은 아닙니다. 실제 `UpdateMac`이 최종 `.nupkg`를 추출·교체해 기존 앱 안의 표식 파일이 없어졌고, 별도 데이터 표식의 해시는 그대로였으며, 자동 재시작한 진단과 교체 후 코드 서명이 통과했습니다. 진단은 일부 시간·선택·인증 흐름을 모의하므로 실제 로그인·결제·사람의 입력 검증으로 해석하지 않습니다.

발견하고 수정한 문제는 업데이트 전역 초기화에 의존하던 기존 UI 테스트 회귀, Mac 링크/서명 정보 복사와 인증서 없는 Hardened Runtime 로딩 오류, Windows 제거 시 자동 실행 잔류, 플랫폼 전용 훅의 분석기 오류, 재시작 캐시의 채널 혼입입니다. 최초 실패/중단 로그는 보존하고 최종 통과 결과를 별도로 기록했습니다.

## 미검증·위험

- **Windows 실제 OS에서 설치·업데이트·제거·로그인 자동 실행은 아직 실행하지 않았습니다.** 전용 검증 스크립트와 CI에 준비했지만 실행/통과한 것으로 보고하지 않습니다. 이번 Windows 증거는 크로스 빌드·설치 파일 생성·262개 ZIP 항목·AMD64 실행 파일·버전/채널·해시 검사입니다.
- 실제 Intel Mac은 미검증이며, x64 실행 결과는 Apple Silicon의 Rosetta에서 확인했습니다. Windows Arm64 패키지는 이번에 만들거나 실행하지 않았습니다.
- `/Applications`에서 관리자 권한이 필요한 Mac 교체, 잠긴 파일·실제 전원 차단·실제 사용자 프로필의 이전 설치 전환은 미검증입니다.
- **이번 Mac 파일은 로컬 ad-hoc 서명이며, 공개 배포용 Developer ID 서명·공증을 수행하지 않았습니다. Windows 서명도 없습니다.** 공개 배포 전 별도 출시 검증이 필요합니다. 앱 본체 권한은 기존 `com.apple.security.cs.allow-jit` 한 개를 유지했습니다.
- GitHub Releases와 홈페이지는 변경하지 않았습니다. 실제 사용자에게 업데이트를 제공하려면 서명된 설치본과 각 채널의 JSON·참조 `.nupkg`를 같은 릴리스에 게시해야 합니다. 구버전에는 업데이트 코드가 없어 새 설치본을 **최초 한 번 직접 설치**해야 하며 그 이후부터 앱 내 업데이트를 사용할 수 있습니다.

최종 로컬 배포물은 `/Users/hwanghyeonseong/Documents/GitHub/Unfold/artifacts/local-in-app-updates-2026-10-03`에 있습니다. [build.json](/Users/hwanghyeonseong/Documents/GitHub/Unfold/artifacts/local-in-app-updates-2026-10-03/build.json)에 소스·배포 파일 해시와 검증 범위를 기록했습니다.

# 릴리스 QA 감사

## 환경과 범위

2026-10-03, macOS `26.6.2`, Apple Silicon, .NET 10, Avalonia 12.1.2. 최신 Beta 작업 폴더에 구현·반영하고, 새 `UNFOLD_DATA_DIR`로 테스트와 진단을 실행했습니다. 원본 프로필·설치된 사용자 앱·기존 v1.0.2 배포물을 교체하지 않았습니다.

## 실행한 명령과 결과

```sh
# 실제 Beta 소스에서 수행: /Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold
dotnet restore Unfold.slnx --locked-mode
dotnet build Unfold.slnx -c Release --no-restore
dotnet test Unfold.slnx -c Release --no-restore

# 동일 해시의 격리 빌드 입력에서 수행
dotnet test Unfold.slnx -c Release --no-restore --filter FullyQualifiedName~AppUpdate
dotnet publish src/Unfold.Desktop/Unfold.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:PublishSingleFile=false -o artifacts/win-x64/app
python3 Scripts/package-updates.py --runtime win-x64
bash Scripts/make-macos-bundle.sh osx-arm64
bash Scripts/make-macos-bundle.sh osx-x64
```

Mac native helper의 다운로드/교체/재시작 및 Rosetta 진단 명령은 별도 로그에 있습니다. 검증 도구는 `native-update-harness.cs`와 `.csproj`에 보존했습니다. 패키징 Python/Bash 구문 검사도 통과했습니다.

## 경계면 교차 검증

| 경계 | 확인 |
|---|---|
| 트레이 → 창 → 서비스 → SDK | 접근 제한과 독립된 창, 한 번의 작업, 진행률·오류·재시도 |
| 서비스 → 앱 종료 | 휴식/초안 보호 후 helper 실행, 실패 시 앱 유지 |
| 버전/채널 → feed/캐시 | 프로젝트 버전·패키지 ID·OS/CPU/배포 채널 일치, 다운그레이드 차단 |
| 패키지 → 네이티브 Mac helper | 상대 링크 복원, 전체 앱 교체, 데이터 분리, 자동 재시작 진단 |
| Windows 설치 → 실행 → 제거 | 고정 실행 경로와 분리된 데이터 경로, 소유한 자동 실행만 제거하는 코드/검사; 실제 OS 동작 대기 |

## 통과·실패·미검증

자동 검사와 실제 Mac 진단은 최종 통과했습니다. 초기 실패는 위에 기록한 수정 후 해결했습니다. 모델이 생성된 업데이트 창 캡처를 확인했으며, 사람이 키보드·마우스로 검수한 결과는 아닙니다. Windows 실기·실제 Intel 하드웨어·공개 배포 신뢰는 미검증입니다.

## 출시 차단 요소

Windows 실제 설치·업데이트·제거 검증, Mac Developer ID 서명/공증, 공개 릴리스 asset 게시와 실제 HTTPS 다운로드 업데이트 검증이 남았습니다. 이번 구현과 로컬 검증의 완료가 공개 배포 완료를 뜻하지 않습니다.

## 다음 검증 순서

1. 일회용 Windows 환경에서 CI 또는 `Scripts/verify-windows-installer.ps1`로 설치·재설치·제거·데이터 보존·자동 실행 제거를 확인합니다. 연속된 두 버전으로 실제 업데이트도 확인합니다.
2. 공개 배포용 Mac 서명·공증과 필요한 Windows 서명을 적용해 패키지를 다시 생성합니다.
3. OS별 feed와 참조 `.nupkg`를 같은 GitHub 릴리스에 게시한 뒤 기존 관리 설치본에서 실제 HTTPS 확인·다운로드·재시작을 검증합니다.
4. 구 Beta에서 최초 수동 전환, `/Applications` 권한 상승, 실제 로그인 자동 실행과 계정 복원을 별도로 확인합니다.

설계 참고: [Velopack 통합](https://docs.velopack.io/integrating/overview), [릴리스 채널](https://docs.velopack.io/packaging/channels), [Mac 패키징](https://docs.velopack.io/packaging/operating-systems/macos). 공식 라이브러리/도구의 1.2.0 계약과 실제 생성 파일을 함께 확인했습니다.
