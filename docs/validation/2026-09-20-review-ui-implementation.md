# 기록 UI 적용 검증

2026-09-20 · `release/mvp`, 기준 HEAD `0b72a36`의 작업 트리에 승인된 기록 2차 시안을 적용했다.
macOS arm64, .NET 10, Avalonia 12.1.2. [검증 수치와 파일 해시](2026-09-20-review-ui-implementation.json).

## 원인과 변경

기존 기록 화면은 기간과 이동 버튼이 떨어져 있었고, 한 문장 요약과 상세 이름이 시각적 위계를 복잡하게 했다.
승인된 [시안](../plans/2026-09-20-review-ui-proposal.md)에 따라 다음을 적용했다.

- 기간 오른쪽에 이전·다음·새로고침 40px 아이콘 버튼을 배치했다.
- 본문 최대 760px, 카드 패딩·간격 20px, 3열 요약과 날짜별 기록 카드로 정리했다.
- 상세에서 루틴 이름을 제거하고 완료 당시 시각·실제 휴식 시간만 표시한다.
  구형 기록은 “실제 휴식 시간 미기록”으로 표시하며 목표 시간을 실제 시간으로 표시하지 않는다.
- 하단 상태 문구와 CSV 버튼을 한 행에 두고 본문 스크롤 밖에 고정했다.
- 기존 7일 이동 범위, 날짜 펼침·갱신, 집계, CSV 스냅샷·이름·목표 시간 데이터는 유지했다.

## 수정 파일

제품 UI는 `BreakReviewWindow.cs`와 `DesignSystem.cs`의 기록 전용 크기 토큰을 변경했다.
`SmokeDiagnostics.cs`의 기록 검사를 보완했고, `BreakReviewTests`·`LocalizationTests`·
`UiAuditRegressionTests`·`PersonalizationWindowTests`·`DesignSystemTests`를 새 UI 계약에 맞췄다.
Core 기록 모델과 저장 형식, 타이머·펫·말풍선 제품 코드는 이번 단계에서 변경하지 않았다.
이미 적용되어 있던 홈 UI의 작업 트리 변경은 보존했다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| 관련 첫 검사 | 38개 중 37 통과. 새 폭 검사에서 761px를 발견해 본문 최대 폭을 명시적으로 제한 |
| 폭 보완 집중 검사 | 1/1 통과, 기본·최소 탭과 호환 창 4개 크기 |
| 최종 전체 Release 테스트 | 208/208 통과, 실패·건너뜀 0, 종료 코드 0 |
| 최종 Release 빌드 | 경고 0·오류 0, 종료 코드 0 |
| 새 프로필 macOS off-screen 진단 | `success: true`, 종료 코드 0, PNG 54장 |
| 날짜 상세 | 이름 숨김, 완료 시각·실제 시간만 표시, 0초·구형 기록·UTC offset·정렬·펼침 유지 확인 |
| 내보내기 | 이름/목표/실제 시간 보존, 기간 변경 중 스냅샷 유지, 오류 문구와 재시도 가능 상태 확인 |
| UI | 40px 아이콘, 최대 본문 폭, 세로 스크롤, 하단 고정, 가로 넘침 없음 |

| 실제 렌더링 크기 | 본문 카드 폭 | 하단 y | 세로 스크롤 |
|---|---|---|---|
| 탭 1120×800 | 760px | 729px | 불필요 |
| 탭 860×680 | 694px | 609px | 필요 |
| 호환 창 620×650 | 530px | 577px | 필요 |
| 호환 창 560×600 | 470px | 527px | 필요 |

최초 전체 검사에서는 이전 한 문장 요약을 기대하는 한글화 테스트 1개가 실패했다.
세 수치와 기존 CSV 이름을 확인하도록 갱신한 뒤 전체 208개가 통과했다.
최초 네이티브 진단은 진단 창의 최소 크기 고정 때문에 최소 크기로 줄지 않아 실패했다.
실제 `ClientSize`를 검사하고 최소 크기 제한을 명시한 뒤 새 프로필에서 다시 통과했다.
전체 테스트 이후 변경한 것은 이 진단용 최소 크기 제한뿐이며 최종 빌드·네이티브 진단으로 검증했다.

명령은 `/tmp/unfold-review-implementation.V4jRl6` 아래 검사별 새 `UNFOLD_DATA_DIR`을 사용했다.
`AVALONIA_TELEMETRY_OPTOUT=1`, `DOTNET_CLI_TELEMETRY_OPTOUT=1`을 지정했다.

```sh
dotnet test Unfold.slnx -c Release --no-restore --disable-build-servers --logger trx
dotnet build src/Unfold.Desktop -c Release --no-restore --disable-build-servers
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --smoke-test
```

## 화면 근거

- [기본 기록 탭](images/2026-09-20-review-ui-implementation/review-default.png)
- [최소 기록 탭](images/2026-09-20-review-ui-implementation/review-minimum.png)
- [호환 창](images/2026-09-20-review-ui-implementation/review-compatibility.png)
- [최소 호환 창](images/2026-09-20-review-ui-implementation/review-compatibility-minimum.png)
- [빈 기록](images/2026-09-20-review-ui-implementation/settings-review-tab.png)

진단의 기록·경과 시간·CSV 저장 경로는 테스트용이다. 캡처를 직접 확인했으며,
오류·구형 기록·날짜 전환 등은 headless 자동 검사 근거다.

## 미검증과 다음 단계

자동 검사와 macOS 네이티브 off-screen 렌더링 결과다. 실제 마우스·키보드 입력,
Windows 실기, OS 배율 변경과 다중 모니터는 이번 단계에서 미검증이다.
배포 파일·서명·공증 작업은 수행하지 않았다.

다음 [말풍선 시안](../plans/2026-09-20-speech-ui-proposal.md)은 별도로 보여주고 사용자 확인 후 적용한다.
