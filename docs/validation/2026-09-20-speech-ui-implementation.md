# 말풍선 UI 구현·검증

2026-09-20 · BUI-01 4차 시안 적용.

## 원인

제품 말풍선은 폭 320px에 높이 170/268px, 제목 17px, 여백 18px를 사용했고 제목 아래에
루틴·동작·완료 설명 본문을 표시했다. 승인된 시안은 현재 조작과 하단 상태 안내를 보존하면서
본문을 제거하고 제목을 중앙 정렬하며 상태별 세로 크기를 줄이는 구성이었다.

## 변경

- 제목을 18px semibold·중앙 정렬로 바꾸고 모든 상태의 설명 본문과 해당 툴팁을 제거했다.
- 폭 320px, 상하 14px·좌우 20px 여백, 모서리 24px, 요소 간격 6px를 적용했다.
- 버튼 높이 40px, 타이머 크기 40px를 적용했다.
- 상태별 높이는 사전 알림 96px, 휴식 초대 159px, 휴식·초과 196px, 완료 113px다.
- 미루기·휴식 시작·완료, 타이머, 하단 접기·완료·한도 안내와 네 가지 테마를 유지했다.
- `PetWindow`는 기존부터 실제 `bubble.Height`를 `PetBubbleLayout`에 전달하므로 그 계약을 그대로 사용했다.

## 소유 파일

- `src/Unfold.Desktop/PetSpeechBubble.cs`
- `src/Unfold.Desktop/DesignSystem.cs`
- `src/Unfold.Desktop/SmokeDiagnostics.cs`
- `Tests/Unfold.Tests/BreakReminderTests.cs`
- `Tests/Unfold.Tests/UiAuditRegressionTests.cs`

## 자동 검사

| 검사 | 결과 |
|---|---|
| 말풍선·배치 집중 테스트 | 6/6 통과 |
| 전체 Release 테스트 | 212/212 통과, 실패·건너뜀 0 |
| macOS 네이티브 off-screen 진단 | 성공, PNG 82장 생성 |
| 말풍선 구조 계약 | 본문 0, 제목 중앙 정렬, 상태별 높이·40px 조작 확인 |
| 기능 회귀 | 네 방향, 시작·초과·완료·접기/펼치기, 효과음 호출·기록 저장 통과 |
| 파일 검사 | `git diff --check` 통과 |

실행 명령:

```sh
dotnet test Unfold.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
UNFOLD_DATA_DIR=<new-empty-directory> dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
```

진단에는 새 격리 프로필을 사용했다. 세션 경과와 파일 선택·내보내기 목적지는 진단용으로 주입했고,
효과음은 실제 청취 대신 호출 횟수와 WAV 파일을 검사했다.

## 캡처 확인

| 상태·방향 | 결과 |
|---|---|
| [위](images/2026-09-20-speech-ui-implementation/speech-top.png) / [아래](images/2026-09-20-speech-ui-implementation/speech-bottom.png) | 초대 제목·두 버튼·하단 안내, 꼬리와 펫 기준 위치 확인 |
| [왼쪽](images/2026-09-20-speech-ui-implementation/speech-left.png) / [오른쪽](images/2026-09-20-speech-ui-implementation/speech-right.png) | 159px 말풍선의 세로 중앙 배치와 조작 영역 확인 |
| [초과](images/2026-09-20-speech-ui-implementation/speech-overtime.png) | 제목·`+00:10`·완료·하단 안내 확인 |
| [완료](images/2026-09-20-speech-ui-implementation/speech-completed.png) | 제목과 접기 안내만 표시, 본문 제거 확인 |
| [5분 전](images/2026-09-20-speech-ui-implementation/speech-five-minutes.png) | 96px 높이와 중앙 제목 확인 |
| [접힘](images/2026-09-20-speech-ui-implementation/speech-folded.png) | 세션 유지와 펫 단독 상태 확인 |

## 미검증·위험

현재 macOS에서 자동 테스트와 네이티브 off-screen 렌더링을 확인했다. 실제 포인터·키보드 조작,
효과음 청취, Windows 실기, 125/150/200% 배율, 다중 모니터 경계와 장시간 사용은 이번 단계에서 미검증이다.
화면 읽기·낭독 기능은 추가하지 않았다.
