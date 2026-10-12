# GIF·GLB 추가 안내 제거

2026년 10월 12일 · `codex/gif-glb-import-guidance`, `1.1.3-beta`, 시작 커밋 `4c74880`.

- 사용자 요청으로 준비 안내, 지원 형식 caption, 저장 조건·GLB 행동 설명, 기본 필수 표기와 새 오류 설명을 제거했다.
- 관련 UI·진단 4개를 복원하고 참조 없는 안내 전용 helper·테스트 3개를 삭제했다. `src/Tests/Assets`는 안내 추가 전 `6f0a3dd`와 동일하다. 기존 사용자 작업트리는 보존했다.
- `AGENTS.md`에 모든 수정 전 slop 점검·승인 범위 안 최소화 원칙을 추가했다. 현재 문서의 안내 기능 주장을 제거하고 과거 검증은 이력으로 보존했다.

| 검증 | 결과 |
|---|---|
| Release 빌드 | 경고 0·오류 0 |
| 전체 Release 테스트 | 790/790 통과, 실패·건너뜀 0 |
| macOS smoke | 성공, 110 PNG, GIF 제작·초안·640×560·기존 기능 확인 |
| macOS GLB review | 성공, 27 PNG, 편집 UI 가져오기·저장·디스크 재열기 확인 |
| Windows x64 crosspublish | 성공, PE32+ GUI x86-64 확인 |
| 독립 source·화면 검토 / diff | 추가 설명 제거 확인, `git diff --check` 통과 |

최초 집중 검사 60개 중 GLB pickup 전환 1건이 대기 시간 초과로 실패했다. 이전 개발 앱의 CPU 106.1%를 확인해 그 프로세스만 종료했다. 코드·검사를 변경하지 않고 해당 재검사 5/5와 최종 전체 790/790을 통과했다. CPU 부하를 유일한 원인으로 단정하지 않는다. 안내 전용 검사 50개는 제거된 기능과 함께 삭제했다.

Mac은 네이티브 렌더링·자동 이벤트·격리 IO 증거다. 물리 입력·실제 picker와 Windows 실기·설치본은 미검증이다. GLB는 삼각형 1개/Idle 0.2초 진단 모델이며 포인터 행동 검증은 포함하지 않는다. 배포·push는 하지 않았다.

원본 로그: `/tmp/unfold-gif-glb-guidance-removal`의 `full/full.trx`, `glb-retry/glb-retry.trx`, `implementation.md`, `review.md`. Native JSON: `/tmp/unfold-guide-removed-native.ToJjPF/verification/smoke.json`, `/tmp/unfold-guide-removed-glb.8FgN7w/verification/glb/result.json`.

화면·검사 보존: `/Users/hwanghyeonseong/.codex/visualizations/2026/10/11/01a12c4a-aab6-77f2-ad0e-8fbe5e551078/pet-editor-guidance-removal`의 `gif.png`, `gif-minimum.png`, `glb.png`, `glb-minimum.png`, TRX·JSON.
