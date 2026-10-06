# GLB 파일 행·즉시 제거

2026-10-06 · `codex/glb-import-compat` / HEAD `671729a` / `1.1.0-beta`의 마지막 수정본에 적용했다. 작업 경로는 `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold`이며 테스트 빌드는 같은 경로의 `src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll`을 사용한다. [이전 파일 관리 구현](2026-10-06-glb-file-management.md)의 후속 수정이다. 커밋·배포·설치본 교체는 수행하지 않았다.

## 원인

이전 구현은 저장 여부에 따라 같은 텍스트 버튼을 파일 제거 또는 펫 삭제로 바꾸었다. GIF·MP4의 파일명 옆 휴지통처럼 현재 넣은 파일만 바로 비우는 조작이 GLB에는 없었다.

## 변경

- 미리보기 아래 **3D 파일** 행을 추가했다. GIF·MP4와 같은 **썸네일 → 파일명 → ＋ → 휴지통** 배치와 32px 아이콘 버튼을 사용한다. 기존 버튼 스타일 함수를 공유하고 휴지통은 Danger 토큰을 쓴다.
- 휴지통은 확인 창 없이 편집 중인 GLB와 미리보기·행동 연결·썸네일만 비운다. 펫 이름, 원본 GLB, 앱에 저장한 펫, 현재 바탕화면 펫은 그대로 유지한다.
- 파일이 없으면 **파일 없음**을 표시하고 휴지통·재생·저장·팩 생성은 비활성화한다. **＋** 또는 기존 **파일 열기…**로 다시 넣을 수 있다. 입력한 펫 이름은 파일을 다시 추가해도 유지한다.
- 긴 파일명은 한 줄 말줄임으로 표시하고 툴팁으로 전체 이름을 제공한다. 아이콘에는 한국어 접근성 이름과 기존 500ms 도구 설명을 적용했다.
- 앱에 저장된 펫 자체를 삭제하는 **펫 삭제**는 별도 동작으로 유지한다. 해당 펫을 열었을 때만 표시하고, 기존 확인·취소·기본 펫 전환을 유지한다.
- 제거 전 시작된 미리보기 작업은 무효화한다. 모델 참조·픽셀 버퍼·썸네일 비트맵도 정리해 늦은 렌더가 제거한 파일을 다시 표시하지 않도록 했다. 고정 설명 문구는 추가하지 않았다.

## 소유 파일

- `src/Unfold.Desktop/GlbPetView.cs`: 파일 행, 즉시 제거, 썸네일 수명, 이름 유지, 별도 펫 삭제
- `src/Unfold.Desktop/CustomPetWindow.cs`: 기존 파일 아이콘 스타일 함수를 GLB에서도 사용하도록 접근 범위만 변경
- `Tests/Unfold.Tests/GlbFileManagementTests.cs`: 파일 제거와 펫 삭제의 서로 다른 계약에 맞춰 기존 검사 갱신
- 신규 `Tests/Unfold.Tests/GlbFileSlotTests.cs`: 파일 제거·저장본 보존·재추가·긴 이름·작은 화면 검사 5개
- `docs/glb-pets.md`, `docs/README.md`, 이 보고서·증거

[이번 변경만의 차이](2026-10-06-glb-file-slot/file-slot-source.diff), [소스 해시·시작 Git 상태](2026-10-06-glb-file-slot/provenance.json). 기존 메모리 최적화·호환성·Windows 입력 코드를 보존했다.

## 검증

- GLB 파일/펫 관리·GLB UI·GIF/MP4 가져오기·파이프라인 집중 검사 **62/62 통과**. [로그](2026-10-06-glb-file-slot/tests-focused.log)
- 활성 펫을 편집 화면에서 비워도 런타임 선택·저장된 라이브러리가 유지되는 검사를 포함한 최종 `dotnet test Unfold.slnx -c Release --no-restore`: **733/733 통과**, 실패·건너뜀 0. Release Core/Desktop/Tests 빌드를 포함한다. [전체 로그](2026-10-06-glb-file-slot/tests-full.log)
- 저장 전·후 즉시 제거에서 확인 창이 뜨지 않는 것, 원본과 설치된 GLB 바이트 보존, 이름 유지, 파일 없음/버튼 상태, 다시 추가와 파일 선택 취소를 확인했다.
- 480/860/1120px 폭에서 150자 파일명의 말줄임·툴팁·아이콘 접근성과 가로 넘침 방지를 검사했다.
- 새 임시 데이터 경로와 합성 GLB로 Avalonia Headless + Skia 화면을 렌더했다. 저장한 펫을 편집 화면에서 비운 후에도 라이브러리 1개와 원본 파일이 남고 ＋로 다시 추가되는 것을 확인했다. [프로브 로그](2026-10-06-glb-file-slot/capture.log), [경고·오류 0개 빌드](2026-10-06-glb-file-slot/capture-build.log)
- `git diff --check` 통과. 소유 범위 밖의 기존 파일은 시작 해시와 동일하다. [보존 검사](2026-10-06-glb-file-slot/preservation.json)

| 상태 | Headless 화면 |
|---|---|
| 파일을 넣은 상태 | [1000×780](2026-10-06-glb-file-slot/01-added-wide.png) |
| 작은 창·저장된 펫 | [640×560](2026-10-06-glb-file-slot/03-saved-small.png) |
| 휴지통으로 비운 상태 | [이름과 저장된 펫 보존](2026-10-06-glb-file-slot/04-file-cleared.png) |
| ＋로 다시 넣은 상태 | [재추가](2026-10-06-glb-file-slot/05-readded.png) |

## 미검증·위험

macOS/Windows 네이티브 창과 OS 파일 선택기를 직접 조작한 결과는 아니다. 모든 저장·삭제 검사는 격리 프로필의 합성 파일로 수행했으며 사용자 파일을 제거하지 않았다. 파일을 비우면 현재 GLB에 연결한 미저장 행동 설정도 함께 초기화된다. 저장된 팩의 설정은 유지되어 **펫 선택**으로 다시 열 수 있다.
