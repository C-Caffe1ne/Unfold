> **보관 문서 · Garden 실험 — experiment/garden-windows / 3098f20**
> 현재 MVP의 작업 지침이 아닙니다. 원문의 명령, 담당 배정, 경로와 검증 결과는 당시 기록입니다.
> 원래 위치: `docs/team/reports/C-01-platform.md` · [현재 문서 안내](../../../../README.md)

# C-01 — Windows 경로와 배포 감사

작업 ID / 담당: **C-01 / C — Windows 플랫폼·배포**

OS / 브랜치 / 기준 커밋 / 시작 시 변경 상태:

- OS: macOS (darwin 25.6.0, arm64). **Windows 실기기 없음.**
- 브랜치: `experiment/garden-windows` (`git branch --show-current`)
- HEAD: `3098f2057bca28f7024aa2a9413556111b3dea09` (지시서 기준 커밋과 일치)
- `git status --short`: `?? docs/team/` 한 줄. 제품 파일 변경 없음.
- 이 라운드에서 실행한 것: `git`, `gh`, `grep`, `cat`, PNG 알파 측정 스크립트(읽기 전용).
  **`dotnet build` / `test` / `run`, publish 스크립트는 실행하지 않았다(D 전담).**

검증 상태 표기: `정적 확인`(코드/설정 판독), `CI 기록 확인`(GitHub Actions 실제 실행 이력),
`측정`(저장소 자산의 수치 계측), `미검증`(실제 OS 동작 확인 필요).
**이 보고서에 `실제 OS 검증 통과` 항목은 하나도 없다.**

---

## 확인한 현재 동작

정적 확인 기준의 Windows 경로 지도다.

| 기능 | 코드 | Windows 경로 | 상태 |
|---|---|---|---|
| idle 감지 | `PlatformServices.cs:11,17-22` | `user32!GetLastInputInfo` + `Environment.TickCount` 언체크드 차분 | 정적 확인 (롤오버 처리 자체는 정상) |
| 로그인 실행 | `PlatformServices.cs:35-51` | HKCU `Software\Microsoft\Windows\CurrentVersion\Run` 값 `Unfold` = `"<exe>" --background` | 정적 확인, 실동작 미검증 |
| `--background` 처리 | `App.cs:25` → `AppRuntime.cs:65-84` | `desktop.Args`에 포함 시 `Start(true)` → `ShowSettings()` 생략, 트레이+펫만 | 정적 확인 (인자 전달 경로는 성립) |
| OS 알림 | `NativeReminder.cs:21-37` | `shell32!Shell_NotifyIconW` NIM_ADD, `NIF_ICON\|NIF_TIP\|NIF_INFO`(2\|4\|16), 12초 후 NIM_DELETE | 정적 확인, 전달 여부 미검증 |
| 중복 실행 방지 | `Program.cs:17-18` | `%LOCALAPPDATA%\Unfold\.instance.lock`을 `FileShare.None`으로 점유 | 정적 확인 |
| 재활성화 | `SingleInstance.cs:9-20`, `AppRuntime.cs:69` | 명명 파이프 `unfold-<SHA256(DataRoot)[..24]>`, 수신 시 `ShowSettings` | 정적 확인 |
| 종료 | `AppRuntime.cs:228-239` | `Quit()` → `Dispose()`(트레이/펫/파이프) → `desktop.Shutdown()` | 정적 확인 |
| 투명 픽셀 클릭 통과 | `PetWindow.cs:117-130` | 40 ms 타이머 → `GetCursorPos` → `OpaqueAt` → `SetWindowLongPtrW(GWL_EXSTYLE, ±WS_EX_TRANSPARENT)` | 정적 확인, 실동작 미검증 |
| 게시 | `Scripts/publish-desktop.ps1` | `dotnet publish -r win-x64 --self-contained -p:PublishReadyToRun=true -p:PublishSingleFile=false` → `Compress-Archive` | CI 기록 확인 (Cat 기준 커밋에서 성공) |
| 자산 포함 | `Unfold.Desktop.csproj:14-19` | `Sources/Unfold/Resources/Characters/**/*` → `Assets/Characters/%(RecursiveDir)...`, `CopyToPublishDirectory` | 정적 확인 |

README.md:40-41 / docs/mvp.md:46의 "클릭 통과는 Windows 전용, macOS는 미구현" 주장은
**코드와 일치한다.** `PetWindow.UpdateClickThrough`는 `PetWindow.cs:123`에서
`if (!OperatingSystem.IsWindows() ... ) return;`으로 즉시 빠져나가고, macOS에는 대응 경로가 없다.
다만 이 문서 주장이 검증하는 것은 "코드가 Windows에만 존재한다"까지이며,
"Windows에서 실제로 동작한다"는 아니다. 후자는 아래 C-B4·C-B7 참조.

### 입력 판정(A 소유)과 OS 어댑터(C 소유)의 경계

| 구간 | 파일:줄 | 소유 |
|---|---|---|
| 알파 히트 판정 `OpaqueAt` | `AnimationView.cs:61-72` | **A** (README 소유권 표에 `AnimationView.cs` = A) |
| 포인터 press/move/release, 드래그 임계값, 클릭 시간 | `PetWindow.cs:35-55` | **A** |
| 화면 클램프 | `PetWindow.cs:111-116` | **A** |
| `GetCursorPos` / `GetWindowLongPtrW` / `SetWindowLongPtrW` / `WS_EX_TRANSPARENT` | `PetWindow.cs:117-130` | **C**(OS 어댑터) — 그러나 **파일 소유자는 A** |

`docs/team/README.md`의 소유권 표는 `PetWindow.cs`를 A에게 배정하는데,
`03-platform.md`는 C에게 클릭 통과 경로 분석을 지시한다.
**`UpdateClickThrough`는 C가 사양을 쓰고 A가 편집하는 공동 티켓으로 발행해야 한다.**
C가 단독으로 이 함수를 고치면 소유권 규칙 위반이다. 이 보고서는 그래서 수정을 하지 않았다.

---

## 발견 사항

각 항목: 심각도 / 관찰 사실(코드 근거) / 재현 절차 / 영향 / 검증 상태.

### C-B1 — Windows 로그오프·종료를 앱이 차단할 수 있다

- **심각도: 높음 (출시 차단 후보)**
- 코드 근거: `AppRuntime.cs:59-63`
  ```
  desktop.ShutdownRequested += async (_, e) =>
  { if (quitting) return; e.Cancel = true; await Quit(); };
  ```
  `Quit()`(`AppRuntime.cs:228-238`)은 `editor is not null && !await editor.CanCloseDocument()`이면
  **모달 확인 창을 띄우고 `return`한다.** 그 경로에서는 `desktop.Shutdown()`이 호출되지 않는다.
- 관찰 가능한 증상(예상): Windows 로그아웃/재시작 시
  "이 앱이 종료를 방해하고 있습니다 / Unfold" 차단 화면이 뜬다.
  에디터에 저장하지 않은 문서가 있으면 로그오프가 무기한 멈춘다.
- 원인 가설: Avalonia Win32는 `WM_QUERYENDSESSION`을 `ShutdownRequested`로 올린다.
  `e.Cancel = true`는 `WM_QUERYENDSESSION`에 FALSE를 돌려주는 것과 같고,
  Windows는 이를 "세션 종료 거부"로 해석한다. 뒤이은 `Quit()`은 비동기라 첫 종료 요청은 이미 거부된 뒤다.
- 재현 절차(Windows 필요): 게시된 `Unfold.exe` 실행 → 시작 메뉴 → 로그아웃.
  차단 화면과 그 안의 앱 이름을 캡처한다. 이어서 에디터를 연 상태(설정에서 진입 가능한 빌드)에서 반복한다.
- 조건: Windows 10/11, 게시 빌드, 모니터·배율 무관.
- 검증 상태: **미검증** (정적 확인만).

### C-B2 — 실행 중 모니터가 분리되면 펫이 화면 밖에 남고 복구 수단이 없다

- **심각도: 높음 (출시 차단 후보 — 로그의 BLOCKER 목록 "the pet vanishes"에 해당)**
- 코드 근거:
  - `ClampPosition()`은 **두 곳에서만** 호출된다: `PetWindow.cs:62`(`Opened`), `PetWindow.cs:52`(`PointerReleased`).
  - `ShowPet()` `PetWindow.cs:108`은 `Show(); hitTimer.Start(); animation.SetRunning(true);`뿐 — **재클램프 없음.**
  - `AppRuntime.UpdatePet()` `AppRuntime.cs:138-143`도 클램프하지 않는다.
  - 디스플레이 변경 이벤트 구독이 저장소 전체에 없다(`Screens.Changed` 미사용).
  - 트레이 메뉴 `AppRuntime.cs:217-224`에는 Settings / Hide Pet / Pause / Reset timer / Break now / Quit만 있고
    **"펫 위치 초기화"가 없다.** 펫은 `ShowInTaskbar = false`(`PetWindow.cs:30`)라 작업 표시줄로도 못 잡는다.
- 관찰 가능한 증상: 보조 모니터에 펫을 둔 채 그 모니터를 뽑으면 펫이 사라지고,
  트레이 Hide Pet → Show Pet을 해도 돌아오지 않는다. 앱을 Quit 후 재실행해야 `Opened`의 클램프로 복귀한다.
- 재현 절차(Windows + 모니터 2대 필요): 펫을 보조 모니터로 드래그 → 마우스를 놓아 위치 저장 →
  보조 모니터 케이블 분리(또는 `Win+P` → PC 화면만) → 트레이 Hide Pet → Show Pet.
  펫이 돌아오는지 확인 → 돌아오지 않으면 Quit 후 재실행해 복귀 여부 확인.
- 조건: Windows 10/11, 모니터 2대 이상, 게시 빌드 권장(개발 빌드로도 재현 가능).
- 검증 상태: **미검증**.

### C-B3 — 로그인 실행: 개발 빌드 경로가 등록될 수 있고, 체크박스가 죽은 경로를 "켜짐"으로 표시한다

- **심각도: 높음 (출시 차단 후보)**
- 코드 근거 (a) 게시 여부 가드가 불충분: `PlatformServices.cs:44-46`
  ```
  var exe = Environment.ProcessPath ?? throw new IOException(...);
  if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
      throw new IOException("Publish Unfold before enabling launch at login.");
  ```
  가드는 프로세스 이름이 정확히 `dotnet`일 때만 작동한다. `.NET`은 `UseAppHost` 기본값이 참이라
  `dotnet run`도 **apphost(`bin\Debug\net10.0\Unfold.exe`)를 실행**하므로 `ProcessPath`가 `dotnet.exe`가 아닐 개연성이 높다.
  그러면 가드가 통과되고 `PlatformServices.cs:50`이 빌드 출력 경로를 Run 키에 쓴다.
  `Scripts/run-desktop.ps1:4`가 바로 그 `dotnet run` 경로다.
- 코드 근거 (b) 상태 조회가 경로를 대조하지 않는다: `PlatformServices.cs:37-38`
  ```
  using var key = Registry.CurrentUser.OpenSubKey(@"Software\...\Run");
  return key?.GetValue("Unfold") is string;
  ```
  값이 **문자열이기만 하면** true다. 현재 실행 파일 경로와 비교하지 않는다.
- 관찰 가능한 증상:
  - (a) 개발용으로 한 번 켜면, 저장소를 지우거나 `dotnet clean`한 뒤에도 Run 키가 남아
    로그인마다 없는 exe를 가리킨다(Windows는 조용히 실패한다).
  - (b) 포터블 zip을 다른 폴더로 옮긴 뒤 설정을 열면 체크박스는 **켜짐**으로 보이는데
    실제 로그인 시에는 아무것도 실행되지 않는다.
    `docs/cross-platform.md:34-35`가 "옮겼으면 껐다 켜라"고 적어 둔 것이 바로 이 결함의 수동 우회다.
- 재현 절차(Windows 필요):
  1. `./Scripts/run-desktop.ps1` 로 개발 빌드 실행 → 설정 → Launch at login 켜기.
  2. `Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Unfold`
     → 값이 `bin\Debug\...\Unfold.exe`를 가리키면 (a) 확정. 예외가 떴다면 가드가 실제로 작동한 것이니 그렇게 기록한다.
  3. 앱 종료 → `artifacts\win-x64\Unfold.exe` 를 다른 폴더로 복사해 실행 →
     설정의 체크박스 상태와 Run 키 값을 비교 → 불일치면 (b) 확정.
- **레지스트리 값 존재와 실제 로그인 실행은 별개다.** 값 확인(1분)으로 증명되는 것은 쓰기 경로뿐이고,
  로그인 시 정상 실행은 **로그아웃 후 재로그인**으로만 증명된다(재부팅 불필요).
  재로그인 후 확인할 것: 트레이 아이콘과 펫만 나타나고 **설정 창은 뜨지 않아야 한다**(`--background`의 정의, `App.cs:25`).
- 조건: Windows 10/11, (a)는 개발 빌드, (b)는 게시 빌드, 모니터·배율 무관.
- 검증 상태: **미검증**. (a)는 `dotnet run`의 `ProcessPath` 실측이 필요한 가설이다.

### C-B4 — sprout의 8 px 줄기와 40 ms 폴링 클릭 통과가 충돌한다 (Garden 고유)

- **심각도: 높음 (출시 차단 후보, Garden 전용)**
- 코드 근거:
  - 폴링 주기: `PetWindow.cs:16` `Interval = TimeSpan.FromMilliseconds(40)`.
  - 판정·적용: `PetWindow.cs:121-130`. 커서 위치를 40 ms마다 읽어 `WS_EX_TRANSPARENT`(0x20)를 켜고 끈다.
  - 히트 관용치: `AnimationView.cs:67` `radius = Math.Max(1, (int)Math.Ceiling(2 * image.Width / rect.Width))`
    → 384 소스 px 프레임이 192 DIP 창에 맞춰지므로 **radius = 4 소스 px = 2 DIP**.
  - 알파 임계값: `AnimationView.cs:70` `>> 24 >= 26`.
- 측정 (`Sources/Unfold/Resources/Characters/sprout/spritesheet.png`, 3072×1536, 8 × 4 프레임, 프레임 384×384):

  | 항목 | 값 |
  |---|---|
  | idle 프레임 0의 불투명(α≥26) 픽셀 | 20,398 px = 프레임의 **13.83 %** |
  | 실루엣 바운딩 박스 | x 103–280, y 99–343 (178 × 245 소스 px) |
  | 가장 넓은 행 (y=241, 화분) | **178 소스 px** ≈ 89 DIP |
  | 가장 좁은 행 (y=192, 줄기) | **8 소스 px** ≈ **4 DIP** |
  | 줄기 + 관용치(±4 소스 px) 유효 폭 | 16 소스 px = **8 DIP** (100 % 배율에서 8 물리 px) |
  | 반투명(α 1–223) 픽셀 비율 | 전체 가시 픽셀의 약 3 % (플레이스홀더는 거의 하드 엣지) |

- 관찰 가능한 증상(예상): 커서를 옮겨 줄기를 바로 클릭하면, 창이 아직
  `WS_EX_TRANSPARENT` 상태라 클릭이 **아래 창으로 새어 나간다**(밑에 텍스트 편집기를 두면 캐럿이 찍힌다).
  반대로 줄기에서 막 벗어난 직후의 클릭은 펫이 삼킨다.
  화분(89 DIP 폭)에서는 같은 지연이 있어도 표적이 커서 거의 드러나지 않는다.
- 원인 가설: 상태 갱신이 이벤트가 아니라 40 ms 폴링이라 최대 40 ms의 지연이 있다.
  초당 1000 px로 움직이는 커서는 그 사이 약 40 px를 지나간다. 표적이 8 DIP이면 지연 구간이 표적보다 넓다.
  Cat(Mochi)은 큰 덩어리라 같은 지연에도 증상이 드러나지 않는다 — **그래서 Cat 기준 통과가 Garden에 전이되지 않는다.**
- 재현 절차(Windows 필요, 배율별 반복): 메모장을 전체 화면으로 띄우고 그 위에 펫을 놓는다.
  1. 커서를 화면 반대편에 두었다가 **한 번에 줄기(펫 상단 중앙의 가는 부분)로 이동해 즉시 클릭** × 20회.
     메모장에 캐럿이 찍힌 횟수를 센다.
  2. 커서를 줄기 위에 **1초 이상 정지시킨 뒤** 클릭 × 20회. 두 수치의 차이가 폴링 지연의 크기다.
  3. 화분으로 같은 절차 × 20회씩. 줄기와 화분의 실패율 차이를 기록한다.
  4. 배율 100 % / 125 % / 150 %에서 각각 반복(배율 변경 후 **앱 재시작**).
- 조건: Windows 10/11, 게시 빌드, 아래에 클릭이 보이는 앱, 배율 3종, 모니터 1대로 충분.
- 검증 상태: **미검증** (수치는 저장소 자산 측정, 클릭 동작은 미검증).
- 경계: 수치·임계값은 A 소유 코드(`AnimationView.OpaqueAt`)에서 나오고,
  폴링 주기와 `WS_EX_TRANSPARENT` 적용은 OS 어댑터다. 수정은 A/C 공동 티켓이어야 한다.

### C-B5 — 시작 실패가 화면에도 로그에도 남지 않는다

- **심각도: 중간~높음**
- 코드 근거: `Unfold.Desktop.csproj:3` `<OutputType>WinExe</OutputType>` → 콘솔 없음.
  `Program.cs:12-13`
  ```
  var root = AppPaths.DataRoot;
  Directory.CreateDirectory(root);
  ```
  는 `try` 바깥이고, `Program.cs:17-18`의 `catch (IOException)`은
  `UnauthorizedAccessException`을 **잡지 않는다**. 두 경우 모두 예외가 `Main`을 벗어난다.
  `AppPaths.Log`(`Program.cs:36-39`)는 그 시점에 호출되지 않으므로 `unfold.log`도 생기지 않는다.
- 관찰 가능한 증상: `Unfold.exe`를 더블클릭해도 아무 일도 일어나지 않는다.
  창도, 오류 대화 상자도, 로그 파일도 없다. 이는 로그 A절 마지막 항목
  (`%LOCALAPPDATA%\Unfold\unfold.log`에 치명적 오류 없음)을 **거짓 통과**시킨다 —
  로그가 비어 있는 이유가 "정상"과 "기록조차 못 함" 두 가지이기 때문이다.
- 재현 절차(Windows 필요, 표준 사용자 계정):
  ```powershell
  $env:UNFOLD_DATA_DIR = 'C:\Windows\System32\UnfoldTest'
  Start-Process -Wait -PassThru .\artifacts\win-x64\Unfold.exe | Select-Object ExitCode
  ```
  창이 뜨지 않고 `unfold.log`도 생기지 않으면 재현이다.
  (권한 없는 경로 대신, 데이터 폴더를 읽기 전용으로 만든 뒤 실행해도 같다.)
- 조건: Windows 10/11, 게시 빌드, 표준 사용자 권한. 모니터·배율 무관.
- 검증 상태: **미검증**.

### C-B6 — 배포 서명이 없다 (SmartScreen)

- **심각도: 중간 (배포 차단 후보이며, 이번 도그푸딩 세션의 차단 사유는 아니다)**
- 코드 근거: `Scripts/publish-desktop.ps1`에 서명 단계 없음.
  `.github/workflows/desktop.yml:26-38`도 게시 후 바로 업로드한다.
  `docs/cross-platform.md:13` "The local build is unsigned."
- 구분: `docs/windows-dogfooding-log.md:314-331`은 SmartScreen을 **SAFE TO DEFER**로 분류하고,
  그 근거로 "로컬 빌드는 Mark-of-the-Web가 없어 실제 다운로더가 보는 경고를 재현하지 못한다"고 적었다.
  그 판단은 **도그푸딩 세션 범위에서는 옳다.** 다만 CI 산출물(`Unfold-win-x64.zip`)은
  웹으로 내려받는 순간 MotW가 붙으므로, **출시 판정에서는 여전히 차단 요인이다.**
  이 두 가지를 같은 항목으로 처리하면 안 된다.
- 재현 절차(Windows 2대 또는 다운로드 경로 필요):
  GitHub Actions 산출물 zip을 브라우저로 내려받아 → 다른 Windows PC에서 압축 해제 → `Unfold.exe` 실행.
  "Windows에서 PC를 보호했습니다" 화면과 "추가 정보" 클릭 후의 게시자 표기를 캡처한다.
- 조건: Windows 10/11, **웹 경유로 전달된** 게시 zip, 다른 머신.
- 검증 상태: **미검증**. 지시대로 서명·설치기는 구현하지 않았다.

### C-B7 — `clickThrough` 캐시가 실제 창 스타일과 어긋날 수 있다

- **심각도: 중간**
- 코드 근거: `PetWindow.cs:19` `private bool ... clickThrough;`,
  `PetWindow.cs:126` `if (ignore == clickThrough) return;`,
  `PetWindow.cs:128-129` `GetWindowLong` → `SetWindowLong` 읽기-수정-쓰기.
  캐시는 **앱이 마지막으로 쓴 값**을 기억할 뿐, 창의 실제 ex-style을 다시 읽어 확인하지 않는다.
  `HidePet()`/`ShowPet()`(`PetWindow.cs:108-109`)은 타이머만 멈추고 켜며 캐시를 재동기화하지 않는다.
- 관찰 가능한 증상(가설): 커서가 투명 영역에 있는 상태(클릭 통과 켜짐)에서 트레이 Hide Pet → Show Pet 후,
  Avalonia가 ex-style을 다시 쓰면 캐시(true)와 실제(비트 없음)가 어긋나 조기 반환에 걸린다.
  커서가 다음에 불투명 픽셀로 들어갈 때까지 클릭 통과가 죽는다(펫 주변 사각형이 클릭을 삼킨다).
- 재현 절차(Windows 필요): 메모장 위에 펫을 놓고 커서를 펫 **바깥 투명 영역**에 둔 채
  트레이 → Hide Pet → Show Pet → 커서를 움직이지 않고 그 자리에서 클릭.
  메모장에 캐럿이 찍히지 않으면 재현이다. `Win+D` → 복귀, 잠금 화면 → 복귀로도 반복한다.
- 조건: Windows 10/11, 배율 무관, 모니터 1대로 충분.
- 검증 상태: **미검증** (가설).

### C-B8 — 게시 시 RID별 lock 파일이 없어 잠금 복원이 적용되지 않는다

- **심각도: 중간 (재현성·공급망)**
- 코드 근거:
  - `Directory.Build.props:8-9`
    ```
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <NuGetLockFilePath Condition="'$(RuntimeIdentifier)' != ''">$(MSBuildProjectDirectory)/obj/$(RuntimeIdentifier)/packages.lock.json</NuGetLockFilePath>
    ```
  - `.gitignore`에 `**/obj/`가 있어 `obj/win-x64/packages.lock.json`은 **저장소에 없다.**
  - 커밋된 것은 RID 없는 `src/Unfold.Desktop/packages.lock.json` 하나뿐이다.
  - CI(`.github/workflows/desktop.yml:24`)는 `dotnet restore Unfold.slnx --locked-mode`를 RID 없이 돌린 뒤,
    `desktop.yml:29`에서 `publish-desktop.ps1`을 부른다.
    `Scripts/publish-desktop.ps1:18-20`의 `dotnet publish -r $Runtime`에는 `--no-restore`도 `--locked-mode`도 없다.
- 결과: **검증에 쓰인 잠금 그래프와 실제로 배포되는 바이너리의 의존성 그래프가 같다는 보장이 없다.**
  RID 잠금 파일이 없으면 NuGet은 실패하지 않고 새로 만들기 때문에 CI는 초록으로 남는다(실제로 그렇다, 아래 CI 절 참조).
- 재현 절차(Windows 또는 macOS, D 실행): `publish-desktop.ps1` 수행 후
  `src/Unfold.Desktop/obj/win-x64/packages.lock.json`이 새로 생겼는지, RID 없는 lock과 패키지 집합이 다른지 비교한다.
- 검증 상태: **정적 확인**. 하드 실패는 아니다.

### C-B9 — CI는 Garden 커밋을 한 번도 빌드한 적이 없다

- **심각도: 중간**
- 사실: `git branch -r --contains 3098f20` → 비어 있음. `origin/experiment/garden-windows`는 **존재하지 않는다.**
  `gh run list --branch experiment/garden-windows` → **0건.**
- 결과: `dotnet build`가 Windows에서 통과하는지조차 Garden 커밋에서는 기록이 없다.
- 검증 상태: **CI 기록 확인** (부재를 확인).

### C-B10 — `app.manifest`에 DPI 인식과 긴 경로 선언이 없다

- **심각도: 낮음~중간**
- 코드 근거: `src/Unfold.Desktop/app.manifest` 전체 6줄. `trustInfo`(asInvoker)와
  `supportedOS {8e0f7a12-…}`(Windows 10/11)만 있고,
  `<dpiAware>` / `<dpiAwareness>` / `<longPathAware>`가 **없다.**
  매니페스트는 `Unfold.Desktop.csproj:5`로 실제 임베드되므로 파일이 무시되는 것은 아니다.
- 결과: DPI 인식 수준이 전적으로 Avalonia 런타임 호출에 달려 있다.
  `docs/windows-dogfooding-log.md:112-114`도 "앱이 자체 DPI 인식을 선언하지 않으며
  프레임워크 기본값에 의존하므로 실행 중 배율 변경 동작은 미검증"이라고 이미 적어 두었다.
  프로세스가 per-monitor 인식이 아니면 `GetCursorPos`(`PetWindow.cs:118`)가 돌려주는 좌표계와
  Avalonia의 `PointToClient` 좌표계가 어긋나 **클릭 통과 마스크가 통째로 어긋난다**(최대 2배).
- 재현 절차(Windows + 배율이 다른 모니터 2대 필요): 주 모니터 100 %, 보조 200 %로 설정 →
  펫을 보조 모니터로 옮김 → 그림 위와 그림 밖에서 각각 클릭 →
  히트 영역이 그림과 **어긋나 보이는 방향과 거리**를 기록(그림 오른쪽/아래로 밀리는지).
  이어서 실행 중 주 모니터 배율을 125 %로 바꾸고 재시작 없이 같은 확인을 반복한다.
- 검증 상태: **미검증**. 이것이 실기기 검증 1순위 항목이다.

### C-B11 — `win-arm64`는 지원한다고 선언되지만 한 번도 빌드된 적이 없다

- **심각도: 낮음**
- 코드 근거: `Scripts/publish-desktop.ps1:2` `ValidateSet('win-x64','win-arm64','osx-x64','osx-arm64')`,
  `README.md:29`가 사용자에게 안내한다. 그러나 `.github/workflows/desktop.yml:13-17` 매트릭스는
  `win-x64`와 `osx-arm64` 두 개뿐이다. `PublishReadyToRun=true`의 arm64 교차 컴파일은 검증된 적이 없다.
- 검증 상태: **CI 기록 확인** (부재).

### C-B12 — 알림 경로의 부수 효과 두 가지

- **심각도: 낮음**
- (a) 두 번째 트레이 아이콘: `NativeReminder.cs:29-30`은 Avalonia가 이미 띄운 트레이 아이콘
  (`AppRuntime.cs:215`)과 **별개로** `Id = 17` 아이콘을 추가하고 12초 뒤 지운다.
  알림 영역에 Unfold 아이콘이 잠깐 두 개가 된다(Windows 11에서는 새 아이콘이 기본적으로 오버플로에 숨는다).
- (b) 핸들러 누적: `NativeReminder.cs:35` `owner.Closed += (_, _) => Remove();`는 해제되지 않는다.
  현재는 알림 창이 매번 새로 만들어지므로(`AppRuntime.cs:195`) 실질 누수는 아니지만,
  같은 창에 재사용되면 누적된다.
- (c) `Timeout = 10000`(`NativeReminder.cs:30`)은 Vista 이후 `uTimeout`이 폐기되어 무시된다. 무해.
- 검증 상태: **정적 확인**.

---

## Cat 기준 검증 양식과 Garden 커밋의 범위 분리

`docs/windows-dogfooding-log.md`는 **빈 양식이다.** 체크박스 82개 전부가 비어 있고,
Session 블록(날짜·Windows 버전·모니터 수·배율·Focus Assist)도 전부 공란이며,
Timeline 표는 빈 행 하나, Session Summary는 제목만 있다.
양식 스스로 "빈 칸은 '미검증'이지 '아마 괜찮음'이 절대 아니다"(56-58행)라고 못 박았다.
**따라서 이 문서는 어떤 항목의 통과 증거도 아니다.**

### 게이트 위반의 확인

- 양식 52행: **"Garden work does not start until this session records zero BLOCKERs."**
- 양식 356행: "Garden work starts only when the BLOCKER list is empty."
- 양식 29행이 고정한 기준 커밋: `dc77ab9` (`release/mvp`). `git merge-base --is-ancestor dc77ab9 HEAD` → **참**.
  즉 Cat 기준 커밋은 현재 히스토리 안에 있고, Garden 커밋 `3098f20`은 그로부터 **코드 커밋 한 개 뒤**다
  (`dc77ab9` → `97f9a84`(문서) → `3098f20`(Garden)).
- BLOCKER 절(279-293행)은 비어 있다. 0건이 **기록된** 것이 아니라 **아무것도 기록되지 않았다.**
- 그럼에도 `3098f20`은 커밋되었다. 커밋 메시지는 이 상태를 스스로 인정한다:
  "Windows is NOT verified. … click-through, DPI scaling, notifications, launch at login,
  SmartScreen and code signing have never been executed."
- 동시에 같은 메시지가 **"Observation says the pot is easy and the thin stem is hard,
  and that the pot is where the hand goes"**라고 적는다.
  그 "관찰"은 K절(217-251행)이 수집하기로 한 것이고, K절은 비어 있다.
  **저장소에 그 관찰의 산출물이 없다.** 결론이 맞을 수는 있으나(측정상 화분 89 DIP / 줄기 4 DIP로 방향은 일치한다)
  근거 문서가 없으므로 "측정에 근거해 설계했다"는 주장은 현재 성립하지 않는다.

### 항목 분리표 (체크박스 82개 기준)

| 구분 | 개수 | 절 | 근거 |
|---|---|---|---|
| **① Garden에 그대로 재사용 가능** | **38** | A(10) B(8) F(7) I(7) J(6) | 해당 코드 경로를 `3098f20`이 건드리지 않았다. 캐릭터에 무관한 OS 경로다. |
| **② 문항은 유효하나 sprout 대상으로 다시 실행해야 함** | **30** | C(6) D(3) E(4) K(9) L(8) | 코드는 그대로지만 **실루엣과 클립 구성이 바뀌었다.** Cat 결과가 전이되지 않는다. |
| **③ 문항 자체가 낡아 수정 후 실행해야 함** | **14** | G(4) H(10) | Garden이 문구를 바꿨다. 양식의 기대값이 이미 틀렸다. |
| **④ Cat 양식에 아예 없는 신규 항목** | **+7** | — | 아래 목록 |

**① 재사용 가능(38)의 근거** — `git show 3098f20 --stat`이 건드린 파일은
`AppSettings.cs`, `AppRuntime.cs`, `NativeReminder.cs`, `PetWindow.cs`, `SettingsWindow.cs`,
`SmokeDiagnostics.cs`, sprout 자산, 생성 스크립트, `CoreTests.cs`뿐이다.
**`PlatformServices.cs`, `SingleInstance.cs`, `Program.cs`, `app.manifest`,
`Scripts/*.ps1`, `.github/workflows/desktop.yml`은 하나도 바뀌지 않았다.**
그래서 idle 감지(F), 로그인 실행(J), 중복 실행·종료(A), 창 플래그(B), Hide/Show(I)의 문항은 그대로 쓸 수 있다.
단 A절 "the pet appears"의 대상은 이제 Mochi가 아니라 sprout이고,
A절 "unfold.log에 치명적 오류 없음"은 C-B5 때문에 단독으로는 신뢰할 수 없다.

**② 재실행 필요(30)의 근거** — C·D·E·K는 전부 "클릭 가능 영역이 그림과 일치하는가"를 묻는다.
실루엣이 13.83 % 면적, 최소 폭 8 소스 px(4 DIP)로 바뀌었으므로
Cat에서의 통과는 sprout에 대해 아무것도 증명하지 않는다(C-B4 참조).
L(장시간)은 Garden이 초당 `IsNight` 비교(`AppRuntime.cs:97`)와
idle-day/idle-night/click 세 클립을 캐시에 추가했으므로 메모리·CPU 수치를 다시 재야 한다.
K는 "plant를 그리기 전에 측정한다"는 목적이 이미 소멸했으니,
**"sprout를 실제로 잡을 수 있는가"를 사후 측정하는 문항으로 다시 써야 한다.**

**③ 문항 수정 필요(14)의 구체적 불일치** —

| 양식 문구 | 현재 코드 | 파일:줄 |
|---|---|---|
| H절 "tray **Stretch now**" / "pet context menu **Stretch now**" | 메뉴 이름이 **"Break now"** | `PetWindow.cs:33`, `AppRuntime.cs:223` |
| H절 "**I'm refreshed** closes the reminder" | 버튼이 **"I'm back"** | `AppRuntime.cs:198` |
| G절 "a Windows notification appears" (Cat 문구 전제) | 제목 **"Time for a break"**, 본문 **"Look away for a moment."** | `NativeReminder.cs:30` |
| K절 "Mochi has no `click` clip, so a plain click does nothing" | **sprout에는 `click` 클립이 있다**(프레임 24–29, 18 fps) | `sprout/character.json` |
| K절 표 "Hit tolerance: about 4 source px ≈ 2 DIP" | **정확하다.** `radius = ceil(2×384/192) = 4` | `AnimationView.cs:67` |
| K절 표 "Source frame 384 × 384" | **정확하다.** | `sprout/character.json` |

**④ 신규 항목(+7)** — Cat 양식에 대응 문항이 없다.

1. 06:00 / 18:00 경계에서 idle 클립이 바뀌는가 (`AppRuntime.cs:21,24,97`). 시간대 변경·절전 복귀 포함.
2. 그림을 클릭했을 때 `click` 클립이 재생되고 idle로 돌아오는가 (`PetWindow.cs:94`).
3. 휴식 반응 중 클릭이 반응을 끊지 못하는가 (`reacting` 가드, `PetWindow.cs:91,97,104`).
4. **깨끗한 Windows 프로필의 첫 실행에서 기본 캐릭터가 sprout인가** (`AppSettings.cs:11`).
   기존 `settings.json`이 있으면 바뀌지 않아야 한다.
5. 게시 산출물에 `Assets\Characters\sprout\`(character.json + spritesheet.png)가 들어가는가.
   `Unfold.Desktop.csproj:14-19`의 글롭상 포함되어야 하지만,
   **로컬 `artifacts/osx-arm64/Assets/Characters`에는 `default-cat`밖에 없다** — 그 산출물이 sprout 이전 것이라는 뜻이고,
   **어느 OS에서도 sprout를 포함한 게시본이 존재한 적이 없다.**
6. 화분과 줄기의 잡기 성공률을 배율별로 분리 측정 (C-B4의 절차).
7. "Break now" 문구가 트레이·펫 메뉴·설정 세 곳에서 일치하는가.

---

## CI 실체

`gh run list`로 실제 실행 이력을 확인했다. **확인 가능했다.**

| run | 커밋 | 현재 히스토리 | Windows 잡 | macOS 잡 | 날짜 |
|---|---|---|---|---|---|
| 34574448121 | `dc77ab9` | **포함** | **success** | success | 2026-09-11 |
| 34574484767 | `dc77ab9` | **포함** | **success** | success | 2026-09-11 |
| 34571500140 | `8f416b9` | 포함 | success | success | 2026-09-11 |
| 34577861365 | `95f6198` | 미포함(Astra-mvp) | success | success | 2026-09-11 |
| 34678959967 | `6da89ee` | 미포함(release/mvp) | **cancelled** | **failure** | 2026-09-12 |
| `experiment/garden-windows` | `3098f20` | HEAD | **실행 없음** | 실행 없음 | — |

읽어야 할 결론 세 가지:

1. **Windows 잡의 실제 성공 기록은 있다 — 예.**
   `dc77ab9`, 즉 도그푸딩 양식이 고정한 바로 그 Cat 기준 커밋에서
   `build (windows-latest, win-x64)`가 성공했다. 그 잡은 `desktop.yml:24-29`에 따라
   `dotnet restore --locked-mode` + `dotnet test -c Release` + `publish-desktop.ps1 -Runtime win-x64`까지 수행했다.
   따라서 **"Windows에서 컴파일·단위 테스트·자체 포함 게시가 된다"는 증명된 사실이다.**
2. **그 성공은 앱 동작에 대해 아무것도 증명하지 않는다.**
   워크플로에는 게시본 실행이 없다. `docs/cross-platform.md:115`가 문서화한
   `./artifacts/win-x64/Unfold.exe --smoke-test`는 **CI에서 돌지 않는다.**
   창, 트레이, 클릭 통과, 알림, 로그인 실행은 CI 범위 밖이다.
3. **Garden 커밋에는 그 성공조차 없다.** `3098f20`은 푸시된 적이 없어 실행 이력이 0건이다.
   가장 최근 실행(`6da89ee`, 2026-09-12)은 macOS 실패 + Windows 취소(fail-fast)로 **빨간불**이다.
   그 커밋은 현재 히스토리에 없으므로 이 브랜치의 상태를 뜻하지는 않지만,
   워크플로가 지금 어딘가에서 깨져 있다는 신호다.

---

## 원인 가설과 추가 확인 방법

| 항목 | 가설 | 확인 방법 |
|---|---|---|
| C-B3(a) | `dotnet run`이 apphost를 실행하므로 `ProcessPath`가 `dotnet.exe`가 아니고, 가드가 무력화된다 | Windows에서 개발 빌드로 로그인 실행을 켠 뒤 Run 키 값을 읽는다. `bin\` 경로가 보이면 확정 |
| C-B7 | Avalonia가 Show/Hide에서 ex-style을 재작성해 캐시와 어긋난다 | 재현 절차 수행 + Spy++/`Get-WindowLong` 계열 도구로 실제 `WS_EX_TRANSPARENT` 비트 확인 |
| C-B10 | 프로세스 DPI 인식이 per-monitor가 아니면 `GetCursorPos`와 Avalonia 좌표계가 어긋난다 | 배율이 다른 두 모니터에서 히트 영역이 밀리는 방향·거리 측정. 밀림이 배율비에 비례하면 확정 |
| C-B1 | `e.Cancel = true`가 `WM_QUERYENDSESSION`에 FALSE로 전달된다 | 실제 로그아웃 시도. 차단 화면의 앱 이름 캡처 |
| C-B2 | 디스플레이 변경 이벤트를 구독하지 않아 재클램프가 없다 | 모니터 분리 후 Hide/Show → 복귀 실패 → Quit 후 재실행 → 복귀 성공이면 확정 |

---

## 검증

- 실행한 명령과 결과:
  - `git branch --show-current` → `experiment/garden-windows`
  - `git rev-parse HEAD` → `3098f2057bca28f7024aa2a9413556111b3dea09`
  - `git status --short` → `?? docs/team/` (1행)
  - `git merge-base --is-ancestor dc77ab9 HEAD` → 종료 코드 0 (**포함**)
  - `git merge-base --is-ancestor 95f6198 HEAD` → 종료 코드 1 (미포함)
  - `git branch -r --contains 3098f20` → 출력 없음 (원격에 없음)
  - `gh run list --branch experiment/garden-windows --limit 20` → **0건**
  - `gh run view <id> --json jobs` → 위 CI 표
  - sprout 스프라이트 알파 계측: Python 표준 라이브러리로 PNG를 직접 디코드(읽기 전용).
    3072×1536, 8비트, 컬러타입 6. 위 C-B4 측정표.
- **빌드·테스트·앱 실행: 미실행 (설계된 범위. D 전담).**
- 통과·실패·건너뜀 수: 해당 없음.
- 로그 위치: 없음(파일을 만들지 않았다).

---

## 다른 담당자에게 필요한 요청

### D (QA)에게 — 실행 요청

**지금 macOS에서 가능한 것** (Windows 동작의 증거로는 쓸 수 없음, 회귀·게시 경로 점검용):

1. `dotnet test Unfold.slnx -c Release` — Garden 커밋의 기준선. Windows CI 이력이 없으므로
   빌드 자체가 깨졌는지부터 확인이 필요하다.
2. `bash Scripts/make-macos-bundle.sh osx-arm64` 후
   **`artifacts/…/Assets/Characters/sprout/`에 `character.json`과 `spritesheet.png`가 있는지 확인.**
   현재 `artifacts/osx-arm64/Assets/Characters`에는 `default-cat`뿐이다.
   이것은 OS 무관한 **패키징 계약** 확인이므로 macOS에서 해도 유효하다(신규 항목 ⑤).
3. 전용 `UNFOLD_DATA_DIR`로 `--smoke-test` 1회 — `smoke.json`의 `characters` 수가 2인지 확인.
4. 가능하면 **`experiment/garden-windows`를 푸시해 CI를 한 번 돌린다.**
   Windows 잡이 초록이 되어야 C-B9가 닫힌다. 푸시 여부는 총괄 판단 사항이다.

**Windows 실기기가 필요한 것** (macOS로 대체 불가):

우선순위대로. 각 항목의 조건을 반드시 함께 기록한다 —
**Windows 버전/빌드, 게시 빌드 여부, 모니터 수, 모니터별 배율, Focus Assist 상태.**

| 순위 | 항목 | 조건 | 절차 |
|---|---|---|---|
| 1 | C-B10 DPI + C-B4 클릭 통과 | 게시 빌드, 배율 다른 모니터 2대, 100/125/150 % | C-B4·C-B10의 재현 절차. 클릭 성공률을 횟수로 센다 |
| 2 | C-B3 로그인 실행 | 개발 빌드 1회 + 게시 빌드 1회, **로그아웃 후 재로그인** | C-B3의 3단계. 레지스트리 값 확인과 재로그인 실행을 **별도 항목으로** 기록 |
| 3 | C-B1 로그오프 차단 | 게시 빌드 | 앱 실행 중 로그아웃 시도 |
| 4 | C-B2 모니터 분리 | 모니터 2대 | 드래그 → 분리 → Hide/Show → Quit/재실행 |
| 5 | C-B7 캐시 불일치 | 게시 빌드 | Hide/Show 후 제자리 클릭 |
| 6 | C-B5 조용한 시작 실패 | 게시 빌드, 표준 사용자 | 권한 없는 `UNFOLD_DATA_DIR`로 실행 |
| 7 | 알림 전달 | Focus Assist **끔**과 **켬** 각 1회 | `NativeReminder` 문구가 실제로 뜨는지. 트레이 아이콘이 2개가 되는지 |
| 8 | C-B6 SmartScreen | 웹 경유 zip, **다른 머신** | 다운로드 → 압축 해제 → 실행 |

**절차 정정 하나:** `docs/cross-platform.md:115`의
`./artifacts/win-x64/Unfold.exe --smoke-test`는 그대로 쓰면 안 된다.
`OutputType=WinExe`(`Unfold.Desktop.csproj:3`)이라 PowerShell이 **종료를 기다리지 않고 즉시 반환**하며
`$LASTEXITCODE`도 신뢰할 수 없다. 다음으로 바꿔 실행한다.

```powershell
$env:UNFOLD_DATA_DIR = Join-Path $PWD '.tools/smoke-win'
$p = Start-Process -Wait -PassThru .\artifacts\win-x64\Unfold.exe -ArgumentList '--smoke-test'
$p.ExitCode
Get-Content (Join-Path $env:UNFOLD_DATA_DIR 'verification/smoke.json')
```

### A (런타임)에게

- **공동 티켓 필요:** `PetWindow.UpdateClickThrough`(`PetWindow.cs:117-130`)는 OS 어댑터지만
  파일 소유자는 A다. C-B4·C-B7의 수정은 C가 사양을 쓰고 A가 편집하는 형태여야 한다.
  이번 라운드에서는 **아무것도 수정하지 않았다.**
- 확인 요청: `ClampPosition`(`PetWindow.cs:111-116`)이 `Opened`와 `PointerReleased`에서만 호출되는 것이
  의도인지. C-B2는 여기에 디스플레이 변경 시 재클램프가 없다는 사실에 걸려 있다.
  트레이에 "펫 위치 초기화"를 넣는 편이 나을지도 A/총괄 판단 사항이다.
- 정보 제공: `OpaqueAt`의 `radius`가 sprout 기준 **4 소스 px = 2 DIP**로 계산되며,
  줄기의 유효 잡기 폭은 8 DIP다. 이 값은 도그푸딩 양식 K절 표(229행)와 일치한다.

### B (UI·자산)에게

- 문구 불일치: 도그푸딩 양식이 기대하는 **"I'm refreshed"**와 실제 버튼 **"I'm back"**(`AppRuntime.cs:198`),
  **"Stretch now"**와 **"Break now"**가 어긋난다. 양식 수정은 문서 소유 문제라 총괄 조정 대상이다.
- `NativeReminder`의 알림 문구("Time for a break" / "Look away for a moment.", `NativeReminder.cs:30`)는
  C 소유 파일이지만 문구 제안은 B다. 인앱 휴식 창 문구(`AppRuntime.cs:197-198`)와
  OS 알림 문구가 지금 서로 다른데(전자 "A little room to breathe.", 후자 "Look away for a moment.") 의도인지 확인 바란다.
- 자산 요청: C-B4 측정상 화분(89 DIP 폭)은 잡기 쉽고 줄기(4 DIP)는 어렵다.
  실제 아트로 교체할 때 **불투명한 화분을 주 드래그 핸들로 유지**해야 한다.
  이는 커밋 메시지가 이미 주장한 설계 원칙이지만, 저장소에 그 근거 문서는 없다.

### 총괄에게

- `docs/windows-dogfooding-log.md`의 게이트가 충족되지 않은 채 Garden이 커밋된 사실(위 게이트 위반 절)에 대해
  판정이 필요하다. 선택지는 (a) 양식을 sprout 기준으로 개정해 한 번 실행, (b) Cat 기준으로 먼저 실행 후 Garden 재실행,
  (c) 게이트를 공식적으로 해제. **어느 쪽이든 문서에 기록되어야 하며, 문서 수정은 C 권한 밖이다.**
- `experiment/garden-windows` 푸시 여부(C-B9 해소를 위한 CI 1회 실행) 판단이 필요하다.

---

## 변경한 파일

`docs/team/reports/C-01-platform.md` (이 파일) **하나뿐이다.**
코드·설정·배포 파일·기존 문서는 열람만 했고 수정하지 않았다.
`git checkout` / `reset` / `commit` / `stash` / 브랜치 변경도 하지 않았다.

---

## 미검증 항목과 환경 제약

**환경 제약:** 이 세션은 macOS(darwin)에서 실행되었고 Windows 실기기가 없다.
따라서 **모든 OS 동작 항목은 예외 없이 `미검증`이다.**
macOS 테스트 통과나 게시 성공, 그리고 CI의 Windows 잡 성공은
Windows에서의 **클릭 통과·알림 전달·로그인 실행·DPI 좌표 일치**에 대한 증거가 아니다.
CI 성공이 증명하는 것은 컴파일·단위 테스트·게시 산출물 생성까지다.

미검증으로 남은 것:

- C-B1 ~ C-B7, C-B10, C-B11의 모든 OS 동작 (정적 확인 또는 가설 단계)
- 도그푸딩 양식 82개 체크박스 **전부**
- 게시 산출물에 sprout 자산이 실제로 포함되는지 (macOS 산출물도 sprout 이전 것이다)
- Garden 커밋이 Windows에서 컴파일되는지 (CI 이력 0건)
- `win-arm64` 게시가 성립하는지
- `dotnet run`에서 `Environment.ProcessPath`가 무엇인지 (C-B3(a)의 전제)

---

## 다음 작업 제안 (최대 3개)

1. **Windows 실기기 1회 세션으로 C-B10 + C-B4를 함께 측정한다.**
   DPI 좌표 일치와 클릭 통과는 같은 코드 경로(`GetCursorPos` → `PointToClient` → `OpaqueAt`)를 공유하므로
   따로 재는 것이 낭비다. 배율 3종 × (줄기·화분) × (즉시 클릭·정지 후 클릭) 20회씩이면
   폴링 지연과 좌표 어긋남을 동시에 분리해 낼 수 있다. 나머지 Windows 항목은 이 세션에 얹는다.

2. **도그푸딩 양식을 sprout 기준으로 개정한다(총괄 승인 후, 문서 소유자가 편집).**
   위 분리표대로 ① 38개는 그대로, ② 30개는 대상만 sprout로, ③ 14개는 문구 수정,
   ④ 7개는 신규 추가. 그리고 K절은 "그리기 전 측정"에서 "그린 것의 사후 검증"으로 목적을 바꾼다.
   개정 없이 그대로 실행하면 H·G절에서 존재하지 않는 문구를 찾게 된다.

3. **`experiment/garden-windows`를 푸시해 Windows CI를 한 번 통과시키고,
   워크플로에 게시본 `--smoke-test` 단계를 추가하는 안을 검토한다.**
   현재 CI는 "컴파일된다"까지만 말한다. `Start-Process -Wait`로 게시된 exe의 스모크를 돌리면
   시작 실패(C-B5)와 자산 누락(신규 항목 ⑤)을 사람 손 없이 잡을 수 있다.
   워크플로 파일은 총괄 조정 대상이므로 편집자 지정이 필요하다.
