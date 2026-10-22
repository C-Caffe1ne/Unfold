# 웹 문구·기능 소개 수정 결과

2026년 10월 9일. 사용자 정정을 반영해 원하는 펫과 3D(GLB) 지원을 강조하고, 제거된 별도 펫 팩 확인 미리보기 소개와 사진을 삭제했다. GPT-6 Luna가 콘텐츠와 동적 계정 문구를 나눠 수정했고, 별도 Luna가 현재 앱 소스와 캡처를 검수했다.

## 반영한 내용

- 첫 화면: **내가 고른 펫과, 잠깐 쉬어가세요.**
- 지원 형식: **이미지·영상부터 3D(GLB) 모델까지, 원하는 펫을 데스크톱에.**
- 펫 소개: **이미지·영상부터 3D(GLB)까지 / 원하는 펫을 데려와요.**
- 기능별 추가 설명문과 체크리스트를 제거했다. 화면 캡션은 화면명과 개발본 버전으로 줄였다.
- 삭제된 별도 팩 확인 화면, 저장 결과 사진, 링크와 전용 CSS를 제거했다. 사진 파일은 작업 폴더와 Git 스테이징 모두에서 제외했고 로컬 HTTP 404를 확인했다.
- 가격·FAQ·다운로드·계정·푸터·메타 문구를 검수하고 다듬었다. 계정의 조회 중·실패·권한 확인·결제 준비·Google 로그인 안내도 같은 표현으로 맞췄다. 조회 실패에서는 결제 대신 권한 재확인을 안내한다.
- 테스트 결제의 실제 청구 없음, 앱 권한과의 구분, 발급 코드가 있는 Beta 테스터 조건과 기존 약관·환불 조건은 유지했다.

## 앱과 화면 근거

최신 앱 작업본은 `/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features`, HEAD `17941dbd`, csproj 버전 `1.1.3-beta`다. 해당 작업본의 미커밋 정정은 별도 팩 확인 미리보기 창을 제거하고 홈 및 미디어·GLB 편집 화면을 유지한다. 웹에서는 현재 펫 관리 화면만 소개한다.

이미지는 기존에 성공한 격리 진단 `/private/tmp/unfold-pack-preview-correction-20261009-smoke/verification/`에서 가져왔다. `smoke.json`의 `success: true`, `timerPetPreviewRestored: true`, `packImportPreviewRemoved: true`를 확인했다. 네 테마의 홈·기록·펫 관리·휴식 알림 16개 파일이 해당 진단 캡처와 바이트 단위로 일치한다. 이번 웹 수정에서 앱 진단을 새로 실행한 것은 아니다.

앱 화면은 **Beta v1.1.3 개발본**, 공개 다운로드는 **Beta v1.1.2**로 구분해 표시한다. 실제 앱에 없는 기능을 새로 소개하지 않았다.

## 검증

- 자동 검사 76/76 통과, 사이트 검사 HTML 2개·내부 참조 98개 통과.
- 로컬 HTTP·자산 내용·기존 다운로드 주소 검사 126개 통과. 삭제한 결과 이미지 URL은 404.
- 배포 dry run 통과. 실제 업로드는 하지 않았다.
- Chromium에서 9개 화면에 대해 39개 폭·화면 조합을 확인했다. 가로 넘침과 실행 예외는 없었고, 홈 제목은 320~1440px의 7개 폭에서 모두 2줄이었다.
- 네 테마의 이미지와 확대 링크가 일치했다. 기능 추가 설명 및 삭제된 결과 이미지 DOM 참조는 0개였다.
- 외부 요청 없는 가상 계정으로 조회 실패·미보유·권한 있음의 세 상태를 확인했다. 조회 실패에는 결제가 차단되고, 권한 있음에는 재결제 버튼이 숨겨진다. 390px에서 세 상태 모두 가로 넘침이 없었다.
- 전체 홈페이지 캡처는 지연 로딩 이미지를 읽고 동작 줄이기를 켜서 저장했다. 실제 OAuth·코드 등록·결제·모바일 실기기 검증은 수행하지 않았다.

## 확인 자료

- [로컬 미리보기](http://127.0.0.1:4174/)
- [데스크톱 첫 화면](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/home-desktop.png), [모바일 첫 화면](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/home-mobile.png)
- [데스크톱 기능 소개](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/features-desktop.png), [모바일 기능 소개](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/features-mobile.png), [홈 전체](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/home-full.png)
- [브라우저 실측](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/browser-results.json), [가상 계정 결과](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/account-results.json), [자동 검사 로그](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/tests.log)
- [웹 소스](/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/unfold-web-ui/public/index.html)

작업본은 `/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/unfold-web-ui`, `codex/web-ui-ux-review`다. 기존 통합·미커밋 변경을 보존했다. 커밋·푸시·실제 배포는 하지 않았다.
