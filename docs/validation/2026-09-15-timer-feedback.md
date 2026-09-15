# 타이머 피드백 반영 검증

2026-09-15 · `release/mvp`, 기준 커밋 `e0149e7` 위의 작업 트리. macOS 26.6.2 arm64,
.NET SDK 10.0.401, 런타임 10.0.12에서 확인했다.

## 구현 결과

- 진행 중은 크림색, 일시정지·자리 비움·휴식 중은 노란색, 정지는 붉은색 상태 점과
  설명 문구로 구분한다. 트레이의 카운트다운에도 현재 상태를 함께 표시한다.
- 진행 중에는 알림 간격 입력을 비활성화한다. 수동 일시정지·정지 상태에서만 5~240분을
  1분 단위로 조절할 수 있다.
- 알림 간격은 입력만으로 저장되지 않는다. 알림 설정 카드 우측 상단의 **적용**을 누르면
  간격·자리 비움 기준·루틴을 함께 저장한다. 일시정지와 정지 상태는 그대로 유지한다.
- 런타임 저장 계층에서도 진행 중 간격 변경과 업무 프로필의 타이머 재예약을 거부한다.
- 설정과 트레이의 초기화 동작을 제거했다. 설정의 타이머 조작은 재생/일시정지와 정지
  두 아이콘 버튼으로 구성한다. 내부 진단에서 사용하는 시계 초기화 API는 사용자 UI가 아니다.

## 자동 검사

```sh
dotnet restore Unfold.slnx --locked-mode
dotnet test Tests/Unfold.Tests/Unfold.Tests.csproj -c Release --no-restore \
  --filter "FullyQualifiedName~TimerControlTests|FullyQualifiedName~SettingsDashboardTests|FullyQualifiedName~PersonalizationWindowTests"
dotnet test Unfold.slnx -c Release --no-restore
```

- 고정 패키지 복원 통과. 패키지 잠금 파일 변경 없음.
- 타이머·설정 대시보드·프로필 집중 테스트 **16개 통과**, 실패·건너뜀 0.
- 전체 Release 테스트 **138개 통과**, 실패·건너뜀 0.
- 실행 중 UI 잠금과 런타임 우회 차단, 적용 전 저장되지 않음, 일시정지·정지 후 적용,
  1분 증감, 상태 문구·색상, 초기화 버튼 부재, 버튼 접근성, 우측 상단 적용 배치,
  860×680 최소 창 배치와 프로필 편집기의 1분 증감을 검사했다.

## macOS Avalonia 진단

```sh
UNFOLD_DATA_DIR=/private/tmp/unfold-timer-final.SDWGCM \
  dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
```

새 빈 데이터 프로필에서 종료 코드 0과 [진단 JSON](2026-09-15-timer-feedback.json)의
`success: true`를 확인했다. 실제 macOS Avalonia 백엔드로 PNG 24장을 생성했다.
진행 중 입력 잠금, 일시정지 상태의 25분 명시적 적용과 1.2초 유지, 정지 상태의 00:00과
간격 적용 후 1.2초 유지, 재생 시작, 열린 초대의 정지 취소가 모두 통과했다.

기본·일시정지·정지 및 860×680 최소 화면을 직접 확인했다. 적용 버튼은 알림 설정 카드
우측 상단에 있고, 진행 중 숫자 입력과 화살표는 비활성 상태로 보인다. 일시정지에는 재생
아이콘과 노란 상태 배지, 정지에는 재생 아이콘과 붉은 상태 배지가 표시되며 두 조작 버튼과
카운트다운이 잘리지 않았다.

## 검증 한계

macOS 진단은 실제 네이티브 렌더러를 사용하지만 창을 화면 밖에 두고 버튼 이벤트를
프로그램 방식으로 실행한다. 실제 마우스·키보드 조작, 스크린리더, 다중 모니터·DPI 변경,
OS 알림과 장시간 사용은 확인하지 않았다. 이번 작업 환경에서 Windows 실기 실행은 하지
않았으며 [Windows 실기 체크리스트](../windows-dogfooding-log.md)의 새 타이머 항목은 미검증이다.
