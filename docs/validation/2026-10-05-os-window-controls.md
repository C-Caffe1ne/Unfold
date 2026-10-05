# 구현 보고서

## 원인

기존 주 창은 모든 OS에서 좌측 신호등 버튼을 만들었다. 일반 Fluent 버튼 템플릿의 호버 배경이 창 제어에도 적용되고, 최소화 아이콘처럼 높이가 0인 선 경로에 `Stretch.Uniform`을 사용했다.

## 변경

- macOS: 좌측 닫기·최소화·최대화 신호등. 일반 버튼 호버 배경을 제거하고, 그룹 호버/키보드 포커스에서 아이콘을 표시한다.
- Windows: 우측 최소화·최대화/복원·닫기. 사각 버튼과 닫기 빨간 호버, 최대화/복원 아이콘 전환을 적용한다.
- 전용 템플릿과 고정 10px 좌표 캔버스에 벡터 아이콘을 그린다. 폰트 글리프와 경로 자동 늘리기를 사용하지 않는다.
- Windows 역할은 [Avalonia 공식 ElementRole API](https://docs.avaloniaui.net/api/avalonia/input/windowdecorationselementrole)에 따라 지정한다. 실제 Windows 스냅 팝업은 미검증이다.
- 기존 상단 24px 행, 가장자리 31px 여백과 타이머 카드 위치를 유지한다. 닫기는 기존 트레이 숨김 동작이다.

## 소유 파일

- `src/Unfold.Desktop/SettingsWindow.Chrome.cs`
- `Tests/Unfold.Tests/WindowControlTests.cs`
- 이 보고서와 `2026-10-05-os-window-controls/` 증거 자료

기준은 `codex/beta-v1.0.4`, `1.0.4-beta` 작업 트리다. 시작 HEAD는 `253699e`였다. 검증 중 기존 별도 작업이 커밋되어 HEAD가 바뀌었으며, 시작 시 기록한 기존 변경 파일들의 내용 해시는 모두 동일하다. 이번 작업은 커밋하지 않았다. 최종 HEAD와 소스 해시는 [verification.json](2026-10-05-os-window-controls/verification.json)에 기록했다.

## 검증

- 변경 전 창 제어 검사: 4/4 통과.
- 최종 창 제어·테마·대시보드·반응형 집중 검사: **29/29 통과**, 실패/건너뜀 0. [TRX](2026-10-05-os-window-controls/focused.trx)
- 1배·2배 최소화 아이콘의 픽셀 두께·가로 길이, macOS 배경 유지, Windows 닫기 흰색 아이콘, OS별 순서·정렬·입력 영역과 최대화/복원 도형 전환 검사 포함.
- Release 빌드: 경고 0, 오류 0. `git diff --check` 통과.
- 새 `UNFOLD_DATA_DIR`의 macOS 실제 네이티브 통합 진단: 종료 코드 0, `success=true`, `windowControlsVerified=true`, `roundedWindowVerified=true`. [smoke.json](2026-10-05-os-window-controls/smoke.json)
- 별도 미리보기 앱의 실제 macOS 드래그: 창 좌표 `(80,90)` → `(160,130)`. 실제 CUA 포인터로 macOS 및 Windows형 버튼 호버를 확인했다.
- 기존 변경 파일 해시 비교: 변경 없음.

[macOS 실행 화면](2026-10-05-os-window-controls/macos-native.png) · [macOS 호버](2026-10-05-os-window-controls/macos-native-hover.png) · [Windows 배치 미리보기](2026-10-05-os-window-controls/windows-preview-native.png) · [Windows 닫기 호버 미리보기](2026-10-05-os-window-controls/windows-hover-preview-native.png)

## 미검증·위험

Windows 캡처는 같은 구현의 Windows 분기를 macOS 임시 검증 앱에서 선택한 실제 창 화면이다. Windows 실기 실행·DPI·스냅 팝업 검증을 뜻하지 않는다. macOS 실제 화면 배율은 1이며 2배 아이콘은 자동 렌더링 검사로 검증했다. 전체 테스트 스위트는 재실행하지 않았다. 계정 창·대화상자·배포 설치 파일은 수정하지 않았다.

임시 미리보기 도구는 제품 코드 밖에 있으며 새 격리 프로필을 사용한다. 스크린샷의 타이머 일시정지는 캡처를 위한 상태다. 캡처 도구가 표시하는 포인터 주변 강조는 앱의 버튼 호버 배경이 아니다.
