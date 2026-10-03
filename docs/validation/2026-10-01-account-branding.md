# 최초 계정·결제 화면 브랜드 적용

2026-10-01 · 현재 C#·Avalonia 작업본.

## 변경

- 왼쪽 패널의 문구를 제거하고 기존 Mochi 이미지만 중앙에 배치했다.
- 좌측 상단의 브랜드 텍스트를 승인된 브랜드 PNG로 교체했다. 원본은 640×176이며 표시 영역은 110×30이다.
- 기본 로고와 어두운 테마용 밝은 로고를 Avalonia 리소스에 포함했다. 창을 연 상태의 테마 변경에도 로고가 전환되며 창 종료 시 비트맵을 해제한다.
- 기존 JSON의 `companionText`는 빈 값으로 유지했다. 로그인·구매 동작, 가격 및 창 이동 처리는 유지했다.

구현 파일: `AccountWindow.axaml`, `AccountWindow.axaml.cs`, `Unfold.Desktop.csproj`, `Assets/Account/entry-screen.json`. 기존 `AccountScreenTests.cs`에 이미지 전용 패널·로고 크기·창 내부 배치 검증을 반영했다.

## 검증

- 관련 Release 검사 15개 통과, 실패·건너뜀 0개. 계정 화면, 표시 설정, 창 이동 관련 검사를 실행했다.
- macOS 네이티브 진단 빌드 성공, 경고·오류 0개. 격리된 새 `UNFOLD_DATA_DIR`와 가짜 로컬 계정 서비스를 사용했다.
- 940×620, 최소 640×560, 어두운 테마 940×620의 렌더링 3장을 확인했다. 왼쪽 패널에는 이미지 하나만 있고 로고와 주요 버튼의 잘림이 없었다.
- 네이티브 진단에서 열린 창의 테마를 변경해 밝은 로고 전환을 확인했다. 출력 픽셀을 브랜드 원본과 비교했다.
- `git diff --check` 통과.

근거: `artifacts/validation/2026-10-01-account-branding/`의 `focused.trx`, `focused.log`, `native-result.json`, `native.log`, `purchase-native.png`, `purchase-native-minimum.png`, `purchase-native-dark.png`.

네이티브 창은 화면 밖에서 렌더링했다. 실제 Google 인증·결제, 물리 입력 및 Windows에서의 화면 관찰은 이번 변경에서 검증하지 않았다. 기존 공개 설치 파일은 변경하지 않았다.
