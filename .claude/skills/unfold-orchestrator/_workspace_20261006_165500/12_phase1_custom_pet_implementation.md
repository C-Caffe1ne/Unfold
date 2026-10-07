# 구현 보고서 — Phase 1B · UI-03 커스텀 펫 파일 덮어쓰기 방지

2026년 09월 16일 · `release/mvp` · C#/.NET 10 Avalonia.

## 원인

`CustomPetView`는 파일을 동작에 배정할 때 해당 동작이 이미 채워져 있는지 확인하지 않았다.

- `AssignPending()`은 `action.SelectedItem`이 가리키는 동작에 곧바로 `Assign`을 호출했고,
  `SelectFile(key)`도 파일을 고르면 확인 없이 `Assign`으로 넘겼다. 두 경로 모두 기존 클립을
  `CustomPetDraft.SetClip`으로 말없이 덮어썼다.
- `CustomPetAction` 선택기는 배정에 성공해도 그대로 남았다. 기본값이 첫 항목인 `쉬는 모습`이라
  가져오기를 반복하면 사용자가 선택을 바꾸지 않는 한 계속 같은 동작을 덮어썼다.

즉 사용자가 의도한 "다음 동작에 넣기"가 실제로는 "직전 동작 교체"가 되는 흐름이었다.

## 변경

### `src/Unfold.Desktop/CustomPetWindow.cs`

1. `ConfirmReplace(key, path)`를 추가했다. 대상 동작에 클립이 없으면 그대로 진행하고,
   있으면 `Ui.Confirm`으로 확인을 받는다.
   - 제목: `<동작 이름> 파일을 바꿀까요?` (예: `쉬는 모습 파일을 바꿀까요?`)
   - 본문: 현재 들어 있는 파일 이름과 새로 가져온 파일 이름을 함께 알리고
     `다른 동작은 그대로예요.`로 영향 범위를 한정한다.
   - 선택지는 `바꾸기`, `취소`. 기존 `Ui.Confirm` 안전 규칙이 `취소`에 `IsCancel`과 초기 포커스를
     주고, 제목 표시줄로 닫으면 `-1`을 돌려주므로 두 경우 모두 교체가 일어나지 않는다.
   - 취소 시 `CustomPetStatus`에 `<동작 이름>의 기존 파일을 그대로 두었어요.`를 `Muted`로 표시한다.
2. `AssignPending()`과 `SelectFile(key)` 양쪽이 `Assign` 앞에서 `ConfirmReplace`를 거치도록 했다.
   `AssignPending()`은 취소 시 `pendingPath`를 비우지 않으므로 가져온 파일을 다른 동작에 그대로
   넣을 수 있다.
3. `SelectNextEmptyAction()`을 추가하고 `AssignPending()` 성공 경로에서 호출한다. 현재 선택
   다음 인덱스부터 순환 탐색해 아직 비어 있는 첫 동작으로 이동하고, 뒤가 모두 차 있으면 앞에서부터
   찾으며, 5개 동작이 전부 차 있으면 현재 선택을 유지한다.
4. 회귀 검사가 "어떤 파일이 남아 있는지"를 직접 확인할 수 있도록 동작 카드의 파일 설명 라벨에
   `CustomPetLabel_<key>`, 가져오기 안내 문구에 `CustomPetPending` 이름을 부여했다. 시각 표현은
   바꾸지 않았다.

교체를 승인한 뒤 새 파일 검증이 실패하면 기존과 같이 `PetMediaImporter.Import` 또는
`CustomPetDraft.SetClip`이 던지고 `draft`는 그대로 남는다. 이 계약은 변경하지 않았다.

### `Tests/Unfold.Tests/CustomPetTests.cs`

- 대화상자 조작 헬퍼 `Dialog`, `Choice`, `Answer`와 상태 확인 헬퍼 `Label`, `Assigned`, `Copy`를 추가했다.
- 신규 회귀 검사 3건.
  - `ReplacingAFilledActionAsksFirstAndCancellingKeepsBothTheClipAndTheImportedFile`:
    확인 문구가 동작 이름과 두 파일 이름을 담는지, `취소`가 포커스를 갖고 `바꾸기`가 기본 버튼이
    아닌지 확인한다. `취소` 버튼과 제목 표시줄 닫기 두 경로 모두에서 기존 클립 이름,
    `펫 팩 만들기…` 활성 상태, `미리보기` 활성 상태가 유지되는지 본다. 이어서 같은 pending 파일을
    `스트레칭`에 배정해 가져온 파일이 살아 있음을 확인한다.
  - `AnApprovedReplacementThatFailsValidationKeepsTheExistingClip`:
    `바꾸기`를 누른 뒤 손상된 GIF를 넣어도 기존 파일 이름과 생성 가능 상태가 남는지 확인한다.
  - `AssigningMovesThePickerToTheNextEmptyActionAndStopsWhenEveryActionIsFilled`:
    `쉬는 모습` 배정 후 `휴식 안내`로 이동, 마지막 동작 `클릭 반응` 배정 후 앞으로 되돌아
    `휴식 안내`로 이동, 5개가 모두 찬 뒤에는 선택이 `클릭 반응`에 그대로 남는지 확인한다.
- 기존 `ImportedFileCanBeAssignedReplacedRemovedAndExportedOnlyWithIdle`은 이제 채워진
  `쉬는 모습`을 교체하므로 `바꾸기` 응답을 추가했다. 검사 의도와 나머지 단언은 그대로다.

## 소유 파일

- `src/Unfold.Desktop/CustomPetWindow.cs`
- `Tests/Unfold.Tests/CustomPetTests.cs`
- `.claude/skills/unfold-orchestrator/_workspace/12_phase1_custom_pet_implementation.md` (이 보고서)

소유 파일 밖은 수정하지 않았다. 같은 작업 트리의 다른 변경(`PersonalizationWindow.cs`,
`SettingsWindow*.cs`, `PetWindow.cs`, `PetPackDiagnostics.cs`, `AnimationLifecycleTests.cs` 등)은
건드리지 않았다.

## 검증

빌드:

```
dotnet build Unfold.slnx -c Release
# Build succeeded. 0 Warning(s) 0 Error(s)
```

집중 테스트(격리 데이터 디렉터리 사용):

```
UNFOLD_DATA_DIR=/tmp/claude-501/unfold-custompet-20260916 \
  dotnet test Unfold.slnx -c Release --no-build --filter "FullyQualifiedName~CustomPet"
# Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8
```

인접 UI 검사(펫 팩 탭, 설정 화면, 공통 대화상자 테마·취소 규칙):

```
UNFOLD_DATA_DIR=/tmp/claude-501/unfold-custompet-20260916b \
  dotnet test Unfold.slnx -c Release --no-build \
  --filter "FullyQualifiedName~CustomPet|FullyQualifiedName~PetPackWindowTests|FullyQualifiedName~SettingsUiTests|FullyQualifiedName~DesignSystemTests"
# Passed! - Failed: 0, Passed: 24, Skipped: 0, Total: 24
```

`CustomPetWindowTests`만 새 데이터 디렉터리로 2회 반복 실행해 대화상자 대기 로직의 불안정성이
없음을 확인했다(각각 5/5 통과).

`SmokeDiagnostics.VerifyCustomPet`은 `AssignPetMedia`를 빈 `쉬는 모습`에,
`CustomPetFile_click`을 빈 `클릭 반응`에 사용하므로 새 확인 대화상자가 끼어들지 않는다.
코드 경로를 읽어 확인했고 이번 세션에서 스모크 진단을 다시 실행하지는 않았다.

## 미검증·위험

- 실제 macOS/Windows에서 마우스·키보드로 확인 대화상자를 조작한 결과는 확인하지 않았다.
  위 결과는 모두 헤드리스 자동 검사다. 실제 파일 선택기와 연결한 흐름도 미검증이다.
- VoiceOver·Narrator에서 확인 문구가 읽히는 방식은 확인하지 않았다. UI-09(비활성 사유
  접근성 연결)는 이번 범위가 아니다.
- 전체 `dotnet test Unfold.slnx -c Release`는 실행하지 않았다. 작업 트리에 다른 워커의
  미완성 변경이 함께 있어 통합 후 감독자가 실행하는 것을 전제로 한다.
- `Ui.Confirm`은 `ShowDialog`로 소유 창을 모달로 잠근다. 설정 창의 펫 탭 안에서 쓰일 때도
  소유자는 해당 창이므로 동작은 같지만, 모달 중 타이머 알림이 겹치는 상황은 실기에서 보지 않았다.
- 확인 문구는 파일 이름을 그대로 노출한다. 매우 긴 파일 이름은 460px 고정 폭 대화상자에서
  줄바꿈으로 늘어난다(`Ui.Confirm`이 `SizeToContent.Height`라 잘리지는 않는다). 실제 긴 이름으로
  캡처하지는 않았다.
