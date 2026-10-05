# 펫 캔버스 효과 제거 — 2026-10-05

## 원인

마우스로 펫을 누르거나 유지할 때 `PetPose`가 캔버스의 가로·세로 배율과 수직 위치를 바꿨다.
포인터 업에서는 들고 있는 프레임을 고정한 채 0.64초 바운스를 넣은 뒤 지정된 `land` 동작을 재생했다.
수정 전 회귀 검사 2개에서 배율 `1.12 / 0.82`와 수직 이동 `0.0481`을 재현했다.

작업 기준은 `/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold`,
`codex/beta-v1.0.4`, HEAD `717a63b`, 프로젝트 `1.0.4-beta`다.
다른 체크아웃의 `0.2.2`, `1.0.3-beta`와 비교해 최신 개발본을 확인했다.
직전 펫 팩 편집기 통합과 GLB 메모리 최적화, 기존 외부 작업 변경을 보존했다.

## 변경

- 포인터 다운·유지·드래그·업에서 캔버스를 눌리거나 들리거나 튀게 만들던 변형을 제거했다.
- 포인터 업에서 바운스 대기 없이 지정된 `land` 동작을 즉시 재생한다.
- 클릭·호버·사용자 지정 `pointerDown` / `pointerUp` 동작, GLB 클립과 정상 드래그 입력 경로를 유지했다.
- 다른 상황별 동작에서도 캔버스 변형이 추가되지 않는지 검사했다. 자산 자체에 포함된 동작은 유지한다.
- 실제 펫 창 이동과 산책 방향 전환은 기존 동작을 유지한다.
- 새로운 제품 UI 설명 문구는 추가하지 않았다.

## 소유 파일

- 런타임: `PetWindow.PointerArt.cs`, `PetWindow.Companion.cs`, `PetMotion.cs`.
- 주석: `PetWindow.cs`, `AnimationView.cs`.
- 회귀 검사: `OriginalCompanionTests.cs`, `GlbTests.cs`, `CustomPetInteractionTests.cs`, `AnimationRenderingTests.cs`.
- 네이티브 진단: `OriginalCompanionDiagnostics.cs`, `GlbDiagnostics.cs`.
- 문서: `docs/README.md`, `docs/verification.md`, `docs/pet-packs.md`, `docs/glb-pets.md`, 이 검증 기록.

## 검증

| 검사 | 결과 |
|---|---|
| 수정 전 재현 | 새 캔버스 고정 검사 2개 실패로 기존 효과 확인 |
| Release 컴파일 및 최종 집중 검사 | 8 통과 / 실패·건너뜀 0 |
| 전체 자동 검사 | 630 통과 / 실패·건너뜀 0 |
| 기본 펫 네이티브 진단 | 5종 모두 success=true, canvasStayedFixed=true, 즉시 land 확인 |
| 실제 Kazusa GLB 네이티브 진단 | success=true, pointerCanvasVerified=true, 가져오기·저장·해상도 검사도 통과 |
| 취소 및 재입력 | 캡처 상실·숨김·펫 변경·놓기 중 재입력 및 다음 동작 검사 통과 |
| 정적 검사 | git diff --check 통과, 제거한 변형 함수의 런타임 참조 없음 |

집중 검사 중 펫 변경 직후의 상태를 idle로만 가정한 기존 검사가 한 번 실패했다.
정지한 포인터 아래에서 새 펫의 정상 hover 동작이 시작된 경우였으며,
잘못된 click/pickup/held/land/pointerDown/pointerUp 발생을 금지하고 hover 완료 후 idle을 확인하도록 보정했다.
보정 후 집중 검사와 전체 검사가 통과했다.

네이티브 진단은 각각 새 `UNFOLD_DATA_DIR`을 사용했다.
10ms 간격으로 pose와 렌더 변환이 기본값인지 관찰하고 놓기 직후의 클립·단계를 확인했다.
기본 펫 진단은 화면 밖에서 실시간 재생했으며, Kazusa 진단의 포인터 단계도 자동으로 호출했다.
캡처는 재생 확인을 보조하며, 단일 정지 이미지로 전체 시간 구간의 고정을 판정하지 않았다.

원시 결과와 코드·DLL 해시는 [verification.json](2026-10-05-pet-canvas-motion/verification.json)에 있다.
결과: [기본 펫](2026-10-05-pet-canvas-motion/original-pets.json),
[Kazusa GLB](2026-10-05-pet-canvas-motion/glb-result.json),
[전체 검사 로그](2026-10-05-pet-canvas-motion/full-tests.log).
캡처: [GLB 놓기](2026-10-05-pet-canvas-motion/glb-release.png),
[보리 놓기](2026-10-05-pet-canvas-motion/bori-release.png).

## 미검증·위험

- 물리 마우스로 누르기·드래그·놓기 및 Windows 실기 실행은 이번 검증에 포함하지 않았다.
- 지정한 애니메이션 파일 안의 움직임은 재생된다. 제거 대상은 앱이 캔버스 전체에 덧붙이던 변형이다.
- 커밋·공개 배포·설치본 교체는 수행하지 않았다. 실행 중인 이전 프로세스에는 재시작 후 반영된다.
