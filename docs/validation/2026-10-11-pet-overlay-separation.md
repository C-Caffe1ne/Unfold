# 펫 렌더 창과 말풍선·타이머 창 분리

2026-10-11 · `1.1.3-beta` · `codex/remove-unused-features` · 기준 HEAD `17941dbd5cb9e64de3eb452783695be2f312dc6f`.

작업 경로: `/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features`.

## 기준과 보존

- 작업 시작 시 worktree별 브랜치·HEAD·미커밋 변경과 csproj 버전을 비교했다. 이 개발본은 `bbdb4a8`의 `1.1.1-beta`보다 16커밋, `v1.1.2-beta` 소스 `ce86c7b`보다 4커밋 앞선 소스다. 메인 checkout의 `ce0ccda`는 프로젝트 버전 `0.2.2`라 기준으로 사용하지 않았다.
- 기존 미커밋 경로 52개를 해시와 일부 원본 파일로 기록했다. 이번 작업에서 함께 수정한 `AppRuntime.cs`와 `SmokeDiagnostics.cs`를 제외한 50개는 내용과 삭제 상태가 동일했다. 두 공유 파일도 원본과 비교해 기존 홈 미리보기·기능 제거 변경을 유지했다.
- 사용자 프로필과 설치 앱은 수정하지 않았다. 모든 검사에 별도 `UNFOLD_DATA_DIR`를 사용했다. 커밋·배포는 수행하지 않았다.

## 원인과 변경

기존 `PetWindow.RefreshSpeech()`는 같은 Canvas 안에서 펫과 말풍선을 배치한 뒤 native window의 위치를 보정했다. 좌우 전환 시 펫의 Canvas 좌표와 native 창 좌표를 순차 변경해야 하는 구조였다. 이번 변경은 말풍선 갱신이 펫의 렌더 면을 배치·이동·리사이즈하는 경로 자체를 제거한다. macOS 합성기의 하위 원인을 특정한 변경은 아니다.

- `PetWindow`에는 `AnimationView`만 남겼다. 펫의 내부 원점은 `(0, 0)`이고 창 크기는 펫 크기에만 따라간다.
- 별도 native `PetOverlayWindow`에 말풍선, 꼬리와 타이머 조작 바를 배치했다. 기본 면은 `332 × 260`이며 알림 표시·해제와 타이머 상태 변경은 이 창 안에서만 처리한다.
- 펫이 화면 왼쪽에 있으면 오버레이를 오른쪽에, 오른쪽에 있으면 왼쪽에 배치한다. 가운데 24 logical unit 구간에서는 이전 방향을 유지하며, 화면 가장자리에서는 공간이 있는 쪽을 선택한다. 말풍선과 조작 바가 펫 면을 확장하지 않는다.
- 화면의 WorkingArea 안으로 오버레이를 맞추고, 펫의 기존 Dock 근처 배치 계약을 유지했다.
- 조작 바는 남은 시간과 44 × 44 버튼 두 개로 구성한다. 시작/일시정지/계속 버튼과 중지 버튼이 홈·트레이와 같은 `StretchClock` 및 기존 휴식 상태를 사용한다. `AnimatedCountdown`을 재사용한다.
- 표시된 버튼만 native 입력을 받는다. 시간 표시, 대사, 꼬리와 투명 여백은 클릭을 통과시킨다. 버튼을 누른 채 포인터가 벗어나도 캡처가 해제될 때까지 입력을 유지한다.
- 설정의 말풍선 상세 영역에 “펫 옆에 타이머 조작 바 표시”를 추가했다. 기본값은 표시이며 저장·복구 계약을 포함한다. 조작 바를 숨겨도 휴식 알림과 펫은 유지한다.
- 펫 숨김·재표시·종료에 오버레이 수명을 연결했다. 중지 확인창은 오버레이를 owner로 사용하고 펫 옆에 표시한다. 실제 macOS에서 발견한 확인창과 조작 바의 표시 순서도 보완했다.
- 버튼을 누르거나 확인창이 떠 있는 동안 배회 위치 이동을 잠시 보류한다. 펫 프레임 재생은 기존 상태에 따라 계속한다. 휴식 시작·완료에 따른 의도된 행동 전환은 유지했다.

## 수정 범위

- 펫·오버레이: `PetWindow.cs`, `PetWindow.Input.cs`, `PetWindow.Companion.cs`, 새 `PetOverlayWindow.cs`, `WindowsPetWindow.cs`.
- 조작·확인창: `TimerControls.cs`, `AppRuntime.cs`, `Ui.cs`.
- 설정: `AppSettings.cs`, `AppSettings.Recovery.cs`, `SettingsWindow.Preferences.cs`, `SettingsWindow.Notifications.cs`.
- 검사·진단: 새 `PetOverlayWindowTests.cs`, 기존 알림·클릭 통과·펫 행동·설정·복구 테스트, `SmokeDiagnostics.cs`의 별도 창 참조.

## 자동 검사

| 검사 | 결과 |
| --- | --- |
| 수정 전 관련 검사 | 81 통과 |
| 기존 단일 창 구조를 잡는 회귀 검사 | 수정 전 예상 실패 1개 확인 |
| 분리 구현 집중 검사 | 101 통과 |
| 최종 설정·조작·배치 집중 검사 | 90 통과 |
| 전체 Release 회귀 검사 | 789 통과, 실패 0, 건너뜀 0 |
| 마지막 확인창 Topmost 보완 이후 관련 검사 | 16 통과, 실패 0 |
| 제품 실행 파일 `--smoke-test` | 종료 코드 0, `success: true`, PNG 95장 |
| Release 및 네이티브 검증 host 빌드 | 오류·경고 0 |
| `git diff --check` | 통과 |

전체 789개 검사와 제품 smoke는 마지막 확인창 Topmost 한 속성 보완 이전에 실행했다. 이 보완 후에는 관련 16개 검사와 실제 macOS 확인창의 취소·중지·재시작을 다시 검증했다.

추가 회귀 검사는 버튼과 알림 전환 중 펫 native 위치·크기·내부 Bounds 이벤트가 발생하지 않는지, 음수 좌표 모니터·2배 배율·가장자리에서 오버레이가 펫과 겹치지 않는지, 방향 전환 구간과 저장·숨김·종료·확인창 계약을 확인한다.

## 실제 macOS 검사

자동 headless 검사와 별도로 실제 NSWindow와 제품 렌더러를 실행했다. 외부 진단 host는 제품과 동일한 `Unfold.dll`을 사용하며 해시 일치를 확인했다. 인증·사용자 데이터는 격리하고, 제품의 `PetWindow`, `PetOverlayWindow`, 클릭 통과와 타이머 명령을 사용했다. UI 입력은 CUA로 실행했다.

- 보리 150%와 기존 GLB 펫 Kei를 각각 양쪽 화면 위치에 배치했다. 좌우 전환은 진단 키로 실제 `PetWindow.Position`을 이동시켜 자동 배치를 실행했다.
- 각 위치에서 미루기·휴식 시작·완료 버튼을 직접 눌렀다. 시작·일시정지·계속·중지 확인 취소·중지 확인·재시작도 확인했다.
- GLB 휴식 중 기존 걷기 행동의 위치 이동은 관찰됐다. 이는 이번 변경의 리사이즈/내부 원점 변화와 구분했다.
- 네이티브 기록에서 펫은 `288 × 288`, 내부 원점 `(0, 0)`을 유지했고 펫/오버레이의 서로 다른 native handle을 유지했다. 기록된 버튼 이벤트에서 펫 Size/Bounds 변경 횟수는 0이었다.
- 시간 표시를 클릭했을 때 조작 바 뒤쪽에 둔 검증 버튼의 클릭 횟수가 증가했다. 실제 클릭 통과를 확인했다.
- 조작 바 표시 값을 껐다 켰을 때 휴식 말풍선은 유지되고 조작 바만 숨김·복원됐다.
- 실제 중지 확인창은 펫을 가리지 않았으며 조작 바보다 위에 표시됐다. 취소와 확인 후에도 펫의 크기·내부 배치가 유지됐다.

유효한 전체 화면 합성 녹화에서 다음을 확인했다.

| 녹화 | 디코딩한 프레임 | 펫 색상이 사라진 프레임 | 양쪽 위치에 중복 검출 |
| --- | ---: | ---: | ---: |
| 보리 프레임 고정, 초기 분리 구현 | 2,584 | 0 | 0 |
| 보리 버튼·좌우 전환, 초기 분리 구현 | 2,584 | 0 | 0 |
| GLB 버튼·좌우 전환, 최종 빌드 | 1,148 | 0 | 0 |
| 보리 최종 짧은 녹화 | 316 | 0 | 0 |

보리 고정 영상의 색상 중심점 변동은 X 약 0.44px, Y 약 0.55px였다. 영상 검사는 보리의 크림색 영역과 GLB의 분홍색 특징을 기준으로 사라짐·양쪽 중복을 찾았다. 모든 잔상 형태나 GPU presentation을 포괄하는 검사는 아니다. 중간에 제품 smoke와 겹쳐 실행한 녹화 한 개는 검증 창의 배경만 담겨 유효한 펫 합성 녹화로 사용하지 않았다. 최종 검사는 단일 검증 앱에서 수행했다. 짧은 녹화는 실제 저장된 구간만 집계했다.

![오른쪽 보리, 왼쪽 말풍선과 타이머 조작 바](/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features/docs/validation/2026-10-11-pet-overlay-separation/bori-right.png)

![왼쪽 보리, 오른쪽 말풍선과 타이머 조작 바](/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features/docs/validation/2026-10-11-pet-overlay-separation/bori-left.png)

![GLB 펫과 별도 말풍선·조작 바](/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features/docs/validation/2026-10-11-pet-overlay-separation/glb-right.png)

![펫 옆의 중지 확인창](/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features/docs/validation/2026-10-11-pet-overlay-separation/stop-confirmation.png)

## 근거와 검증 범위

- [검사·네이티브 이벤트·프레임 분석 요약](/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features/docs/validation/2026-10-11-pet-overlay-separation/verification.json)
- [제품 smoke 결과](/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features/docs/validation/2026-10-11-pet-overlay-separation/smoke.json)
- 원본 TRX, 진단 host, 프레임 분석 스크립트, 영상과 native JSONL: `/tmp/unfold-overlay-20261011`.
- 최종 GLB 녹화: `/tmp/unfold-overlay-20261011/native-glb-final.mov`.

Windows 실제 실행, 여러 물리 모니터 사이 이동, 전체 화면 Spaces 전환과 사람이 직접 드래그하는 입력은 이번 실제 OS 검사에 포함하지 않았다. 실제 클릭과 native 위치 전환에서 튐이 재현되지 않았다는 결과이며 모든 장비·OS에서의 무조건적인 무발생을 의미하지 않는다.
