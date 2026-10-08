# 툴팁·아이콘·글자 테마 색상 · 2026년 10월 8일

## 원인

기준은 `codex/remove-unused-features`, HEAD `2715fd183c42a25badb29cbf15fc3a017b468464`, **Beta v1.1.2**다.
실행 경로는 `/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features`다.
모든 작업트리의 버전·HEAD·변경을 다시 비교했으며 `bbdb4a8`/1.1.1보다 11커밋 앞선 개발본이다.
공개 배포 API는 `v1.1.1-beta`, 소스 `887d0127e57e5145062d0d558fb71b113284542d`를 반환했다.

기존 공통 색상 브러시는 갱신됐지만 기본 컨트롤이 사용하는 일부 Fluent 브러시는 최초 색을 유지했다.
Avalonia 12.1.2의 [기본 브러시 소스](https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/src/Avalonia.Themes.Fluent/Accents/BaseResources.xaml)는
강조색 이외의 여러 브러시를 `StaticResource` 색으로 생성한다. 밝은 테마끼리 바꿀 때 팔레트만 교체해도 이 브러시의 색이 갱신되지 않았다.

| 발견 항목 | 수정 전 증거·영향 | 심각도 | 완료 기준 |
|---|---|---|---|
| 체크박스·드롭다운·스크롤 화살표 | [오트 설정](images/2026-10-08-theme-control-colors/before-OatLatte-settings.png), [숲 펫 화면](images/2026-10-08-theme-control-colors/before-Sage-pets.png): 라일락 글자·화살표 색이 남음 | P2 | 같은 계열 테마를 연속 전환해도 현재 팔레트 색 사용 |
| 툴팁 배경·전경 | [오트 툴팁](images/2026-10-08-theme-control-colors/before-OatLatte-tooltip.png): 기본 회색 바탕과 최초 글자색 사용 | P2 | 바탕·글자·경계가 현재 테마 토큰과 일치, 열린 상태 갱신 |
| 입력 안내·비활성 입력 | [숲 펫 화면](images/2026-10-08-theme-control-colors/before-Sage-pets.png): 투명 검정 안내·비활성 글자와 화살표 | P2 | 안내 `Muted`, 비활성 `DisabledText` 사용 |

## 변경

- 버튼·반복 버튼·체크박스·텍스트 입력·선택 항목·드롭다운/스크롤 화살표의 Fluent 전경 리소스를 공통 테마 브러시와 연결했다. 기존 화면·팝업·호버·선택·비활성 상태에서 같은 브러시를 갱신한다.
- 툴팁을 `Surface` 바탕, `Cream` 글자, `OutlineStrong` 경계와 공통 글꼴에 연결했다.
- 텍스트·선택 입력 안내에는 `Muted`, 비활성 값과 화살표에는 `DisabledText`를 적용했다.
- 선택된 주요 버튼·목록 항목의 `Ink`, 오류·삭제의 `Error`, 정지 배지의 `Stopped` 역할을 유지한다.

## 소유 파일

- [DesignSystem.Themes.cs](../../src/Unfold.Desktop/DesignSystem.Themes.cs): 기본 컨트롤 전경 리소스 연결.
- [DesignSystem.cs](../../src/Unfold.Desktop/DesignSystem.cs): 툴팁·입력 안내·비활성 값 스타일.
- [ThemeControlColorTests.cs](../../Tests/Unfold.Tests/ThemeControlColorTests.cs): 연속 테마 변경·열린/다시 열린 툴팁·입력 상태 회귀 검사 3개.
- 현재 디자인 시스템 안내·문서 색인을 보완했다. 기존 문서·펫·소리 변경은 보존했다.

## 검증

- 수정 전 새 검사 **3개 모두 실패**: 오트 전환 뒤 라일락 글자 잔류, 툴팁 기본 회색, 입력 안내 투명 검정.
- 관련 집중 검사 **54/54 통과**, 실패·건너뜀 0개. 기존 테마 저장·초안·타이머 보존, 버튼 상태·아이콘 픽셀·키보드 포커스와 소리 설정 동작을 포함한다.
- 전체 Release 검사 **787/787 통과**, 실패·건너뜀 0개, 빌드 경고·오류 0개. 결과는 `/private/tmp/unfold-theme-audit.gu6209da/full.trx`다.
- 실제 macOS Avalonia/AppRuntime에서 격리된 새 `UNFOLD_DATA_DIR`로 네 테마를 순차 적용했다. 1120×800 기본 창·640×560 최소 창, 홈·테마 선택·소리 설정 스크롤·펫 관리 화면을 확인하고 PNG 24개를 저장했다.
- 검사 대상 글자·입력 전경·SVG/방향 아이콘에서 팔레트 밖 색 **0개**. 최소 창의 실제 크기와 설정 페이지 가로 넘침 없음도 확인했다.
- 열린 툴팁에 다른 테마를 적용한 뒤 배경·글자가 즉시 변경됨을 확인했다.
- 수정한 세 문서의 로컬 링크 186개·`git diff --check`가 통과했다. 시작 시 변경돼 있던 파일 64개 중 문서 보완 대상 2개를 제외한 62개가 동일한 내용으로 보존됐다.

[수정 전 native 결과](images/2026-10-08-theme-control-colors/native-before.json) · [수정 후 native 결과](images/2026-10-08-theme-control-colors/native-after.json)

| 테마 | 기본 설정 창 | 최소 설정 창 | 최소 펫 관리 | 툴팁 |
|---|---|---|---|---|
| 유연한 라일락 | [1120×800](images/2026-10-08-theme-control-colors/Plum-settings.png) | [640×560](images/2026-10-08-theme-control-colors/Plum-settings-minimum.png) | [펫 관리](images/2026-10-08-theme-control-colors/Plum-pets.png) | [툴팁](images/2026-10-08-theme-control-colors/Plum-tooltip.png) |
| 다정한 오트 | [1120×800](images/2026-10-08-theme-control-colors/OatLatte-settings.png) | [640×560](images/2026-10-08-theme-control-colors/OatLatte-settings-minimum.png) | [펫 관리](images/2026-10-08-theme-control-colors/OatLatte-pets.png) | [툴팁](images/2026-10-08-theme-control-colors/OatLatte-tooltip.png) |
| 숨 고르는 숲 | [1120×800](images/2026-10-08-theme-control-colors/Sage-settings.png) | [640×560](images/2026-10-08-theme-control-colors/Sage-settings-minimum.png) | [펫 관리](images/2026-10-08-theme-control-colors/Sage-pets.png) | [툴팁](images/2026-10-08-theme-control-colors/Sage-tooltip.png) |
| 밤의 버터 | [1120×800](images/2026-10-08-theme-control-colors/MidnightBlue-settings.png) | [640×560](images/2026-10-08-theme-control-colors/MidnightBlue-settings-minimum.png) | [펫 관리](images/2026-10-08-theme-control-colors/MidnightBlue-pets.png) | [툴팁](images/2026-10-08-theme-control-colors/MidnightBlue-tooltip.png) |

## 미검증·위험

- macOS native 창과 팝업을 실제로 생성해 렌더링했으며 입력은 프로그램으로 발생시켰다. 물리 포인터 호버·Windows 실기 화면 검증과 구분한다.
- 검증은 로컬 1.1.2 개발본으로 수행했다. 설치된 공개 앱 교체·배포는 수행하지 않았다. 실행 중인 이전 개발 앱에는 재시작이 필요하다.
