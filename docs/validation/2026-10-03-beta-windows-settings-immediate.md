# 릴리스 QA 감사

## 환경과 범위

사용자 요청: 설정 즉시 적용 변경을 Windows 버전에도 동일하게 반영한다.

macOS arm64 호스트에서 .NET 10과 NSIS 3.12로 Windows x64 배포본을 생성했다. 실제 소스는 `/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold`, 기반 커밋 `37c6de354ceb390500d4bcb586bde1a74d16e205`, 프로젝트 버전 `1.0.2-beta`이다. 루트 작업 트리의 구버전 소스를 빌드하지 않았다.

앞선 [설정 변경 보고서](2026-10-03-beta-settings-immediate.md)에 기록된 실행 코드 5개와 검사 파일 10개의 해시를 대조해 모두 일치함을 확인했다. 해당 설정 경로에는 OS별 구현 분기가 없으므로 같은 코드로 Windows 배포본을 빌드했다. 이전 펫 추가·타이머·앱 내부 창 버튼·외곽 라운딩 변경도 같은 Beta 소스에 포함되어 있다. 이번 작업에서 제품 코드나 기존 UI 문구는 추가 수정하지 않았다.

패키징은 기존 Beta 스크립트·설치 정의·아이콘·문서를 새 임시 경로에 복사한 뒤 실행했다. 기존 설치 파일과 macOS 수정본을 덮어쓰지 않았다. 결과는 [로컬 Windows Beta 폴더](../../artifacts/local-windows-beta-2026-10-03/)에 보관했다.

| 파일 | 크기 (bytes) | SHA-256 |
|---|---:|---|
| Unfold-v1.0.2-beta-win-x64-setup.exe | 77487415 | `fee21c66a24030a70f33db7656087b0f75e284caf07dd57cd4e5361ebbc79be5` |
| Unfold-v1.0.2-beta-win-x64.zip | 106390812 | `22999a4af2b9a087a959dcb5460bb501f4bc94fb35e5effa2179740237837ac5` |

## 실행한 명령과 결과

- `dotnet publish <Beta>/src/Unfold.Desktop/Unfold.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:PublishSingleFile=false -o <isolated>/artifacts/win-x64/app`: 종료 코드 0. [정확한 명령](../../artifacts/local-windows-beta-2026-10-03/publish-command.json), [빌드 로그](../../artifacts/local-windows-beta-2026-10-03/publish-win-x64.log).
- 기존 `Scripts/package-windows-installer.py --compiler /opt/homebrew/bin/makensis`: 종료 코드 0. NSIS 경고도 실패로 처리하는 `-WX` 옵션 사용, 경고·오류 0. [설치 파일 로그](../../artifacts/local-windows-beta-2026-10-03/windows-installer.log).
- 기존 `Scripts/package-windows.py win-x64`: 종료 코드 0. [ZIP 로그](../../artifacts/local-windows-beta-2026-10-03/windows-portable.log).
- `7zz t <setup.exe>`와 `7zz x -y -o<isolated> <setup.exe>`: 종료 코드 0. 설치 파일 CRC 검사·추출 통과. 추출 파일 262개의 SHA-256이 게시 입력과 전부 일치했다.
- Python ZIP CRC 검사와 게시 파일 해시 대조: 통과. ZIP 내부 앱 파일 262개가 같은 게시 입력과 일치하고 정식 `Unfold.cmd` 실행 래퍼를 포함한다.
- 빌드 전후 소스 입력 288개의 해시와 Git 상태가 동일했다. 기존 추적·미추적 변경을 보존했다. [보존·검사 근거](../../artifacts/local-windows-beta-2026-10-03/source-and-tests-verification.json).

## 경계면 교차 검증

- `SettingsWindow.PreferencesEdited` → `ApplyPreferences` → `AppRuntime.UpdateSettings`를 확인했다. 런타임 설정 저장과 값 갱신은 비동기 펫 갱신을 기다리기 전에 실행된다. 숫자 입력·음량·효과음·말풍선 변경은 공통 경로를 사용한다.
- 설정 페이지의 저장·취소 버튼과 이탈 확인 모달 경로가 제거된 동일 소스를 빌드했다. 홈의 스트레칭·휴식 시간에 있는 별도 저장 범위는 기존 동작을 유지한다.
- Windows 게시 어셈블리 메타데이터의 정보 버전은 `1.0.2-beta+37c6de354ceb390500d4bcb586bde1a74d16e205`이고, AMD64 ReadyToRun 출력이다. `PreferencesEdited`, `ApplyPreferences`, `TryReadPreferences`, `BuildWindowControls`, `ToggleWindowMaximized` 메서드가 포함되고 제거한 설정 메서드는 없다. [어셈블리 검사](../../artifacts/local-windows-beta-2026-10-03/windows-assembly.json). 이 검사는 코드 포함 여부를 확인하며 Windows UI 실행 결과를 의미하지 않는다.
- `Unfold.exe`는 Windows AMD64 PE 실행 파일이고 deps의 RID는 `win-x64`이다. runtimeconfig의 `includedFrameworks`를 확인해 .NET 자체 포함 출력임을 확인했다.
- Windows 미디어 도구 원본 아카이브의 고정 SHA-256을 확인했다. 캐시 12개 파일을 원본 아카이브와 비교하고, 게시된 `Tools/` 사본과도 비교했다.
- 기본 펫 5종의 `character.json`과 모든 자산이 소스와 일치한다. PNG 시트 크기·기본 idle 행동과 폰트·미디어 라이선스를 확인했다. 사용자 설정·로그·환경 파일·심볼릭 링크가 패키지에 섞이지 않았다. [패키지 검사](../../artifacts/local-windows-beta-2026-10-03/windows-package-validation.json).
- 설치 정의는 현재 사용자 `%LOCALAPPDATA%\Programs\Unfold`에 앱을 설치하고 `%LOCALAPPDATA%\Unfold` 사용자 데이터를 보존하는 기존 계약을 사용한다. 설치·제거 시 `.instance.lock` 검사와 자동 실행 경로 갱신도 기존 구현을 유지한다. 실제 Windows에서 이 계약을 실행한 결과는 아니다.

## 통과·실패·미검증

- **자동 검사 통과**: Windows x64 교차 게시, 설치 파일 컴파일, 설치 파일·ZIP CRC, 262개 앱 파일 해시 일치, 버전·RID·자체 포함·ReadyToRun 확인, 자산·라이선스·소스 보존.
- **이전 공통 코드 검사**: [집중 검사 76/76](2026-10-03-beta-settings-immediate/settings-immediate-focused.trx), [전체 Release 검사 543/543](2026-10-03-beta-settings-immediate/settings-immediate-full.trx), 실패·건너뜀 0. 변경 파일 해시 일치를 확인하고 기존 근거를 재사용했다. 이번 Windows 패키징에서 테스트를 재실행하거나 이를 실제 Windows 테스트로 표시하지 않았다.
- **이전 실제 macOS 자동 진단**: 동일 설정 변경 소스의 즉시 적용·버튼 제거·모달 제거·타이머·창 버튼·외곽 라운딩 진단이 통과했다. 이번 Windows 설치 파일을 macOS에서 실행한 결과는 아니다.
- **실제 Windows / 사람 검수 미검증**: 설치·실행·업데이트·제거, 설정 조작과 재시작 후 값 유지, 창 버튼·라운딩·최대화·DPI·트레이, 물리 입력과 실제 소리 출력.

## 출시 차단 요소

이번 산출물은 로컬 Windows Beta 수정본이다. 공개 다운로드·릴리스 게시나 기존 설치본 교체는 수행하지 않았다. Windows 실기 실행 검증과 Authenticode 서명은 완료되지 않았다. 기존 공개 Beta와 같은 버전 번호이므로 이번 수정본은 [빌드 정보와 파일 해시](../../artifacts/local-windows-beta-2026-10-03/build.json)로 구별한다.

## 다음 검증 순서

1. Windows 10/11 x64에서 이번 설치 파일을 실행하고 설정 변경이 즉시 반영·저장되는지 확인한다. ZIP은 전체를 압축 해제하고 `win-x64/Unfold.cmd`로 실행한다.
2. 새 `UNFOLD_DATA_DIR`로 설치·재설치·제거 검사와 `--smoke-test`를 실행하고 실제 입력·창 상태·효과음도 확인한다. 기존 사용자 데이터를 테스트에 사용하지 않는다.
3. 공개 배포 요청 시 버전·서명·Windows 실기 결과를 확정하고 설치 파일을 게시한다.
