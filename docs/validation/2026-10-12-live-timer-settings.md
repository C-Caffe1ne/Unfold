# 타이머 숫자 전환·시간 자동 저장 검증

## 기준과 원인

2026년 10월 12일 · 개발 `1.1.3-beta`, `codex/remove-unused-features`, HEAD `17941db`와 기존 미커밋 변경을 포함한 최신 작업트리에서 수행했다.
실행·빌드 경로는 `/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features`다.
최신 공개 배포는 `v1.1.2-beta`(소스 `ce86c7b`)이며 이번 변경을 공개 배포하지 않았다.

기존 숫자 전환은 활성 타이머의 1초 감소에만 적용됐다. 중지 후 전체 간격으로 복귀하거나 입력으로 시간을 바꿀 때는 숫자가 바로 교체됐다.
홈의 시간 설정은 저장 버튼이 필요했고 실행 중에는 알림 간격을 수정할 수 없었다.

## 변경

- **중지**는 확인 창 없이 바로 멈추며 설정된 전체 간격으로 숫자를 전환한다. 같은 중지 상태의 반복 갱신이 애니메이션을 끊지 않는다.
- 알림 간격 **1~240분**은 실행·일시정지·중지 상태에서 편집할 수 있다. 변경한 전체 간격으로 즉시 재예약하고 증가·감소 방향에 맞춰 바뀐 숫자만 약 380ms 동안 전환한다.
- 일시정지·중지 상태는 유지한다. 이미 열린 휴식 세션과 완료 기록은 바꾸지 않는다.
- 홈의 **저장** 버튼·클릭 처리·접근성 이름을 제거했다. 알림 간격과 휴식 시간 **1~10분**은 유효한 입력을 변경하면 자동 저장한다. 직접 숫자를 입력할 때 Enter도 필요 없다.
- 숫자 입력의 Text·Value 갱신이 끝난 다음 값을 읽고 빠른 편집에서는 최신 값을 저장한다. 다른 알림 설정은 현재 런타임 값에서 가져와 보존한다.
- 잘못된 입력은 저장하지 않는다. 파일 저장 실패 시 타이머 간격과 저장값은 유지하고 입력·오류 안내를 남긴다. 다음 편집에서 다시 저장하며 정상 저장 문구는 표시하지 않는다.
- 홈과 펫 타이머는 같은 `AnimatedCountdown`을 사용한다. 창 숨김·컨트롤 분리·Dispose 시 프레임 타이머를 종료한다.

## 소유 파일

- `src/Unfold.Desktop/AnimatedCountdown.cs`: 증가·감소·중지 복귀와 반복 갱신 처리.
- `src/Unfold.Desktop/AppRuntime.cs`: 실행 중 간격 변경 허용, 기존 저장 후 재예약 순서 유지.
- `src/Unfold.Desktop/SettingsWindow.cs`, `SettingsWindow.Layout.cs`: 홈 시간 자동 저장·실패 안내, 저장 버튼 제거.
- `SmokeDiagnostics.cs`, `SmokeDiagnostics.TimerRefinements.cs`: 새 조작 흐름과 네이티브 숫자 전환 캡처.
- `Tests/Unfold.Tests/{AnimatedCountdown,TimerControl,TimerRefinement,SettingsDashboard,SettingsPreferences,UiAuditRegression}Tests.cs`: 변경된 계약과 회귀 검사.
- 현재 설정 안내·한글화 안내·배포 및 Windows 체크리스트와 이 검증 기록을 갱신했다. 과거 버전의 검증 기록은 당시 동작으로 보존했다.

## 검증

| 검사 | 결과 |
|---|---|
| 전체 Release 빌드·테스트 | `dotnet test Unfold.slnx -c Release --no-restore -m:1`: **790 통과**, 실패·건너뜀 0, 종료 코드 0 |
| 저장 실패·재시도 보강 | 위 전체 검사 후 간격 변경 실패 시 시계 값 보존 검사를 보강하고 해당 검사 **1개 통과**, 종료 코드 0 |
| 상태·입력 회귀 | 실행/일시정지/중지 간격 변경, Enter 없는 입력, 빠른 변경, 직접 중지 복귀, 휴식 세션 보존, 정상 안내 숨김 확인 |
| macOS native 진단 | 새 `UNFOLD_DATA_DIR=/tmp/unfold-timer-motion-ddkpvS`, Release `Unfold.dll --smoke-test`: 종료 코드 0, `smoke.json`의 **success=true**, **110장** |
| 네이티브 렌더러 숫자 전환 | 중지 복귀·간격 증가/감소의 전환 중간과 완료 화면 캡처, 반복 inactive 갱신 및 전환 종료 확인 |
| 최소 창·레이아웃 | 전체 검사와 native 진단에서 640×560 화면·입력·타이머 제어의 배치 검사 통과 |

네이티브 진단은 약 58초 실행했으며 테스트 모드에서 계정 권한을 우회하고 새 데이터만 사용했다.
원본 보고서는 `/tmp/unfold-timer-motion-ddkpvS/verification/smoke.json`이다.

[중지 복귀 중](images/2026-10-12-live-timer-settings/timer-stopped-reset-in.png),
[중지 복귀 완료](images/2026-10-12-live-timer-settings/timer-stopped-reset-settled.png),
[간격 증가 중](images/2026-10-12-live-timer-settings/timer-home-interval-up-in.png),
[간격 감소 중](images/2026-10-12-live-timer-settings/timer-home-interval-down-in.png).

## 검증 경계

자동 UI 검사는 Headless이며 숫자 입력·버튼 이벤트를 주입했다. native 진단은 macOS 렌더러와 실제 native 창을 사용하지만 창은 off-screen이고 버튼 이벤트와 경과 시간을 주입했다.
실제 마우스·키보드의 반복 입력 감각, Windows 실기 화면·입력, 설치 파일·공개 배포는 이번에 확인하지 않았다.
시간 변경 시 현재 남은 시간에 차이만 더하는 방식이 아니라 새 전체 간격부터 예약하는 동작이다.
