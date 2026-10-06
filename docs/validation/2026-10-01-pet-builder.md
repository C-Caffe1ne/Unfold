# 펫 만들기 행동 행 개편 검증

> 이 기록은 `release/mvp` 0.2.2 체크아웃의 검사이며, 배포 Beta v1.0.2의 검증이 아니다.
> 최신 Beta 기준 수정과 검증은 [2026-10-02 기록](2026-10-02-beta-pet-builder.md)을 따른다.

## 원인

기존 다섯 개의 세로 카드에서는 파일명을 확인할 수 없고, 기본 행동만 반복하도록 고정돼 있었다.

## 변경

- 이름 입력을 미리보기 아래, 행동별 파일 섹션 바로 위로 이동했다.
- 여덟 행동을 세로로 쌓은 가로 행으로 표시한다. 썸네일, 행동, 파일명, 재생 설정, 미리보기·파일추가·삭제 버튼 순서다.
- 파일명은 한 줄 말줄임과 전체 이름 툴팁을 사용한다.
- 기본·알림·휴식으로 표시 이름을 변경하고 hover/pointerDown/pointerUp을 추가했다. 기존 행동 ID는 보존한다.
- 한 번·반복·핑퐁 설정을 내보내기, 설치, 미리보기와 커스텀 펫 런타임에 연결했다. 기존 manifest는 PingPong=false가 기본값이다.
- 기본 행동의 한 번 재생과 이벤트 반복을 커스텀 팩 검사에서 허용하며, 기본 제공 original 행동 프로필의 기존 검증은 유지한다.
- 마우스 뗌 파일이 있으면 클릭 반응보다 우선한다. 없으면 누름 반복을 종료하고 기본/클릭으로 복귀한다. 입력 캡처 취소도 누름 반복을 종료한다.
- 파일 교체 확인, 실패·취소 시 초안 보존, 저장 후 초기화는 유지한다.

## 소유 파일

Core의 CustomPetDraft, CharacterLibrary, CharacterAssetAudit; Desktop의 CustomPetWindow, AnimationView,
PetPackWindow, PetWindow, PetWindow.Companion, SmokeDiagnostics; 관련 테스트 및 pet-packs 문서.
기존 PetHoverTests/PetWindow의 HWND 가드 변경과 Supabase 변경은 보존했다.

## 검증

- macOS에서 Release 전체 테스트 423/423 통과. 마지막 누름 해제 보완 후 포인터·호버·재생 설정 집중 검사 23/23 통과.
- Release 앱 빌드: 경고 0, 오류 0. 최종 집중 테스트에서도 Core/Desktop/Tests 빌드 통과.
- 새 빈 프로필 `/tmp/Unfold-pet-final-SF3qTs`의 smoke 진단 success=true.
- 진단은 기본·최소·반응형 창의 가로 넘침, 행 스크롤, 고정 하단, 미디어 가져오기·미리보기·팩 저장을 검사했다.
- [파일이 있는 행 캡처](2026-10-01-pet-builder/editor.png), [최소 창 캡처](2026-10-01-pet-builder/minimum.png)를 시각 확인했다.
- git diff --check 통과.

## 미검증·위험

캡처는 macOS off-screen 진단이며 마우스 행동 검사는 Avalonia headless 합성 입력이다.
실제 OS 파일 선택, 물리 마우스·키보드, Windows 실기, 접근성 도구, 새 설치 배포물은 미검증이다.
배포 파일 생성·게시·설치된 앱 교체는 수행하지 않았다.
전체 smoke 후 추가한 누름 해제 보완은 집중 회귀 검사로 확인했고 전체 smoke를 반복하지 않았다.
