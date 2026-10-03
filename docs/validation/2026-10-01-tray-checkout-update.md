# 트레이 브랜드 아이콘·첫 실행 결제 드롭다운 수정

2026년 10월 1일. [배포 아이콘 감사](2026-10-01-status-icons.md)에서 발견한 트레이 미적용과 사용자 요청의 결제 화면 드롭다운 제거를 현재 C#·Avalonia 코드에 반영했다.

## 원인과 변경

- `AppRuntime.BuildTray()`가 직접 그리던 주황색 원·U 비트맵을 제거하고 라일락 브랜드 파일을 불러온다. Windows는 7개 해상도의 ICO, macOS는 64×64 PNG를 사용한다.
- 두 브랜드 파일을 Avalonia 리소스로 포함했다. 설치 위치·작업 디렉터리·원본 Art 폴더에 의존하지 않는다. macOS의 템플릿 마스크를 명시적으로 끄고 브랜드 색상을 유지한다.
- 첫 실행 결제 화면의 `AccountMarket` ComboBox를 제거했다. `4,900원` 가격 텍스트는 유지한다. 기본 시장·가격과 주문 로직은 기존 JSON 설정에서 정한다.
- 트레이 메뉴·클릭 동작, 로그인·구매 확인·무료 코드 기능은 기존 경로를 유지한다.

## 소유 파일

- `src/Unfold.Desktop/BrandTrayIcon.cs`: 플랫폼별 브랜드 리소스 로딩.
- `src/Unfold.Desktop/AppRuntime.cs`: 트레이 생성 호출 교체.
- `src/Unfold.Desktop/Unfold.Desktop.csproj`: PNG·ICO 임베딩.
- `src/Unfold.Desktop/AccountWindow.axaml`: 통화·시장 드롭다운 제거.
- `Tests/Unfold.Tests/BrandTrayIconTests.cs`: 자산 원본 해시·ICO 표현 수·PNG 크기와 런타임 트레이 컨트롤 검사.
- `Tests/Unfold.Tests/AccountScreenTests.cs`: 드롭다운 제거 및 설정 가격 바인딩 검사.
- `docs/account-screen.md`: 현재 가격·시장 표시 계약.

기존 미커밋 작업은 보존했고 작업 전 파일과 이번 수정만의 diff를 `artifacts/validation/2026-10-01-tray-checkout-update/baseline/`, `scoped.diff`에 기록했다.

## 자동 검사

- Release 회귀 검사 **526개 통과**, 실패·건너뜀 0. 새 `UNFOLD_DATA_DIR` 사용. `release.trx`, `release-tests.log`.
- 집중 트레이 검사 **3개 통과**, 실패·건너뜀 0. 임베딩한 PNG·ICO가 승인된 브랜드 원본과 SHA-256이 일치한다. `tray-final.trx`.
- 계정·구매 잠금 집중 검사의 계정 관련 항목은 통과했다. 기본·최소 창과 4개 테마에서 드롭다운 제거를 확인한다. 표시 가격을 JSON/모델에서 바꿨을 때 텍스트가 갱신되는 검사도 유지했다.
- Windows x64 자체 포함 게시 성공, 종료 코드 0. AMD64 앱 호스트와 배포 DLL 안의 ICO·PNG 원본 바이트를 확인했다. `windows-resource-check.json`.
- `git diff --check` 통과.

첫 제한 환경 실행은 MSBuild 로컬 소켓 권한 오류로 테스트 전에 중단됐다. 네이티브 접근이 가능한 환경에서 실행했다. 헤드리스 플랫폼의 아이콘 저장은 이미지 데이터를 내보내지 않아 초기 픽셀 검사는 사용할 수 없었다. 헤드리스는 자산·컨트롤 계약을 검사하고 실제 아이콘 픽셀은 아래 macOS 진단에서 별도 확인했다.

## 실제 macOS에서 실행한 자동 진단

- 제품 런타임 시작 경로로 만든 트레이의 네이티브 `WindowIcon`을 PNG로 내보내 승인된 브랜드 PNG와 비교했다. **64×64 전체 픽셀 동일**, 템플릿 마스크 비활성. `native-tray-icon.png`, `native-result.json`.
- 현재 결제 화면을 네이티브 창에서 940×620·640×560으로 렌더링했다. 가격 `4,900원` 유지, `AccountMarket` 없음. 두 캡처에서 구매·계정 변경·코드 입력·종료 버튼 배치를 확인했다.
- 진단 도구는 가짜 로컬 계정 서비스를 사용했고 새 임시 프로필에서 실행했다. OAuth·실결제·서버 권한 변경은 수행하지 않았다. 화면 창은 off-screen이다.

![드롭다운을 제거한 결제 화면](../../artifacts/validation/2026-10-01-tray-checkout-update/purchase-native.png)

## 미검증과 배포 경계

- macOS 메뉴 막대의 최종 표시 크기·밝은/어두운 배경에서의 사람 검수와 Windows 트레이 실화면은 미검증이다. macOS 네이티브 이미지 데이터 확인과 Windows 게시 리소스 확인을 실화면 확인으로 표현하지 않는다.
- Windows 실제 실행과 Intel Mac 실제 실행은 미검증이다.
- 현재 작업은 코드와 로컬 검증까지다. 웹사이트에 공개한 설치 ZIP은 이전 배포 파일이므로 이 수정의 공개 반영에는 재패키징·릴리스 게시가 필요하다. 기존 공개 파일을 덮어쓰거나 새 릴리스를 게시하지 않았다.
