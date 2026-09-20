# 홈 UI 적용 검증

2026-09-20 · `release/mvp`, 기준 HEAD `0b72a36`에 승인된 홈 2차 시안을 적용한 작업 트리.
macOS arm64, .NET 10, Avalonia 12.1.2. [검증 수치와 SHA-256](2026-09-20-home-ui-implementation.json).

## 적용 내용

- 타이머를 위, 펫을 아래에 배치했다. 타이머 196px 고정 높이·카드 간격 20px이며 펫이 남은 높이를 채운다.
- 홈 카드 모서리 24px·패딩 20px, 제목 18px·본문 14px·설명 12px로 정리했다.
- 상태 배지는 상태명 너비로 표시하고 설명을 아래로 분리했다. 진행·일시정지·중지·자리 비움·휴식 대기·휴식 중을 구별한다.
  활동 감지 오류는 “상태 확인 필요”와 상세로 표시하고, 긴 상세는 생략하되 툴팁에 전문을 보존한다.
- 숫자 입력 160×40px, 홈 적용 버튼 80×40px, 펫 선택 200×40px를 적용했다. 입력의 기존 단일 테두리와 호버·포커스 배경 규칙은 유지했다.
- 펫 옵션을 세로로 배치하고, 긴 펫 이름은 최대 두 줄로 표시한다. 작은 창에서는 펫 미리보기부터 줄어든다.
- 오늘 완료 횟수는 40px, 기록 이동 버튼은 내용 너비로 정리했다.

타이머 전이·간격 잠금·명시적 적용·다음 휴식 시간 적용, 설정 저장 방식, 펫 리소스는 유지했다.
기록·말풍선 제품 코드는 수정하지 않았다. [다음 기록 UI 시안](../plans/2026-09-20-review-ui-proposal.md)을 별도로 작성했다.

## 실행 결과

| 검사 | 결과 |
|---|---|
| 관련 Release 테스트 | 28/28 통과, 실패·건너뜀 0, 종료 코드 0 |
| 전체 Release 테스트 | 207/207 통과, 실패·건너뜀 0, 종료 코드 0 |
| 최종 Release 빌드 | 경고 0·오류 0, 종료 코드 0 |
| 최종 macOS off-screen 진단 | 새 `verified-profile`, `success: true`, 종료 코드 0, PNG 50장 |
| 기본 창 | 1120×800, 타이머 y31·높이196, 펫 y247·높이522 |
| 최소 창 | 860×680, 타이머 y31·높이196, 펫 y247·높이402 |
| 추가 회귀 | 여섯 상태명/설명, 긴 이름·실제 저장 실패 시 초안 유지·적용 재시도, 카드 내부 버튼 위치, 입력 크기, 시각·컨트롤 순서 |

실행 명령은 새 임시 루트 `/tmp/unfold-home-implementation.6KJt7u` 아래에서 각각 다른
`UNFOLD_DATA_DIR`을 사용했다. 명령 앞에 `AVALONIA_TELEMETRY_OPTOUT=1`, `DOTNET_CLI_TELEMETRY_OPTOUT=1`을 지정했다.

```sh
dotnet test Unfold.slnx -c Release --no-restore --disable-build-servers \
  --filter 'FullyQualifiedName~SettingsDashboardTests|FullyQualifiedName~TimerControlTests|FullyQualifiedName~UiAuditRegressionTests'
dotnet test Unfold.slnx -c Release --no-restore --disable-build-servers
dotnet build src/Unfold.Desktop -c Release --no-restore --disable-build-servers
dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
```

전체 테스트 후에는 진단의 창 크기·팝업 캡처만 보완했다. 제품 UI와 테스트 소스는 추가 변경하지 않았으며
최종 빌드와 새 프로필의 진단으로 이 보완을 검증했다. 이미 통과한 전체 테스트는 반복하지 않았다.

## 렌더링 근거

- [기본 홈](images/2026-09-20-home-ui-implementation/home-default.png)
- [최소 홈](images/2026-09-20-home-ui-implementation/home-minimum.png)
- [최소 창·긴 이름·저장 오류 배치](images/2026-09-20-home-ui-implementation/home-minimum-long-name-error.png)
- [실제 펫 선택 팝업 표면](images/2026-09-20-home-ui-implementation/home-pet-picker.png)
- [일시정지 및 시간 적용](images/2026-09-20-home-ui-implementation/timer-paused.png)
- [중지 상태](images/2026-09-20-home-ui-implementation/timer-stopped.png)

초기 추가 캡처는 진단용 `PrepareDiagnosticWindow`가 최소 크기를 1120×800으로 고정한 탓에
파일명과 실제 창 크기가 달랐다. 실제 `ClientSize` 일치 검사를 추가하고 860×680 제한을 명시해 수정했다.
또한 창 본체의 캡처에 별도 팝업이 포함되지 않아, 팝업의 실제 Border를 직접 렌더링하도록 수정했다.
이 보고서와 복사한 이미지는 두 문제를 보완한 최종 진단 결과다.

긴 이름·저장 오류 이미지는 렌더링용 문구 주입이다. 실제 파일 저장 실패와 초안 보존은
headless 회귀 테스트에서 `settings.json` 위치에 디렉터리를 만들어 재현했다.
여섯 상태는 주입된 타이머 시간과 런타임 명령으로 검사했으며, 자리 비움을 실시간으로 기다린 결과는 아니다.

## 검증 범위

자동 검사와 macOS 네이티브 off-screen 렌더링을 완료했다. 타이머 조작·시간 저장·펫 탭·설정 탭·CSV·말풍선의
기존 통합 진단도 함께 통과했다. 실제 사람의 포인터·키보드 조작, 효과음 청취, 로그인 등록,
Windows 실기, 배율 125/150/200%, 다중 모니터와 장시간 사용은 이번 작업에서 미검증이다.
음성 낭독 기능이나 새로운 타이머·휴식 동작을 추가하지 않았다.
