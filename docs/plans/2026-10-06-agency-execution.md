# Agency Agents 실행 작업표 — 2026-10-06

## 기준과 소유권

최신 앱: `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold`,
`codex/glb-import-compat`, `1.1.0-beta`, 기존 변경 보존 커밋 `18a3a31`.
서버: `/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold`, `95f7a21`.
서버 변경을 최신 앱에 자동 병합하지 않는다. 서로 다른 버전의 기존 변경은 각 브랜치에 보존했다.
현재 작업에서 제품 코드는 수정하지 않는다. 이후 구현은 재현 결과·파일 소유권을 정한 뒤 진행한다.
Orca CLI가 현재 PATH에 없어 이 채팅의 Codex 멀티에이전트로 기획·검증·콘텐츠를 분담했다.

| ID | 담당 / 소유 범위 | 산출물·완료 기준 | 상태 / 선행 조건 |
|---|---|---|---|
| A01 | Product Manager / product-direction, business-model | 현재 구매 gate·기능 설명 정합성 | 완료, 문서 대조 |
| A02 | 감독 / 브랜치 기준·검증 기록 | 기존 변경 커밋·버전·소유권 기록 | 완료, 아래 검증 한계 유지 |
| D01 | Evidence Collector / Windows 검증 자료 | 2D/GLB 투명 클릭, 드래그 해제, DPI·다중 모니터 실기 증거 | 환경 대기, 실제 Windows |
| D02 | Performance Benchmarker / 성능 자료 | 고정 모델·해상도·기기에서 실제 앱 30분 이상 RSS/managed/CPU 비교 | 예정, 모델·비교 실행본 확정 |
| D03 | Minimal Change Engineer / 재현된 관련 파일 | 실패 재현→수정→집중 회귀 통과 | D01/D02 결과 후 |
| B01 | Payments & Billing Engineer / 서버 계약·검증 자료 | test/live 구분, Google·구매·복원·환불 실증 | 로컬 43개 통과, 실제 거래 환경 필요 |
| R01 | Desktop App Engineer / 패키지·검증 자료 | 구버전→업데이트·설정/기록/펫/로그인 보존 증거 | 최종 입력 확정·실제 OS 환경 필요 |
| Q01 | Accessibility Auditor / 접근성 자료 | VoiceOver/Narrator, Tab·최소 창·대비 검증 | 실제 대상 OS 검증 예정 |
| O01 | Operations Manager + Support Responder / support-playbook | 문의 분류·복구·환불 전달·정책 결정 양식 | 내부 초안 완료 |
| M01 | Content Creator + Brand Guardian / launch-materials | 데모·게시물4개·가격·지원 범위 명시 | 내부 초안 완료, 공개 전 QA 필요 |
| M02 | Social Media Strategist / 채널 일정 | 실제 계정 기준 채널·게시 일정 | 공개 출시 판단 후 |

## 검증 결과

| 브랜치 / 보존 커밋 | 이번 검증 | 경계 |
|---|---|---|
| release/mvp / fff3aee | Release 빌드, GLB·설정 19/19 | 전체 검사 중 입력 실패 3건 관찰. 해당 항목 단독 재검사 7/7 통과, 전체 완료 결과 없음 |
| feat/pet-pack-ux / 5dc4f7e | Release 빌드, 파일 연결 18/18 | 전체 검사 중 공간 부족·미디어 도구 없음 skip 관찰, 완료 결과 없음 |
| codex/beta-v1.0.4 / 95f7a21 | 서버 43/43, ZIP CRC·hash 77/77 | 실제 서버 배포·실거래 미검증 |
| codex/glb-import-compat / 18a3a31 | 격리 Release 빌드, 집중 67/67 | 순차 전체 746/748 통과, 출력 경로 권한 실패 2개는 표준 빌드에서 2/2 통과. 실제 Windows·장시간 성능 미검증 |

대표 비밀키·JWT·개인키 패턴 검사에서 발견 없음. 전체 비밀정보 부재 인증으로 해석하지 않는다.
전체 검사는 동시 실행 중 임시 파일 공간 부족이 있었으며, 이후 여유 공간은 약 7.3GiB였다.
파일을 삭제하지 않았다. 기존 748개 통과 기록은 이번 새 전체 검사 결과가 아니다.
이 커밋들은 작업 보존을 뜻하며 모든 브랜치의 출시 품질 통과를 뜻하지 않는다.
이전 입력 실패는 단독 실행에서 재현되지 않았다. 동시 부하가 원인이라는 결론도 확정하지 않는다.

전체/재검사 TRX와 브랜치 기준은 [이번 실행 검증 기록](../validation/2026-10-06-agency-execution.md)에 보존했다.

## 다음 통과 조건

Windows 입력과 실제 앱 성능은 서로 독립 실행한다. 실패가 재현되면 수정 파일을 좁혀 배정한다.
현재 입력의 전체 Release 검사는 단독 실행했다. 격리 출력 위치를 가정하지 않은 스크린샷 테스트 2개가 `/private/artifacts` 쓰기에서 실패했고, 표준 빌드 위치에서 해당 항목 2/2가 통과했다. 단일 전체 실행 748/748 통과로 기록하지 않는다. 실제 거래·설치 검증은
테스트 데이터와 허용된 환경에서 수행하고 자동 검사·실제 OS·실거래를 각각 기록한다.
공용 테스트 이용코드의 무기한 무료 권한은 공개 운영 전에 사용 정책/회수 여부를 결정한다.
기기 수·오프라인 권한·세금 표시·원화 청구·환불/지원 정책은 운영자가 결정한다.
공개 게시·배포·광고 지출·외부 메시지를 이번 내부 준비 작업과 함께 실행하지 않는다.
