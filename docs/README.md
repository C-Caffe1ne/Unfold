# 문서 안내

v1.2.1 정식 배포: [릴리스 노트](releases/v1.2.1.md) · [배포 검증](validation/2026-10-20-v1.2.1-release.md). 이전 v1.2.0: [릴리스 노트](releases/v1.2.0.md) · [배포 검증](validation/2026-10-15-v1.2.0-release.md).

백엔드 관리: [보안 운영 절차](operations/backend-security.md), [적용·검증 결과](validation/2026-10-11-backend-security.md).
운영 Live 결제의 pending 원인·웹훅 v21 배포·실제 결제 재전송과 앱 권한 조회 결과는 [Live 결제 복구](validation/2026-10-12-live-payment-pending.md)에 기록한다.

새 세션·다른 IDE에서는 **[작업 인계](HANDOFF.md)**부터 읽는다. 기준 경로, 로컬에만 있는 커밋, 실행·저장 계약, 검증 근거와 다음 확인 순서를 모았다.

현재 제품은 **C#·Avalonia 기반 Unfold v1.2.1**이다. 2026-10-20 동료 펫 P1 작업의 시작 소스는 `1.2.1`, HEAD `2de478a`이며, 작업 경로는 `/Users/hwanghyeonseong/Developer/GitHub/Unfold/.worktrees/pet-overlay-hover`, 브랜치는 `codex/pet-overlay-hover`다.
기록된 최신 공개 배포본은 `v1.2.1` (제품 소스 `09c9e72`)이다. 배포 검사와 실제 OS·업데이트 미검증 범위는 [배포 검증](validation/2026-10-20-v1.2.1-release.md)에 있다.
[동료 펫 P1 실행 계획](plans/2026-10-20-companion-p1.md)의 S1 펫별 크기·즐겨찾기, S2 로컬 라이브러리, S3 선택형 CPU·메모리 정보, S4 미니바 빠른 메뉴를 구현했다. 구현 단계의 Release 빌드와 214개 검사, 후속 포커스 수정의 8개 검사가 통과했다. [실제 macOS QA](validation/2026-10-20-companion-p1-macos.md)와 [포커스 수정·재검증](validation/2026-10-21-favorite-focus.md)까지 완료해 관련 변경을 로컬 커밋으로 정리했다. 버전은 `1.2.1`이며 미푸시·미배포 상태다. Windows·잠자기·장시간 사용은 미검증이다.

## 현재 사용 안내

| 문서 | 확인할 내용 |
|---|---|
| [작업 인계](HANDOFF.md), [10월 14일 인계 정리 이력](validation/2026-10-14-handoff-refresh.md) | 최신 소스·배포·실행 앱의 구분, IDE 재개, 보존할 변경과 문서 검증 |
| [MVP 범위](mvp.md) | 현재 기능·제외 범위와 이전 데이터 호환 |
| [설정·타이머](settings-ui.md), [말풍선·효과음](stretch-notifications.md) | 설정·홈 시간 자동 저장·대사·불투명도·자동 배치 |
| [펫 관리](pet-packs.md), [GLB 펫](glb-pets.md) | 통합 편집·설치·동작 설정·입력 제한 |
| [계정](account-screen.md), [이용 코드](access-codes.md) | OS 보안 저장·로그인 복원·구매/무료 권한 |
| [설치·업데이트](cross-platform.md), [공개 릴리스 노트](releases/v1.2.1.md) | 실제 배포 파일·아키텍처·서명·업데이트 방법 |
| [검증 안내](verification.md), [배포 체크리스트](release-checklist.md) | 기록된 검사·OS별 한계와 새 설치본 검사 절차 |
| [개발 계획](development-plan.md) | 구현된 MVP, 현재 승인된 후속 작업, 과거 계획·검증의 구분 |
| [동료 펫 P1 실행 계획](plans/2026-10-20-companion-p1.md) | S1~S4 순서·소유 모듈·수용 기준·집중검사와 OS 미검증 범위 |
| [동료 펫 P1 구현·검증](validation/2026-10-20-companion-p1.md) | S1~S4 완료, 최종 집중 214개·Release 빌드, 화면 증거와 실제 OS 미검증 범위 |
| [P1 실제 macOS QA](validation/2026-10-20-companion-p1-macos.md) | 격리 앱의 클릭·키보드·초안·네 모서리 확인과 발견한 포커스 결함 |
| [즐겨찾기 포커스 수정](validation/2026-10-21-favorite-focus.md) | 카드 유지·제거·빈 목록 포커스, 자동 8개·실제 macOS 재검증, 로그 격리 보완 |
| [문서 최신화 결과](validation/2026-10-08-docs-refresh.md) | 코드 대조·링크 복구·보존 범위 |

## 구현·검증 이력

2026-10-07 메모리 최적화의 기준은 미사용 기능 제거·로그인 복원 수정을 포함한 `codex/remove-unused-features`의 `1.1.1-beta`, 커밋 `a8fbd5b`다. 최적화와 실제 macOS 측정은 같은 작업트리에서 이어서 수행했다. 이후 작업 기준은 전체 작업트리와 배포 태그에서 다시 확인한다. `release/mvp`의 `0.2.2`는 이전 소스다.
기능 구현 전에 [작업 안내](../AGENTS.md)에 따라 마지막 수정본의 브랜치·HEAD·미커밋 변경·프로젝트 버전과 실행 경로를 다시 확인한다. 아래 문서는 구현·범위·실행 방법을 설명하며 과거 Swift 구현과 Garden 실험은 보관 자료다.

| 문서 | 역할 |
|---|---|
| [Lemon 스토어 직접 구매 → 권한 연결](validation/2026-10-20-lemon-store-purchase.md) | 구매자 이메일의 확인된 Google 계정에 연결, 52개 검사·운영 상품 모의, 운영 마이그레이션·웹훅 v22 적용과 사후 확인 |
| [Lemon 결제 → 앱 권한 확인](validation/2026-10-20-lemon-entitlement-check.md) | 실제 결제 없이 운영 함수·DB 일치, USD 세 가지 세금 방식의 권한 반영, 스토어 직접 구매 시 권한 미부여 위험 |
| [v1.2.1 정식 배포](validation/2026-10-20-v1.2.1-release.md) | 서명·공증, 공개 자산 16개·업데이트 피드, Windows CI 제목 표시줄 결과, 웹·노션 패치노트와 미검증 범위 |
| [macOS 신호등 네이티브 배치](validation/2026-10-20-macos-native-title-bar.md) | 실행 파일 SDK 표기로 AppKit 현재 배치(32pt·14×14·x=9/32/55) 적용, 1,107개 검사와 펫 창 비교 |
| [일반 창 네이티브 크롬](validation/2026-10-19-native-window-chrome.md) | macOS 계정·업데이트 창 신호등, Windows 11 제목 표시줄 테마 색·제목 숨김, 1,106개 검사와 Windows 미검증 경계 |
| [최상위 유지·디스플레이 변경·동작 줄이기](validation/2026-10-19-dsh-pet-adoption.md) | Windows 최상위 재적용, 모니터 변경 시 위치 보정, OS 동작 줄이기, MP4 원본 규칙과 1,096개 검사 |
| [Lemon US$2.99 가격 전환 준비](validation/2026-10-14-lemon-usd-price.md) | 운영 KRW 가격과 USD 스토어의 불일치, 마이그레이션·앱·웹 수정, 44·94·77개 검사와 미적용 운영 순서 |
| [휴식 타이머 숫자 전환 검증](validation/2026-10-14-rest-timer-motion.md) | 배포 후 로컬 `f9953a0`의 고유 집중 검사 16개, 초기 실패·수정과 전체/OS 미실행 경계 |
| [알림·스트레칭·완료 행동 수명 검증](validation/2026-10-13-reminder-action-lifetime.md) | 버튼·자동 미루기까지 상태 유지, 완료 전체 재생과 자연 만료·명시적 취소 분리 |
| [펫 행동 반복·타이머 호버 후속 검증](validation/2026-10-13-pet-playback-hover.md) | 누름 상태 유지·전체 재생 복귀, 호버 유지·즉시 이탈 숨김과 944개 검사 |
| [펫 하단 타이머·호버 표시 1차 검증](validation/2026-10-13-pet-timer-hover.md) | 최초 2초 표시 구현·928개 검사. 후속 요청으로 호버 유지·즉시 이탈 숨김으로 변경 |
| [말풍선 접합부 보더 검증](validation/2026-10-15-bubble-border-join.md) | 본체·꼬리 합집합 외곽선, 4개 테마·좌우·상태·불투명도·배율 픽셀 검사와 전후 이미지 |
| [GLB 1% 외곽선 검증](validation/2026-10-15-glb-one-percent-outline.md) | 현재 고정 두께 1%, 요소별 경계 유지·서브픽셀 표시와 검사 |
| [GLB 요소별 외곽선 검증](validation/2026-10-15-glb-element-outline.md) | 내부 고정 5%, 보이는 메시·재질 경계, 가림·투명도·저장·회전 검사와 실제 Kei 비교 |
| [GLB 외곽선 검증](validation/2026-10-15-glb-outline.md) | 최초 10% 전체 테두리 구현 이력. 현재 동작은 위 5% 요소별 외곽선 검증 참고 |
| [GLB 미리보기 높이·펫 표시 크기 검증](validation/2026-10-13-glb-preview-size.md) | 기본 360px·펫 약 2.1배 확대, 작은 창 적응·스크롤 크기 유지와 902개 검사·macOS 확인 |
| [GLB 미리보기 확장·설정 배치 검증](validation/2026-10-13-glb-wide-preview.md) | 전체 폭 미리보기, 하단 파일·펫 선택 오른쪽 행동 연결, 899개 검사·macOS 기본/최소 창 |
| [GLB 미리보기 내부 화살표 검증](validation/2026-10-13-glb-preview-overlay-arrows.md) | XYZ 입력·Z축 조절 제거, 내부 화살표 배치와 887개 검사·macOS 저장 복원 |
| [GLB 회전 화살표 검증](validation/2026-10-13-glb-rotation-arrows.md) | 좌우·위아래 360° 버튼, 최소 창 가시성, 저장 복원과 876개 검사·macOS 입력 확인 |
| [GIF·GLB 추가 설명 제거](validation/2026-10-12-gif-glb-guidance-removal.md) | 안내 제거·기존 편집 복원·790개 검사·수정 전 slop 점검 원칙 |
| [GIF·GLB 안내 추가 당시 검증](validation/2026-10-12-gif-glb-import-guidance.md) | `4c74880`의 과거 구현·검증; 후속 요청으로 안내 제거 |
| [Beta v1.1.4 배포 검증](validation/2026-10-14-beta-v1.1.4-release.md) | 집중 검사·Mac 공증·공개 자산과 앱 OS 진단·설치/업데이트 미실행 경계 |
| [Beta v1.1.3 배포·업데이트 검증](validation/2026-10-12-beta-v1.1.3-release.md) | Mac 서명·공증, Windows CI, 공개 파일 10개, 세 채널과 실제 Mac/Windows 업데이트·재시작·데이터 보존, 웹·노션 반영 |
| [타이머 숫자 전환·자동 저장 검증](validation/2026-10-12-live-timer-settings.md) | 확인 없는 중지 복귀, 간격 증가·감소 애니메이션, 홈 시간 자동 저장·실패 복구와 790개 검사·macOS 렌더링 진단 |
| [Beta v1.1.3 커밋 검증](validation/2026-10-09-beta-v1.1.3-commit.md) | GLB XYZ 회전·저장, 탭 이름, 펫 호버·모션 수정의 커밋 범위와 버전 변경 후 56개 검사 |
| [펫 호버 제거·모션 복귀 검증](validation/2026-10-09-pet-hover-removal.md) | 모든 펫의 호버 반응 제거, 투명 배경 입력 통과, 스트레칭·걷기 지속과 803개 검사·macOS 실제 입력 확인 |
| [Beta v1.1.2 배포·업데이트 검증](validation/2026-10-08-beta-v1.1.2-release.md) | Mac 서명·공증, Windows native CI, 세 공개 업데이트 채널, 실제 1.1.1→1.1.2 교체·재시작과 웹 반영 |
| [툴팁·아이콘·글자 테마 색상 검증](validation/2026-10-08-theme-control-colors.md) | 밝은 테마 전환의 이전 색 잔류·입력 안내·비활성 색 보완, 네 테마의 macOS 기본·최소 창과 열린 툴팁 확인 |
| [소리 설정 수정 검증](validation/2026-10-08-sound-settings.md) | 세 음량 %·OGG 지원·미리듣기 자연 종료, 784개 검사와 macOS 네이티브 재생·최소 창 확인 |
| [커스텀 펫 편집·삭제 검증](validation/2026-10-08-pet-management-edit-delete.md) | 설치한 모든 형식의 편집·저장·삭제, 원본/행동 보존, 777개 회귀 검사와 macOS 최소 창 확인 |
| [Beta v1.1.1 배포·업데이트 검증](validation/2026-10-07-beta-v1.1.1-release.md) | Mac 공증·Windows native CI·공개 다운로드·실제 Mac/Windows 업데이트 교체·재시작과 검증 경계 |
| [시작 업데이트 안내 검증](validation/2026-10-08-startup-updates.md) | 계정 복원 뒤 새 버전 모달·변경내역·백그라운드 유예·중복 방지와 네 테마 Mac 실제 창 |
| [400MB 메모리 검증](validation/2026-10-07-memory-budget.md) | 최신 소스·동일 native 실행기의 전후 측정, 캐시·2D 재생·가져오기 최적화와 복구 회귀 검사 |
| [400MB 최적화 계획](plans/2026-10-07-memory-budget.md) | Agency 역할·측정 기준·변경 순서·장시간 검증과 임의 입력 경계 |
| [홈 미리보기 복원·팩 미리보기 제거 정정](validation/2026-10-09-pack-preview-correction.md) | 사용자 첨부 화면 기준으로 제거 범위를 정정한 검증 |
| [보존 코드·데이터 이전 제거 검증](validation/2026-10-09-retired-feature-removal.md) | Beta v1.1.3 루틴·프로필 설정, 타이머 펫 미리보기와 Piskel 제거·데이터 이전 검증 |
| [미사용 기능 제거 검증](validation/2026-10-07-unused-features.md) | 최신 제품 커밋 기준, 루틴·프로필 설정과 픽셀 에디터 제거·기존 데이터 호환·종료/업데이트 회귀 검사 |
| [과거 편집 기능 파일 정리 검증](validation/2026-10-08-legacy-authoring-cleanup.md) | Piskel·루틴·프로필의 폐기 문서/캡처 제거, 호환 코드 분리와 정리 전후 검사 |
| [Agency 다음 단계 검증](validation/2026-10-06-agency-next-stage.md) | 실제 macOS native GLB 36분 자원 추세·Windows 환경/양식과 남은 비교 조건 |
| [Agency 준비 작업 검증](validation/2026-10-06-agency-execution.md) | 기존 변경 커밋·집중 검사·전체/대체경로 결과와 검증 한계 |
| [Agency Agents 실행 작업표](plans/2026-10-06-agency-execution.md) | 브랜치별 보존 커밋, 이번 검증 결과와 담당·소유권·남은 환경 조건 |
| [출시 콘텐츠 초안](operations/2026-10-06-launch-materials.md) | 현재 기능 기반 데모·게시물·가격 안내, 내부 검토용 |
| [지원 운영 초안](operations/2026-10-06-support-playbook.md) | 문의 분류·재현 양식·구매 복원/오류 안내와 정책 결정 항목 |
| [분야별 구현·검증 결과](validation/2026-10-06-audit-followup.md) | GLB 속도·종료 포즈, 펫 포커스·즉시 만료 갱신, 748개 검사와 ARM64 게시본·남은 출시 조건 |
| [분야별 수정 실행 계획](plans/2026-10-06-audit-followup.md) | 미루기 진단, GLB 속도·종료 포즈, 펫 페이지 포커스, 문서와 출시 검증의 소유권·완료 기준 |
| [에이전트 작업 안내](../AGENTS.md) | Codex·Claude 공통 프로젝트 지침. Claude는 `CLAUDE.md`에서 가져온다. |
| [기존 펫 팩 재적용·펫 관리](validation/2026-10-05-pet-management-compatibility.md) | 콘텐츠 버전 제한 제거, 단일 관리 화면, 입력·미리보기·펫 선택 배치와 Kazusa 확인 |
| [버전 표시·타이머·알림 개선](validation/2026-10-05-timer-settings-refinements.md) | 설정 업데이트, Danger 색상, 최소 1분, 30초 자동 미루기와 시계·타이머 전환 |
| [호버 진입·해제 창 안정화](validation/2026-10-05-hover-surface-stability.md) | 호버 전환의 창 크기·위치 변경 제거, GLB·2D 전후 측정과 회귀 검사 |
| [펫 투명 영역 클릭 통과](validation/2026-10-05-pet-click-through.md) | macOS 연결, 정확한 픽셀 판정과 입력 캡처 유지 검증 |
| [펫 캔버스 효과 제거](validation/2026-10-05-pet-canvas-motion.md) | 포인터 업 바운스·누름/유지 변형 제거, 지정 동작과 입력 흐름 검증 |
| [펫 팩 행동별 설정 개편](validation/2026-10-05-pet-action-editor.md) | 파일 열기 배치, 고급 설정 제거, 행동별 방향·반복과 저장·재생 검증 |
| [펫 팩 만들기 통합](validation/2026-10-05-unified-pet-builder.md) | GLB·GIF·MP4 통합 진입점, 공통 행동 편집 배치와 설명 문구 제거 |
| [GLB 고급 설정 스타일 수정](validation/2026-10-05-glb-advanced-style.md) | 기본 Expander 스타일 누락 수정, 공통 카드·방향 아이콘과 네 테마 검증 |
| [GLB 편집 UI/UX 개선](validation/2026-10-05-glb-editor-ux.md) | 행동 선택 중심 배치, 반복 규칙·재생·저장 상태와 반응형 화면 검증 |
| [GLB 파일 행·즉시 제거](validation/2026-10-06-glb-file-slot.md) | GIF·MP4와 같은 파일명 옆 휴지통, 저장된 펫 보존·재추가, 733개 회귀 검사 |
| [GLB 파일 추가·제거](validation/2026-10-06-glb-file-management.md) | 초안 비우기·저장한 펫 삭제, 목록 동기화·기본 펫 전환, 730개 회귀 검사 |
| [GLB 공통 파이프라인 메모리 개선](validation/2026-10-06-glb-pipeline-memory.md) | 지연 로딩·모델 공유·연속 키프레임·재생 버퍼 재사용, 301개 입력 및 718개 회귀 검사 |
| [GLB 메모리 최적화 검증](validation/2026-10-05-glb-memory.md) | 프레임 버퍼 재사용, 화질 동일성, 메모리 전후 측정과 Windows 검증 범위 |
| [GLB 확대 화질 검증](validation/2026-10-04-glb-resolution.md) | 표시 크기·Retina 배율 대응, 가장자리 처리와 실제 모델 전후 비교 |
| [GLB 가져오기 호환성](validation/2026-10-06-glb-compatibility.md) | Hikari 반투명 재질·중복 동작명 수정, 당시 로컬 295개 전수 비교·687개 회귀 검사. 이후 1.1.1 공개 배포에 포함 |
| [GLB 펫 사용법](glb-pets.md) | 모델 가져오기, 상황별 행동 지정, 몸 방향 고정과 지원 범위 |
| [Beta v1.0.4 GLB 통합 검증](validation/2026-10-04-beta-glb-pets.md) | 최신 소스 기준, 회귀 검사와 macOS 재생·설정 화면 증거 |
| [프로젝트 README](../README.md) | 사용자 소개, 빠른 실행, 저장소 구조 |
| [Beta v1.0.2 Mac 공증 배포](validation/2026-10-01-macos-notarization.md) | Developer ID·Apple 공증·티켓·웹 ZIP 교체, Gatekeeper 검사와 첫 실행 미확인 항목 |
| [Beta v1.0.2 재패키징·웹 배포](validation/2026-10-01-v1.0.2-release.md) | 브랜드 수정 포함, 설치 파일·공개 다운로드·브라우저 저장 검증 |
| [Beta v1.0.2 릴리스 노트](releases/v1.0.2-beta.md) | 브랜드 트레이 아이콘·최초 계정 화면 변경과 설치 안내 |
| [Beta v1.0.1 설치 파일](validation/2026-10-01-installers.md) | Windows 설치 마법사·Mac DMG, 파일 해시, 실행 검사와 실기·게시 한계 |
| [Beta v1.0.1 웹 다운로드 배포](validation/2026-10-01-web-installers.md) | 설치 ZIP 공개 릴리스·웹 연결, 다운로드 버튼 저장·해시와 서버 검사 |
| [Beta v1.0.1 릴리스 노트](releases/v1.0.1-beta.md) | 설치 형식과 로그인 유지 변경, 설치·업데이트 안내 |
| [판매 시작 안내](releases/beta-v1.0.0-launch.md) | Live 상품·Secret·서버 전환, 실결제 검증과 macOS 서명·공증 순서 |
| [Beta v1.0.0 검증](validation/2026-09-30-beta-v1.0.0.md) | 구매 잠금·결제 서버·패키지 검사와 판매 시작 전 남은 조건 |
| [Beta v1.0.0 릴리스 노트](releases/v1.0.0-beta.md) | 판매용 구매 잠금, 현재 기능, 설치·업데이트와 운영 미연결 경계 |
| [A안 계정 화면 편집](account-screen.md) | 최초 로그인·구매 화면의 JSON·XAML·테마 편집 위치와 현재 연결 범위 |
| [로그인 유지 검증](validation/2026-10-01-session-persistence.md) | OS 보안 저장, 재실행 복원·토큰 갱신, 로그아웃 삭제와 실기 검증 한계 |
| [재실행 로그인 화면 수정](validation/2026-10-07-login-startup.md) | 세션 복원 후 화면 표시, 정상 재실행의 계정 창 열림 0회, 복원 취소·로그아웃 경합 검사 |
| [설정 이동·계정 로그아웃 검증](validation/2026-09-30-settings-account.md) | 미저장 변경 확인, Google 이메일 표시, 메모리 세션·로그아웃과 검증 한계 |
| [Lemon Squeezy 전환 구현·검증](validation/2026-09-28-lemon-squeezy-transition.md) | 결제 생성·웹훅·누적 환불, Supabase 적용 상태와 사용자가 준비할 외부 설정 |
| [Paddle 샌드박스 이력](validation/2026-09-28-paddle-onboarding.md) | 공급자 전환 전 수행한 샌드박스 검증 기록. 현재 결제 경로에서는 사용하지 않음 |
| [v0.2.2 배포 검증](validation/2026-09-23-v0.2.2-distribution.md) | 최신 복구 수정 포함, macOS·Windows 배포 파일, 해시·버전·게시 앱 검사 |
| [v0.2.2 변경 사항](releases/v0.2.2.md) | macOS 펫 표시, 호버 시계, 알림·미리보기·효과음 복구 |
| [v0.2.1 패치노트](releases/v0.2.1.md) | 기본 펫 5종, 상호작용, UI·타이머·알림 개선과 업데이트 안내 |
| [v0.2.1 배포 검증](validation/2026-09-22-v0.2.1-distribution.md) | macOS·Windows 배포 파일, 기본 펫 포함·체크섬·게시 앱 검사와 실기 한계 |
| [MVP 범위](mvp.md) | 제품 목적, 구현된 동작, 제외 범위, 변경 원칙 |
| [펫 들어 올리기·착지 검증](validation/2026-09-24-pet-lift.md) | 기본 펫 5종의 누름 유지·착지, 기존 클릭·드래그·입력 취소와 렌더링 검사 |
| [펫 공 모양·매달림·바운스 검증](validation/2026-09-24-pointer-art.md) | 고슴도치 말기·펴기, 4종 매달림, 바닥에서 한 번 튕긴 후 기존 클릭 반응 연결 |
| [포인터 전용 펫 제작 원장](../Art/Characters/pointer-interactions-v1/README.md) | 5종 원본·프롬프트·기존 셀 보존·추가 자세와 재생 계약 |
| [보리·Mochi 확장 행동 계획](plans/2026-09-22-original-companions.md) | 랜덤 행동, 클릭·미루기 반응, 스트레칭 후 이동과 커스텀 팩 호환 계약 |
| [보리·Mochi 제작 원장](../Art/Characters/original-companions-v2/README.md) | 새 PNG 원본, 프롬프트, 팩 버전과 생성 방법 |
| [강아지·고슴도치·펭귄 제작 원장](../Art/Characters/original-companions-v3/README.md) | 세 팩의 원본 보존, 16개 자세 정렬, 행동 타임라인과 재생성 |
| [강아지·고슴도치·펭귄 검증](validation/2026-09-22-companion-trio.md) | 세 팩 추가, 자산·패키징·재생 검사와 검증 한계 |
| [동일 콘셉트 펫 생성·적용 가이드](pet-generation-guide.md) | 손그림풍 스타일, 참조 원본·프롬프트 재사용, 시트 정렬·10개 행동, 번들 적용과 잔상 검수 |
| [보리·Mochi 확장 행동 검증](validation/2026-09-22-original-companions.md) | 상태·포인터·이동 경계 검사, 실제 시간 재생과 검증 한계 |
| [방향 및 BM 초안](business-model.md) | 포지셔닝, 무료 다운로드·US$2.99 일회성 구매·체험 제외 |
| [제품 방향](product-direction.md) | 초기 고객, 경쟁 판단, 단일 유료 앱·계정과 로컬 데이터 경계 |
| [UI 한글화](localization.md) | 한국어 적용 범위, 표시 용어, 기존 데이터 보존과 검증 한계 |
| [UI 한글화 검증](validation/2026-09-14-korean-ui.md) | 127개 테스트, macOS 게시 앱 진단과 한글 화면 캡처 |
| [브랜드 팔레트 교체 검증](validation/2026-09-21-brand-palettes.md) | 승인된 네 시안의 색상, 다정한 오트 기본값, 저장 ID 호환과 Windows 자동 캡처 |
| [알림·저장·입력 동작 검증](validation/2026-09-21-ux-refinements.md) | 5분 타이머·5초 안내, 음량, 저장 후 초기화, 호버·클릭 피드백과 Release 240개 통과 |
| [반응형·메뉴·확인 동작 검증](validation/2026-09-21-responsive-actions.md) | 640×560 반응형, 펫 숨기기, 중지·종료 확인, 설정·펫 열기 배치와 Release 254개 통과 |
| [색상 테마 초기 구현·검증](validation/2026-09-20-themes.md) | 최초 네 가지 테마의 선택·저장·초안 보존과 당시 네이티브 캡처 |
| [디자인 시스템](design-system.md) | 공통 시각 토큰, 페이지·버튼·입력 규칙과 중첩 창 적용 범위 |
| [내비게이션 명칭·아이콘 검증](validation/2026-09-29-navigation-icons.md) | Figma 현재 페이지의 아이콘 6종, 명칭 통일, 원본 SVG·테마·크기 검증 |
| [방향 아이콘 교체 검증](validation/2026-09-30-direction-icons.md) | 사용자 SVG 4종, 입력·드롭다운·기록·스크롤바 적용과 회귀 검사 |
| [Figma 디자인 시스템](figma-design-system.md) | 편집 가능한 Figma 파일, 변수·Variant 구성과 로컬 빌더 실행 방법 |
| [디자인 시스템 검증](validation/2026-09-14-design-system.md) | 135개 테스트, 최종 변경 영역 13개 재검사와 macOS 화면 24장 |
| [입력 필드 상태 검증](validation/2026-09-14-input-fields.md) | 호버·포커스 강조 제거, 숫자 왼쪽 정렬과 화살표 모서리, 전체 136개 테스트 |
| [타이머 피드백 검증](validation/2026-09-15-timer-feedback.md) | 상태 배지, 실행 중 간격 잠금, 명시적 적용, 재생/일시정지·정지 버튼과 회귀 검사 |
| [비활성 입력 필드 검증](validation/2026-09-15-disabled-input.md) | 숫자 입력의 내부 반경·배경 통일, 외곽 1px 테두리와 macOS 렌더링 확인 |
| [말풍선 알림 검증](validation/2026-09-16-stretch-speech.md) | 조기·초과 완료, 4방향 배치, 효과음과 macOS 네이티브 진단 |
| [스트레칭 말풍선 알림](stretch-notifications.md) | 알림 흐름, 자동 배치·대사·불투명도, 초과 시간, 효과음과 기록 호환성 |
| [설정 대시보드](settings-ui.md) | 참고 이미지 기반 카드 배치, 바로가기와 창 크기 대응 |
| [설정 대시보드 검증](validation/2026-09-14-settings-dashboard.md) | 130개 테스트, 기본·최소 크기 캡처와 설정 회귀 검증 |
| [사이드바 탭 전환 검증](validation/2026-09-15-navigation-tabs.md) | 같은 창의 타이머·루틴·기록 탭, 펫 팩 항목 제거와 macOS 렌더링 검증 |
| [유료 출시 계획](plans/2026-09-27-paid-launch.md) | 도메인 없이 진행할 계정·Supabase·구매 권한·시안과 운영 전환 순서 |
| [계정·구매 기반 1차 검증](validation/2026-09-27-paid-foundation.md) | 로그인·구매 시안, Supabase RLS·이벤트 처리, C# 클라이언트와 391개 Release 검사 |
| [Supabase 공개키 연결 검사](validation/2026-09-28-supabase-connection.md) | 실제 Auth 응답, Google 비활성·상품/함수 준비 상태와 다음 서버 설정 |
| [계정별 무료 이용 코드](access-codes.md) | Google 로그인 후 코드 등록, 권한 복원, 운영자 코드 발급·회수 |
| [무료 이용 코드 검증](validation/2026-09-30-access-codes.md) | 앱 모달·RLS·코드 제한과 실제 Supabase 반영 |
| [Supabase 연결 안내](../supabase/README.md) | 현재 앱의 계정 계약, 추적된 샌드박스 소스와 과거 원격 반영 기록 |
| [개발 계획](development-plan.md) | 구현 단계, 완료 조건과 후속 범위 |
| [설정 탭 UI 개편 계획](plans/2026-09-18-settings-ui-redesign.md) | UI 우선·UX 후속 순서, 드롭다운·텍스트 위계·간격·레이아웃 검수와 구현 기준 |
| [설정 탭 UI 개편 검증](validation/2026-09-18-settings-ui-redesign.md) | 하단 취소·저장, 변경 상태·오류 복구, 설정 전용 드롭다운, 202개 테스트와 macOS 캡처 |
| [펫 추가 탭 UI 후속 시안](plans/2026-09-18-pet-tabs-ui-proposal.md) | 열기·만들기 배치, 다섯 행동 카드, 상태별 시안과 사용자 확인 후 구현 경계 |
| [펫 추가 탭 UI 적용 검증](validation/2026-09-18-pet-tabs-ui.md) | 카드 클릭 미리보기, 최소 창의 다섯 카드, 고정 하단, Release 205개와 macOS 렌더링 검증 |
| [홈 UI 검수와 수정 시안](plans/2026-09-20-home-ui-proposal.md) | 네 카드의 글자 위계·입력·간격·상태 배지, 최소 창과 긴 이름, 사용자 확인 후 구현 경계 |
| [홈 UI 적용 검증](validation/2026-09-20-home-ui-implementation.md) | 타이머 상단·펫 하단, 입력 크기·상태 설명, Release 207개와 macOS 기본·최소 창 검증 |
| [기록 화면 후속 시안](plans/2026-09-20-review-ui-proposal.md) | 승인된 기간 조작·요약·날짜별 상세 구성 |
| [기록 UI 적용 검증](validation/2026-09-20-review-ui-implementation.md) | 완료 시각·실제 시간 표시, 208개 테스트와 기본·최소 창 캡처 |
| [말풍선 UI 후속 시안](plans/2026-09-20-speech-ui-proposal.md) | 제목·여백·버튼 크기 정리안과 승인 후 구현 범위 |
| [말풍선 UI 적용 검증](validation/2026-09-20-speech-ui-implementation.md) | 본문 제거·제목 중앙 정렬·상태별 크기, Release 212개와 네 방향 macOS 캡처 |
| [펫 크기·타이머·디버그 시안](plans/2026-09-20-pet-scale-timer-debug-proposal.md) | 50~150% 펫 크기, 중지 시간 표시, 우클릭 단순화와 알림 상태 미리보기 기준 |
| [펫 크기·타이머·디버그 구현 검증](validation/2026-09-20-pet-scale-timer-debug-implementation.md) | Release 214개, 82장 자동 캡처와 실제 macOS·Windows 미검증 범위 |
| [UI 설명 문구 제거 검증](validation/2026-09-20-ui-copy-cleanup.md) | 홈·설정·기록·펫 추가·말풍선의 고정 설명 제거, Release 215개와 자동 캡처 |
| [기존 루틴·프로필 호환과 회고](personalization.md) | 제거된 설정 UI의 데이터 보존 경계와 현재 주간 회고/CSV |
| [펫 리소스 관리](pet-resources.md) | 제작 원장, 런타임 계약, 품질 기준과 검사 명령 |
| [펫 추가·커스텀 팩 만들기](pet-packs.md) | 이미지·GIF·MP4 동작 배정, 팩 생성·미리보기·설치·업데이트·재설치, ZIP/버전/해시 계약 |
| [이미지·MP3 가져오기 검증](validation/2026-09-30-media-import.md) | 정지 이미지·사진·효과음 확장, 464개 검사와 macOS 변환·재생 확인 |
| [전체·개별 소리 크기 검증](validation/2026-09-30-sound-mixer.md) | 세 슬라이더·개별 미리듣기, 저장 호환성, 474개 검사와 macOS 재생 확인 |
| [소리 아이콘 검증](validation/2026-09-30-sound-icons.md) | 재생·중지 전환, 세 음량 아이콘, 퍼센트 제거와 최종 477개 검사 |
| [커스텀 펫 검증](validation/2026-09-16-custom-pet.md) | 147개 테스트, 실제 MP4 변환, 동작별 팩 생성·설치, macOS 렌더링 검증 |
| [펫 페이지 탭 검증](validation/2026-09-16-pet-tabs.md) | 사이드바 펫 추가, 열기·만들기 탭, 초안 유지와 최소 크기 렌더링 |
| [UI/UX 전수 감사와 작업 계획](validation/2026-09-16-ui-ux-audit.md) | 현재 전체 화면의 시각·흐름·접근성 감사, P1/P2 우선순위와 단계별 수정·검증 계획 |
| [UI/UX 재검수](validation/2026-09-18-ui-ux-audit.md) | 9/18 체크아웃의 12개 수정 항목과 화면·코드 근거 |
| [UI/UX 후속 구현](validation/2026-09-18-ui-ux-implementation.md) | 기록 날짜·초안 보호·설정·미리보기·접근성·기본 펫 수정, 196개 테스트와 macOS 진단 |
| [UI/UX 1단계 동작 정확성 검증](validation/2026-09-16-ui-ux-phase1.md) | 선택 루틴 편집, 프로필 적용 사전 차단, 커스텀 펫 교체 확인과 회귀 검사 |
| [UI/UX 2단계 최소 창·숨은 상태 검증](validation/2026-09-17-ui-ux-phase2.md) | 스크롤 거터, 접힌 알림 복구, 펫 팩 경고와 상태·하단 동작 그룹의 회귀 검사 |
| [설정 탭 구조 검증](validation/2026-09-17-settings-tab.md) | 설정 내비게이션, 알림·타이머 섹션 분리, 루틴 적용 경계와 macOS 격리 진단 |
| [루틴·프로필 UI 제거 검증](validation/2026-09-17-remove-routine-profiles.md) | 사이드바·타이머 카드 진입점 제거, 기존 데이터 보존과 회귀 검사 |
| [홈 스트레칭·휴식 시간 검증](validation/2026-09-17-home-break-time.md) | 스트레칭 간격 홈 이동, 1~10분 휴식 시간과 다음 세션 적용 검증 |
| [펫 추가 탭 레이아웃 검증](validation/2026-09-17-pet-builder-layout.md) | 열기 탭의 작은 미리보기·동작 선택, 만들기 탭의 이름·다섯 행동 카드와 최소 창 검증 |
| [설정 탭 헤더 제거 검증](validation/2026-09-17-tab-header-removal.md) | 타이머·설정·기록·펫 추가 페이지의 경로·큰 제목·설명 제거와 최소 창 검증 |
| [v0.2.0 배포 검증](validation/2026-09-17-v0.2.0-distribution.md) | macOS arm64/x64·Windows x64 자체 포함 배포물, 체크섬, 게시 앱 진단과 실기 미검증 범위 |
| [펫 팩 기반 검증](validation/2026-09-13-pet-packs.md) | 병합 충돌 복구, 설치 회귀 테스트와 macOS 진단 |
| [보리 아트 후보](../Art/Characters/bori-rabbit/README.md) | 원본·프롬프트·반응 5종·별도 설치 팩과 남은 아트 검수 |
| [보리 제작·재생 검증](validation/2026-09-14-bori-candidate.md) | 119개 테스트, macOS 실제 시간 기반 재생과 검증 한계 |
| [설치 전 미리보기 검증](validation/2026-09-14-pet-pack-preview.md) | 배경·크기·Pause/Replay, 122개 테스트와 2배 렌더링 캡처 |
| [플랫폼 안내](cross-platform.md) | 설치, 데이터 위치, C# 빌드·패키징, OS 제약. Windows 배포물에도 포함된다. |
| [검증 안내](verification.md) | 자동 검사 명령, 검증 수준, 실제 OS 확인 범위와 기록 |
| [휴식 기능 1차 검증](validation/2026-09-13-companion-stage1.md) | 루틴·완료 기록·펫 검사 구현, 56개 테스트와 macOS 진단 결과·한계 |
| [개인화 2차 검증](validation/2026-09-13-companion-stage2.md) | 루틴 라이브러리·업무 프로필·주간 회고·CSV, 71개 테스트와 macOS 진단 |
| [타이머 수정 검증](validation/2026-09-13-timer-controls.md) | 간격 즉시 적용, 정지·리셋 대기, 아이콘 조작과 77개 테스트 |
| [Windows 체크리스트](windows-dogfooding-log.md) | Windows 실기 검증 양식. 공란은 미검증이다. |
| [2026-09-11 macOS 기록](validation/2026-09-11-macos-dogfooding.md) | 특정 과거 커밋의 관찰 기록. 현재 버전 전체의 통과 선언이 아니다. |
| [보관 자료](archive/README.md) | Swift/Piskel 계획·보고서와 Garden 세션 기록 |
| [제품 검증 참고 프레임워크](reference/revenue-first-product-validation.md) | 필요할 때 참고하는 일반 방법론. 프로젝트의 확정 기획이 아니다. |

## 충돌 처리

1. 사용자의 현재 요청이 작업 범위를 정한다. 오래된 문서의 승인 대기·역할 배정·다음
   단계 지시를 현재 작업에 자동 적용하지 않는다.
2. 실제 구현은 현재 체크아웃의 코드와 빌드 입력으로 확인한다. 기획을 구현 완료로
   문서화하지 않는다.
3. 현재 제품 범위는 `mvp.md`, 빌드·설치는 `cross-platform.md`, 검증 사실은
   날짜·커밋·OS가 명시된 기록에서 관리한다. 같은 규칙을 여러 계획에 복제하지 않는다.
4. `archive/`는 역사 기록이고 `reference/`는 선택적 참고 자료다. 둘 다 새 기능 착수,
   브랜치 전환, 외부 세션 실행, 코드 변경의 지침이 아니다.
5. 문서와 코드가 다르면 해당 사실을 확인하고 담당 문서를 고친다. 더 최신인 문서라는
   이유만으로 다른 브랜치의 동작을 현재 구현으로 간주하지 않는다.

## 정리 범위 — 2026-09-13

출시에서 사용하지 않는 Swift 소스·테스트, SwiftPM/Xcode 설정, Swift 전용 스크립트와
수동 CI를 제거했다. 실제 캐릭터 파일은 `Assets/Characters/`로 옮겼고 파일 내용은
변경하지 않았다. 이전 소스 조회 방법은 [보관 자료](archive/README.md)에 있다.

C#에서 연결되지 않은 Edit/Delete 버튼과 삭제 처리, 사용하지 않는
`StretchClock.ResumeFromSleep`를 제거했다. 당시 제거했던 `AnimationView.Completed`는
이후 병합된 재생 회귀 테스트와 펫 팩 반응 미리보기에서 사용하므로 복구했다.
캐릭터 로딩·저장 호환성은 기존 파일을 지원하도록 유지한다. 진단용 픽셀 에디터와
루틴·프로필 편집기는 [2026-10-07 정리](validation/2026-10-07-unused-features.md)에서 제거했다.
