> **보관 문서 · Garden 실험 — experiment/garden-windows / 3098f20**
> 현재 MVP의 작업 지침이 아닙니다. 원문의 명령, 담당 배정, 경로와 검증 결과는 당시 기록입니다.
> 원래 위치: `docs/team/02-presentation.md` · [현재 문서 안내](../../../README.md)

# B — UI·자산 담당 / B-01

너는 Unfold의 UI·자산 담당 Claude Code 에이전트다. 총괄은 Codex다. `docs/team/README.md`의 공통 규칙과 보고 형식을 따른다. 다른 에이전트의 변경을 덮어쓰거나 되돌리지 않는다.

## 이번 작업

정원 프로토타입의 표시 문구, 설정 화면, 캐릭터 자산이 현재 사용자 동작과 일치하는지 조사한다. 제품 코드·이미지·테스트를 수정하지 않고 `docs/team/reports/B-01-presentation.md`만 작성한다. 전체 테스트와 앱 실행은 D가 맡는다.

읽을 파일:

- `src/Unfold.Desktop/SettingsWindow.cs`
- `src/Unfold.Desktop/Ui.cs`
- `Sources/Unfold/Resources/Characters/sprout/character.json`
- 같은 디렉터리의 `spritesheet.png`
- `Scripts/generate-plant-placeholder.py`
- 문구와 연결 확인용 `AppRuntime.cs`, `PetWindow.cs`, `NativeReminder.cs`
- `docs/mvp.md`, `docs/windows-dogfooding-log.md`의 자산 관찰 항목

확인할 내용:

1. 설정·트레이·캐릭터 메뉴·휴식 창·OS 알림에서 사용자가 보는 용어와 실제 액션의 대응. Cat과 Garden 용어 혼재는 위치와 영향을 기록한다.
2. sprout의 프레임 크기, clip 이름, 낮·밤·클릭 상태, 재생 시간, 투명 영역과 화분의 잡기 영역을 실제 자산·코드와 대조한다.
3. 일반 UI 및 다양한 배율에서 확인해야 할 정렬·잘림·선명도·히트 영역을 정리한다. 화면을 실행하지 않았다면 시각적 검증 통과라고 쓰지 않는다.
4. 기존 사용자 캐릭터에도 같은 문구와 UI가 자연스러운지 점검한다. 디자인 개선과 기능 추가를 구분한다.

## 완료 기준

- 화면/위치 → 현재 표시 → 실제 동작 → 문제 여부의 표를 작성한다.
- 에셋 구조에서 확인한 사실과 실제 Windows에서 확인해야 할 항목을 분리한다.
- `AppRuntime.cs`, `PetWindow.cs`, `AnimationView.cs`의 변경이 필요하면 A에게, OS 알림 변경은 C에게 요청한다. 직접 수정하지 않는다.
- 캐릭터 ID·clip 계약 변경은 제안만 한다. 식물을 새로 그리거나 새 상태를 추가하지 않는다.
- D에게 전달할 시각·입력 검증 시나리오와 기대 결과를 정리하고 종료한다.
