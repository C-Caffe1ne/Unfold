# 펫 관리의 GLB 파일 추가·제거

2026-10-06 · 최신 `codex/glb-import-compat` / `671729a` / `1.1.0-beta` 개발본에 반영했다. 작업 경로는 `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold`이며 검증 실행 대상은 이 경로의 `src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll`이다. 기존 GLB 파이프라인 최적화·호환성·Windows 입력 미커밋 변경을 보존했다. 커밋·배포·설치본 교체는 수행하지 않았다.

## 원인

통합 펫 관리 화면은 GLB 가져오기와 저장은 지원했지만, 열린 3D 파일을 비우거나 저장한 GLB 펫을 지우는 진입점이 없었다. 삭제 후 두 펫 선택 목록과 현재 바탕화면 펫을 갱신하는 연결도 없었다.

## 변경

- **파일 열기… → GLB 선택 → 저장하고 적용**으로 펫을 추가한다. 서로 다른 파일을 차례로 추가하면 별도 펫으로 저장하고 **펫 선택**에서 다시 편집한다.
- GLB 편집기의 **펫 선택** 아래에 Danger 버튼을 배치했다. 저장 전에는 **파일 제거**, 앱에 저장한 펫이면 **펫 삭제**로 표시한다.
- 파일 제거는 현재 GLB 초안·행동 연결·미리보기를 비운다. 저장한 펫 삭제는 앱 라이브러리의 해당 펫만 지우고 양쪽 선택 목록을 즉시 갱신한다. 가져왔던 원본 GLB와 다른 펫은 유지한다.
- 두 작업 모두 대상 이름을 표시하는 확인 창을 거친다. 취소에 먼저 포커스를 두고 제거·삭제 버튼은 Enter 기본 동작으로 지정하지 않는다. 취소 또는 창 닫기 시 편집 상태를 유지한다.
- 현재 사용 중인 펫을 삭제하면 기본 내장 펫으로 전환하고 선택을 저장한다. 다른 펫을 삭제하면 현재 선택을 유지한다.
- 처리 중 중복 실행을 막고, 삭제 실패 시 초안을 남겨 재시도할 수 있다. 삭제 후 화면 갱신만 실패한 경우는 이미 삭제되었다는 상태를 구분해 안내한다.
- 제거 시 대기 중인 미리보기 요청을 무효화하고 모델·클립·비트맵 참조를 비운다. 뒤늦게 완료된 렌더가 지운 펫을 다시 표시하지 않도록 한다.
- 기존 파일 열기 위치·펫 이름 너비·미리보기·행동 설정 배치를 유지했다. 공통 디자인 토큰을 사용하며 고정 설명 문구는 추가하지 않았다.

## 소유 파일

- `GlbPetView.cs`: 제거·확인·초안 초기화·상태 및 목록 갱신
- `PetBuilderView.cs`, `PetManagementView.cs`, `SettingsWindow.Layout.cs`: 삭제 후 런타임 연결과 두 목록 동기화
- `AppRuntime.cs`: 삭제한 펫이 사용 중일 때 내장 펫 선택·저장
- `Ui.cs`: 제거 확인 버튼에도 기존 Danger 스타일 적용
- 신규 `GlbFileManagementTests.cs`: 12개 회귀 케이스
- 이 보고서·증거, `docs/README.md`, `docs/glb-pets.md`

시작 상태와 소스 해시는 [provenance.json](2026-10-06-glb-file-management/provenance.json), 이번 요청만의 코드는 [소스 차이](2026-10-06-glb-file-management/file-management-source.diff)에 기록했다.

## 검증

- GLB 관리·기존 편집기·파이프라인 집중 검사 **43/43 통과**. [로그](2026-10-06-glb-file-management/tests-focused.log)
- 취소·닫기 및 활성/비활성 펫 삭제 조건을 확장한 최종 `dotnet test Unfold.slnx -c Release --no-restore`: **730/730 통과**, 실패·건너뜀 0. Core/Desktop/Tests의 Release 빌드 포함. [로그](2026-10-06-glb-file-management/tests-full.log)
- 새 회귀 12개는 저장 전/후 취소·창 닫기, 제거 후 다른 파일 추가·저장, 두 모델 중 하나만 삭제, 원본·다른 펫 보존, 양쪽 목록 갱신, 라이브러리 잠금 실패 후 재시도, 활성/비활성 펫 삭제 후 선택과 저장, 480/860/1120px 폭의 가로 넘침 방지를 확인한다.
- 임시 `UNFOLD_DATA_DIR`와 합성 GLB로 Avalonia Headless + Skia 화면 5장을 렌더했다. 제거 뒤 라이브러리는 비었고 원본은 남는 것도 프로브에서 확인했다. [프로브 빌드](2026-10-06-glb-file-management/capture-build.log), [실행 결과](2026-10-06-glb-file-management/capture.log)
- `git diff --check` 통과. 작업 시작 때 기록한 파일 해시로 소유 범위 밖의 기존 변경이 그대로임을 확인했다. [보존 검사](2026-10-06-glb-file-management/preservation.json)

| 상태 | Headless 렌더링 |
|---|---|
| 파일 추가 후 | [1000×780](2026-10-06-glb-file-management/01-added-wide.png) |
| 저장한 펫 | [1000×780](2026-10-06-glb-file-management/02-saved-wide.png) |
| 작은 화면 | [640×560](2026-10-06-glb-file-management/03-saved-small.png) |
| 삭제 확인 | [확인 창](2026-10-06-glb-file-management/04-confirm.png) |
| 제거 후 | [빈 편집기](2026-10-06-glb-file-management/05-removed.png) |

## 미검증·위험

- 화면·버튼·확인 창·저장·삭제·실제 런타임 선택 경로는 macOS 호스트의 Headless 자동 실행으로 검증했다. macOS/Windows 네이티브 창과 OS 파일 선택기의 직접 조작, 물리 입력은 이번 검증에 포함하지 않았다.
- 사용자의 기존 GLB·라이브러리를 테스트에서 삭제하지 않았다. 모든 삭제 검사는 새 임시 프로필의 합성 GLB에 한정했다.
- 제거 시 참조 정리는 코드와 빈 미리보기 상태로 확인했다. OS 작업 관리자의 즉시 RAM 감소량이나 장시간 사용은 측정하지 않았다.
