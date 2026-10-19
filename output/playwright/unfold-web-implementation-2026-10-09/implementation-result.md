# Unfold 웹 UI·UX 구현 결과

2026년 10월 9일 후속 정정: 아래 초기 구현의 펫 팩 미리보기 소개·결과 사진과 기능별 설명문은 제거했다. 원하는 펫과 3D(GLB) 지원을 강조하고 현재 앱 캡처로 교체한 [최신 수정 결과](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-copy-2026-10-09/copy-result.md)를 따른다.

2026년 10월 9일. 보고서의 변경을 별도 작업본에서 구현하고 로컬 검증을 마쳤다. GPT-6 Luna 3개가 콘텐츠·스타일·인터랙션을 나누어 구현했고, 별도 Luna가 코드와 실제 캡처를 검토했다. 원래 웹 폴더는 그대로 보존했다. 커밋·푸시·실제 배포는 수행하지 않았다.

## 작업본과 기준

- 작업본: `/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/unfold-web-ui`
- 브랜치: `codex/web-ui-ux-review`
- 원격 웹 `1bb6fb9`와 로컬 `6902da6`의 두 미배포 커밋을 보존해 통합했다. merge는 commit 전 상태다.
- 공개 HTML·CSS·JS 8개는 `1bb6fb9`와 일치했다. 공개 앱 자료는 v1.1.2, 소스 `ce86c7b`를 기준으로 했다. 개발본 v1.1.3 화면은 사용하지 않았다.
- [로컬 미리보기](http://127.0.0.1:4174/)는 실제 배포 구성의 로컬 Wrangler 서버다. Google 인증은 로컬에서 비활성화된다.

## 반영한 변경

| 보고서 항목 | 결과 |
|---|---|
| F01·F02·F05 | 홈·가격·다운로드에 Beta 코드 조건과 Test 결제의 범위를 표시했다. 로그인 통합·약관·환불 페이지를 보존하고 정식 판매 전 초안임을 구분했다. 앱 재실행의 세션 복원 안내도 고쳤다. |
| F03 | 네 테마의 홈·기록·펫 관리·알림 캡처를 v1.1.2로 교체했다. 실제 저장된 GIF 펫 미리보기를 추가했다. 행동 수를 단정하지 않고 2D 파일과 GLB 애니메이션의 연결 방식으로 설명한다. |
| F04 | 모바일 메뉴에서 계정 창을 닫으면 보이는 메뉴 토글에 포커스를 복원한다. Escape·닫기 버튼·바깥 클릭·가상 로그인 후 자동 닫힘에서 확인했다. |
| F06 | 휴식 알림 시연을 넓히고 편집 화면과 완성 결과를 함께 배치했다. 이미지 reveal과 데스크톱의 짧은 pin을 추가했다. 모바일·동작 줄이기·JavaScript 비활성화에서도 본문을 읽을 수 있다. |
| F07·F08 | 가격 상세 조건 링크와 계정 상태별 안내·다음 행동을 추가했다. 조회 실패에서는 결제를 차단하고, Test 권한이 있으면 다시 결제를 권하지 않고 다운로드·이용 조건을 보여준다. |
| F09·F10 | 작은 조작 영역을 44px 이상으로 늘리고 마지막 FAQ의 빈 셀을 없앴다. 4,900원은 세금 별도 Test 기준가로 일관되게 표시한다. |

## 검증

| 범위 | 결과 |
|---|---|
| Node 검사 | 76/76 통과 |
| 사이트 검사 | HTML 2개, 내부 참조 100개, ARIA·이미지 비율 통과 |
| 로컬 라우트 | HTTP·파일 내용·39개 다운로드 주소·내부 파일 비노출을 포함한 127개 검사 통과 |
| 배포 검사 | `WRANGLER_WRITE_LOGS=false npm run deploy:check` dry run 통과. 실제 업로드 없음 |
| 반응형 | 홈·가격·다운로드 × 7개 폭, 21개 조합 모두 가로 넘침 없음. Hero 2줄, 마지막 FAQ 전체 폭 |
| 입력·상태 | 모바일 닫기 4종, 가상 조회 실패·미보유·해제·권한 있음·긴 이메일, 확대 포커스, OS 탭 키보드·새로고침 통과 |
| 테마·모션 | 네 테마의 이미지·확대 링크 일치. pin은 홈 이탈 0개·재진입 1개·동작 줄이기 0개. JavaScript 없이 본문 표시 |
| 독립 QA | 잘못된 행동 수 문구를 발견해 수정했다. 수정 후 코드·캡처에서 추가 blocker를 찾지 못했다. |

앱 캡처는 10월 8일 공개 릴리스의 격리 SmokeDiagnostics 결과를 재사용했다. 이번 새 격리 빌드는 완료했으나 Native 렌더 타이머 오류로 캡처 재생성에 실패했다. 이전 성공 결과를 이번 새 실행 성공으로 기록하지 않았다. 전체 페이지 캡처는 지연 로딩 이미지를 모두 읽고 동작 줄이기를 켜서 저장했다.

실제 OAuth·코드 등록·결제·모바일 Safari·VoiceOver는 미검증이다. 가상 계정 QA는 실제 Google·Supabase·Lemon Squeezy 주문을 호출하지 않았다. 기존 Wrangler 개발 의존성 감사의 high 4건은 이번 UI 수정과 별도로 기록했다. 해당 개발 도구는 public 배포 자산에 포함되지 않는다.

## 검토 자료

- [데스크톱 첫 화면](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-implementation-2026-10-09/after-home-desktop.png), [모바일 첫 화면](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-implementation-2026-10-09/after-home-mobile.png), [전체 홈페이지](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-implementation-2026-10-09/after-home-full.png)
- [가격·FAQ](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-implementation-2026-10-09/after-pricing-desktop.png), [모바일 로그인](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-implementation-2026-10-09/after-login-mobile.png), [Test 권한 확인 후](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-implementation-2026-10-09/after-account-active.png), [알림 시연](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-implementation-2026-10-09/after-demo-pinned.png)
- [반응형 실측](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-implementation-2026-10-09/responsive-metrics.json), [사용자 흐름](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-implementation-2026-10-09/user-flow-results.json), [테마·모션](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-implementation-2026-10-09/theme-motion-results.json), [공개 기준](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-implementation-2026-10-09/public-baseline.json)
- [홈페이지 소스](/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/unfold-web-ui/public/index.html), [파일 소유권·구현 기록](/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/unfold-web-ui/docs/ui-ux-implementation.md), [검증 기록](/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/unfold-web-ui/docs/verification.md)

운영 배포는 별도 요청 때 이 작업본의 통합 변경을 검토해 수행한다. 원래 main으로 무심코 푸시하면 기존 자동 배포가 실행되므로, 현재 작업본을 배포된 상태로 보지 않는다.
