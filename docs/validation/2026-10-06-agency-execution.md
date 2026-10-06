# Agency Agents 기존 변경 커밋·준비 작업 검증 — 2026-10-06

## 변경과 기준

사용자가 기존 미커밋 변경 커밋과 Agency 계획 실행을 승인했다. 네 브랜치의 기존 변경을
각각 보존하고 최신 `codex/glb-import-compat`, `1.1.0-beta`에서 준비 작업을 진행했다.

| 브랜치 | 기존 변경 보존 커밋 | 이번 검증 |
|---|---|---|
| release/mvp | fff3aee | GLB·설정 19/19, 기존 입력 실패 항목 단독 7/7 |
| codex/beta-v1.0.4 | 95f7a21 | Supabase 43/43, 브랜드 ZIP CRC 정상·hash 77/77 |
| feat/pet-pack-ux | 5dc4f7e | 파일 연결·기존 인스턴스 전달 18/18 |
| codex/glb-import-compat | 18a3a31 | 격리 Release 빌드, GLB·Windows 연결·호버 집중 67/67 |

보존 커밋 뒤에는 제품 소스·테스트를 수정하지 않았다. 현재 기능과 다른 제품/BM 문서,
검사 범위를 과장한 Windows 설치 안내를 바로잡았다. 담당·소유권·남은 조건을 담은
[실행표](../plans/2026-10-06-agency-execution.md), [지원 초안](../operations/2026-10-06-support-playbook.md),
[데모·게시물 초안](../operations/2026-10-06-launch-materials.md)을 추가했다.
문서 링크·diff 공백 오류 확인과 현재 코드 대조를 수행했다.

## 최신 전체 검사와 재검사

최신 제품 입력 `18a3a31`에서 격리 산출물 기반 전체 Release 검사 748개를 단독 실행했다.
746 통과, 2 실패, skip 0, 종료 코드 1. 실패는 아래 두 테스트가 `AppContext.BaseDirectory`에서
상위 5단계로 이동해 `/private/artifacts`에 스크린샷을 쓰려다 권한 오류가 난 것이다.

- `UiTests.EditorRendersAtDefaultAndMinimumWindowSizes`
- `AccountWindowTests.ALayoutRendersLoginAndPurchaseWithSharedThemesAndFitsMinimumSize`

표준 빌드 출력 위치에서 새 Release 빌드 후 두 항목만 실행하여 2/2 통과했다.
제품·테스트 코드를 수정하거나 `/private/artifacts`를 새로 만들지 않았다.
집중/전체/대체경로 검사를 구분하며 단일 전체 실행 748/748 통과라고 보고하지 않는다.

근거: [전체 TRX](2026-10-06-agency-execution/final-full.trx),
[표준 경로 2항목](2026-10-06-agency-execution/layout-standard.trx),
[최신 집중 TRX](2026-10-06-agency-execution/latest-focused.trx),
[브랜치·검사 요약](2026-10-06-agency-execution/summary.json).

## 이전 작업본 검사 경계

처음 이전 작업본 전체 검사들을 동시 실행하던 중 공간 부족 오류가 발생했고 사용자 중단으로
완료 TRX를 얻지 못했다. legacy 입력 검사에서 실패 3건을 관찰했으나 해당 항목 단독 실행은
7/7 통과했다. 동시 부하가 모든 실패의 원인이라고 확정하지 않는다. pet-pack-ux 전체에서는
미디어 도구 부재 skip도 있었다. 이후 최신 단독 전체 검사에서는 공간 부족이 없었다.
임시파일·기존 사용자 파일은 삭제하지 않았다.

## 미검증과 운영 결정

실제 Windows 입력/GPU/DPI·다중 모니터, 실제 앱 GLB 30분 이상 자원 추세,
최종 설치본 업데이트와 설정/기록/펫/로그인 보존, 운영 OAuth·실결제·환불은 남아 있다.
서버·브랜드 커밋을 최신 앱에 병합하지 않았다. 공개 게시·배포·광고·외부 연락도 수행하지 않았다.
현재 계정 권한 재확인 실패는 앱 사용을 잠그는 경로가 있으므로 오프라인 사용을 보장하지 않는다.
공용 테스트 무료 이용코드 정책, 원화 청구·세금·기기 수·오프라인·환불 정책은 결정이 필요하다.
