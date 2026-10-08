# 설치한 커스텀 펫 편집·삭제 · 2026년 10월 8일

## 기준과 범위

- 개발 경로: `/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features`.
- 브랜치: `codex/remove-unused-features`, 시작 HEAD와 원격: `2715fd183c42a25badb29cbf15fc3a017b468464`.
- 개발 버전: **Beta v1.1.2** (`1.1.2-beta`). 최신 공개 배포는 `v1.1.1-beta`, 소스 `887d0127e57e5145062d0d558fb71b113284542d`다.
- 후보 작업트리의 브랜치·HEAD·미커밋 변경·프로젝트 버전을 비교하고 위 경로의 소스로 구현·검증했다.
- 사용자가 지정한 대상은 **설치한 커스텀 펫만**이다. 기본 제공 펫 5종은 편집·삭제 목록에서 제외하고 저장 계층에서도 보호한다.
- 앞선 Markdown 최신화의 33개 변경과 다른 작업트리의 변경을 보존했다. 이 작업의 제품 변경은 아래 구현·검사 파일에 한정한다.

## 확인한 문제와 수정

기존 펫 선택 목록은 GLB만 표시했고, 이미지·영상 화면은 새 팩 생성만 지원했다. 설치한 이미지·GIF·MP4 기반 펫과 스프라이트 펫은 저장된 내용을 불러와 편집·삭제할 수 없었다.

- 두 형식의 **펫 선택**에 모든 설치 커스텀 펫을 표시하고 선택한 형식의 편집 화면을 연다.
- **편집**으로 저장된 최신 내용을 다시 불러온다. 이름·행동 파일·재생 설정을 수정한 뒤 **저장하고 적용**하면 같은 펫 ID로 저장하고 실행 펫에 반영한다.
- 미디어 편집은 원래 스프라이트 프레임·추가 행동·재생 시간·렌더링 설정을 보존한다. 수정하지 않은 PNG/GIF는 재인코딩하지 않는다. 정지 이미지 교체는 PNG를 사용한다.
- 저장 내용 버전을 증가시킨다. 저장 실패 시 초안을 보존하며, 편집 중 외부에서 변경되거나 삭제된 펫은 덮어쓰거나 다시 생성하지 않는다. 오래된 목록 항목에서도 현재 manifest를 읽는다.
- **펫 삭제**는 확인·취소를 제공한다. 현재 사용 중인 펫을 삭제하면 기본 펫으로 전환하고 선택을 저장한다. 다른 설치 펫과 가져온 원본 파일은 유지한다.
- 이전 픽셀 펫의 현재 실행 행동도 편집할 수 있다. 저장 시 `source.piskel`은 보존하며, 폐기한 픽셀 그림 편집기는 다시 추가하지 않는다.
- 기존 GIF 프레임과 스프라이트 미리보기의 지연 로딩을 유지하고 중복 GIF 바이트 복사를 줄였다. 썸네일 교체·초기화 시 Bitmap을 해제한다.

## 구현과 회귀 검사 파일

| 파일 | 역할 |
|---|---|
| [CharacterLibrary.Editing.cs](../../src/Unfold.Core/CharacterLibrary.Editing.cs), [CharacterLibrary.cs](../../src/Unfold.Core/CharacterLibrary.cs) | 보호 ID, 변경 감지, 기존 설치 팩 교체와 삭제 |
| [CustomPetDraft.Editing.cs](../../src/Unfold.Core/CustomPetDraft.Editing.cs), [CustomPetDraft.cs](../../src/Unfold.Core/CustomPetDraft.cs) | 기존 미디어·스프라이트 불러오기, 보존·저장·버전·스냅샷 |
| [GlbPetDraft.cs](../../src/Unfold.Core/GlbPetDraft.cs) | 최신 GLB 불러오기와 변경된 설치 펫 덮어쓰기 방지 |
| [CustomPetWindow.cs](../../src/Unfold.Desktop/CustomPetWindow.cs), [GlbPetView.cs](../../src/Unfold.Desktop/GlbPetView.cs), [PetBuilderView.cs](../../src/Unfold.Desktop/PetBuilderView.cs) | 전체 설치 목록, 형식 전환, 편집·저장·삭제·오류 복구 |
| [PetEditingTests.cs](../../Tests/Unfold.Tests/PetEditingTests.cs) | 새 회귀 검사 14개 |

## 자동 검사

- 수정 전 관련 검사 **65/65 통과**.
- 편집·삭제·기존 펫 팩·GLB·커스텀 미디어 관련 집중 검사 **97/97 통과**.
- 최종 Release 전체 검사 **777/777 통과**, 실패·건너뜀 0개. 빌드 경고·오류 0개.
- 새 14개 검사는 GIF·정지 이미지 저장, 픽셀·시간·추가 행동 보존, PNG/GIF 교체, 반복 저장 버전 증가, 기존 Piskel 원본 보존, 기본 펫 보호, 외부 변경·삭제 충돌, 저장 실패 후 재시도, 삭제 취소, 형식 전환과 현재/다른 펫 삭제 후 선택 보존을 포함한다.
- 최초 전체 실행에서 취소 후 선택 복원 검사의 비동기 완료 대기가 부족했다. 실제 선택 복원을 기다리도록 검사 조건을 수정한 뒤 최종 전체 검사를 통과했다.
- Markdown 323개·상대 파일 링크 1,038개 검사에서 깨진 링크 0개이며 `git diff --check`를 통과했다.

## macOS 실행 검증

최신 소스를 참조한 임시 native Avalonia 실행기로 실제 macOS 설정 창과 `AppRuntime`을 실행했다. 매번 새 `UNFOLD_DATA_DIR`을 생성했다. 이미지 fixture와 합성 GLB를 설치해 아래 흐름을 확인했다.

- 설치한 이미지·GLB 2종이 공통 선택 목록에 표시된다.
- 이미지 이름·핑퐁 설정을 저장하고 실행 펫에 적용한다.
- 이미지 삭제 취소 시 설치를 유지하고, 삭제 확인 시 기본 펫으로 전환한다.
- GLB 이름·방향 설정을 저장하고 실행 펫에 적용한다.
- GLB 삭제 후 기본 펫으로 전환하며 가져온 원본 GLB를 보존한다.
- 두 편집 화면의 실제 창 크기 **1120×800 / 640×560**를 확인하고 가로 넘침·하단 버튼 잘림을 검사했다. 최소 크기 캡처도 실제 640×560 PNG다.

[Native 실행 결과 JSON](images/2026-10-08-pet-management-edit-delete/native-result.json)

| 화면 | 기본 크기 | 최소 크기 |
|---|---|---|
| 이미지 펫 | [1120×800](images/2026-10-08-pet-management-edit-delete/media-editor.png) | [640×560](images/2026-10-08-pet-management-edit-delete/media-editor-minimum.png) |
| GLB 펫 | [1120×800](images/2026-10-08-pet-management-edit-delete/glb-editor.png) | [640×560](images/2026-10-08-pet-management-edit-delete/glb-editor-minimum.png) |

진단 실행의 off-screen 창 고정값을 임시 검증 실행기에서 해제한 뒤 최소 크기를 검사했다. 제품의 진단 창 정책은 변경하지 않았다. GLB 캡처의 흰 삼각형은 합성 테스트 모델이다.

기존 앱의 `--smoke-test`도 새 격리 프로필에서 성공했다. 설정·타이머·완료 기록·CSV·펫 팩 설치/업데이트/복구·GIF 팩 작성·초안 유지·기존 데이터 호환·하단 버튼과 최소 크기를 확인했고 PNG 100장을 생성했다. 약 56초 실행 결과는 [smoke.json](images/2026-10-08-pet-management-edit-delete/smoke.json)에 보존한다. 이 진단의 업데이트 창 검사는 실제 업데이트 설치 성공을 의미하지 않는다.

버튼 이벤트·파일 선택 경로는 프로그래밍 방식으로 실행했다. 실제 macOS 창·렌더링·런타임 확인이며 물리 마우스 입력 검증은 아니다. Windows 실기 입력·표시, 임의 대용량 펫의 장시간 메모리 상한, OAuth·결제·업데이트 설치는 이번 검증 범위에 포함하지 않는다.

이번 변경은 로컬 개발본이다. 커밋·푸시·공개 배포·설치 앱 교체는 수행하지 않았다.
