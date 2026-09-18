# 펫 추가 탭 UI 구현·검증

2026-09-18 · `release/mvp` · macOS arm64 · .NET 10.0.12 / Avalonia 12.1.2

## 원인과 범위

[승인된 2차 시안](../plans/2026-09-18-pet-tabs-ui-proposal.md)을 기준으로 펫 팩 열기·만들기 탭을 정리했다.
기존 행동 카드의 별도 미리보기 아이콘은 작은 클릭 영역을 요구했고, 카드·드롭다운·안내의 크기와 간격이 달랐다.
이번 변경은 해당 두 탭에 한정한다. 이전 작업의 설정 저장 기능·리소스 수정과 미커밋 변경을 보존했다.

## 변경

- 탭 18px/준굵게, 본문 최대 760px, 요소 간격 20px, 입력·버튼 높이 40px.
- 만들기: 이름 320×40px, 미리보기 최대 폭 520px 및 높이 272/156px, 행동 카드 128×148px.
- 별도 미리보기 아이콘 제거. 파일이 있는 카드 전체를 클릭하거나 Enter/Space로 선택하면 미리본다.
  선택은 카드 테두리와 미리보기 아래 행동명으로 표시한다. 빈 카드 클릭은 선택을 바꾸지 않는다.
- 카드의 추가·제거 아이콘은 독립 컨트롤이다. 파일명 대신 크기, 프레임, 시간만 두 줄로 표시하고 카드 크기는 고정한다.
- 열기: 동작 선택 200px 오른쪽에 열기 버튼 120px, 파일을 연 뒤 팩 정보 표시,
  미리보기 하단 중앙의 재생 아이콘, 그 아래 배경·크기 선택기 각 148px.
- 상태·오류·참고 사항 요약은 하단 왼쪽, 설치·만들기 버튼은 오른쪽에 고정한다.
- 교체 확인과 안전한 취소, 탭 전환 시 초안·선택 유지, 재생·내보내기·설치 규칙을 유지한다.

## 소유 파일

제품 변경은 `PetManagementView.cs`, `CustomPetWindow.cs`, `PetPackWindow.cs`, `DesignSystem.cs`다.
`SmokeDiagnostics.cs`에 카드 전체 미리보기와 최소 창 첫 화면의 다섯 추가 버튼 검증을 보강했다.
`PetTabsUiTests.cs`에 세 가지 상호작용·레이아웃 회귀 검사를 추가하고,
기존 `CustomPetTests.cs`·`PetPackWindowTests.cs`의 변경된 메타데이터 형식과 폭 기대값을 갱신했다.
관련 사용법·디자인 시스템·계획·문서 인덱스도 갱신했다.

## 검증

원본 산출물: `/tmp/unfold-pet-tabs-ui.Vt23p4`.
영구 보관 결과: [검증 JSON](2026-09-18-pet-tabs-ui.json).

| 분류 | 결과 |
|---|---|
| 집중 검사 | 최초 50개 중 48개 통과. 2개는 이전 메타데이터 형식 기대값 불일치였으며 갱신 후 통과 |
| 새 상호작용·레이아웃 및 교체 회귀 | 5/5 통과. 헤드리스 포인터·Enter·Space, 빈 카드, 추가/제거 독립성, 초안·선택 보존 |
| 전체 Release | 205/205 통과, 실패·건너뜀 0. 빌드와 테스트 종료 코드 0 |
| macOS 네이티브 off-screen 진단 | 종료 코드 0, `success: true`, 캡처 46장, 카드 선택·GIF 생성·내보내기·설치·업데이트·복구 통과 |
| 최소 창 | 실제 렌더링 860×680에서 다섯 행동 카드와 추가 버튼이 한 줄에 있고 본문 가로 넘침 없음 |
| 기본 창 | 1120×800에서 본문 최대 폭, 중앙 미리보기, 고정 하단 확인 |
| 미리보기 크기 | 기존 테스트에서 100/150/200% 선택의 192/288/384 논리 픽셀 및 큰 미리보기 세로 스크롤 통과 |
| 오류·참고 사항 | 오류 시 설치 차단, 고정 경고 요약·이동 버튼, 창 하단 작업 버튼 경계 검사 통과 |
| 캡처 검토 | 기본/최소 열기·만들기, 파일 입력 후 선택 테두리·메타데이터, 참고 사항·오류 캡처 확인 |
| 변경 형식·문서 | `git diff --check` 통과, 관련 문서의 로컬 링크 116개 중 누락 0 |

전체 검사 명령:

```sh
UNFOLD_DATA_DIR=/tmp/unfold-pet-tabs-ui.Vt23p4/full-profile \
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
~/.dotnet/dotnet test Unfold.slnx -c Release --no-restore --disable-build-servers \
  --logger trx --results-directory /tmp/unfold-pet-tabs-ui.Vt23p4/full

UNFOLD_DATA_DIR=/tmp/unfold-pet-tabs-ui.Vt23p4/smoke-profile \
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
~/.dotnet/dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
```

최초 제한 환경에서는 .NET 로컬 IPC 소켓 권한 때문에 빌드가 시작되지 않았다.
로컬 IPC와 렌더링 서비스 접근이 허용된 실행으로 위 결과를 확인했다. 제품 오류로 집계하지 않았다.

## 실제 앱 렌더링 캡처

| 화면 | 캡처 |
|---|---|
| 만들기 · 최소 창 | [860×680](images/2026-09-18-pet-tabs-ui/settings-pet-create-minimum.png) |
| 만들기 · 기본 창 | [1120×800](images/2026-09-18-pet-tabs-ui/settings-pet-create-tab.png) |
| 열기 · 최소 창 | [860×680](images/2026-09-18-pet-tabs-ui/settings-pet-open-minimum.png) |
| 열기 · 기본 창 | [1120×800](images/2026-09-18-pet-tabs-ui/settings-pet-open-tab.png) |
| 파일 입력 후 카드 선택 | [별도 진단 창](images/2026-09-18-pet-tabs-ui/custom-pet-editor.png) |
| 생성된 팩과 참고 사항 | [별도 진단 창](images/2026-09-18-pet-tabs-ui/custom-pet-preview.png) |
| 잘못된 팩 | [별도 진단 창](images/2026-09-18-pet-tabs-ui/pack-error.png) |

별도 진단 창은 호환성 검사용 좁은 창으로 큰 페이지 제목을 유지한다.
실제 설정 창의 두 펫 탭에는 큰 페이지 제목이 없다. 위 캡처는 테스트 펫과 격리 데이터로 만들었다.

## 미검증·위험

- macOS 결과는 네이티브 off-screen 렌더링과 프로그래밍 방식 버튼 이벤트다.
  실제 사용자의 마우스·키보드, OS 파일 선택기는 이번 실행에서 직접 조작하지 않았다.
- Windows 실기, OS 화면 배율 100/150/200%, 다중 모니터 이동과 실제 음성 낭독은 미검증이다.
  미리보기 크기 선택 테스트를 OS 배율 검증으로 취급하지 않는다.
- 판매용 펫 아트 품질, MP4 변환의 실제 도구 실행, 배포 파일 생성·서명·공증은 이번 범위가 아니다.
- 후속 홈·기록·말풍선 UI는 별도 검수와 시안을 거쳐 진행한다.
