# UI/UX 감사 후속 구현 — 2026-09-18

## 결과

[UI/UX 감사](2026-09-18-ui-ux-audit.md)의 12개 수정 항목을 구현했다. 기존의 탭 구조, 큰 페이지 헤더 제거, 가로 스크롤 없는 행동 카드, 입력의 단일 테두리와 호버 강조 제거는 유지했다.

전체 Release 테스트 **196개 통과 / 0 실패 / 0 건너뜀**. 새 격리 데이터의 macOS off-screen smoke는 **성공**, **42장 캡처**를 생성했다. 변경 화면을 직접 검토했고 최소 창의 첫 파일 추가 버튼, 비활성 아이콘, 짧은 알림, 기본 펫 투명 배경을 확인했다.

Windows 실기, VoiceOver/Narrator, 실제 키보드·포인터 종단 조작과 소리 청취는 미검증이다. 이를 포함한 제품 전체의 실기 검증 완료를 뜻하지 않는다.

## 원인과 변경

| 감사 ID | 구현한 동작 | 확인 근거 |
|---|---|---|
| UX-01 | 기록 화면이 새로고침·탭 재진입 시 현재 날짜를 다시 확인한다. 최신 기간은 새 날짜를 따라가고 과거 조회는 유지한다. 다음 기간은 오늘을 넘지 않는다. | 날짜 공급원을 바꿔 최신·과거·다음 버튼·CSV 범위를 검사 |
| UX-02 | 사이드바·트레이 종료 시 미저장 펫 초안을 확인한다. 저장/버리기/취소, 불완전 초안의 계속 작성, 저장 창 취소·실패 시 종료 취소를 제공한다. 성공적으로 저장한 초안은 다시 경고하지 않고 후속 변경은 다시 추적한다. | 이름·파일 배정·제거, 제목 표시줄 취소, 저장 취소·실패·성공, 실제 설정 탭의 종료 보호 연결 검사 |
| UX-03 | 홈에서 휴식 초대와 실제 휴식 진행을 구분한다. 상태 배지의 텍스트도 제한된 폭 안에서 줄바꿈한다. | 초대/진행/중지 상태 검사와 최소 창 캡처 |
| UX-04 | 입력을 수정하면 미적용 변경 안내를 표시한다. 미적용 간격을 두고 재생하면 저장된 값으로 되돌렸다는 안내를 표시한다. WAV 선택·기본 복원을 알림 카드의 적용 동작에 포함했다. | 저장 전 값 유지, 저장 후 재수정, 재생 시 복원, 효과음의 명시적 적용 검사 |
| UX-05 | 미리보기 중인 행동을 추적하고 표시한다. 다른 행동 파일을 제거해도 현재 미리보기는 유지한다. | 두 동작을 가져온 뒤 다른 동작/현재 동작 제거를 각각 검사 |
| UI-01 | 아이콘이 버튼 presenter의 활성/비활성/강조/위험 색을 상속한다. 팩 열기·재생·설치 및 알림 설정 오류는 오류 색과 실패 문구로 표시한다. | 비활성 아이콘의 실제 색 검사, 빈 팩·행동 카드·팩 오류 캡처 |
| UI-02 | 창 높이 740px 미만에서 펫 만들기 미리보기를 156px로 줄인다. 기본 높이는 272px다. 첫 줄의 파일 추가 버튼이 최소 창에서도 보인다. | 860×680 첫 화면의 버튼이 스크롤 viewport 안에 있는지 검사, macOS 캡처 |
| UX-06 | 효과음별 미리듣기/정지와 파일명을 제공한다. 다른 미리듣기를 시작하거나 탭 이동·창 숨김 시 기존 재생을 취소한다. 미리듣기 실패는 화면에 표시한다. | 취소 가능한 재생 대역을 이용한 중복·정지·실패 검사, WAV ID/이름 저장과 유효성·길이 검사 |
| A11Y-01 | 키보드 진입 시 연결된 입력 라벨에 ‘· 선택’과 강조색을 표시한다. 입력 테두리는 바꾸지 않는다. 선택 탭을 접근성 이름에 포함하고 탭별 첫 조작 요소로 포커스를 옮긴다. 트레이에 명시적인 ‘휴식 알림으로 이동’을 추가했다. | 필드의 키보드/포인터 포커스·테두리 유지·탭 선택/포커스 검사. 실제 트레이→말풍선 조작은 OS 확인 필요 |
| VIS-01 | 기본 Mochi의 stretch를 기존 투명 스프라이트 시트의 8~15번 프레임을 왕복하는 클립으로 연결했다. 원본 그림은 재생성·재인코딩하지 않았다. | idle/stretch 모두 384×384, 모든 프레임 투명 픽셀 포함, 리소스 검사 성공 |
| UI-03 | 홈 필드명을 ‘스트레칭 알림 간격’으로 명확히 하고 시간 설정 라벨·설명을 공통 caption 크기로 맞췄다. | 설정 레이블 회귀 검사, 최소 창 캡처 |
| UI-04 | 5분 전/완료 안내는 320×170, 초대/휴식 타이머는 320×268로 표시한다. 네 방향 창 크기·꼬리·펫 배치를 동일한 높이로 계산한다. | 두 높이×네 방향의 영역 검사, 사전·완료·진행 캡처 |

## 소유 파일과 변경 경계

- 기록: `src/Unfold.Desktop/BreakReviewWindow.cs`.
- 타이머·설정·접근성: `SettingsWindow.cs`, `SettingsWindow.Layout.cs`, `SettingsWindow.Notifications.cs`, `TimerControls.cs`, `Ui.cs`.
- 펫 작성·팩 미리보기: `CustomPetWindow.cs`, `PetManagementView.cs`, `PetPackWindow.cs`.
- 휴식·종료 연결: `AppRuntime.cs`, `PetWindow.cs`, `PetSpeechBubble.cs`.
- 효과음: `src/Unfold.Core/AppSettings.cs`, `ReminderSounds.cs`, `src/Unfold.Desktop/ReminderSoundPlayer.cs`.
- 기본 펫: `Assets/Characters/default-cat/character.json`, `Art/Characters/default-cat/resource.json`. 원장 contentVersion은 1.0.1로 갱신했다. 기존 GIF와 PNG 바이트는 보존했다.
- 검사: `Tests/Unfold.Tests/UiAuditRegressionTests.cs`에 의미 있는 회귀 검사 12개를 추가했다. 기존 `SettingsDashboardTests.cs`의 필드명 기대값을 갱신했다. `Unfold.Desktop.csproj`에는 테스트 어셈블리의 내부 구성 요소 접근을 허용했다.
- 실제 동작 변경에 맞춰 설정·효과음·디자인 시스템·펫 작성·리소스 관리 문서를 갱신했다.

펫 초안은 여전히 메모리에 있다. 종료 보호는 사이드바·트레이의 정상 종료에 적용하며 프로세스 강제 종료나 시스템 장애에서 복구하는 자동 저장을 새로 구현하지 않았다. 창을 숨기거나 탭을 이동할 때는 초안을 유지하고 확인창을 띄우지 않는다.

WAV 가져오기는 검증된 파일을 보관 폴더에 복사하되, 선택한 효과음 설정은 적용 전까지 변경하지 않는다. 기존의 ID만 있는 설정은 계속 읽으며 파일명이 없는 항목은 짧은 ID로 식별한다. 미리듣기는 알림 음소거 여부와 무관하게 사용자가 명시적으로 실행할 수 있다.

## 검증

기준 체크아웃은 `release/mvp`, `5e91545aabefe3f89fee7cb5f8e20a55f17b92de`다. 이 커밋 위의 작업 트리에서 검사했다.

| 검사 | 결과 |
|---|---|
| 새 회귀 + 설정 화면 집중 검사 | 17/17 통과 |
| 전체 Release 빌드·테스트 | 196/196 통과, 0 실패, 0 건너뜀 |
| macOS 격리 smoke | exit 0, `success: true`, PNG 42장 |
| 최소 창/탭/가로 넘침 | 관련 smoke 플래그 통과, `noHorizontalOverflow: true` |
| 기본 펫 자산 | exit 0, idle 8프레임·stretch 15프레임, 두 클립 모두 384×384, 빈 프레임 없음 |
| 남은 자산 경고 | attention/celebrate/click 미제공. 기존 제품 범위를 유지하며 별도 동작을 임의로 추가하지 않음 |
| 문서·diff | 링크/JSON 구문 및 `git diff --check` 확인 |

실행 명령:

```sh
UNFOLD_DATA_DIR=/tmp/unfold-ui-fixes-20260918.3EEBWb/tests-profile \
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
/Users/hwanghyeonseong/.dotnet/dotnet test Unfold.slnx -c Release --no-restore --disable-build-servers \
  --logger trx --results-directory /tmp/unfold-ui-fixes-20260918.3EEBWb/tests

UNFOLD_DATA_DIR=/tmp/unfold-ui-fixes-20260918.3EEBWb/smoke-profile \
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
/Users/hwanghyeonseong/.dotnet/dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test

/Users/hwanghyeonseong/.dotnet/dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll \
  --validate-characters Assets/Characters
```

재실행은 새 빈 `UNFOLD_DATA_DIR`에서 한다. 결과 수치와 원본 TRX 위치, smoke JSON은 [검증 결과](2026-09-18-ui-ux-fixes.json)에 보존했다.

## 화면 근거

| 화면 | 캡처 |
|---|---|
| 홈 최소 창 | [시간 필드·상태·하단 조작](images/2026-09-18-ui-ux-fixes/settings-minimum.png) |
| 알림 설정 | [미리듣기·파일·기본·적용](images/2026-09-18-ui-ux-fixes/settings-options-overview.png) |
| 펫 만들기 최소 창 | [첫 파일 추가 버튼 노출](images/2026-09-18-ui-ux-fixes/settings-pet-create-minimum.png), [스크롤 후](images/2026-09-18-ui-ux-fixes/settings-pet-create-minimum-scrolled.png) |
| 빈 펫 팩 | [비활성 아이콘](images/2026-09-18-ui-ux-fixes/settings-pet-open-tab.png) |
| 팩 오류 | [실패 색상과 안내](images/2026-09-18-ui-ux-fixes/pack-error.png) |
| 짧은 알림 | [5분 전](images/2026-09-18-ui-ux-fixes/speech-five-minutes.png), [완료](images/2026-09-18-ui-ux-fixes/speech-completed.png) |
| 휴식 진행 | [타이머와 투명 기본 펫](images/2026-09-18-ui-ux-fixes/speech-overtime.png) |

팩 오류 등 진단용 별도 창의 상단 제목은 일반 설정 탭의 헤더와 다르다. 일반 탭에 큰 제목을 복원하지 않았다. 진단에서 원래의 GIF를 가져와 만든 사용자 팩에는 원본 배경이 유지되며, 기본 Mochi의 manifest 변경과 구분해야 한다.

## 미검증·남은 확인

- 실제 macOS 키보드·마우스: Tab 순서, 트레이에서 말풍선으로 진입, 드래그/클릭 통과, OS 파일 선택 창.
- 실제 Windows: 125/150/200% 배율, 모니터 이동, 기본·최소 창, 네 방향 알림.
- VoiceOver/Narrator: 선택 탭과 입력 레이블 읽기, 오류 안내, 타이머가 매초 불필요하게 반복 낭독되지 않는지.
- 실제 오디오 장치: 가져온 WAV와 기본 소리 청취, 체감 음량, 지연, 장치 변경. 이번 재생 UI 테스트는 대역을 사용했다.
- 장시간 사용: 자정·잠자기 복귀·강제 종료는 실시간 재현하지 않았다. 자정의 날짜 전환은 제어 가능한 날짜 공급원으로 자동 검증했다.
- 캐릭터: 투명 배경과 캔버스 일치는 확인했지만 고DPI·전 프레임의 움직임 품질과 상업적 권리 승인은 별도다.

이번 작업은 구현·로컬 검증까지다. 배포 파일 재생성·외부 게시·커밋은 수행하지 않았다.
