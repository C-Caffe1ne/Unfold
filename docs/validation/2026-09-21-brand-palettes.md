# 브랜드 팔레트 교체 검증

2026-09-21 · `release/mvp` 작업 트리 · Windows 10.0.26200 · .NET SDK 10.0.301 / 런타임 10.0.9.

## 요청과 변경

승인된 네 가지 브랜드 시안의 컬러를 기존 앱 테마에 적용했다. 현재 Core·Desktop·Tests의
역할, 빌드 입력, 설정 저장부터 화면 갱신까지의 경로와 별도 Figma 빌더를 확인했다.
팔레트 값은 [디자인 시스템](../design-system.md)과 `DesignSystem.Themes.cs`가 기준이다.

| 저장 ID / 이전 테마 | 새 표시 이름 | 브랜드 시안 | 밝기 |
|---|---|---|---|
| 0 / 오트 라떼 | 다정한 오트 | Oat & Apricot | 밝음 · 기본값 |
| 1 / 세이지 | 숨 고르는 숲 | Moss & Paper | 밝음 |
| 2 / 미드나이트 블루 | 밤의 버터 | Midnight & Butter | 어두움 |
| 3 / 플럼 | 유연한 라일락 | Lilac & Ink | 밝음 |

시안의 바탕·카드·본문·주조색·보조색, 보조 글자와 펫 배경색을 유지했다.
시안에 없던 프레임·보조 표면·호버·비활성·경계·상태색은 기존 UI에 맞춰 보완했다.
테마 메뉴의 원형 표본은 각 팔레트의 보조색과 주조색을 함께 표시한다.

공유 브러시 갱신 방식과 숫자 ID를 유지하므로 기존 설정 파일을 마이그레이션하지 않는다.
색상과 표시 이름을 변경했으며 레이아웃·글꼴·로고·펫 원화·타이머·기록 로직은 유지했다.
트레이의 공통 아이콘, 그림 편집용 물감과 미리보기의 고정 밝은 배경은 테마 팔레트와 별도다.

## 자동 검사

각 실행에 서로 다른 새 `UNFOLD_DATA_DIR`을 사용했다. 실제 사용자 데이터는 사용하지 않았다.

```powershell
dotnet test Unfold.slnx -c Release --no-restore --logger 'trx;LogFileName=theme-replacement.trx' --results-directory artifacts/theme-replacement-results
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --smoke-test
```

- Release 빌드 및 전체 테스트: 종료 코드 0, **218개 통과 / 2개 건너뜀 / 0개 실패**.
- 건너뛴 2개는 변환 도구가 없는 환경의 실제 MP4 변환 검사다.
- 기존 검사로 테마 선택·저장·재시작 복원·실패 복구·화면 객체 및 초안 유지·타이머 상태 보존을 확인했다.
- 네 팔레트의 본문·보조·상태 글자와 주요 표면, 주요 버튼의 기본·호버 글자 대비 4.5:1 검사가 통과했다.
- 기본 테마의 비활성 버튼 대비, 버튼 상태와 키보드 포커스 검사도 통과했다.
- [Windows 진단 결과](2026-09-21-brand-palettes.json): 종료 코드 0, `success=true`, PNG 82장.
  테마별 홈·설정·기록·펫 열기·만들기·말풍선·선택창 7장씩 28장을 포함한다.
- `themes.persisted=true`, `minimumNavigationFits=true`. 테마 선택은 프로그램이 이벤트를 발생시키는 방식이다.

## 캡처 시각 확인

실제 현재 Avalonia 코드로 Windows에서 생성한 off-screen 이미지다. 네 홈 화면에서
배경·글자·주요 버튼·펫 배경이 새 색으로 일관되게 전환됨을 확인했다.
라일락과 밤의 버터 선택창에서 이름·선택 표시·두 색상 표본의 잘림이 없었다.
대표 설정·기록·펫 만들기·말풍선과 기본 테마의 860×680 화면도 확인했다.

| 화면 | 캡처 |
|---|---|
| 다정한 오트 홈 | [보기](images/2026-09-21-brand-palettes/theme-OatLatte-home.png) |
| 숨 고르는 숲 홈 | [보기](images/2026-09-21-brand-palettes/theme-Sage-home.png) |
| 밤의 버터 홈 | [보기](images/2026-09-21-brand-palettes/theme-MidnightBlue-home.png) |
| 유연한 라일락 홈 | [보기](images/2026-09-21-brand-palettes/theme-Plum-home.png) |
| 밝은 / 어두운 테마 선택창 | [라일락](images/2026-09-21-brand-palettes/theme-Plum-picker.png), [밤의 버터](images/2026-09-21-brand-palettes/theme-MidnightBlue-picker.png) |
| 대표 화면 | [설정](images/2026-09-21-brand-palettes/theme-Plum-settings.png), [펫 만들기](images/2026-09-21-brand-palettes/theme-MidnightBlue-builder.png), [기록](images/2026-09-21-brand-palettes/theme-Sage-review.png), [말풍선](images/2026-09-21-brand-palettes/theme-OatLatte-speech.png) |
| 기본 테마 최소 창 | [860×680](images/2026-09-21-brand-palettes/home-minimum.png) |

## 미검증 범위

실제 Windows 마우스·키보드 조작, 각 테마의 모든 컨트롤 상태 조합, macOS 실기와
고대비·보조기술 검증은 수행하지 않았다. 저장된 캡처와 자동 이벤트 검사를 사람의 실기 조작으로 간주하지 않는다.
원격 Figma 파일의 변수나 디자인은 변경하지 않았다.
