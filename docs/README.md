# 문서 안내

현재 제품은 `release/mvp`의 **휴식 세션·로컬 회고를 포함한 C#·Avalonia 기반 Cat MVP**다. 아래 문서가 현재
구현·범위·실행 방법을 설명한다. 과거 Swift 구현과 Garden 실험은 보관 자료다.

| 문서 | 역할 |
|---|---|
| [에이전트 작업 안내](../AGENTS.md) | Codex·Claude 공통 프로젝트 지침. Claude는 `CLAUDE.md`에서 가져온다. |
| [프로젝트 README](../README.md) | 사용자 소개, 빠른 실행, 저장소 구조 |
| [MVP 범위](mvp.md) | 제품 목적, 구현된 동작, 제외 범위, 변경 원칙 |
| [방향 및 BM 초안](business-model.md) | 현재 Cat MVP의 포지셔닝과 조건부 수익 가설, 검증 순서 |
| [제품 방향](product-direction.md) | 초기 고객, 경쟁 판단, 무료·Plus·펫 팩의 가치 제안 |
| [UI 한글화](localization.md) | 한국어 적용 범위, 표시 용어, 기존 데이터 보존과 검증 한계 |
| [UI 한글화 검증](validation/2026-09-14-korean-ui.md) | 127개 테스트, macOS 게시 앱 진단과 한글 화면 캡처 |
| [브랜드 팔레트 교체 검증](validation/2026-09-21-brand-palettes.md) | 승인된 네 시안의 색상, 다정한 오트 기본값, 저장 ID 호환과 Windows 자동 캡처 |
| [알림·저장·입력 동작 검증](validation/2026-09-21-ux-refinements.md) | 5분 타이머·5초 안내, 음량, 저장 후 초기화, 호버·클릭 피드백과 Release 240개 통과 |
| [반응형·메뉴·확인 동작 검증](validation/2026-09-21-responsive-actions.md) | 640×560 반응형, 펫 숨기기, 중지·종료 확인, 설정·펫 열기 배치와 Release 254개 통과 |
| [색상 테마 초기 구현·검증](validation/2026-09-20-themes.md) | 최초 네 가지 테마의 선택·저장·초안 보존과 당시 네이티브 캡처 |
| [디자인 시스템](design-system.md) | 공통 시각 토큰, 페이지·버튼·입력 규칙과 중첩 창 적용 범위 |
| [Figma 디자인 시스템](figma-design-system.md) | 편집 가능한 Figma 파일, 변수·Variant 구성과 로컬 빌더 실행 방법 |
| [디자인 시스템 검증](validation/2026-09-14-design-system.md) | 135개 테스트, 최종 변경 영역 13개 재검사와 macOS 화면 24장 |
| [입력 필드 상태 검증](validation/2026-09-14-input-fields.md) | 호버·포커스 강조 제거, 숫자 왼쪽 정렬과 화살표 모서리, 전체 136개 테스트 |
| [타이머 피드백 검증](validation/2026-09-15-timer-feedback.md) | 상태 배지, 실행 중 간격 잠금, 명시적 적용, 재생/일시정지·정지 버튼과 회귀 검사 |
| [비활성 입력 필드 검증](validation/2026-09-15-disabled-input.md) | 숫자 입력의 내부 반경·배경 통일, 외곽 1px 테두리와 macOS 렌더링 확인 |
| [말풍선 알림 검증](validation/2026-09-16-stretch-speech.md) | 조기·초과 완료, 4방향 배치, 효과음과 macOS 네이티브 진단 |
| [스트레칭 말풍선 알림](stretch-notifications.md) | 알림 흐름, 4방향 위치, 초과 시간, 효과음과 기록 호환성 |
| [설정 대시보드](settings-ui.md) | 참고 이미지 기반 카드 배치, 바로가기와 창 크기 대응 |
| [설정 대시보드 검증](validation/2026-09-14-settings-dashboard.md) | 130개 테스트, 기본·최소 크기 캡처와 설정 회귀 검증 |
| [사이드바 탭 전환 검증](validation/2026-09-15-navigation-tabs.md) | 같은 창의 타이머·루틴·기록 탭, 펫 팩 항목 제거와 macOS 렌더링 검증 |
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
| [펫 추가·커스텀 팩 만들기](pet-packs.md) | GIF·MP4 동작 배정, 팩 생성·미리보기·설치·업데이트·재설치, ZIP/버전/해시 계약 |
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
캐릭터 로딩·저장 호환성과 진단용 에디터는 기존 호출 경로와 테스트가 사용하므로 유지한다.
