# 타이머 설정·아이콘 조작 수정

2026-09-13 · `release/mvp`, 기존 정리·개인화 데모 위의 작업 트리. macOS arm64, .NET 10.
이번 범위는 간격 적용, Reset의 자동 재시작 제거, Stretch now 제거와 타이머 아이콘 조작이다.

## 재현과 원인

제품 코드를 바꾸기 전에 두 회귀 검사를 추가해 실패를 확인했다.

- 간격 입력값을 25로 바꿔도 `Settings.IntervalMinutes`는 60이었다. 입력의 ValueChanged와
  저장 경로가 연결되어 있지 않아 Apply를 누르기 전까지 기존 설정으로 Reset했다.
- Reset 직후 `Paused`가 false였다. `StretchClock.Reset`이 일시정지를 명시적으로 해제했다.

수정 전 결과는 2개 실패다. 입력·버튼 경로의 검사에는 별도의 `UNFOLD_DATA_DIR`을 사용했다.
원래 사용자 데이터와 설정은 사용하지 않았다.

## 현재 동작

| 조작 | 결과 |
|---|---|
| Remind me every 변경 | 유효한 정수 5–240분을 즉시 저장하고 타이머에 반영. 직접 입력 후 Enter도 지원 |
| 일시정지 / 재생 | 남은 시간을 보존해 멈춤 / 이어가기 |
| 정지 | 현재 카운트다운을 00:00으로 만들고 대기. 재생 시 현재 설정 간격으로 시작 |
| 리셋 | 현재 설정 간격으로 되돌리고 일시정지. 재생 전에는 감소하지 않음 |
| 정지 중 간격·프로필 변경 | 새 설정만 저장하고 00:00 유지 |
| 열린/생성 중 초대에서 정지·리셋 | 초대를 취소하고 완료 기록을 추가하지 않음 |

Settings는 44×44 벡터 아이콘 버튼 3개를 사용한다. 재생·일시정지 아이콘은 상태에
따라 바뀐다. 접근성 이름과 500ms 지연 툴팁을 제공하며 키보드 Space 조작을 검사했다.
네이티브 트레이 메뉴는 Start/Pause/Resume, Stop timer, Reset timer 텍스트 항목을 유지한다.
Stretch now는 Settings·트레이·펫 메뉴에서 모두 제거했다. 자동 초대와 내부 진단 경로는 유지한다.

간격만 바꿀 때 아직 Apply하지 않은 유휴 기준·루틴 선택까지 함께 적용하지 않는다.
기존 Apply reminder settings는 이 두 설정과 저장 재시도에 사용한다. 캐릭터 미리보기
로드를 기다리는 동안 사용자 입력을 무시하지 않도록 UI 갱신 보호 구간도 동기 부분으로 제한했다.

## 자동 검사

전체 Release 검사 **77 통과, 실패 0, 건너뜀 0**. 이번에 추가한 검사는 6개다.

- Reset 후 대기와 명시적 재생 후 감소.
- 입력 컨트롤 변경 → 런타임 타이머 → settings.json 저장.
- 직접 텍스트 입력·Enter → Reset이 새 간격으로 대기.
- 일시정지·정지·리셋의 서로 다른 재개 규칙과 정지 중 간격 변경.
- 간격 변경이 Pause와 다른 필드의 미적용 입력을 보존.
- 아이콘의 접근성 이름·툴팁, 최소 창 크기에서 버튼 배치, Space 재생, Stretch now 부재.

기존 타이머 검사의 실행 준비는 Reset 대신 명시적인 Start를 사용하도록 변경했다.
idle/sleep·활성 시간 검사의 기대값은 그대로 유지했다.

```sh
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  dotnet test Unfold.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
```

## macOS 게시·진단

저장소는 `RuntimeIdentifier`에 따라 `obj/<RID>/packages.lock.json`을 선택한다.
게시용 복원이 없던 상태의 NETSDK1047과 `restore -r`의 잠금 파일 불일치를 확인했고,
아래 명령으로 기존 RID별 잠금 파일을 사용해 복원했다. 의존성 버전과 추적 중인
잠금 파일은 변경하지 않았다.

```sh
dotnet restore src/Unfold.Desktop/Unfold.Desktop.csproj -p:RuntimeIdentifier=osx-arm64 --locked-mode
dotnet publish src/Unfold.Desktop/Unfold.Desktop.csproj -c Release -r osx-arm64 \
  --self-contained true --no-restore -p:PublishReadyToRun=true \
  -p:UseSharedCompilation=false -m:1 -o <새-publish-디렉터리>
UNFOLD_DATA_DIR=<새-테스트-프로필> <게시-디렉터리>/Unfold --smoke-test
```

최종 게시는 종료 코드 0, 진단은 `success: true`와 종료 코드 0으로 통과했다.
[진단 JSON](2026-09-13-timer-controls-smoke.json)의 `timerControlsVerified`와
`resetWhileOpening`은 모두 true다. PNG 14장과 기존 완료·CSV 1건을 확인했다.
600×850 Settings 화면에서 세 아이콘의 배치와 Pause/Play 전환을 확인했다.
타이머 진단은 실제 게시 앱의 입력 컨트롤과 버튼 이벤트를 사용한다. Reset·Stop 뒤
각각 1.2초를 실제로 기다려 상태·남은 시간을 비교한다. 기존 루틴·프로필·완료·CSV
진단도 함께 실행하며, 휴식 세션의 시간과 CSV 목적지는 이전과 같이 주입한다.

로컬 화면·로그는 git 제외 경로 `artifacts/verification/timer-controls/`에 보관한다.
[Reset 후 화면](../../artifacts/verification/timer-controls/timer-reset.png),
[정지 후 화면](../../artifacts/verification/timer-controls/timer-stopped.png).
저장소 복제본에는 이 로컬 캡처 파일들이 포함되지 않는다.

Windows 실기, 실제 사용자 키보드·마우스 조작 및 스크린리더 음성 출력,
툴팁의 실제 화면 표시 시간, 장시간 타이머·배터리는 별도 확인이 필요하다.
짧은 네이티브 진단을 장시간 실사용·배포 서명·공증의 통과로 해석하지 않는다.
