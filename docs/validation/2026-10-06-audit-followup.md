# 분야별 실행 결과 — 2026-10-06

최신 `1.1.0-beta` 개발본의 P1 진단과 P2 버그·UI·문서를 수정하고, 최종 **748개 테스트 모두 통과**했다. macOS ARM64 독립 게시본의 통합 진단도 통과했다. 실제 Windows와 설치본 업데이트·장시간 메모리는 출시 전 확인이 남아 있다.

## 기준과 협업

- 소스: `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold`
- 브랜치: `codex/glb-import-compat`, HEAD: `c44ef129d76011f29c1b875f3019c3923a274e04` + 기존 사용자 변경과 이번 미커밋 수정.
- 작업 시작 전 worktree·버전·실행 경로 재확인. 이전 주 작업 디렉터리 `release/mvp` 0.2.2에는 제품 변경을 적용하지 않았다.
- Team Alignment 감사 계획을 실행 기준으로 사용했다. 감독자가 QA·문서·릴리스 검증을 맡고, GLB 구현과 UI 구현 담당이 파일을 나눠 작업했다. 런타임 감사 담당은 수정 후 읽기 전용 검토를 수행했다.
- 기존 GLB 호환성·Windows 클릭 통과 변경을 보존했다. 기존 171개 소스·테스트 입력 중 이번 소유 범위를 벗어난 변경 0, HEAD 동일. 새 terminal 테스트 파일 1개 추가. 실제 사용자 데이터는 사용하지 않았다.

## 완료한 작업

| 항목 | 중요도 | 변경과 검증 | 상태 |
|---|---|---|---|
| QA-01 자동 미루기 | P1 | 같은 monotonic deadline으로 계측하고 조건별 JSON을 기록. 세션 종료와 native 숨김 완료를 분리하되 2초 지연 상한 유지 | 완료 |
| QA-01 즉시 만료 분기 | P1 | 지난 deadline을 즉시 처리할 때 새 상태로 화면 갱신. Pause 2조건이 수정 전 숨김 실패 → 수정 후 통과 | 완료 |
| R-01 GLB 임의 속도 | P2 | 1.25배 등 유효한 가져오기 속도를 선택지에 보존. 방향·클립·반복 편집과 저장·재열기 정상 | 완료 |
| R-02 GLB 종료 포즈 | P2 | one-shot을 실제 clip.End로 렌더. 완료 후 확대·완료 이벤트 1회·loop·소비자 버퍼 유지 | 완료 |
| A-02 키보드 포커스 | P2 | 표시된 media/GLB 이름 입력에 포커스·스크롤. 최소/기본창 4조건 자동 검사와 macOS 실제 Enter·Shift+Tab 확인 | 완료 |
| D-01 문서 기준 | P2 | AGENTS, 문서 안내·MVP·개발 계획·검증·GLB 사용법을 현재 1.1.0 코드와 맞춤 | 완료 |
| G-01 OS 입력 | P1 출시 조건 | macOS native 창에서 외부 OS 키 입력으로 두 편집기 확인. Windows·화면 읽기·사용자 물리 입력 범위는 미검증 | 일부 검증 |
| G-02 성능 | P1 출시 조건 | 두 모델 6회 전환·1,440프레임의 이전/현재 비교. 실제 앱 장시간 검증은 필요 | 일부 검증 |
| G-03 릴리스 | P1 출시 조건 | 동일 입력 clean copy의 locked restore·Release build·ARM64 publish·게시본 smoke 확인 | 로컬 통과, 설치 검증 남음 |

## 통합 검증

| 검사 | 결과 | 증거 |
|---|---|---|
| 최종 전체 Release 테스트 | 748/748, 실패·skip 0 | final-tests-v3.log / final-test-results-v3/final-release-v3.trx |
| 즉시 만료와 인접 알림 검사 | 37/37 | expiry-green.log / expiry-green/expiry-green.trx |
| 독립 소스 Release build | 경고 0, 오류 0 | clean-build-v4.log |
| canonical/RID locked restore | 모두 종료 0, 기존 canonical lock 동일 | clean-restore-v4.log / clean-rid-locked-v4.log |
| ARM64 self-contained publish | 종료 0, 419개 빌드 입력 hash 동일 | clean-publish-v4.log / clean-publish-verification-v4.json |
| 최종 게시본 macOS smoke | success=true, 56.672초 | published-smoke-v4.json |
| 최종 자동 미루기 | 30.013598초, deadline 지연 0.016732초, 실패 없음 | published-auto-snooze-v4.json |
| Hikari native GLB 검사 | 35애니메이션, 13,344삼각형; 6행동 완료 이벤트·가져오기·저장·재로드·설정 통합 통과 | glb-result.json |
| macOS 키보드 | 640×560, media와 GLB 모두 Enter→이름 포커스, Shift+Tab→메뉴. OS 자동 입력이며 사람이 직접 누른 검증은 아님 | native-keyboard-observations.json |
| 변경 보존 | 허용 파일 외 소스·테스트 hash 변화 없음, HEAD 동일 | preservation-final.json |

최종 자동 미루기는 pause 유지, stopped=false, 남은 180초, 펫 숨김, 완료 기록 불변, snooze +1을 확인했다. 세션 종료 직후 가시성은 `False`, 숨김까지 추가 관찰 시간은 0.000180초다.

Hikari와 macOS 키보드 검사는 GLB·UI 수정 후 수행했다. 이후 추가한 변경은 알림 즉시 만료와 진단 계측이며 GLB·UI 코드는 동일하다. 게시본 smoke는 이 최종 알림 변경을 포함한다. GLB 확대는 native renderScaling=1에서 192/288/384픽셀을 확인했고 2x Retina나 Windows DPI 결과로 해석하지 않는다.

## 실패 이력과 원인 범위

최초 감사 smoke 2회는 복합 오류만 남아 과거 각각의 원인은 확정할 수 없다. 이번 첫 게시본에서는 Session=null/Notice=None/남은180초/snooze+1인데 펫이 visible인 상태를 계측했다. 세션 완료 이후 창 갱신이 이어지는 정상 경로도 있으므로 이 JSON만으로 지속적인 제품 숨김 실패는 확정하지 않았다. 별도로 이미 지난 deadline의 즉시 분기에서 갱신이 빠진 결함은 새 2조건으로 재현·수정했다. 수정 후 통과가 이전 세 실패의 원인 모두를 확정하는 것은 아니다.

최초 통합 전체 검사는 745/746이었다. 기존 GLB 클릭 테스트가 click 진입과 idle 복귀를 한 줄에서 기다렸다. 렌더 준비·painted hit·down Pending·up click을 단계별로 확인하도록 보강했다. 입력 타이머나 대기 상한을 늘리지 않았다. 그 뒤 746/746, 알림 추가 수정 후 최종 748/748이 통과했다. 최초 click 관찰 실패 원인은 재현하지 못해 미확정이다. 원시 실패 로그와 TRX를 보존했다.

독립 복사본의 첫 빌드는 Bori Art 입력 누락으로 실패했고 입력을 보충했다. RID 복원은 `-r`과 단일 `RuntimeIdentifier` 기반 lock 경로의 차이 때문에 임시 canonical lock을 잘못 선택했다. 복사본에서 이를 되돌리고 `-p:RuntimeIdentifier=osx-arm64`로 RID lock을 분리했다. 최신 소스의 lock은 바꾸지 않았고 두 lock의 net10.0 종속성은 동일하다.

## 성능 판정

Hikari·Kazusa 각 120프레임, 6회 반복(총1,440), 192/384픽셀, 독립 console 렌더러를 동일 조건으로 비교했다. 이 결과는 실제 앱 UI·장시간 메모리 인증이 아니다.

| 측정 | 이전 구현 | 현재 구현 |
|---|---:|---:|
| 소요 시간 | 14.483초 | 13.378초 |
| CPU 누적 | 13.639초 | 13.008초 |
| 누적 managed 할당 | 580,470,888B | 580,209,168B |
| 첫 반복 GC 후 managed | 151,232B | 151,168B |
| 마지막 GC 후 managed | 152,544B | 152,528B |
| 최대 RSS | 76,939,264B | 93,388,800B |
| 마지막 RSS | 69,468,160B | 93,388,800B |

관리 메모리와 누적 할당은 비슷하지만 이번 한 쌍의 실행에서 현재 RSS가 높다. native 메모리 변동·회귀·누수를 구분할 추가 근거가 필요하다. 메모리 개선이나 무회귀 완료로 판정하지 않는다.

## 다음 작업 순서와 통과 기준

| 순서 | 담당 분야 | 중요도 | 다음 작업 | 완료 기준 |
|---|---|---|---|---|
| 1 | Release QA / Windows | P1 | 실제 Windows에서 투명 영역 클릭 통과·클릭/드래그 해제·다중 DPI 확인 | native 입력 결과·OS/GPU/DPI와 캡처 기록 |
| 2 | Runtime / 성능 QA | P1 | 실제 앱에서 고정 모델로 30분 이상 재생·펫/행동 반복 전환, 동일 기준 재측정 | warm-up 이후 RSS/managed/CPU 추세와 반복 결과로 이번 RSS 차이 판정 |
| 3 | Release QA | P1 | 최종 설치 패키지 서명·공증, 기존 설치본→업데이트와 사용자 데이터 보존 확인 | 실제 설치·재실행·기록/설정/펫 보존·버전/해시 일치 |
| 4 | UI 접근성 QA | P2 | VoiceOver/Narrator·테마 대비·실제 Tab 순서 검수 | OS별 탐색/레이블/포커스와 오류 복구 확인 |
| 5 | Runtime / UX | P3 | RootNode 중복 이름 호환성, 이름-only 초안과 저장/적용 피드백 후보 선별 | 사용자 영향·재현 근거를 확보한 뒤 별도 범위 결정 |

Windows 실행 환경, 공개 배포·실거래·설치본 교체는 이번 작업에서 수행하지 않았다. 이 결과는 현재 개발본 수정 및 로컬 검증 완료이며 위 출시 조건을 충족했다는 의미는 아니다.
