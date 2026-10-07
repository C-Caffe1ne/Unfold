# 01 · 코어 로직 버그 감사 (A1 Code Reviewer)

## 요약

- 가장 위험한 결함은 `BreakHistory.Add`의 90일 정리 로직이다. 시스템 시계가 미래로 점프한 상태에서 휴식 1건을 완료하면 기존 전체 기록을 메모리에서 삭제하고 그 결과를 즉시 파일에 원자적으로 기록한다. 백업이 없어 복구 불가다.
- 저장 복구 설계가 비대칭이다. `settings.json`은 필드 단위로 복구하고 손상본을 백업하지만, `break-history.json`은 단일 레코드 손상에도 전체를 거부하고 백업·복구 경로가 없어 기록 기능이 영구적으로 비활성화된다. 사용자에게 보이는 안내 문구도 실제 상태(영구)와 다르다(세션 한정).
- 시작 경로에 두 개의 취약점이 있다. 설정 로드 catch 필터에 `ArgumentException`이 빠져 있고, 업데이트 하위 시스템 초기화 실패가 트레이 구축과 구매 게이트 플로우 전체를 중단시킨다.
- 펫 팩·캐릭터 라이브러리는 예상보다 견고하다. zip slip, 압축 폭탄, 링크·리파스 포인트, manifest 인벤토리 불일치, 설치 중 실패 롤백, 파일 락 기반 동시 설치 차단이 모두 구현·검증되어 있어 새 결함을 찾지 못했다.
- 타이머·세션 상태 기계는 monotonic `Stopwatch` 기반이라 DST·시간대·시계 변경에 영향받지 않는다. 오프라인 시 잠금은 `docs/account-screen.md:50`("오프라인 이용 권한은 제공하지 않는다")에 따른 의도된 계약이므로 버그로 보고하지 않았다.

## 범위·방법

- 대상: `codex/glb-import-compat` HEAD `1a74cbf` (1.1.0-beta)의 격리 복사본. 원본 워크트리는 읽기만 했고 파일 생성·수정·빌드·테스트를 하지 않았다.
- 읽은 코어: `AppSettings.cs`, `AppSettings.Recovery.cs`, `Personalization.cs`, `BreakHistory.cs`, `BreakReview.cs`, `BreakSession.cs`, `StretchClock.cs`, `PetReminder.cs`, `ReminderSounds.cs`, `ReminderSounds.Cleanup.cs`, `CharacterLibrary.cs`, `CharacterPack.cs`, `CharacterAssetAudit.cs`, `CustomPetDraft.cs`, `GlbPetDraft.cs`, `ImageCodec.cs`, `SupabaseAccountClient.cs`.
- 읽은 데스크톱: `AppRuntime.cs`, `AppRuntime.Account.cs`, `AppRuntime.Updates.cs`, `AppUpdates.cs`, `DesktopAccountService.cs`, `AccountSessionVault.cs`, `NativeAccountCredentialStore.cs`, `Program.cs`, `SettingsWindow.Preferences.cs`(저장·정리 구간), `BreakReviewWindow.cs`(CSV·탐색 구간), `GlbPetView.cs`(이벤트 구간).
- 계약 확인: `docs/stretch-notifications.md`, `docs/account-screen.md`, `docs/mvp.md`, `docs/personalization.md`, `docs/cross-platform.md`.
- 기존 테스트 경계 확인: `BreakSessionTests.cs`, `PersonalizationTests.cs`, `PurchaseGateTests.cs`, `BreakReviewTests.cs`, `SettingsRecoveryTests.cs` 목록·관련 구간.

## 근거 유형

- 모든 발견은 **정적 코드 분석**이다. 빌드·테스트·GUI 실행·computer-use를 하지 않았다.
- smoke 결과(`smoke-data-165424/verification/smoke.json` success=true, 캡처 약 100장)는 "정상 경로가 통과한다"는 배경 정보로만 사용했다. 아래 발견들은 모두 smoke가 밟지 않는 경계·실패 경로이므로 smoke 통과가 반증이 되지 않는다.

## 발견

| ID | 심각도 | 제목 | 근거 | 재현/실패 시나리오 | 확신도 | 수정 방향 | 소유 후보 | 기존 테스트 |
|---|---|---|---|---|---|---|---|---|
| BUG-C-01 | P0 | 시계가 미래로 점프하면 휴식 1건 완료가 전체 기록을 삭제·영구 저장 | `src/Unfold.Core/BreakHistory.cs:40`, `src/Unfold.Desktop/AppRuntime.cs:564-570` | RTC 소진·수동 날짜 오입력·NTP 글리치로 시스템 날짜가 90일 이상 앞으로 이동 → 사용자가 휴식 1건 완료 → `FinishBreak`가 `BreakHistory.Add(session, DateTimeOffset.Now)` 호출 → `completions.RemoveAll(p => p.CompletedAt < completedAt.AddDays(-90))`가 실제 기록 전부를 제거 → 같은 블록에서 `BreakHistory.Save(historyFile)`가 1건만 담긴 파일로 원자적 교체. 백업·휴지통 없음 | 확인됨 | 정리 기준을 `DateTimeOffset.Now` 단독이 아니라 `max(completedAt, 기존 최신 CompletedAt)` 기준으로 잡고, 새 `completedAt`이 기존 최신값보다 비정상적으로 미래(예: +1일 초과)면 정리를 건너뛰고 추가만 한다 | `src/Unfold.Core/BreakHistory.cs` | 없음. 90일 정리 자체를 덮는 테스트가 없다(`MaxRecords` 상한만 `PersonalizationTests.cs:104-105`에서 검증) |
| BUG-C-02 | P1 | 손상된 `break-history.json`은 기록 기능을 영구 비활성화하고, 백업·복구 경로가 없으며 안내 문구가 사실과 다름 | `src/Unfold.Desktop/AppRuntime.cs:110-115`, `src/Unfold.Desktop/AppRuntime.cs:564`, `src/Unfold.Core/BreakHistory.cs:18-33` | 어떤 이유로든 `break-history.json`이 읽히지 않으면 `historyWritable = false`로 고정되고 이번 세션은 물론 **이후 모든 실행**에서 같은 실패가 반복된다. 설정 경로(`AppRuntime.cs:97-106`)는 손상본을 `.invalid-<타임스탬프>`로 복사한 뒤 기본값으로 회복하지만, 기록 경로에는 그 대응이 없다. 사용자에게는 "새 휴식은 앱을 종료할 때까지만 보관돼요"(`AppRuntime.cs:114`)가 표시되어 일시적 문제로 읽히고, 파일을 직접 지우는 것 말고는 앱 안에서 복구할 방법이 없다 | 확인됨 | 설정과 같은 패턴을 적용한다. 손상본을 `break-history.json.invalid-<타임스탬프>`로 격리하고 빈 기록으로 쓰기 가능 상태를 회복하며, 안내 문구를 "이전 기록을 열지 못해 따로 보관했어요. 새 기록부터 다시 저장돼요"로 교체 | `src/Unfold.Desktop/AppRuntime.cs` | 부분. `BreakSessionTests.cs:94-98`이 `Load`의 예외 발생과 원본 파일 보존만 확인하고, 런타임의 영구 잠금·복구 부재는 덮지 않는다 |
| BUG-C-03 | P2 | 레코드 1건 손상이 90일 기록 전체를 버림(설정의 필드 단위 복구와 비대칭) | `src/Unfold.Core/BreakHistory.cs:23-31`, `src/Unfold.Core/BreakHistory.cs:64-71` 대비 `src/Unfold.Core/AppSettings.Recovery.cs:9-79` | 레코드 하나가 `Validate`를 못 넘기면(`Seconds` 범위 이탈, `RoutineName` 61자 이상, `SafeId`를 깨는 `RoutineId`, 중복 `SessionId`) `Load`가 `InvalidDataException`을 던져 나머지 1,999건까지 모두 버린다. 설정 쪽은 "A single damaged value must not discard the rest of the file"(`AppSettings.cs:55`)를 명시적으로 지키는데 기록 쪽은 반대 정책이다 | 확인됨 | `Load`에서 레코드 단위로 건너뛰고 `needsBackup` 같은 플래그를 올려 호출자가 손상본을 격리하게 한다. BUG-C-02와 함께 고치는 것이 자연스럽다 | `src/Unfold.Core/BreakHistory.cs` | 없음(부분 손상 입력 테스트 부재) |
| BUG-C-04 | P2 | 설정 로드 catch 필터에 `ArgumentException`이 빠져 시작 실패가 조용한 종료로 이어짐 | `src/Unfold.Desktop/AppRuntime.cs:92-96` vs `src/Unfold.Desktop/AppRuntime.cs:187`, `src/Unfold.Core/CharacterLibrary.cs:157`(`List`의 필터) | `AppSettings.Load`는 `RecoverPersonalization → ValidatePersonalization`(`Personalization.cs:19-49`)을 거치는데 이 경로의 실패 타입은 `ArgumentException`이다. 생성자 필터는 `IOException / UnauthorizedAccessException / JsonException / InvalidDataException`만 잡으므로 `ArgumentException`이 올라오면 `AppRuntime` 생성이 실패하고 `Program.cs:76-77`이 로그만 남기고 종료 코드 1로 끝난다. 사용자에게는 "클릭했는데 아무 일도 안 일어남"으로 보인다. 같은 코드베이스의 다른 두 곳은 `ArgumentException`을 필터에 포함하고 있어 의도된 누락으로 보이지 않는다 | 추정(현재 입력으로 `ValidatePersonalization`까지 도달하는 `ArgumentException` 경로를 특정하지 못했다. 복구 단계가 동일 검증을 먼저 수행해 걸러내는 구조다) | 생성자 필터에 `ArgumentException`을 추가하고, 더 낫게는 `AppSettings.Load`가 복구 불가 입력에 대해 `InvalidDataException`만 던지도록 타입을 좁힌다 | `src/Unfold.Desktop/AppRuntime.cs`, `src/Unfold.Core/AppSettings.cs` | 없음 |
| BUG-C-05 | P2 | 업데이트 하위 시스템 초기화 실패가 트레이 구축과 구매 게이트 플로우 전체를 중단 | `src/Unfold.Desktop/AppRuntime.Updates.cs:12,20`, `src/Unfold.Desktop/AppUpdates.cs:84-91`, `src/Unfold.Desktop/AppUpdates.cs:14-16,37-46` | `AppUpdates` 생성자는 `this.backend = backend ?? new VelopackUpdateBackend();`를 **try 블록 밖**에서 실행한다. `VelopackUpdateBackend` 생성자는 `AppUpdateConfiguration.Channel(...)`(미지원 런타임에서 `PlatformNotSupportedException`)과 `VelopackLocator.CreateDefaultForPlatform()`을 호출한다. 여기서 던지면 `Updates => updates ??= new()`가 전파하고 `InitializeUpdates` → `BuildTray()` → `Start()`가 중단된다. 그 결과 `AppRuntime.cs:154-161`의 `Clock.Stop` / `ShowAccount` / `RestoreAsync` 전체가 실행되지 않고, catch(`AppRuntime.cs:168-173`)는 오류 창만 띄운다. 타이머는 멈춘 채 트레이도 없는 상태가 된다 | 가능성 높음(정적) | `new VelopackUpdateBackend()`를 `AppUpdates` 생성자의 try 안으로 옮기고 실패 시 `UnsupportedInstall` 또는 `Error` 상태로 떨어뜨린다. 추가로 `InitializeUpdates`를 자체 try로 감싸 트레이 나머지 항목이 살아남게 한다 | `src/Unfold.Desktop/AppUpdates.cs`, `src/Unfold.Desktop/AppRuntime.Updates.cs` | 부분. `AppUpdateTests.cs`는 주입된 백엔드로 상태 전이를 덮지만 백엔드 **생성 실패**는 덮지 않는다 |
| BUG-C-06 | P2 | 손상된 설정이 재저장되지 않으면 `.invalid-*` 백업이 실행마다 무한 누적 | `src/Unfold.Desktop/AppRuntime.cs:97-106` | `backupSettings`가 참이면 매 시작마다 `settings.json`을 `settings.json.invalid-<yyyyMMddHHmmss>`로 복사한다. 복구된 값은 메모리에만 있고, 사용자가 설정 변경·테마 변경·펫 드래그(`SetTheme`/`UpdateSettings`/`SavePosition`) 중 하나도 하지 않으면 `settings.json`은 그대로 남는다. 즉 손상 상태가 유지되는 동안 실행 횟수만큼 최대 256 KiB 파일이 데이터 폴더에 쌓인다 | 확인됨 | 백업은 한 번만 만들고(동일 내용 해시 또는 기존 `.invalid-*` 존재 확인), 백업 직후 복구된 설정을 즉시 저장해 손상 상태를 끝낸다 | `src/Unfold.Desktop/AppRuntime.cs` | 없음 |
| BUG-C-07 | P3 | `unfold.log`가 무제한 증가하고 동시 기록 시 로그 라인이 조용히 유실 | `src/Unfold.Desktop/Program.cs:110-114` | `File.AppendAllText`는 크기 제한이 없다. `RecheckPurchaseAccess`(5분 주기 실패마다), `Library.Warning`(매 `Reload`에서 건너뛴 팩마다), GLB 디코드 실패 등이 반복 로그를 남긴다. 또한 UI 스레드와 `Task.Run` 백그라운드(예: `Reload`의 `Library.Warning`, `AccountSessionVault.Storage`)에서 동시에 호출되면 기본 공유 모드 때문에 `IOException`이 발생하고 같은 메서드의 catch가 이를 삼켜 로그 라인이 사라진다 | 확인됨 | 크기 상한과 1회 롤링(`unfold.log.1`)을 넣고, 쓰기를 전용 락으로 직렬화한다 | `src/Unfold.Desktop/Program.cs` | 없음 |
| BUG-C-08 | P3 | CSV 수식 주입 필터가 선두 TAB·CR을 다루지 않음 | `src/Unfold.Core/BreakReview.cs:25-31` | `Cell`은 `TrimStart()` 후 선두 문자가 `=+-@`일 때만 `'`를 붙인다. 스프레드시트는 선두 TAB(0x09)·CR(0x0D)로 시작하는 셀도 수식으로 해석한다. 루틴명·프로필명은 `Length > 60`만 제한하고 제어문자를 거르지 않으므로(`BreakSession.cs:10-13`, `Personalization.cs:8-10`) 사용자가 TAB으로 시작하는 루틴명을 만들 수 있다. 다만 `TrimStart()`가 TAB을 제거하므로 판정 대상이 2번째 문자가 되고, `\t=cmd()` 형태가 필터를 우회한다 | 확인됨 | 금지 선두 집합에 `\t`와 `\r`을 추가하고, `TrimStart` 전 원본 선두 문자도 함께 검사한다. 더 낫게는 이름 입력 단계에서 제어문자를 거부한다 | `src/Unfold.Core/BreakReview.cs` | 부분. `BreakReviewTests.cs`에 `=+-@` 케이스는 있으나 TAB·CR 케이스는 없다 |
| BUG-C-09 | P3 | 기록 리뷰에서 보존된 78~90일 구간을 볼 수 없음 | `src/Unfold.Desktop/BreakReviewWindow.cs:298-303`(`Navigate`), `src/Unfold.Core/BreakHistory.cs:40,52-62` | 저장소는 90일을 보존하는데 리뷰 탐색은 `today.AddDays(-77)`로 하한을 고정하고 한 화면이 7일이므로 최대 소급 구간이 84일이다. 85~90일 전 기록은 파일에 남아 있으나 UI로 도달할 수 없고 CSV 내보내기도 현재 화면 스냅샷 기준이다 | 확인됨 | 하한을 `today.AddDays(-83)`(7일 창 기준 90일 커버)로 맞추거나, 보존 기간과 탐색 한계를 하나의 상수에서 파생시킨다 | `src/Unfold.Desktop/BreakReviewWindow.cs`, `src/Unfold.Core/BreakHistory.cs` | 없음 |
| BUG-C-10 | P3 | 자리 비움 경계가 비대칭이라 임계값 정각의 1틱이 작업시간으로 집계 | `src/Unfold.Core/StretchClock.cs:41` vs `src/Unfold.Core/StretchClock.cs:44` | `IdlePaused = idleFor >= idleThreshold`(포함)인데 차감량은 `idleFor > idleThreshold`(배타)로 계산한다. `idleFor == idleThreshold`인 틱에서 트레이·말풍선은 "자리 비움"을 표시하면서 해당 틱의 경과 시간은 전액 작업시간으로 차감된다 | 확인됨 | 두 비교를 `>=`로 통일한다 | `src/Unfold.Core/StretchClock.cs` | 없음 |
| BUG-C-11 | P3 | `ScheduleNoticeExpiry`의 자기 재귀에 종료 보장이 없음 | `src/Unfold.Desktop/AppRuntime.cs:537-554`(특히 545-549) | 만료 시각이 이미 지났으면 `Reminder.Tick` 후 `RefreshPetNotice()`를 다시 호출하고, 이는 `ScheduleNoticeExpiry`로 돌아온다. 종료는 "`Reminder.Tick`이 반드시 `NoticeExpiresAt`을 바꾼다"에 의존한다. `PetReminder.Tick:45`의 자동 미루기가 `Snooze()` 실패로 상태를 바꾸지 못하면 같은 만료 시각이 유지되어 스택이 소진된다. 현재 `Snooze()`의 `Session is { State: Ready }` 전제를 깨는 경로는 찾지 못했다 | 추정(현재 도달 불가, 방어 결함) | 재귀 대신 `while` 루프로 만료를 소비하거나 반복 횟수 상한을 둔다 | `src/Unfold.Desktop/AppRuntime.cs`, `src/Unfold.Core/PetReminder.cs` | 없음 |
| BUG-C-12 | P3 | 펫 위치 저장이 실패해도 메모리 설정을 먼저 바꾸고, 접근 게이트를 확인하지 않음 | `src/Unfold.Desktop/AppRuntime.cs:373-378` | `SavePosition`은 `Settings = Settings with { PetX, PetY }`를 먼저 적용한 뒤 저장을 시도하고 실패를 로그로만 삼킨다. 디스크가 가득 차거나 권한이 없으면 메모리와 파일이 어긋난 채 다음 저장(`UpdateSettings` 등)에서 그 좌표가 함께 기록된다. 또 `AccessAllowed`를 확인하지 않는 유일한 설정 쓰기 경로다(`UpdateSettings:342`, `SetTheme:365`는 확인함) | 확인됨 | 저장 성공 후에 메모리 값을 교체하고, 다른 쓰기 경로와 같게 `AccessAllowed` 가드를 추가한다 | `src/Unfold.Desktop/AppRuntime.cs` | 없음 |

## 결함이 아님을 확인한 항목(중복 보고 방지용)

- **zip slip·압축 폭탄·링크**: `CharacterPack.Open`(`CharacterPack.cs:28-65`)이 플랫 ASCII 파일명만 허용하고(`ValidateFileName:155-163`, 폴더 구분자 전면 금지, Windows 예약명 차단), `ExternalAttributes`로 심볼릭 링크·디바이스·리파스 포인트를 거부하며, 엔트리별 32 MiB·총 64 MiB 상한과 선언 길이 대비 실제 바이트 일치 검사(`stream.ReadByte() != -1`)를 수행한다. SHA-256 인벤토리 일치, 디코딩 예산 128 MiB, 설치 중 실패 시 백업 롤백(`CharacterLibrary.cs:212-234`), `.library.lock` 기반 단일 writer까지 모두 구현되어 있다.
- **오프라인 시 잠금**: `AppRuntime.Account.cs:76-88`이 네트워크 실패(`AccountFailure.Unavailable`)에도 잠금을 거는 것은 `docs/account-screen.md:50`의 "오프라인 이용 권한은 제공하지 않는다"에 따른 의도된 계약이며 `PurchaseGateTests.cs:61-66`이 이를 덮는다.
- **휴식 무기한 보류**: 확인 대기 상태가 작업 타이머를 계속 보류하는 동작은 `docs/stretch-notifications.md`의 "+60:00에서 멈추며 자동 완료하지 않는다 / 진행 중인 휴식은 자동으로 닫지 않는다"와 일치한다.
- **DST·시간대·시계 변경**: 타이머·세션 경과는 `Stopwatch` monotonic 값만 사용하고(`AppRuntime.cs:55`, `StretchClock`, `BreakSession`) 10초 초과 간격을 활동으로 간주하지 않는다. 벽시계는 기록 타임스탬프와 게이트 재확인 주기에만 쓰인다. 기록 타임스탬프 쪽 위험은 BUG-C-01로 분리했다.
- **커스텀 펫 시트 폭 초과**: `CustomPetDraft.Export`(`CustomPetDraft.cs:85-93`)가 정지 이미지가 하나라도 있으면 idle GIF 프레임도 `DecodePetImage`로 512 px 이하로 재정규화하므로, 최대 8칸 × 512 px = 4096 px로 `LoadPackage`의 그리드 상한과 정확히 맞는다. 초과 경로를 찾지 못했다.
- **Stop 중 알림 열림 경합**: `reminderGeneration` 세대 비교와 `openingReminder` 플래그, `heldForBreak` 전달(`AppRuntime.cs:215,454,468`)로 취소 후 알림 부활이 차단된다.

## 미검증 항목과 이유

- 실제 런타임 재현을 하지 못했다. 전체 빌드·테스트·GUI 실행이 금지된 조건이라 모든 발견은 정적 분석이다. 특히 BUG-C-01은 시스템 시계를 조작해야 재현되므로 실기 확인이 필요하다.
- `GlbModel*.cs`, `GlbModel.Rendering.cs`, `PixelDocument.cs`, `PiskelCodec.cs`의 디코딩 내부는 토큰 예산 때문에 상위 호출 계약(크기·프레임 상한, 예외 타입)만 확인하고 알고리즘 수준 검토를 하지 않았다. GLB 관련 기존 항목(R-01, R-02)이 수정 완료로 기록되어 있어 우선순위를 낮췄다.
- `SmokeDiagnostics*.cs`(1,263줄)는 진단 전용 경로라 제품 결함 후보에서 제외했다.
- Windows 전용 네이티브 경로(`CredRead/CredWrite`, `CRED_PERSIST_LOCAL_MACHINE`)와 macOS Keychain 상호작용은 코드 레벨로만 읽었다. 실제 OS에서의 권한 거부·키체인 잠금 동작은 확인하지 못했다.
- 장시간 실행(로그 증가, 메모리, `clips` 캐시 수명)은 측정하지 않았다. BUG-C-07은 코드 구조에서 도출한 것이다.

## 다른 감사자에게 넘길 질문

**데스크톱 런타임 감사자에게**
1. BUG-C-05와 관련해, Velopack이 관리하지 않는 설치본(압축 해제 실행, 개발 빌드)에서 `VelopackLocator.CreateDefaultForPlatform()` 또는 `UpdateManager` 생성자가 실제로 던지는지 실행으로 확인해 줄 수 있는가. 던진다면 심각도를 P1로 올려야 한다.
2. `break-history.json`에 대해 `AtomicFile.Write`의 `File.Move(temp, path, true)`가 Windows에서 전원 손실 시 원자성을 보장하는지. BUG-C-02의 발생 빈도 추정에 필요하다.
3. 백그라운드 스레드(`Reload`의 `Task.Run` 안 `Library.Warning` → `AppPaths.Log`)에서 올라오는 `Warning` 이벤트가 UI 스레드 외부에서 UI를 건드리는 구독자를 갖고 있지 않은지 전체 구독 지점을 교차 확인해 줄 수 있는가.

**UX 흐름 감사자에게**
4. BUG-C-02의 안내 문구("새 휴식은 앱을 종료할 때까지만 보관돼요")는 실제로 영구 상태인데 일시적으로 읽힌다. 사용자가 밟을 수 있는 복구 행동을 문구에 담을지, 아니면 자동 격리 후 조용히 회복할지에 대한 UX 판단이 필요하다.
5. BUG-C-09의 78~90일 구간 접근 불가가 제품상 의도인지(리뷰는 12주로 제한, 저장은 90일) 확인이 필요하다. 의도라면 보존 기간을 84일로 낮추는 것이 일관적이다.

**접근성 감사자에게**
6. 설정 화면의 기록 오류 표시(`SettingsWindow.cs:229-230`, `SettingsWindow.Layout.cs:235`)가 스크린 리더에 상태 변경으로 전달되는지. BUG-C-02의 문구 수정과 함께 처리하는 것이 효율적이다.
