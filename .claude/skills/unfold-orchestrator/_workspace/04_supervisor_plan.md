# 감독자 통합 계획

- Run: `run_fbe4d3ace2d2`
- 기준 커밋: `7e81f77` (`release/mvp`)
- 입력: 제품·BM, C#·Avalonia 아키텍처, 릴리스 QA 감사 보고서

## 공통 판정

1. 현재 C# 릴리스 후보는 Release 테스트 159개, 소스 스모크 진단, macOS arm64 자체 포함 게시와 게시 바이너리 스모크를 통과했다.
2. `--review-pet-pack` 진단은 현재 `PetWindow` 시각 트리 계약과 맞지 않아 종료 코드 1로 실패한다. 런타임 팩 재생 자체보다 검증 경로가 끊긴 상태다.
3. 프로젝트 버전은 `0.1.1`인데 커밋·제품 상태는 `0.2.0`으로 표시되어 배포물 이름과 번들 메타데이터가 어긋난다.
4. Windows 실기 기록은 공란이므로 Windows 지원 품질은 현재 자동 검사로 판정할 수 없다.
5. 제품 가설을 검증하려면 배포 가능한 파일, 행동 측정 항목, 문서와 구현의 일치가 기능 확장보다 먼저다.

## 실행 큐

| 우선순위 | 작업 | 소유 파일 | 의존성 | 완료 증거 |
|---|---|---|---|---|
| P0-1 | 펫 팩 재생 진단의 오래된 `PetWindow.Content` 가정 제거 | `src/Unfold.Desktop/PetWindow.cs`, `src/Unfold.Desktop/PetPackDiagnostics.cs`, 관련 테스트 | 없음 | 집중 테스트 + 전체 테스트 + 격리 `--review-pet-pack` 성공 |
| P0-2 | 버전 단일 출처를 실제 릴리스 `0.2.0`과 맞춤 | `src/Unfold.Desktop/Unfold.Desktop.csproj`, 패키징 검증 | P0-1과 파일 비중복 | 패키지·번들 버전과 파일명 일치 |
| P0-3 | 루트 README와 제품 문서의 없는 기능 서술 정리 | `README.md`, `docs/product-direction.md`, `docs/development-plan.md` | P0-1 뒤 현재 구현 재확인 | 모든 기능 문구에 현재 코드 근거 존재 |
| P1-1 | 펫 위치 저장과 일반 설정 저장의 스냅샷 경합 재현·수정 | `src/Unfold.Desktop/AppRuntime.cs`, 관련 테스트 | P0 완료 뒤 독립 진행 | 교차 저장 회귀 테스트와 스모크 통과 |
| P1-2 | 로컬 완료 기록에 초대·미루기·취소 행동을 측정 가능한 형태로 추가할지 기획 확정 | Core 기록 계약, 회고·CSV | 실제 사용자 실험 설계 선행 | 계정·서버 없이 초대/시작/완료/미루기/취소 집계 가능 |
| 실제 OS | Windows 실기 체크리스트 수행 | 코드 변경 없음, `docs/validation/` 기록 | win-x64 배포물 | 실제 Windows 결과와 미검증 항목 구분 |

## 병렬화 규칙

- P0-1과 P0-2는 소유 파일이 겹치지 않아 구현은 병렬 가능하지만, 이번 첫 실행은 회귀 원인이 명확한 P0-1부터 수행한다.
- P0-1 구현 뒤 release-qa가 동일 명령으로 독립 재검증한다.
- `AppRuntime.cs`를 소유하는 P1-1은 다른 `AppRuntime` 변경과 병렬 실행하지 않는다.
- Windows 실기와 서명·공증은 실제 환경·자격 증명이 필요하므로 자동 검사와 분리한다.
