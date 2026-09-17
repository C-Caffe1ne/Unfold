# 문서 안내

현재 제품은 `release/mvp`의 **휴식 세션·개인화 데모를 포함한 C#·Avalonia 기반 Cat MVP**다. 아래 문서가 현재
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
| [루틴·프로필·회고 사용 안내](personalization.md) | 여러 루틴, 수동 업무 프로필, 주간 회고/CSV와 기존 데이터 호환 |
| [펫 리소스 관리](pet-resources.md) | 제작 원장, 런타임 계약, 품질 기준과 검사 명령 |
| [펫 추가·커스텀 팩 만들기](pet-packs.md) | GIF·MP4 동작 배정, 팩 생성·미리보기·설치·업데이트·재설치, ZIP/버전/해시 계약 |
| [커스텀 펫 검증](validation/2026-09-16-custom-pet.md) | 147개 테스트, 실제 MP4 변환, 동작별 팩 생성·설치, macOS 렌더링 검증 |
| [펫 페이지 탭 검증](validation/2026-09-16-pet-tabs.md) | 사이드바 펫 추가, 열기·만들기 탭, 초안 유지와 최소 크기 렌더링 |
| [UI/UX 전수 감사와 작업 계획](validation/2026-09-16-ui-ux-audit.md) | 현재 전체 화면의 시각·흐름·접근성 감사, P1/P2 우선순위와 단계별 수정·검증 계획 |
| [UI/UX 1단계 동작 정확성 검증](validation/2026-09-16-ui-ux-phase1.md) | 선택 루틴 편집, 프로필 적용 사전 차단, 커스텀 펫 교체 확인과 회귀 검사 |
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
