# 릴리스 QA 감사 — 2026-09-20

## 요약

**Release 테스트: 통과 215 / 실패 0 / 건너뜀 0 (전체 215, 종료 코드 0). `dotnet restore --locked-mode` 통과. Release 빌드 경고 0·오류 0.**
`docs/verification.md`가 가리키는 최신 검증 문서(`docs/validation/2026-09-20-pet-scale-timer-debug-implementation.md`, `2026-09-20-ui-copy-cleanup.md`)의 "215 통과" 주장과 **정확히 일치**한다. 단, 감사 도중 다른 작업자가 `Tests/Unfold.Tests/BreakReminderTests.cs`를 실시간으로 수정하면서 일시적으로 컴파일 에러(CS0104, `Path` 모호성)가 발생한 순간을 관측했다 — 재확인 시점에는 해소되어 최종 215/215가 재현됐다. 저장소가 감사 중에도 계속 변경되는 상태였다는 점을 출시 판정에 반영해야 한다.

패키징 산출물은 **v0.2.0**까지만 존재하고 SHA256이 실제 파일과 일치하지만, Git 커밋 이력은 이미 v0.2.1 라벨의 커밋 2개(`5e91545`, `0b72a36`)를 포함하며 `Unfold.Desktop.csproj`의 `<Version>`은 여전히 `0.2.0`으로 남아 있다 — 버전 표기 불일치. Windows 실기 검증은 `docs/windows-dogfooding-log.md`가 **전면 미기입 템플릿**이라 0%.

## 환경과 범위

- 저장소: `/Users/hwanghyeonseong/Documents/GitHub/Unfold`, 브랜치 `release/mvp`, 시작 시점 HEAD `0b72a36` + 미커밋 변경 37개(테스트 11개 수정, `ThemeTests.cs` 신규 포함, 소스 12개 수정, 문서 다수 수정, `docs/validation/2026-09-20-*` 다수 신규)
- 실행 호스트: macOS(arm64), .NET SDK `10.0.401`
- 격리: 진단용 `UNFOLD_DATA_DIR`는 세션 scratchpad 하위 `.../scratchpad/qa-run/data`(새 빈 디렉터리, 실제 사용자 프로필 미접촉). 빌드/테스트 자체는 저장소 내 `bin`/`obj`를 사용(제품 코드·문서는 수정하지 않음).
- 이 세션이 수정한 파일: **보고서 파일 하나**(`.claude/skills/unfold-orchestrator/_workspace/42_release_qa.md`)뿐. git 상태 변경 없음(`git status --porcelain` 세션 종료 시 71줄, 시작 시와 동일 — 이 보고서 파일은 `.claude/` 아래라 git 추적 대상 외일 수 있음, 별도 확인 안 함).

## 실행한 명령과 결과

### 1) restore (lock 모드)
```
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet restore Unfold.slnx --locked-mode
```
→ 종료 코드 0. `Unfold.Desktop.csproj`, `Unfold.Core.csproj`, `Unfold.Tests.csproj` 각 900ms 내외로 복원. 실패 없음.

### 2) Release 테스트 (1차, 백그라운드)
```
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 UNFOLD_DATA_DIR=<격리 경로> \
  dotnet test Unfold.slnx -c Release --no-restore
```
→ 종료 코드 0.
```
통과!  - 실패: 0, 통과: 215, 건너뜀: 0, 전체: 215, 기간: 15 s - Unfold.Tests.dll (net10.0)
```

### 3) Release 빌드(경고 수집 목적, `-v:normal`)
```
dotnet build Unfold.slnx -c Release --no-restore -v:normal
```
→ **실패**. 유일한 오류:
```
Tests/Unfold.Tests/BreakReminderTests.cs(93,48): error CS0104:
'Path'은(는) 'Avalonia.Controls.Shapes.Path' 및 'System.IO.Path' 사이에 모호한 참조입니다.
[/Users/hwanghyeonseong/Documents/GitHub/Unfold/Tests/Unfold.Tests/Unfold.Tests.csproj]
```
`stat`으로 확인한 파일 mtime이 `18:30:39`로, 이 명령 실행 시각(`18:30:47`)과 8초 차이 — 다른 작업자가 실행 도중 해당 파일을 편집한 것으로 판단(우리는 소스·테스트 파일을 건드리지 않음).

### 4) Release 빌드 재확인(`-v:minimal`, 약 1분 후)
```
dotnet build Unfold.slnx -c Release --no-restore -v:minimal
```
→ 종료 코드 0.
```
빌드했습니다.
    경고 0개
    오류 0개
경과 시간: 00:00:01.37
```

### 5) Release 테스트 재확인(`--no-build`, 최종)
```
dotnet test Unfold.slnx -c Release --no-restore --no-build
```
→ 종료 코드 0.
```
통과!  - 실패: 0, 통과: 215, 건너뜀: 0, 전체: 215, 기간: 13 s - Unfold.Tests.dll (net10.0)
```

**해석:** 3)의 컴파일 에러는 실제 제품/테스트 코드의 결함이 아니라, 감사 도중 동시에 다른 작업자가 저장소를 편집한 **경쟁 상태(race)** 였다. 4), 5)에서 안정된 상태로 재현해 최종 215/215·경고 0·오류 0을 확정했다. 1)의 첫 통과(2)도 같은 215/215였으므로, 이 CS0104는 편집이 들어왔다가 곧바로 수정된 짧은 창(약 1분 이내)에서만 존재했다.

### 6) SHA256SUMS 정적 검증(퍼블리시 미실행)
```
shasum -a 256 artifacts/Unfold-v0.2.0-osx-arm64.{zip,tar.gz} \
  artifacts/Unfold-v0.2.0-osx-x64.{zip,tar.gz} artifacts/Unfold-v0.2.0-win-x64.zip
```
→ `artifacts/SHA256SUMS-v0.2.0.txt`의 5개 해시와 **전부 일치**.

## 경계면 교차 검증

- **테스트 파일 신규 등록 ↔ 실행:** 미커밋 신규 파일 `Tests/Unfold.Tests/ThemeTests.cs`에 `[Fact]` 2개 확인. `xunit.runner.json`/프로젝트 파일 변경 없이 `.csproj`의 글롭 포함으로 자동 인식되어 215개 총합에 실제로 포함되어 실행됨(빌드 산출물 `Unfold.Tests.dll`이 정상 로드·통과).
- **상태 생산자 ↔ 소비자(Theme):** `src/Unfold.Core/AppSettings.cs:7`에 `Theme` 필드(기본값 `AppTheme.OatLatte`)와 `:34`, `:63`의 유효성 검사(`Enum.IsDefined`) 확인 — 저장/복구 시 잘못된 enum 값을 기본값으로 되돌리거나(로드) 예외를 던짐(저장). `ThemeTests.cs`가 이 계약을 대상으로 하는 headless UI 테스트이며 전체 스위트에 포함되어 통과했으므로, 생산자(AppSettings)·소비자(UI) 경계는 자동 검사 수준에서 정합성 확인됨.
- **문서(`docs/verification.md`) ↔ 실제 최신 검증 기록:** `docs/verification.md` 5~11행이 가리키는 최신 문서 2건(`2026-09-20-pet-scale-timer-debug-implementation.md`, `2026-09-20-ui-copy-cleanup.md`)이 모두 "215 통과"를 주장 — 오늘 실행한 실측치와 일치. 같은 날짜의 더 이른 단계 문서들(`home-ui-implementation.md` 207, `speech-ui-implementation.md` 212, `review-ui-implementation.md` 208, `themes.md` 212)은 개발이 진행되며 테스트가 순차적으로 늘어난 스냅샷으로 판단되며, 서로 모순되지 않음(시간 순으로 207→208→212→212→215 증가).
- **패키징 산출물 ↔ 소스 버전 표기:** `src/Unfold.Desktop/Unfold.Desktop.csproj:6`의 `<Version>0.2.0</Version>`은 `artifacts/`의 v0.2.0 산출물과 일치하나, `git log`상 이미 v0.2.1 라벨 커밋 2건(`5e91545` 9/17, `0b72a36` 9/18)이 존재하고 csproj Version은 그 이후로 갱신되지 않음(`git log -p`로 Version 필드 변경이 `0.1.1→0.2.0`(7e81f77) 이후 없음을 확인). `artifacts/`에는 v0.2.1 산출물·SHA256SUMS가 없음.

## 자동 검사로 확인한 사실

- `dotnet restore --locked-mode` 통과, 종료 코드 0.
- `dotnet test -c Release` 최종·재현 결과 215 통과/0 실패/0 건너뜀, 종료 코드 0(2회 독립 실행으로 재현).
- `dotnet build -c Release` 최종 상태 경고 0개·오류 0개.
- `Tests/Unfold.Tests/ThemeTests.cs`(미커밋 신규 파일)가 215개 총합에 실제로 포함되어 실행됨.
- `artifacts/SHA256SUMS-v0.2.0.txt`의 5개 해시가 실제 파일과 전부 일치.
- `docs/windows-dogfooding-log.md`는 실행 환경 필드·체크리스트 전부 미기입 템플릿(자체 명시: "아직 실행 결과가 기입되지 않았다").
- `docs/validation/2026-09-11-macos-dogfooding.md`는 39개 체크 항목 중 22개 체크, 17개 미체크 — 부분 관찰.
- 감사 도중 `Tests/Unfold.Tests/BreakReminderTests.cs`에 대한 동시 편집으로 인한 약 1분 내 일시적 컴파일 에러(CS0104)를 관측 — 최종적으로는 해소됨.

## 실제 macOS 확인

- 이번 세션에서는 **패키징된 앱을 실제로 실행하지 않았다**(Task 지시에 따라 `dotnet publish`·`--smoke-test` 진단을 생략했음 — "퍼블리시는 실행하지 마라"). 따라서 이번 QA 라운드에서 macOS 실기(UI 조작, 트레이, 펫 표시 등) 관찰 사실은 없음. 기존 기록은 `docs/validation/2026-09-11-macos-dogfooding.md`(부분)만 존재.

## 실제 Windows 확인

- 없음. `docs/windows-dogfooding-log.md`는 완전 미기입 상태이며, 이번 세션도 Windows 환경에 접근하지 않았다.

## 미검증

- Release 산출물 재퍼블리시(v0.2.0 이후 커밋·미커밋 변경 반영한 새 빌드)는 Task 지시에 따라 미실행 — 현재 HEAD+미커밋 변경 기준의 실제 패키지는 존재하지 않는다.
- `--smoke-test` 격리 진단(Settings/펫 off-screen 캡처, 프로필 저장 등)은 이번 라운드에서 미실행(직전 구현 라운드의 `docs/validation/2026-09-20-*.json`에 기록된 결과가 있으나, 오늘 QA 세션이 독자적으로 재실행하지 않음).
- macOS/Windows 실기 UI·알림·idle/sleep·다중 모니터·클릭 통과·로그인 실행·서명/공증 상태는 전부 미검증(자동 검사로 대체 불가).
- 커버리지(coverage) 수치, 성능/자원 사용 측정은 미실행.

## 문서 주장과 실제 결과의 불일치 목록

| 항목 | 문서 주장 | 실측 | 판정 |
|---|---|---|---|
| 최신 Release 테스트 총수 | `docs/validation/2026-09-20-pet-scale-timer-debug-implementation.md`, `2026-09-20-ui-copy-cleanup.md`: 215 통과 | 215 통과/0 실패/0 건너뜀 | **일치** |
| 패키지 버전 vs 커밋 라벨 | `artifacts/`·csproj 모두 v0.2.0 | Git 커밋 이력은 이미 v0.2.1 라벨 2건 진행 중, v0.2.1 산출물·SHA256SUMS 없음, csproj Version 미갱신 | **불일치** — 출시 전 버전 정합성 정리 필요 |
| Windows 실기 검증 | `docs/verification.md`가 `windows-dogfooding-log.md`를 실기 체크리스트로 안내 | 해당 문서는 전면 미기입 템플릿 | **문서가 이미 "미검증"임을 명시**하므로 모순은 아니나, 출시 차단 요소로 재확인 필요 |
| 감사 중 컴파일 안정성 | 문서들은 빌드를 항상 "성공, 경고 0·오류 0"으로 기록 | 감사 중 약 1분간 CS0104로 컴파일 실패 순간을 실측(동시 편집 때문, 최종적으로 해소) | 문서 오류 아님 — 저장소가 QA 시점에 정적이지 않았다는 환경적 사실 |

## 출시 차단 요소

- **QA-1 (P0)**: Windows 실기 검증이 전무하다(`docs/windows-dogfooding-log.md` 전면 미기입). `docs/release-checklist.md`의 OS 매트릭스도 Windows ARM64·macOS Intel은 CI 미커버·수동 필요로 명시. 실제 Windows 배포 전 이 체크리스트 전체를 실기로 완료해야 한다.
- **QA-2 (P1)**: 소스 버전(`csproj` 0.2.0)과 커밋 이력(v0.2.1 라벨 2건)이 어긋나 있고, `artifacts/`에 v0.2.1 산출물이 없다. 이 상태로 출시하면 "v0.2.1" 커밋 이후 변경분(예: 펫 크기 슬라이더, 테마, 말풍선 UI 변경 등 미커밋분 다수 포함)이 실제 배포판에 반영됐는지 추적 불가능하다. 출시 전 버전 번호 정책을 확정하고 산출물을 재생성해야 한다.
- **QA-3 (P2)**: 저장소가 여러 워커에 의해 동시 편집되는 환경에서 QA를 수행했다. 이번엔 우연히 안정 상태에서 최종 확인했지만, 병렬 편집 중에 캡처한 스냅샷은 재현성이 보장되지 않는다. 출시 직전 "동결(freeze)" 시점을 정하고 그 시점의 단일 커밋에서 최종 QA를 재실행하는 절차가 필요하다.

## 다음 검증 순서

1. 미커밋 변경 37개를 커밋/정리하고 버전 정책(csproj `<Version>`, 커밋 라벨, `artifacts/` 파일명)을 통일한다.
2. 동결된 단일 커밋 기준으로 `dotnet publish`(macOS arm64/x64, Windows x64) 재실행 후 `SHA256SUMS` 재생성.
3. `docs/release-checklist.md`의 "Fresh profile / Invalid settings / Timer idle-sleep / Pet show-hide / Launch at login / Notifications" 6개 절을 실제 macOS에서 순서대로 실기 확인.
4. `docs/windows-dogfooding-log.md` 체크리스트를 실제 Windows 하드웨어/VM에서 처음부터 끝까지 채운다(현재 0%).
5. 위 4단계가 끝난 뒤에만 최종 Release 테스트를 한 번 더 실행해 215(또는 그 시점의 정확한 수)로 재확인하고 문서에 반영한다.
