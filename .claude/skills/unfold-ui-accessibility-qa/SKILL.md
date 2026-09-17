---
name: unfold-ui-accessibility-qa
description: "Unfold Avalonia UI의 키보드 포커스, 접근성 레이블, 대비, 최소 창 크기, 스크롤, 잘림과 상태 전달을 검증한다. 접근성, 반응형, Windows/macOS UI QA, 후속 보완·재실행 요청에는 반드시 이 스킬을 사용한다."
---

# Unfold UI Accessibility QA

1. 기본·최소·스크롤 캡처에서 핵심 작업의 노출과 도달 가능성을 비교한다.
2. 코드에서 `AutomationProperties`, `SetLabeledBy`, 포커스 이동, 기본/취소 동작을 교차 확인한다.
3. 색상 대비, 색상 단독 상태 전달, 비활성 컨트롤의 설명을 확인한다.
4. 자동 검사, macOS 관찰, Windows·VoiceOver·Narrator 미검증을 분리한다.
5. 문제마다 재현 상태, 소유 파일, 자동 검사와 실제 OS 검증 방법을 적는다.

보고서만 작성하고 제품 코드는 수정하지 않는다.
