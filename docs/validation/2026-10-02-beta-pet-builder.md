# Beta v1.0.2 기준 펫 만들기 수정

## 버전 기준

2026-10-02 GitHub Release API에서 최신 공개 릴리스 `v1.0.2-beta`를 확인했다.
설치 앱 `/Applications/Unfold.app`의 `UnfoldReleaseVersion=1.0.2-beta`, 숫자 버전 `1.0.2`, 빌드 번호 `1000002`도 확인했다.

실제 Beta 소스는 `/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold`에 있다.
해당 작업 트리의 Git HEAD는 `37c6de3` detached 상태지만, **배포 Beta 변경은 미커밋 작업 트리에 포함돼 있으므로 HEAD만으로 배포 코드를 식별할 수 없다.**
이 작업 트리의 프로젝트 버전과 수정 후 실행 파일 `--version`은 `Unfold Beta v1.0.2 (1.0.2-beta)`다.
기존 현재 디렉터리의 `release/mvp` `c93e168` 프로젝트 버전 `0.2.2`는 배포 Beta 기준이 아니다.

배포 당시 `artifacts/validation/2026-10-01-v1.0.2-release/source-inputs.json`의 216개 입력 중 수정 직전 212개가 일치했다.
앱 소스·자산·공통 빌드 입력은 모두 일치했다. 기존 차이는 make-macos-bundle.sh, package-macos-dmg.sh,
package-installers-zip.py, v1.0.2-beta 릴리스 노트 4개이며 이번 변경에서 보존했다.

## 원인과 변경

기존 작업은 배포 기준을 확인하지 않고 0.2.2 소스에 적용했으므로 Beta의 PNG/JPG 지원과 브랜드·계정 UI를 포함한 검증이 빠졌다.
이번에는 Beta 작업 트리에 필요한 펫 변경만 옮겼다.

- 이름 입력을 행동별 파일 섹션 바로 위로 배치했다.
- 썸네일 → 행동 → 파일명 → 한 번/반복/핑퐁 → 미리보기/파일추가/삭제 순서의 가로 행으로 변경했다.
- 긴 파일명은 말줄임과 전체 이름 툴팁을 사용한다.
- 기본·알림·휴식 이름과 마우스 호버·눌림·뗌 행동을 적용했다.
- 재생 설정을 GIF와 PNG/JPG 정지 이미지 모두의 팩 저장·설치·미리보기·런타임에 연결했다.
- 누름 반복의 해제·입력 취소 복귀와 기존 파일 교체 확인을 유지했다.
- Beta의 PNG/JPG/MP4 가져오기, 정지 이미지 시트 구성, 로그인 유지·구매 게이트·브랜드·폰트·테마는 보존했다.

## 소유 파일

Beta 작업 트리의 Core CustomPetDraft/CharacterLibrary/CharacterAssetAudit,
Desktop CustomPetWindow/AnimationView/PetPackWindow/PetWindow/PetWindow.Companion/SmokeDiagnostics,
관련 CustomPet/PetTabsUi/PetSaveReset/PetHover/DesignSystem/OriginalCompanion/UiAudit/StaticPetMedia 테스트 및 pet-packs 문서.
기존 변경은 `/tmp/Unfold-beta-pet-baseline`에 패치·소유 파일·입력 해시를 보존했다.

## 검증

- 최종 전체 Release 테스트 **534/534 통과**, 실패·건너뜀 0. PNG/JPG·GIF 혼합 팩의 여덟 행동과 재생 설정, 파일명·썸네일·툴팁, 기존 계정·브랜드·알림 회귀를 포함한다.

- Beta Core/Desktop/Tests Release 빌드 통과. 경고를 오류로 처리하는 기존 빌드 계약을 유지한다.
- 격리 프로필 `/tmp/Unfold-beta-pet-smoke-7o7Vm0`의 smoke 결과 `success=true`.
- Beta 테마·폰트의 [파일 첨부 화면](2026-10-02-beta-pet-builder/editor.png)과 [640×560 스크롤 화면](2026-10-02-beta-pet-builder/minimum.png)을 시각 확인했다.
- [진단 원본](2026-10-02-beta-pet-builder/smoke.json): 가로 넘침·스크롤·하단 고정·미리보기·팩 저장·설치 검사.
- 수정한 Beta 작업 트리의 `git diff --check` 통과.

[Beta 기준 변경 패치](2026-10-02-beta-pet-builder/pet-update.patch)도 보존했다. 이 패치는 Beta 수정 직전 상태 기준이며 0.2.2에 적용하기 위한 패치가 아니다.

## 미검증과 배포 경계

캡처는 macOS off-screen 진단이고 포인터 검사는 Avalonia headless 합성 입력이다.
물리 입력·Windows 실기·서명·공증된 새 설치 파일·공개 배포는 수행하지 않았다.
설치된 앱과 공개 v1.0.2-beta 다운로드는 이번 수정본으로 교체하지 않았다.
