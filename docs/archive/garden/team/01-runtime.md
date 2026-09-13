> **보관 문서 · Garden 실험 — experiment/garden-windows / 3098f20**
> 현재 MVP의 작업 지침이 아닙니다. 원문의 명령, 담당 배정, 경로와 검증 결과는 당시 기록입니다.
> 원래 위치: `docs/team/01-runtime.md` · [현재 문서 안내](../../../README.md)

# A — 핵심 동작 담당 / A-01

너는 Unfold의 핵심 동작 담당 Claude Code 에이전트다. 총괄은 Codex다. `docs/team/README.md`의 공통 규칙과 보고 형식을 따른다. 다른 에이전트도 같은 프로젝트를 사용하므로 그들의 변경을 되돌리지 않는다.

## 이번 작업

현재 Garden 프로토타입의 타이머·캐릭터 전환·애니메이션 상태가 어떻게 연결되는지 조사하고, 재현할 가치가 있는 문제를 좁힌다. 제품 코드나 테스트를 수정하지 않고 `docs/team/reports/A-01-runtime.md`만 작성한다. 전체 테스트와 앱 실행은 D가 맡는다.

읽을 파일:

- `src/Unfold.Core/AppSettings.cs`
- `src/Unfold.Core/StretchClock.cs`
- `src/Unfold.Core/CharacterLibrary.cs`
- `src/Unfold.Desktop/AppRuntime.cs`
- `src/Unfold.Desktop/PetWindow.cs`
- `src/Unfold.Desktop/AnimationView.cs`
- 관련 `Tests/Unfold.Tests/CoreTests.cs`

확인할 시나리오:

1. 신규 프로필의 sprout 선택과 기존 프로필의 캐릭터 선택 보존.
2. 낮·밤 idle 선택, 반응 clip 재생 중 시간대 변경, 반응 완료 후 올바른 idle 복귀.
3. 빠른 캐릭터 전환, 클릭·휴식 반응의 중첩, 로딩 중 Hide/Quit 이후 늦은 완료가 표시·종료 상태와 어긋날 가능성.
4. 자동·수동 휴식 요청, 이미 열린 알림, 숨긴 캐릭터의 처리. Cat MVP 문서의 규칙과 현재 Garden 코드가 다른 부분은 분리해서 기록.

초기 조사 단서: `AppRuntime.UpdatePet()`의 `await SetCharacter()` 이후 표시 상태 재확인 여부, `PetWindow`의 Hide/Close 시 대기 작업 무효화 여부, 06:00·18:00 시간대 전환이 진행 중 반응에 미치는 영향이다. 모두 재현 전 조사 후보이며 확정 결함으로 취급하지 않는다. 코드에서 실제 메서드명과 현재 구현을 다시 확인한다.

## 완료 기준

- 이벤트 → 호출 경로 → 상태 변경 → 화면 결과를 코드 근거와 함께 설명한다.
- 동시성 또는 상태 문제 후보는 실행 순서까지 제시하고, 아직 재현하지 않았다면 가설로 표시한다.
- B/C가 의존하는 메서드·설정값·캐릭터 clip 계약을 나열한다.
- D에게 검증할 시나리오와 기대 결과를 전달한다. 후속 `GardenRuntimeTests.cs`가 필요하다면 검증할 행동을 제안하되 작성하지 않는다.
- 우선순위가 높은 후속 작업을 최대 3개 제안하고 종료한다.
