# D01 Windows 실기 입력 검증 준비 — 2026-10-06

## 결론과 현재 상태

**상태: 환경 대기 / 실제 Windows NOT TESTED.** 이번 작업은 로컬 실행 환경과 최신 입력 계약의 읽기 전용 확인, 실행 체크리스트와 증거 양식 작성이다. Windows 실행·물리 입력·GPU 표시의 통과를 주장하지 않는다. 제품 코드·자산을 수정하지 않았다. 환경 확인 뒤 감독자가 기존 Windows 실기 양식을 현재 계약에 맞게 갱신했다. 클라우드 VM 설치·원격 접속·외부 게시·배포를 수행하지 않았다.

최신 확인 대상:

- 경로: `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold`
- 브랜치: `codex/glb-import-compat`
- HEAD: `1a74cbf2720937b3e4712f821f481603064281de`
- 프로젝트 버전: `1.1.0-beta`
- 검사 시작 시 `git status --short`: 출력 없음
- 실행될 정확한 Windows EXE/DLL: 아직 확정하지 않음. 아래 실행 준비 단계에서 최신 입력과 해시를 연결한다.

## 환경 확인과 검사 경계

| 확인 | 관찰 결과 | 해석 |
|---|---|---|
| `sw_vers`, `uname -m` | macOS 26.6.2, build 25G83, arm64 | 현재 실행 호스트는 Windows가 아님 |
| `command -v` | prlctl, VBoxManage, virsh, qemu-system-aarch64, qemu-system-x86_64, utmctl, wine, wine64, vmrun, limactl, multipass 출력 없음 | 해당 도구를 현재 PATH에서 찾지 못함 |
| `/Applications`, `~/Applications` 이름 검사 | Parallels/VirtualBox/VMware/UTM/Wine/CrossOver/QEMU/Windows/Remote Desktop/AnyDesk/RustDesk 일치 없음 | 표준 앱 설치 위치에서 해당 이름 없음 |
| `/opt/homebrew/bin`, `/usr/local/bin` 파일명 검사 | 위 VM/호환 실행 도구의 파일명 없음 | 표준 CLI 경로에서도 실행 도구 미확인 |
| 표준 VM 저장 디렉터리 존재 검사 | `~/Parallels`, `~/Virtual Machines.localized`, `~/Library/Containers/com.utmapp.UTM/Data/Documents` 모두 없음 | 이 세 표준 위치에 VM 디렉터리가 없음. 개인 파일 전체나 다른 사용자 지정 경로는 조사하지 않음 |
| `ps -axo comm` | `operation not permitted: ps` | 실행 중 VM 프로세스 여부 미확인. 권한을 높여 재조사하지 않음 |

확인한 표준 위치에서는 사용할 수 있는 로컬 Windows 환경을 찾지 못했다. 이는 컴퓨터 전체에 Windows가 없다는 인증이 아니다. 사용자도 현재 Mac만 사용할 수 있다고 확인했다. 이번 세션에 접근 가능한 Windows 실행 환경이 없으므로 D01 실기는 환경 대기로 유지한다. 새 전체 테스트·교차 게시를 반복하지 않았다.

## 현재 소스에서 확인한 입력 계약

[WindowsPetWindow.cs](/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold/src/Unfold.Desktop/WindowsPetWindow.cs:21):

- 투명 대상에서는 `WS_EX_LAYERED | WS_EX_TRANSPARENT`를 적용한다.
- 새로 추가한 layer에만 alpha 255를 초기화하고, 기존 layer를 재초기화하지 않는다.
- 매 poll에서 HWND 확장 스타일을 다시 확인한다. 반환값/적용 상태 실패는 성공으로 캐시하지 않고 이전 스타일 복구를 시도한다.
- 보이는 대상에서는 Transparent를 해제하고 Layered와 다른 스타일은 유지한다.

[PetWindow.Input.cs](/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold/src/Unfold.Desktop/PetWindow.Input.cs:12):

- 현재 프레임에서 가장자리 확장 없이 펫 픽셀을 판정한다.
- 누른 상태·같은 펫 창의 입력 캡처·열린 컨텍스트 메뉴는 입력을 유지한다.
- 실제 버튼이 있는 휴식 말풍선은 입력을 유지한다. 정보 표시용 호버 말풍선은 통과 대상이다.
- `DiagnosticMode`와 보이지 않는 창에서는 실제 커서를 사용하지 않는다. 따라서 화면 밖 smoke는 실기 클릭통과 증거가 될 수 없다.

[AnimationView.cs](/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold/src/Unfold.Desktop/AnimationView.cs:222): 현재 표시 프레임(2D 또는 GLB live image)의 알파값 **26/255 이상**을 입력 픽셀로 판정한다. 이보다 낮은 가장자리 알파는 통과 대상이므로 투명/불투명 샘플 선정 때 기록한다.

[PetWindow.cs](/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold/src/Unfold.Desktop/PetWindow.cs:40): poll 간격은 16ms이며 포인터 이동에서도 갱신한다. 실제 Windows UI 스레드 지연에서의 입력 전환은 미검증이다. 드래그와 release/capture-loss 경계는 여기의 실제 이벤트 흐름으로 확인한다.

### 기준 문서의 정확한 요구

[Windows 클릭 통과 수정 기록](/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold/docs/validation/2026-10-06-windows-pet-click-through.md):

> Windows 실기 통과 전까지 사용자 환경의 해결 완료로 판정하지 않는다.

[검증 안내](/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold/docs/verification.md:244)는 실제 OS와 자동 검사를 구별하고, OS·아키텍처·커밋·배포물과 함께 결과를 남기도록 요구한다.

현재 요구사항은 [cross-platform.md](/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold/docs/cross-platform.md)와 현재 소스를 기준으로 한다. [기존 Windows 실기 양식](/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold/docs/windows-dogfooding-log.md)에는 작업 시작 당시 최소 5분, 적용 버튼, 별도 휴식창, 숨긴 펫의 알림 임시 재등장 금지 등 현재 계약과 다른 항목이 있었다. 이번 작업에서 기존 양식도 최소 1분·저장·펫 말풍선·알림의 임시 재등장 계약으로 갱신했다. 모든 실행 체크는 공란으로 유지한다.

## 과거 Windows 테스트 ZIP의 식별 한계

실제로 존재하는 로컬 파일:

- `artifacts/Unfold-Windows-x64-clickthrough-preview-20261006.zip`
- 크기: 107,932,590 bytes
- 이번 `shasum -a 256` 확인: `d13fa02ee2594e56cb5736f57020764c7420c7b62dda3e6f285a8378c8940c4e`
- 기존 verification.json의 해시와 일치한다.
- 기존 기록은 baseline `671729a`에서 당시 미커밋 수정을 포함한 빌드이며, `windowsExecuted=false`, `physicalInputVerified=false`다.

ZIP 해시 일치는 그 과거 파일의 식별만 증명한다. 현재 `1a74cbf` 전체 입력과의 실행 DLL 일치 여부를 이번에 증명하지 않았다. 최신 앱 D01 통과에는 현재 소스에서 만든 Windows 실행본 또는 현재 입력과 동일함이 확인된 실행본이 필요하다. 같은 `1.1.0-beta` 문자열만으로 최신 실행본을 구별하지 않는다.

## Windows 확보 후 실행 준비

1. 사용할 Windows 기기/게스트의 OS build·아키텍처·CPU·GPU·드라이버·모니터별 DPI·해상도·배치·입력 장치를 기록한다. RDP 결과와 물리 마우스 결과를 구별한다.
2. 최신 `1a74cbf` 소스 기준과 미커밋 상태, 생성 방법, EXE/DLL SHA-256을 기록한다. 작업 중 추가 수정이 있으면 새 HEAD 또는 dirty diff 해시를 남긴다.
3. 기존 앱과 구별되는 테스트 실행 경로를 확정하고, 테스트 앱을 정상 모드로 시작한다. 다른 사용자의 실행 중 앱/데이터를 임의로 종료·교체하지 않는다.
4. 새 빈 `UNFOLD_DATA_DIR`을 설정한다. 유효한 승인된 계정/권한으로 진입한다. 로그인·서버 접근이 없는 상태에서 GUI 테스트가 완료됐다고 기록하지 않는다. 로그인/실결제 검증은 B01과 별도다.
5. 2D fixture와 GLB fixture의 ID·파일/manifest 해시·고정 pose 또는 event mapping을 기록한다. 내부 구멍이 있는 프레임/자세를 실제로 확인한다. 구멍 없는 모델에서는 내부 구멍 검사를 통과로 채우지 않는다.
6. Unfold와 **다른 프로세스**의 입력 확인 창을 펫 아래에 둔다. 입력 확인 창은 mouse down/up/click/right-click/wheel의 수신 좌표·시각·카운터 또는 스크롤 위치를 남기도록 한다. 특정 Unfold 컨트롤에 직접 보내는 이벤트는 OS 전달 증거로 사용하지 않는다.
7. 움직이지 않는 샘플과 프레임이 변하는 샘플을 구분하고, 캡처에 커서/좌표·fixture·DPI·각 프로세스 ID를 연결한다.

PowerShell 실행 예시(아직 실행하지 않음):

```powershell
$d01DataDir = Join-Path $env:TEMP ('Unfold-D01-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $d01DataDir | Out-Null
$env:UNFOLD_DATA_DIR = $d01DataDir
# 아래 경로는 실제 시험 실행본의 payload 경로로 지정한다.
$d01Exe = 'C:\Unfold-D01\app\Unfold.exe'
Get-FileHash -Algorithm SHA256 -LiteralPath $d01Exe
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path (Split-Path $d01Exe) 'Unfold.dll')
& $d01Exe --version
& $d01Exe
```

`--smoke-test`/화면 밖 diagnostics 결과를 아래 물리 입력 결과로 대체하지 않는다. 모니터 DPI 설정 화면 캡처와 실제 실행 scaling을 기록하고, 2배 off-screen PNG를 Windows 200% 증거로 사용하지 않는다.

## 실기 체크리스트와 합격 기준

기본 조건 행렬: **2D × Windows DPI 100/150/200%**, **GLB × Windows DPI 100/150/200%**. 각 셀에서 W01–W10을 수행한다. DPI가 적용되기 위해 재시작/로그아웃한 경우 기록한다. W11–W12는 실제 서로 다른 DPI의 두 모니터 조건에서 수행한다. 미실행 셀은 NOT TESTED로 남긴다.

| ID | 행동 / 재현 절차 | 반드시 기록할 실제 결과 | 합격 기준 |
|---|---|---|---|
| W01 | 펫 창 사각형 안의 투명 여백에서 왼쪽 클릭 | 아래 프로세스 down/up/click 수신·좌표·카운터 전후, 펫 반응 | 아래 앱 카운터가 해당 입력만큼 증가하고 펫이 반응하지 않음 |
| W02 | 실제 프레임 내부 구멍에서 클릭 | 프레임/pose·구멍 좌표·아래 앱 입력 수신 | 여백과 같은 통과. 입력 alpha 샘플과 보이는 구멍이 일치 |
| W03 | 불투명 몸체의 짧은 클릭·누름 유지 | 아래 카운터 변화, 클릭 mapping·펫 pose 변화, 창 좌표 | 아래 앱에 누름/클릭이 새지 않음. 지정된 펫 반응이 있으면 발생. 반응 없는 팩은 클릭 클립 부재 기록 |
| W04 | 몸체를 누르고 투명 영역/창 밖으로 드래그, 그 위치에서 해제 | press/move/release 순서, 펫 창 좌표 전후, 아래 이벤트 전후 | 드래그 동안 펫 입력 유지·이동. 해제 후 아래 앱 투명 클릭/스크롤 통과 복원 |
| W05 | 드래그 도중 다른 창으로 capture 전환/앱 전환 등 정상 취소 경로 | 취소 행동·실제 capture-loss·고정된 누름 상태 여부, 다음 통과 입력 | 펫이 계속 붙어 움직이지 않고 이후 투명 입력 통과. 취소를 실제로 재현하지 못하면 미검증 |
| W06 | 투명 여백·구멍에 커서를 놓고 wheel, 정보 호버 말풍선 위에서 click/wheel | 아래 wheel delta·스크롤 위치·클릭 카운터 전후 | 아래 앱 수신/스크롤 증가. 정보 호버 표시가 입력을 막지 않음 |
| W07 | 몸체 우클릭 → 메뉴 항목 선택 → 닫은 뒤 통과 재검사 | 메뉴 전/열림/닫힘 캡처, 설정 창 열림 또는 펫 숨김 결과 | 메뉴가 입력을 유지하고 선택이 실행됨. 닫힌 뒤 투명 입력 복원 |
| W08 | 1분 간격 저장 → 정상 자동 알림 → n분 뒤에 / 휴식 시작 / 완료를 개별 입력 | 상태·countdown 전후, 실제 버튼 수신, break-history 전후 | 알림 버튼이 클릭되고 정확한 상태로 전환됨. 완료는 기록 1건, 미루기는 완료 기록 없음 |
| W09 | 커서를 고정하고 프레임/pose가 해당 위치를 투명↔불투명으로 바꾸게 한 후 입력 | 표시 프레임·샘플 좌표·상태 전후·아래 수신, polling 전환 영상 | 현재 표시 프레임에 맞게 수신 대상 전환. 빠른 이동→즉시 클릭도 별도 관찰 |
| W10 | 숨김/표시·펫 선택·앱 펫 크기 50/100/150% 변경 후 W01–W04 재검사 | 변경 전후 설정·실제 창/픽셀·입력 대상, GPU 표시 영상 | 입력 영역이 현재 크기/펫에 맞고 검은 배경·펫 소실·깜박임이 관찰되지 않음 |
| W11 | 100↔150%, 150↔200% 모니터 사이를 드래그하고 W01–W04·W06 반복 | 각 display 해상도/DPI/원점, 창 좌표·현재 scaling·아래 프로세스 수신 | 양쪽 모니터에서 픽셀과 입력 영역 일치, 해제 후 클릭통과 복원 |
| W12 | 실행 중 DPI 변경·주 모니터 변경·모니터 분리 후 반복 | OS 설정 전후·창 위치·보이는 픽셀·입력, 재시작 여부 | 적용된 표시 조건에 맞게 복구, 펫이 접근 불가능한 화면 밖에 남지 않음 |

W03의 애니메이션 결과는 실제 manifest/mapping 기준으로 정한다. 모든 펫에 같은 click 반응을 요구하지 않는다. W08의 입력은 설정에 실제 저장한 미루기 시간/다음 휴식 길이와 비교한다. 숨긴 펫은 새 알림 동안 임시 표시될 수 있으며, saved ShowPet 설정이 계속 숨김으로 남는지 함께 확인한다.

## 필수 증거 묶음

- `environment.json`: 날짜/시간대, OS/build, architecture, CPU/GPU/driver, input type, display layout/scale, 실행 경로·PID·version·HEAD·dirty 여부·EXE/DLL 해시.
- `fixtures.json`: 2D/GLB ID·해시·표시 크기·재생 pose/animation·알파 샘플 좌표. GLB 내부 구멍이 확인되는 자세의 캡처.
- `Wxx-<2d|glb>-dpi<100|150|200>-before.png` / `-after.png`: 실제 Windows 캡처. appearance·커서/설정 상태용.
- `Wxx-...-screen-recording`: OS 전역 입력과 대상 창/카운터 변화를 연결하는 화면 녹화. 창 지정 이벤트 사용 여부를 기록한다.
- `input-events.jsonl`: 입력 수신 프로세스·좌표·시각·종류·카운터·스크롤 위치 전후. 그림만으로 전달 성공을 추측하지 않는다.
- 드래그: `window-position-before/after`, release 시각, 뒤쪽 앱 수신 복원 기록.
- 메뉴/알림: UI 전후 캡처와 실제 상태/동작 결과. 완료 기록은 격리 데이터의 JSON 전후 차이로 확인한다.
- `unfold.log` 관련 구간, 실패 재현 순서, 환경 조건. 계정 토큰/자격 증명은 증거에 넣지 않는다.
- 증거 파일마다 SHA-256과 테스트 ID를 연결하는 manifest.

최소 실기 결과 JSON 양식:

```json
{
  "testId": "W01",
  "status": "NOT_TESTED",
  "reason": "No available Windows execution host",
  "sourceHead": "1a74cbf2720937b3e4712f821f481603064281de",
  "version": "1.1.0-beta",
  "windowsExecuted": false,
  "inputMethod": null,
  "fixtureType": "2D",
  "fixtureSha256": null,
  "windowsDpiPercent": 100,
  "petScalePercent": 100,
  "displayId": null,
  "displayOriginPhysicalPx": null,
  "cursorPhysicalPx": null,
  "expectedRecipientProcessId": null,
  "actualRecipientProcessId": null,
  "counterBefore": null,
  "counterAfter": null,
  "wheelDelta": null,
  "windowBefore": null,
  "windowAfter": null,
  "screenshots": [],
  "eventLog": null,
  "recording": null,
  "reproSteps": []
}
```

`PASS`는 실제 수신 결과와 요구 일치를 확인할 때만 지정한다. 수신 이벤트 없이 캡처만 있으면 입력 테스트는 NOT TESTED 또는 INCONCLUSIVE다. 특정 모델에서 구멍이 보이지 않아 시험하지 못한 경우 필수 내부 구멍 항목은 미검증으로 남겨 적절한 fixture를 준비한다. 없는 환경을 제품 결함으로 기록하지 않는다.

## 재현 결과와 다음 실행 순서

- 이번 Windows 제품 실행에서 재현한 결함: 없음. 실제 Windows를 실행하지 않았으므로 결함 부재를 뜻하지 않음.
- 이번에 확인한 문서 위험: 기존 dogfooding 양식이 현재 기능과 다른 판정 기준을 포함함. 현재 코드/계약으로 만든 이 양식을 사용한다.
- 현재 출시 준비 판정: **NOT DETERMINED**. D01의 필수 Windows 입력·GPU·DPI·다중 모니터 증거가 비어 있음.
- 다음 순서: 승인된 실제 Windows 환경 확보 → 최신 실행 입력·fixture 해시 확정 → 100% 2D/GLB 핵심 입력 → 150/200% → mixed-DPI/분리/표시 복구 → 재현된 실패만 소유 파일을 좁혀 D03 배정 → 그 실패 범위와 영향 셀 재검사.

## 읽기 전용 수행 명령

`git worktree list --porcelain`, 최신 경로의 `git status --short`와 `git log -1`, 프로젝트 Version 검색, 관련 코드/문서 읽기, `sw_vers`, `uname -m`, 표준 tool PATH/app/VM-directory 존재 검사, `ps -axo comm`(권한 실패), 기존 preview ZIP의 `ls -l`과 `shasum -a 256`을 수행했다. 제품 런타임이나 자동 테스트를 실행하지 않았다.

적용 스킬: [unfold-release-qa](/Users/hwanghyeonseong/Documents/GitHub/Unfold/.agents/skills/unfold-release-qa/SKILL.md). 과거 입력 QA 기억은 조사 시작점과 OS 증거 경계에만 사용했고, 현재 코드·HEAD·파일 해시·로컬 환경은 이번에 다시 확인했다.
