# Unfold 웹사이트 재검수와 작업 계획

검수일: 2026년 10월 9일, 한국 시간. 기존 브랜드와 2줄 Hero는 유지하고, 이용 조건과 제품 시연을 먼저 보완하는 방향을 권한다. 모바일 계정 창의 포커스 복원은 수정이 필요한 동작이다. 웹 Test 결제와 앱 이용 권한의 연결은 정식 판매 전에 확인해야 한다.

이번 작업은 감사와 계획이다. 웹·앱 소스 수정, 브랜치 병합, 커밋, 푸시, 배포는 수행하지 않았다. GPT-6 Luna 서브에이전트 3개가 사용자 흐름, 접근성, 실제 캡처의 시각 구성을 나누어 검수했다.

## 기준 소스와 공개 상태

| 대상 | 확인한 상태 | 계획에 적용할 기준 |
|---|---|---|
| 공개 사이트 | [unfoldpet.dokhustudio.com](https://unfoldpet.dokhustudio.com), 다운로드 Beta v1.1.2 | 실제 방문자가 보는 화면 |
| 원격 웹 main | `1bb6fb90982fff571bb1c0117f8cb1d28a83dc4e` | 최신 웹 기반 |
| 공개 파일 | HTML·CSS·JS 8개가 원격 소스와 SHA-256 일치 | 배포 불일치는 현재 발견 사항에서 제외 |
| 로컬 웹 main | `/Users/hwanghyeonseong/Documents/GitHub/Dokhustudio/unfoldpet-web`, `6902da6`, clean | 로그인 통합·약관 추가 커밋 2개 보존 |
| 로컬과 원격 관계 | `6902da6...1bb6fb9`의 left/right count `2 / 3` | 로컬을 최신 원격으로 덮어쓰지 않고 별도 작업본에서 통합 |
| 다른 웹 작업본 | `/private/tmp/unfold-web-release110`, `a66a6ef`, `codex/beta-v1.1.0-web` | 이전 릴리스 작업본 |
| 앱 최신 공개 배포 | [Beta v1.1.2](https://github.com/C-Caffe1ne/Unfold/releases/tag/v1.1.2-beta), 소스 `ce86c7b` | 공개 제품 설명과 새 캡처의 기준 |
| 앱 최신 개발본 | `/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features`, `95e7ed2`, `1.1.3-beta`, clean | 미배포 기능을 공개 기능으로 소개하지 않음 |

감사용 원격 사본은 `/private/tmp/unfold-web-audit-20261009`의 detached `1bb6fb9`이다. 앱 후보도 비교했다. 현재 대화 경로의 `release/mvp / ce0ccda / 0.2.2`는 이전 소스이며, `codex/glb-import-compat / bbdb4a8 / 1.1.1-beta`보다 최신 개발본은 `codex/remove-unused-features`다. 비교 결과는 `bbdb4a8...95e7ed2`에서 `0 / 15`였다. 사용자가 수정한 AGENTS.md와 다른 작업본의 문서 변경을 보존했다.

공개 다운로드를 직접 확인한 결과 v1.1.2 표시와 링크가 맞았고, v1.1.1과 v1.1.2 Mac 다운로드 주소는 각각 해당 GitHub ZIP으로 302 응답했다. 로컬 v1.1.1 표기를 공개 사이트의 오류로 분류하지 않는다.

## 유지할 구성

- Hero 제목은 320·375·390·768·1050·1100·1440px에서 모두 2줄이었다. 같은 폭에서 홈의 가로 넘침은 없었다.
- 라일락 브랜드, 눈누 기초고딕, 실제 Mochi 자산이 제품과 일관된다. 폰트와 브랜드를 교체할 근거는 없다.
- 제목, 설명, 두 CTA, 제품 화면의 순서는 데스크톱과 모바일에서 읽기 쉽다.
- 네 테마를 실제로 선택했을 때 앱 이미지와 확대 링크가 함께 바뀌었다.
- 동작 줄이기를 켜면 펫은 정지 이미지로 시작하고 스크롤은 즉시 이동한다.
- 헤더의 기능·테마·가격은 홈 섹션으로 이동한다. 이 흐름과 새로고침 시 상단 복귀는 기존 사용자 요청에 맞춰 유지한다.

## 수정과 보완 항목

P1은 실제 이용 조건 또는 구매 판단에 영향을 주는 항목, P2는 탐색과 제품 이해를 방해하는 항목, P3는 사용 편의와 시각 완성도를 높이는 제안이다. 사용자 반응이나 전환율은 측정하지 않았다.

| ID | 우선순위·종류 | 근거와 영향 | 수정 방향 |
|---|---|---|---|
| F01 | P1, 출시 계약 확인 | 웹은 `test`만 허용하고 앱 v1.1.2 소스는 `Live` 설정을 요구한다. 앱 소스에는 로그인 후 코드 등록과 Live 권한 재조회 경로도 있다. 같은 Google 계정이라는 사실만으로 두 환경의 이용 권한이 같은 것은 아니다. 실제 코드 등록이나 웹 주문에서 앱 사용까지는 이번에 검증하지 않았다. | 웹 Test 확인과 앱 Live 이용 조건을 구분한다. 코드 등록은 소스에서 확인된 경로로 안내하되 실제 코드 발급·사용 결과는 별도 확인한다. Test 결제를 앱 사용 권한으로 표현하지 않고, Live 판매는 연결·종단 검증 후 노출한다. |
| F02 | P1, 미배포 변경의 통합 위험 | 로컬 `6902da6`은 홈페이지를 `구매하기`와 정식 판매 문구로 바꿨지만 계정 UI·서비스는 Test다. 로컬 FAQ에는 앱을 켤 때마다 Google 로그인이 필요하다고 쓰여 있으나 공개 앱에는 저장 세션 복원이 있다. | 원격 최신 버전 변경과 로컬 로그인 통합·약관을 보존해 통합한다. 구매 버튼, 권한 조건, 로그인 복원 안내를 실제 환경에 맞춘다. |
| F03 | P2, 확인된 정보 누락·오래된 시연 | 공개 기능 소개는 GIF·MP4와 최대 다섯 행동을 강조하고, GLB는 변경 이력에만 나온다. 캡처는 README에 v1.0.1 기반으로 기록되어 있으며, 펫 만들기 화면은 이전 탭 UI다. | v1.1.2의 펫 관리·GLB·이미지 지원을 현재 계약과 대조해 소개한다. 지원 형식별 행동 차이는 숫자 하나로 묶지 않는다. 네 테마의 캡처를 같은 공개 버전에서 다시 만든다. |
| F04 | P2, 브라우저 재현 | 모바일 메뉴에서 계정 창을 열고 Escape로 닫으면 `document.activeElement`가 `BODY`다. 메뉴가 먼저 닫히는 이벤트 순서 때문에 복귀할 조작 요소를 잃는다. 공개·로컬에서 재현했다. | 실제 열기 요소를 저장하고, 닫힌 메뉴에서 열었으면 보이는 메뉴 토글에 포커스를 복원한다. 닫기 버튼·Escape·바깥 클릭도 같은 규칙으로 처리한다. |
| F05 | P2, 사용 조건 발견성 | 다운로드 화면에는 테스터 코드가 필요하다고 쓰지만 홈 Hero에서는 지원 OS만 보인다. 코드를 갖고 있지 않은 방문자가 사용 가능한지 판단하기 어렵다. | 홈의 다운로드 CTA 가까이에 짧은 Beta 이용 조건을 둔다. 코드가 없는 사람에게 안내할 경로는 운영자가 정한 실제 절차만 사용한다. 다운로드 페이지의 간결한 구성은 유지한다. |
| F06 | P2, 시각 개선 제안 | 좌우로 바뀌는 앱 캡처 구성과 축소된 내부 글자가 반복된다. 빈 펫 제작 화면은 완성 후 결과를 보여주지 못한다. | 기존 기능 구간 중 한 곳을 실제 알림·휴식 반응을 보여주는 넓은 시연으로 보완한다. 펫 제작은 완성된 펫과 핵심 조작 확대를 함께 보여준다. 삭제했던 별도 사용법 섹션을 자동 복원하지 않는다. |
| F07 | P2, 구매 정보 발견성 | 홈 가격 카드의 두 행동은 테스트 구매와 Beta 다운로드다. Test 범위, 세금, 이용 조건의 상세 FAQ로 바로 가는 링크가 없다. | 카드 아래에 `구매·이용 조건` 같은 간결한 상세 링크를 둔다. 헤더의 홈 가격 섹션 이동은 유지한다. |
| F08 | P2, 상태별 다음 행동 제안 | 가상 QA에서 이용 권한 확인 후에도 `결제 후 이용 권한을 다시 확인해 주세요`가 남는다. 이미 권한이 있는 상태에서도 다운로드로 이어지는 행동이 없다. 원격 렌더링 코드에도 같은 구조가 있다. | 조회 중·오류·권한 없음·Test 권한 있음에 맞춰 안내를 바꾼다. Test 권한을 실제 앱 사용 권한으로 표현하지 않는다. Beta 다운로드와 사용 조건을 다음 행동으로 제공한다. |
| F09 | P3, 터치 편의 제안 | 메뉴 42×42px, 소셜 링크 40×40px, 계정 닫기 36×36px, 움직임 버튼 높이 36px다. | 실제 조작 영역을 44px 이상으로 조정하고 320px에서 헤더 밀림·겹침을 확인한다. 이 수치만으로 WCAG 위반이라고 단정하지 않는다. |
| F10 | P3, 가격 표기 통일 | 홈 카드의 4,900원과 상세 페이지의 세금 별도 조건이 떨어져 있다. 현재 공개 구매는 Test다. | 가격 바로 아래에 Test 상태·예정가·세금 조건을 일관되게 표시한다. 실제 판매 금액과 약관은 운영 기준이 확정된 값만 쓴다. |

F03의 새 캡처는 앱을 격리 데이터로 실행한 실제 화면이어야 한다. 정적인 홍보 이미지에 기능을 추가해 실제 UI처럼 보이게 만들지 않는다. 사용자 휴식 기록과 개인 계정 정보는 캡처에 넣지 않는다.

현재 공개 FAQ는 카드 4개여서 2열 그리드가 채워져 있다. 로컬 미배포 FAQ는 5개여서 마지막 오른쪽이 비어 있다. 통합 후에는 마지막 카드를 두 열에 걸치게 하거나 한 열의 질문 목록으로 바꾼다. `grid-auto-flow: dense`만 추가해도 5개 카드의 빈 셀이 사라지는 것은 아니다.

Hero의 고양이는 제품 개성을 만드는 자산이다. 현재 프레임과 캡션을 가로지르는 위치와 작은 재생 버튼의 정렬은 다듬을 수 있지만, 이를 기능 결함으로 분류하지 않는다.

## 목표 사용자 흐름

| 방문 목적 | 목표 흐름 | 완료 조건 |
|---|---|---|
| 처음 보는 방문자 | 제품의 휴식 알림 확인, 실제 시연, 지원 OS와 Beta 조건 확인, 다운로드 | 다운로드 전에 앱 로그인·Live 이용 권한 조건과 실제 코드 발급 절차를 이해할 수 있음 |
| 기존 테스터 | OS·칩 선택, 다운로드, 앱의 Google 로그인·권한 복원 또는 코드 등록 | 최초 로그인과 재실행 안내가 현재 앱 소스와 일치. 실제 코드 등록 성공은 별도 검증 결과로 기록 |
| 펫을 직접 만들고 싶은 사람 | 완성 결과 확인, 지원 형식 확인, 해당 기능 시연, 다운로드 | GLB와 2D 미디어의 지원 범위를 오해하지 않음 |
| Test 결제를 확인하는 사람 | Test 범위 확인, Google 로그인, 링크 준비, 별도 결제창, 서버 권한 재확인 | 실제 청구·앱 Live 권한과의 차이가 보임 |
| 이미 권한을 확인한 사람 | 상태 안내, Beta 다운로드 또는 사용 조건으로 이동 | 같은 결제를 반복하라고 읽히는 문구가 없음 |
| 오류를 만난 사람 | 네트워크·권한 조회 실패 안내, 재시도 또는 닫기 | 조회 실패를 미구매로 표시하지 않고 결제 생성을 차단 |

정식 판매 흐름의 완료 조건은 웹 Live 구매, 웹훅 반영, 앱 Live 권한 조회, 재실행 복원까지의 확인이다. 이번 UI 개선에서 설정값 하나를 바꾸는 작업으로 취급하지 않는다.

## 디자인 적용 방향

Hero는 현재 두 줄과 넓은 제품 화면을 유지한다. 새 문구는 혜택과 Beta 조건을 짧게 전달하고, 둘러보기와 다운로드의 우선순위는 실제 시연을 만든 뒤 정한다. 버튼 색만 바꾸기 전에 각 버튼의 도착점과 기대 결과를 맞춘다.

기능 구간은 기존 세 항목을 재구성한다. 실제 휴식 알림, 완성된 커스텀 펫, 휴식 기록의 순서와 이미지 크기를 검토하고, 하나의 넓은 시연으로 반복되는 좌우 패턴을 줄인다. 모든 화면을 카드로 쪼개거나 근거 없는 고객 후기·수치를 추가하지 않는다.

`gpt-taste`의 Hero 줄 수, 밀도 있는 구성, CTA 대비와 섹션 호흡을 적용한다. 추가 모션은 제품 시연의 이해를 돕는 범위에서만 설계한다. GSAP 시안은 핵심 이미지의 scale reveal과 데스크톱 시연 제목의 짧은 pin 구간을 후보로 둔다. 모바일과 동작 줄이기에서는 일반 문서 흐름을 사용하고, JavaScript가 없어도 본문과 CTA가 보이게 한다. 첫 화면과 결제 조작에는 스크롤에 따른 숨김을 적용하지 않는다.

코드 작성 전 시안 검토에서 `gpt-taste`의 design_plan을 작성한다. 기존 한국어 브랜드 글꼴과 실제 펫 자산을 유지하는 조건을 먼저 명시한다. 이번 감사에서는 UI 코드를 작성하지 않았다.

## GPT-6 Luna 작업 배치

상위 감독자가 기준 버전, 상태별 문구, DOM ID·클래스·data 속성 계약을 먼저 고정한다. 동시에 작업하는 Luna는 최대 3개다. 감독자까지 포함해 4개 실행 슬롯을 사용한다. 각 작업자는 지정한 파일만 수정하며 다른 작업자의 변경을 되돌리지 않는다.

| 작업자 | 모델·권장 추론 | 파일 소유권 | 주요 책임 |
|---|---|---|---|
| Luna A, 콘텐츠·제품 자료 | `gpt-6-luna`, medium | `public/index.html`, `public/_redirects`, `public/assets/themes/**`, `tests/download.test.cjs`, `tests/deployment.test.cjs`, 관련 다운로드·캡처 문서 | 이용 조건, 기능 소개, FAQ, 기존 두 커밋 보존, 새 캡처 적용 |
| Luna B, 시각·반응형 | `gpt-6-luna`, medium | `public/styles/main.css`, `pages.css`, `account.css` | 시연 레이아웃, 캡처 크기, FAQ 배치, 작은 화면과 터치 영역 |
| Luna C, 인터랙션·상태 | `gpt-6-luna`, high | `public/scripts/main.js`, `router.js`, `account-ui.js`, `theme.js`, 선택한 모션 파일, 해당 JS 회귀 검사 | 모바일 포커스, 상태별 다음 행동, 시연 조작과 모션 수명 관리 |
| 독립 Luna QA, 후속 단계 | `gpt-6-luna`, high | 구현 파일 수정 없음 | 통합된 실제 화면·흐름 재검수, 근거와 남은 오류 보고 |

HTML은 Luna A만 수정한다. B와 C가 새 클래스나 버튼을 요구하면 먼저 DOM 계약을 합의한 뒤 A에게 요청한다. QA는 구현자가 제출한 검사 결과를 읽고 실제 화면을 별도로 확인한다. QA에서 수정이 필요하면 소유 작업자가 고친다.

각 Luna 프롬프트에는 기준 commit, 허용 파일, 해당 F 번호, 완료 조건, 실행할 검사만 준다. 전체 대화와 관계없는 앱 소스는 넘기지 않는다. 결과에는 수정 파일, 실제 실행한 검사, 브라우저 증거, 남은 제한을 포함한다.

## 실행 순서와 완료 조건

| 순서 | 작업 | 담당·의존성 | 완료 조건 |
|---|---|---|---|
| S0 | 최신 기준을 다시 조회하고 독립 작업본 준비 | 감독자 | 원격 main·공개 파일·앱 릴리스가 일치하는 기준 기록. 로컬 두 커밋 보존. 기존 작업 폴더 clean 상태 유지 |
| S1 | 원격 최신과 로컬 로그인 통합·약관 변경을 검토해 통합 | 감독자·A, S0 후 | v1.1.2 링크·변경 이력 유지. 로그인 통합 유지. Test 문구와 정식 판매 문구가 섞이지 않음. 기존 커밋을 원래 폴더에서 변경하지 않음 |
| S2 | Beta·Test·향후 Live 상태와 DOM 계약 확정 | 감독자·A·C, S1 후 | 앱 Live 권한 경로와 웹 Test 상태를 구분한 설명·버튼·목적지 합의. 코드 발급 절차와 실제 등록 검증 상태, 정식 판매 정책의 미정 항목 기록. S4에 확정 문구 전달 |
| S3 | 모바일 모달 포커스와 상태 안내 수정 | C, S2 후 | 메뉴에서 열어 Escape·닫기·바깥 클릭 후 보이는 메뉴 토글로 복귀. 조회 실패에서 결제 차단. active 상태에 맞는 안내 |
| S4 | 최신 공개 앱의 시연 자료와 기능 문구 갱신 | A, S2 후, S3와 병렬 가능 | 격리된 v1.1.2 데이터로 네 테마 캡처. 지원 형식·캡션·확대 원본 일치. 새 기능을 미배포 앱에서 가져오지 않음 |
| S5 | 레이아웃·터치 영역과 선택한 시연 모션 적용 | B·C, DOM 계약 및 자료 확정 후 | 320~1440px 가로 넘침 없음. 지정한 7개 폭에서 Hero 2줄 유지. FAQ 빈 셀·이미지 찌그러짐 없음. 44px 조작 영역이 주변 요소와 겹치지 않음 |
| S6 | 독립 통합 QA와 발견 회귀 수정 | Luna QA, S3~S5 후 | 아래 검사와 흐름 통과. 실패는 담당자가 수정하고 영향 범위만 재검사 |
| S7 | 배포용 변경 검토 | 감독자, S6 후 | 사용자에게 diff와 전후 캡처 제시. 실제 푸시·배포는 별도 요청 범위에서 수행. 배포 후 공개 해시·라우트·리다이렉트 재확인 |

S3과 S4는 함께 진행할 수 있다. 세 작업자가 `index.html`을 동시에 편집하거나 상태 계약이 미정인 채 UI를 따로 만드는 방식은 사용하지 않는다.

## 구현 후 검사

- 저장소 지정 검사: `npm test`, `npm run check:site`, `npm run deploy:check`.
- 필요한 라우트 검사: 로컬 서버에서 `npm run check:routes`. 레거시 화면·다운로드·내부 파일 비노출 확인.
- 화면 폭: 320·375·390·768·1050·1100·1440px. 홈 외에 가격·다운로드·모달·긴 문구도 확인.
- 입력: 모바일 메뉴, Tab·Shift+Tab·Escape, OS 탭 화살표·Home·End, 이미지 확대와 복원.
- 상태: 비로그인, 로그인 완료, 권한 조회 중, 조회 실패, 미보유, Test 권한 있음, 해제됨, 연결 재시도, 긴 이메일.
- 시연: 네 테마, normal/reduced motion, 화면 이동·재진입 후 중복 이벤트나 ScrollTrigger가 남지 않는지 확인.
- 콘텐츠: 표시 가격·세금 조건·판매 상태·앱 로그인 복원·코드 사용·지원 형식·캡처 버전 교차 확인.

실제 OAuth·코드 등록·결제는 가상 QA나 소스 경로 확인만으로 성공했다고 보고하지 않는다. 종단 검증을 수행할 때는 Test와 Live를 구별하고 실제 결제 상태·앱 권한을 각각 확인한다. VoiceOver와 실제 모바일 기기 검수는 별도의 확인 항목이다.

## 이번에 확보한 검증

| 범위 | 결과 | 한계 |
|---|---|---|
| 원격 `1bb6fb9` 자동 검사 | `npm test` 71 통과, `verify-site.py` 84 로컬 참조 통과 | 정적 계약과 단위 검사 |
| 로컬 `6902da6` 자동 검사 | `npm test` 71 통과, `verify-site.py` 91 로컬 참조 통과 | 미배포 UI가 실제 판매 기능이라는 증거는 아님 |
| 공개 배포 일치 | HTML·CSS·JS 8개 해시 일치 | 전체 자산·서버 설정 감사는 아님 |
| 공개 반응형 | 7개 폭의 홈에서 가로 넘침 없음, Hero 2줄 | Chromium viewport 검사, 실제 모바일 Safari 미검증 |
| 테마·모션 | 네 테마 이미지/링크 일치, reduced motion 초기 정지와 즉시 스크롤 | 전체 인터랙션 성능 측정은 하지 않음 |
| 모바일 계정 창 | Escape 후 포커스 BODY 재현 | 닫기 버튼·바깥 클릭은 구현 후 추가 검사 필요 |
| 로컬 가상 계정 | Test 구매 진입, 가상 링크 준비, 가상 권한 확인 | 실제 Google·Supabase·주문·웹훅 호출 없음 |
| 다운로드 | 공개 v1.1.2 표시, 두 버전 Mac 주소 정확한 302 | 설치 ZIP 전체 다운로드·해시 검사는 이번에 수행하지 않음 |
| 콘솔 | 검수 세션에서 오류 0, 폰트 preload 미사용 경고 2 | 실제 글꼴 로딩·초기 성능은 추가 측정 가능 |

새 검증은 웹과 앱의 변경 영역에 맞게 선택한다. 통과한 검사를 새 변경이나 실패 근거 없이 반복하지 않는다.

## 주요 근거와 캡처

- 공개 파일 비교: [source-public-comparison.json](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-audit-2026-10-09/source-public-comparison.json)
- 공개 화면 실측: [public-responsive-metrics.txt](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-audit-2026-10-09/public-responsive-metrics.txt)
- 모바일 포커스: [public-mobile-focus.json](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-audit-2026-10-09/public-mobile-focus.json)
- 공개 홈페이지: [데스크톱](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-audit-2026-10-09/public-home-desktop.png), [모바일](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-audit-2026-10-09/public-home-mobile.png), [전체](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-audit-2026-10-09/public-home-full.png)
- 공개 상세 화면: [가격](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-audit-2026-10-09/public-pricing-full.png), [다운로드](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-audit-2026-10-09/public-download-mobile.png), [로그인](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-audit-2026-10-09/public-login-mobile.png)
- 로컬 미배포 변경: [로그인 통합](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-audit-2026-10-09/local-login-mobile.png), [Test 구매 상태](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-audit-2026-10-09/local-purchase-test.png), [5개 FAQ 배치](/Users/hwanghyeonseong/Documents/GitHub/Unfold/output/playwright/unfold-web-audit-2026-10-09/local-pricing-full.png)
- 원격 기능·캡처 문구: [index.html:88](/private/tmp/unfold-web-audit-20261009/public/index.html:88), [README.md:15](/private/tmp/unfold-web-audit-20261009/README.md:15)
- Test 설정 계약: [account-service.mjs:6](/private/tmp/unfold-web-audit-20261009/public/scripts/account-service.mjs:6)
- 모달 포커스 복원: [account-ui.js:110](/private/tmp/unfold-web-audit-20261009/public/scripts/account-ui.js:110), [main.js:45](/private/tmp/unfold-web-audit-20261009/public/scripts/main.js:45)
- 앱 공개 버전의 Live 설정과 로그인 복원: [공개 소스 DesktopAccountService.cs](https://github.com/C-Caffe1ne/Unfold/blob/ce86c7b/src/Unfold.Desktop/DesktopAccountService.cs), [공개 소스 AppRuntime.Account.cs](https://github.com/C-Caffe1ne/Unfold/blob/ce86c7b/src/Unfold.Desktop/AppRuntime.Account.cs)
- 앱 공개 버전의 코드 등록과 권한 재조회: [AccountScreenModel.cs](https://github.com/C-Caffe1ne/Unfold/blob/ce86c7b/src/Unfold.Desktop/AccountScreenModel.cs#L189-L213), [SupabaseAccountClient.cs](https://github.com/C-Caffe1ne/Unfold/blob/ce86c7b/src/Unfold.Core/SupabaseAccountClient.cs#L145-L163)
- 최신 앱 문서: [docs/README.md:3](/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features/docs/README.md:3), [cross-platform.md:12](/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features/docs/cross-platform.md:12)
