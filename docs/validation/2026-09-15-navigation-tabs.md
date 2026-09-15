# 설정 사이드바 탭 전환 검증

2026-09-15 · `release/mvp`, 기준 커밋 `051993c` 위의 작업 트리. macOS 26.6.2 arm64,
.NET SDK 10.0.401, 런타임 10.0.12에서 확인했다.

## 구현 결과

- 설정 사이드바의 타이머, 내 루틴·업무 프로필, 기록·내보내기 아이콘은 같은 설정 창의
  본문을 교체한다. 사이드바 클릭만으로 새 `Window`를 만들지 않는다.
- 현재 탭 버튼은 크림색 배경과 어두운 아이콘으로 구분한다. 타이머 탭으로 돌아오면
  재생·일시정지 버튼에 포커스를 돌려 키보드 흐름을 유지한다.
- 펫 팩 설치 항목을 사이드바에서 제거했다. 펫 카드 안의 **펫 팩 설치…** 버튼과 설치
  대화상자는 유지한다.
- 기존 `PersonalizationWindow`와 `BreakReviewWindow`는 재사용 가능한 뷰를 감싸도록 바꿔
  독립 창을 사용하는 진단과 기존 호출 경로도 유지한다.
- 기록 탭의 7일 행은 공통 3열 그리드로 정렬해 모든 날짜·횟수·시간 열의 폭을 통일했다.

## 자동 검사

```sh
dotnet test Tests/Unfold.Tests/Unfold.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~SettingsDashboardTests|FullyQualifiedName~PersonalizationWindowTests|FullyQualifiedName~DesignSystemTests|FullyQualifiedName~SettingsUiTests"
```

- 탭 전환, 새 창 부재, 타이머 포커스 복귀, 펫 팩 사이드바 항목 부재, 독립 창 호환성,
  공통 디자인과 설정 저장 관련 테스트 **21개 통과**, 실패·건너뜀 0.
- 기록 행 정렬 수정 후 설정 대시보드·개인화·디자인 테스트 **16개 재통과**.
- `dotnet build Unfold.slnx -c Release --no-restore`는 경고·오류 없이 통과했다.

전체 Debug 테스트에서는 139개 중 137개가 통과했다. 이번 변경 경로 밖의 기존 상태 두 건이
실패했다.

- `CharacterAssetAuditTests.BundledAssetsAreDecodedAndInventoryIncludesHashes`: 빌드 출력의
  `coco-siamese`, `luna-blue`, `miso-tabby`에 `character.json`이 없다.
- `TimerControlTests.RuntimeRejectsIntervalChangesWhileRunningAndAllowsThemWhilePaused`: 런타임의
  현재 문구 `일시정지하거나 중지`와 테스트 기대값 `일시정지하거나 정지`가 다르다.

## macOS Avalonia 진단

```sh
dotnet run --project src/Unfold.Desktop/Unfold.Desktop.csproj --no-build -- --smoke-test
```

새 임시 데이터 프로필에서 종료 코드 0과 [진단 JSON](2026-09-15-navigation-tabs.json)의
`success: true`를 확인했다. 실제 macOS Avalonia 백엔드로 PNG 26장을 생성했다.
진단은 사이드바 항목이 정확히 3개인지, `SettingsNavPacks`가 없는지, 루틴·기록 이동 뒤
`OwnedWindows`가 비어 있는지, 각 탭의 핵심 컨트롤이 같은 창에 나타나는지 검사했다.

루틴 탭과 기록 탭의 1120×800 PNG를 직접 확인했다. 사이드바에는 타이머·루틴·기록과
하단 종료만 표시되고, 선택 탭 강조가 이동한다. 루틴 목록과 기록의 7개 날짜 행, 하단 작업
버튼이 잘리지 않았으며 기록의 날짜·횟수·시간 열이 일정하게 정렬됐다.

## 검증 한계

macOS 진단은 실제 네이티브 렌더러를 사용하지만 창을 화면 밖에 두고 버튼 이벤트를 프로그램
방식으로 실행한다. 실제 마우스·키보드 조작, 스크린리더와 Windows 실기 동작은 확인하지 않았다.
