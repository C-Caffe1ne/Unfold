# 스트레칭 말풍선 알림 검증

2026-09-16 · `release/mvp` 작업 트리 · macOS arm64 / .NET 10.

## 구현 범위

[사용 안내와 설계](../stretch-notifications.md)에 알림 단계, 4방향 배치, 접기, 초과 시간,
효과음과 저장 호환성을 기록했다. 기존 독립 `BreakReminderWindow`와 OS 토스트 경로를 제거하고
펫 창 내부의 `PetSpeechBubble`로 대체했다. 사용자 요청대로 n분은 기본 5분·설정 1~60분이며,
초과 표시는 +60:00에서 멈추되 완료 입력을 기다린다.

`PetReminder`와 `BreakSession`은 화면 표시와 별도로 시간을 유지한다. 접기·펫 숨김으로
세션을 완료하지 않는다. 기록은 실제 휴식 시간을 추가로 저장하되 기존 계획 시간과 CSV 열을 보존한다.
두 효과음은 자체 합성 기본음과 복사한 사용자 PCM WAV를 지원한다.

## 자동 검사

```sh
dotnet test Unfold.slnx -c Release --no-restore
dotnet test Tests/Unfold.Tests/Unfold.Tests.csproj -c Release --no-restore \
  --filter 'FullyQualifiedName~BreakReminderTests|FullyQualifiedName~PetReminderTests|FullyQualifiedName~ReminderSoundTests|FullyQualifiedName~SettingsDashboardTests'
dotnet build src/Unfold.Desktop -c Release --no-restore
```

- 전체 **158개 통과**, 실패·건너뜀 0.
- 이후 설정 적용 검사 1개를 추가하고 효과음 음소거·파일 크기 안내를 보완한 최종 관련 **20개 통과**, 실패·건너뜀 0.
- 최종 Release 앱 빌드: **경고 0 / 오류 0**.
- 조기 완료(0초 포함), 늦은 완료, +60:00 상한, 중복 완료 방지, 절전·역행 시간 처리.
- 작업 5분 전 알림의 주기당 1회 발생, 미루기·일시정지·취소, 실제/계획 시간 저장·재로드·CSV.
- 말풍선 버튼, 긴 안내에서 타이머 아래 완료 버튼 배치, 네 방향, 접기 후 진행 유지.
- 음수 원점 모니터·1/1.5/2배 배치 계산, 기존 설정의 기본값, 범위 밖 설정 거부.
- WAV 형식·크기 검증, 사용자 파일 복사 후 원본 삭제, 손상 파일의 기본 소리 대체.
- 작업 중 말풍선 설정 적용이 작업 타이머를 초기화하거나 일시정지하지 않음을 확인.

## macOS 네이티브 진단

```sh
dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
```

최종 실행은 새 격리 프로필에서 **종료 코드 0**, [진단 JSON](2026-09-16-stretch-speech.json)의
`success: true`를 확인했고 PNG **42장**을 만들었다.

- 알림 시 창 수가 늘지 않음. 펫 창에 위·아래·왼쪽·오른쪽 말풍선을 렌더링.
- 초과 타이머와 완료 안내, 접었을 때 192×192 펫만 유지, 펼쳤을 때 동일 세션 유지.
- 알림·완료 효과음 요청이 각 1회, 중복 알림과 음소거 상태에서는 추가 소리 요청 없음.
- 숨겨 둔 펫이 알림 동안 표시되고 미루기 후 숨음. 저장된 ShowPet 값은 유지.
- 기존 타이머·개인화·펫 팩 설치·커스텀 팩 생성 진단도 통과.
- 860×680 설정 창의 말풍선 알림 카드, 네 방향과 초과·완료 캡처를 직접 확인.

최종 캡처 위치:
`/var/folders/r6/7hjm96zd53g3dfxkbbb9jbnh0000gn/T/Unfold-smoke-6ef603c007f949589385fcdc9c4e5f85/verification/`

진단 중 네이티브 오디오는 억제하고 이벤트 횟수와 WAV를 검사한다. 별도로 첫 통과 진단이
생성한 기본 알림·완료 WAV를 각각 `/usr/bin/afplay`로 실제 macOS 오디오 서비스에 전달했고,
두 명령 모두 종료 코드 0이었다. 이 결과는 재생 명령의 성공이며 사용자가 소리를 들었다는 증거는 아니다.

## 확인하지 않은 범위

Windows/Intel Mac에서의 실제 실행, Windows 투명 영역 클릭 통과와 말풍선 클릭,
실제 마우스 드래그·다중 모니터 DPI 전환·OS 파일 선택기, 시스템 음량에 따른 청감은 미검증이다.
macOS 진단은 화면 밖 창과 프로그램 방식 버튼 이벤트를 사용했고 휴식 시간은 주입했다.
실제 60분 대기, 장시간 자원 사용, 배포물 생성·서명·공증은 실행하지 않았다.
기존 사용자 데이터 대신 새 `UNFOLD_DATA_DIR` 프로필을 사용했다.
