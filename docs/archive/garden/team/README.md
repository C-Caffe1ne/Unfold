> **보관 문서 · Garden 실험 — experiment/garden-windows / 3098f20**
> 현재 MVP의 작업 지침이 아닙니다. 원문의 명령, 담당 배정, 경로와 검증 결과는 당시 기록입니다.
> 원래 위치: `docs/team/README.md` · [현재 문서 안내](../../../README.md)

# Unfold 멀티에이전트 운영 계획

작성일: 2026-09-13. 총괄: Codex. 실행 담당: Claude Code 4개 세션.

실행 기록: 사용자 후속 요청으로 A~D 세션을 시작했다. 접속 ID와 확인 방법은 [실행 세션 기록](SESSIONS.md)에 있다. 아래의 미실행 설명은 최초 역할 배정 시점의 기록이다.

## 현재 기준과 첫 목표

- 기준 브랜치: `experiment/garden-windows`
- 조사 기준 커밋: `3098f2057bca28f7024aa2a9413556111b3dea09`
- 실행 본체: `src/Unfold.Core`, `src/Unfold.Desktop`의 C#/.NET 10·Avalonia.
- Swift 구현은 보존된 코드다. 단, `Sources/Unfold/Resources/Characters/`는 C# 빌드에서도 사용하는 실제 자산이다.
- 최근 변경은 기존 데스크톱 동료에 `sprout` 기본값, 낮·밤 idle, 클릭 반응, 휴식 문구를 추가한 정원 프로토타입이다.

별도의 기능 목표가 지정되기 전까지 **현재 정원 브랜치의 동작과 검증 공백 파악**을 첫 목표로 삼는다. 역할을 배정하는 이번 작업에서 제품 코드를 수정하거나 실제 Claude Code 세션을 실행하지 않았다. 아래 파일은 각 세션에서 실행할 작업 지시서다.

`docs/mvp.md`의 Cat MVP 설명과 현재 Garden 변경을 구분한다. `docs/windows-dogfooding-log.md`는 Cat 기준의 빈 검증 양식이며, Windows 검증 완료의 증거가 아니다. 해당 문서의 “Garden work does not start until this session records zero BLOCKERs” 조건이 충족되었다는 저장소 증거도 아직 확인되지 않았다. 이미 존재하는 프로토타입은 점검하되, 새 기능 착수 여부는 총괄이 사용자 목표와 검증 결과를 함께 보고 결정한다.

## 역할과 소유권

아래 제품 파일 소유권은 후속 수정 티켓을 발행할 때 적용한다. **첫 배정은 모두 조사·검증이며 코드 수정은 포함하지 않는다.**

| 담당 | 책임 | 제품 파일 소유권 | 첫 작업 |
|---|---|---|---|
| Codex | 목표·우선순위·작업 배정·통합 판정 | 이 운영 계획, 공통 계약과 문서 | 네 보고서를 대조하여 수정할 문제와 순서를 확정 |
| A — 핵심 동작 | 타이머, 설정, 캐릭터 전환과 반응 상태 | `AppSettings.cs`, `StretchClock.cs`, `CharacterLibrary.cs`, `AppRuntime.cs`, `PetWindow.cs`, `AnimationView.cs` | [A-01: 상태 전이와 생명주기 감사](01-runtime.md) |
| B — UI·자산 | 설정 화면, 표시 일관성, 식물 자산 | `SettingsWindow.cs`, `Ui.cs`, `sprout/`, `Scripts/generate-plant-placeholder.py` | [B-01: 사용자 경험과 자산 계약 점검](02-presentation.md) |
| C — Windows·배포 | OS 연동, 앱 실행·종료, 패키징 | `PlatformServices.cs`, `NativeReminder.cs`, `SingleInstance.cs`, `Program.cs`, Windows 실행·배포 스크립트와 앱 매니페스트 | [C-01: Windows 경로와 배포 감사](03-platform.md) |
| D — QA | 독립 검증, 회귀 확인, 증거 기록 | 기존 C# 테스트, `SmokeDiagnostics.cs`, 이번 작업의 검증 보고서 | [D-01: 자동 검증 기준선과 Windows 미검증 목록](04-qa.md) |

`AppRuntime`, `PetWindow`, `AnimationView`는 캐릭터 로딩·반응·idle 복귀가 연결되어 있으므로 A 한 명이 맡는다. B가 이 파일들의 UI 문제를 발견하면 A에게 변경 요청을 전달한다. `NativeReminder.cs`는 OS 알림 어댑터이므로 C가 맡고, 문구 변경 요청은 B가 제안한다.

별도 `ReminderWindow.cs`는 없다. 앱 내 휴식 창은 `AppRuntime.ShowReminder()`에서 만들기 때문에 B가 배치·문구를 제안하고 A가 적용한다. `Ui.Bitmap`의 반환·수명 계약과 sprout의 `idle`, `idle-day`, `idle-night`, `stretch`, `click` 키는 A/B가 함께 사용하는 계약이다.

`*.csproj`, `packages.lock.json`, `Directory.Build.props`, `NuGet.Config`, `Unfold.slnx`, `.github/workflows/**`, `App.cs`와 기존 공통 문서는 총괄 조정 대상이다. 필요하면 한 티켓에 한 명만 편집자로 지정한다. 표에 없는 파일의 수정 권한을 추정하지 않는다.

## 공통 실행 규칙

1. 세션 시작 시 `git branch --show-current`, `git rev-parse HEAD`, `git status --short`를 보고한다. 기준이 다르면 조사한 실제 커밋을 명시하고, 임의 checkout/reset은 하지 않는다.
2. 이 문서와 자기 지시서, 관련 실제 파일을 읽는다. 오래된 계획이나 다른 에이전트의 보고만으로 현재 구현을 단정하지 않는다.
3. 같은 저장소에서 다른 에이전트가 작업 중이다. 다른 사람의 변경을 되돌리거나 덮어쓰지 않는다.
4. 첫 라운드에서 쓰는 파일은 자기 보고서 하나뿐이다. D는 테스트가 생성하는 빌드·검증 산출물도 만들 수 있다. A/B/C는 보고서에 정적 분석인지 실제 실행 결과인지 표시한다.
5. 모든 보고서는 `docs/team/reports/` 아래 자기 파일에만 기록한다. 공유 환경이면 D만 전체 빌드·테스트·앱 실행을 맡아 빌드 디렉터리와 테스트 프로필의 충돌을 피한다.
6. 버그는 관찰 사실 → 원인 가설 → 재현 또는 검증 방법 순서로 보고한다. 조사만 한 문제를 재현 완료라고 쓰지 않는다.
7. 기술 스택 교체, 픽셀 에디터 재노출·개선, 성장·보상·상점 등 새 기능은 이번 배정에 없다. 발견한 아이디어는 후속 후보로만 남긴다.
8. 실제 사용자 데이터는 테스트에 사용하지 않는다. UI 실행에는 해당 세션 전용 `UNFOLD_DATA_DIR`를 사용한다.
9. 한 작업이 끝나면 결과를 총괄에게 전달한다. 다음 기능이나 다른 에이전트의 수정 작업을 스스로 시작하지 않는다.

## 보고 형식

각 보고서는 다음 항목을 포함한다. 해당 사항이 없으면 없음 또는 미실행이라고 쓴다.

```text
작업 ID / 담당:
OS / 브랜치 / 기준 커밋 / 시작 시 변경 상태:
확인한 현재 동작:
발견 사항: 심각도, 관찰 사실, 파일:줄, 재현 여부, 영향
원인 가설과 추가 확인 방법:
검증: 정확한 명령, 종료 코드, 통과·실패·건너뜀 수, 로그 위치
다른 담당자에게 필요한 요청: 파일, 이유, 변경할 계약
변경한 파일:
미검증 항목과 환경 제약:
다음 작업 제안: 가장 중요한 것 최대 3개
```

검증 상태는 `정적 확인`, `자동 검증 통과/실패`, `실제 OS 검증 통과/실패`, `미검증`으로 구분한다. Windows 게시 성공이나 macOS 테스트 성공을 Windows 클릭 통과·알림·로그인 실행의 증거로 사용하지 않는다.

## 작업 순서와 통합

1. **첫 라운드:** A-01, B-01, C-01, D-01을 병렬로 수행한다. 결과물은 각각 `A-01-runtime.md`, `B-01-presentation.md`, `C-01-platform.md`, `D-01-qa.md`다.
2. **총괄 판정:** 보고서의 근거를 실제 파일·diff·로그와 대조한다. 출시를 막는 문제, 핵심 경험 문제, 새 아이디어를 구분하고 이번에 해결할 범위를 정한다. 미검증은 결함 확정도 통과도 아니다.
3. **후속 수정:** 문제가 확인되면 파일 소유자에게 한정된 수정 티켓을 발행한다. 공통 API·콜백·캐릭터 ID·clip 이름 변경은 소비자와 계약을 먼저 맞춘다. 계약이 바뀌는 작업은 순차 진행한다.
4. **작업 공간:** 조사 단계는 동일 체크아웃을 써도 된다. 수정 단계의 병렬 작업은 동일 기준 커밋에서 분기한 별도 worktree/브랜치를 사용한다. 사용자 변경이 있는 트리는 보존하고, 통합 기준과 가져올 커밋을 총괄이 관리한다.
5. **회귀 테스트 소유권:** A는 신규 `GardenRuntimeTests.cs`, B는 신규 `GardenPresentationTests.cs`, C는 신규 `WindowsPlatformTests.cs`, D는 신규 `GardenIntegrationTests.cs`와 기존 테스트 파일을 맡는다. 이는 필요할 때 사용할 예약 이름이며 빈 테스트를 미리 만들지 않는다. 기존 테스트 수정은 D와 조정한다.
6. **완료 판정:** 구현 담당자의 관련 검증 후 D가 통합 상태의 전체 테스트와 필요한 실제 OS 시나리오를 확인한다. 총괄은 테스트 결과와 사용자 동작의 일치 여부를 확인해 완료/보완/환경상 미검증으로 판정한다. 코드 통합 완료와 Windows 출시 가능 판정을 구분한다.

## 세션에 전달할 시작 문장

각 Claude Code 세션을 프로젝트 루트에서 열고 아래 문장을 하나씩 전달한다. 사용자가 직접 전달하거나 별도 실행 채널을 연결하는 방식이며, 이 파일만으로 에이전트가 실행되지는 않는다.

- A: `docs/team/README.md와 docs/team/01-runtime.md를 읽고 A-01만 수행해. 보고서를 지정 경로에 저장하고 결과를 보고해.`
- B: `docs/team/README.md와 docs/team/02-presentation.md를 읽고 B-01만 수행해. 보고서를 지정 경로에 저장하고 결과를 보고해.`
- C: `docs/team/README.md와 docs/team/03-platform.md를 읽고 C-01만 수행해. 보고서를 지정 경로에 저장하고 결과를 보고해.`
- D: `docs/team/README.md와 docs/team/04-qa.md를 읽고 D-01만 수행해. 보고서를 지정 경로에 저장하고 결과를 보고해.`
