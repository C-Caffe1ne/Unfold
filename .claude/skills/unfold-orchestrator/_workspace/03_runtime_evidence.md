# A3 실행 증거 수집 — 2026-10-06

## 요약

- 오늘 smoke 진단은 `success=true`로 끝났고 크래시·데이터 손실 증거는 없다. 시작 1,546ms, 총 56.9초, workingSet 162MB, 1코어 CPU 1.9%로 비정상 수치는 보이지 않았다.
- 다만 `success=true` 아래에 `nativeCaptionInputVerified:false`, `updaterInstalled:false`, `stopWhileOpening:false`, `weeklyReview.routineNameVisible:false`가 섞여 있어 "미검증"과 "검증 실패"를 JSON만으로 구분할 수 없다.
- 캡처 112장 중 **9쌍이 바이트 동일**했다. 특히 `settings-minimum.png` = `settings-minimum-scrolled.png`, `theme-*-builder.png` = `theme-*-pets.png`(4테마 전부)로, smoke.json이 `true`로 기록한 스크롤·빌더 검증의 **시각 근거가 비어 있다**.
- 제품 결함으로 확인된 것은 로그아웃 상태의 활성 로그아웃 버튼(소스 확인), 픽셀 에디터 전면 영어 UI(소스 확인), 긴 펫 이름 잘림이다.
- 테스트는 최신 입력 전체 748개 중 746 통과·2 실패이며, 실패 2건은 테스트 코드가 `/private/artifacts`에 쓰려다 난 권한 오류로 제품 결함이 아니다. 같은 날짜 문서 두 건이 "746/748"과 "748개 모두 통과"로 서로 모순되고, 후자가 인용한 근거 파일은 저장소에 없다.

## 실행·근거 목록

| 시각/대상 | 방법 | 결과 |
|---|---|---|
| smoke 진단 결과(오늘 16:54~16:55 생성, HEAD 1a74cbf 격리 Release) | `cat`/`ls`로 `smoke-data-165424/verification` 전수 확인 | 파일 112개(PNG 107, JSON·CSV·unfoldpet 등) |
| `verification/smoke.json` | 전 필드 수기 검토 | success=true, false 필드 4개 식별 |
| `unfold.log` | 전문 확인 | 예외 2건(의도된 진단 1, 손상 ZIP 1) |
| `settings.json` / `break-history.json` / `review.csv` | 전문 확인 | 기록 1건, CSV 1행, 영어 문자열은 진단 픽스처 유래로 확인 |
| 캡처 치수 | `sips -g pixelWidth -g pixelHeight` 전수 | 640×560~1140×820, 창 밖 버튼 없음 |
| 캡처 중복 | `md5 -r *.png`로 전수 해시 비교 | 동일 해시 9쌍 |
| 대표 캡처 열람 | Read 도구 18장(home/responsive/review/speech/dialog/pack/theme/editor/routine/custom-pet/picker) | 아래 발견 표 |
| TRX 분석 | `grep -o 'outcome="..."'` + python 정규식으로 Failed 블록만 추출 | final-full 748 중 Failed 2, Error 0, skip 0 |
| 실패 원인 | TRX `ErrorInfo` 메시지·스택 | `UnauthorizedAccessException: '/private/artifacts' is denied` 2건 |
| 문서 대조 | `2026-10-06-agency-execution.md`, `2026-10-06-audit-followup.md`, `summary.json` | 결과 수치 모순 확인 |
| 근거 파일 존재 확인 | `find <worktree> -name final-release-v3.trx` 등 8개 | 전부 미존재 |
| 소스 추적(읽기 전용) | 격리 복사본 `unfold-src-1a74cbf`에서 `grep` | EV-01·EV-02 소스 위치 확정 |

추가 진단 실행은 하지 않았다. 디스크 여유와 "새 근거 없이 통과한 검사를 반복하지 않는다" 원칙에 따라, 오늘 생성된 기존 smoke 산출물만으로 분석했다. 원본 worktree는 읽기만 했다.

## 캡처 색인표

| 캡처(그룹) | 수 | 화면/상태 |
|---|---|---|
| `home-default` / `home-minimum` | 2 | 홈 대시보드(타이머+펫+시간설정+오늘의 작은 쉼), 1120×800 / 860×680 |
| `home-minimum-long-name-error` | 1 | 긴 펫 이름 + "저장하지 못했어요." 오류 픽스처, 860×680 |
| `home-pet-picker` | 1 | 펫 선택 드롭다운 팝업(보리/펭귄/Mochi/강아지/고슴도치) |
| `responsive-home-top|bottom`, `responsive-settings-top|bottom`, `responsive-pet-create`, `responsive-review` | 6 | 최소 640×560에서 각 페이지 스크롤 상/하단 |
| `settings*`(`settings.png`, `-completed`, `-minimum`, `-minimum-scrolled`, `-review-tab`, `-pet-create-tab`, `-pet-create-minimum*`) | 9 | 설정 창의 홈/기록/펫 탭, 기본·최소 크기 |
| `settings-options-*`(default/top/bottom/minimum/applied) | 5 | 설정 탭 옵션 목록(알림·타이머·앱 동작·앱 정보·계정·디버그) |
| `settings-direction-popup` | 1 | 말풍선 방향 선택(위/아래/왼쪽/오른쪽), 현재 "위" |
| `review-default|minimum|compatibility|compatibility-minimum` | 4 | 주간 회고, 1120×800 / 860×680 / 620×650 / 560×600 |
| `weekly-review`, `weekly-review-expanded` | 2 | 날짜별 기록 접힘 / 펼침(완료 시각·실제 휴식 시간) |
| `routine-library`, `routine-editor`, `additional-routine`, `profile-editor`, `work-profiles` | 5 | 루틴 목록·편집기·추가 루틴·프로필 편집기·작업 프로필 |
| `custom-pet-editor`, `custom-pet-preview`, `custom-pet-minimum`, `custom-pet-minimum-scrolled` | 4 | 펫 만들기(이름·미리보기·행동 연결·파일) |
| `pack-preview|installed|update|reinstall|error` | 5 | 펫 팩 미리보기·설치·업데이트·재설치·열기 실패 |
| `editor` | 1 | 픽셀 에디터 전체(1140×820) |
| `pet`, `native-window-client` | 2 | 데스크톱 펫 창, 네이티브 창 클라이언트 영역 |
| `speech-top|bottom|left|right` | 4 | 말풍선 4방향("스트레칭할 시간이에요" + 5분 뒤에/휴식 시작) |
| `speech-five-minutes`, `speech-completed`, `speech-overtime` | 3 | 미루기 후·완료·초과(+00:10) 말풍선 |
| `timer-running|paused|stopped`, `timer-roll-in|out` | 5 | 타이머 상태와 숫자 롤 애니메이션 |
| `timer-refinement-*`(home/settings 1120·640, hover start/mid/settled, rest mid/settled, auto-snooze before/after, updater) | 12 | 타이머 리파인먼트 계측용 캡처 |
| `dialog-confirm`, `dialog-prompt`, `dialog-error` | 3 | 공용 확인·이름 입력·오류 대화상자 |
| `confirm-quit`, `confirm-stop` | 2 | 종료·정지 확인 |
| `app-updates` | 1 | 업데이트 확인 대화상자 |
| `account-login`, `account-login-minimum` | 2 | 로그인 화면 940×620 / 640×560 |
| `theme-{MidnightBlue,OatLatte,Plum,Sage}-{home,settings,review,pets,builder,picker,speech}` | 28 | 테마 4종 × 화면 7종 |
| `review.csv`, `smoke.json`, `timer-refinement-auto-snooze-measurements.json`, `custom-pet.unfoldpet`, `sounds/` | 5 | 비캡처 산출물 |

색인 주의:
- `responsive-review.png`(640×560)만 "이 7일 동안 완료한 휴식이 없어요."의 빈 상태다. 같은 실행의 `weekly-review*`·`review-*`는 1회/3분 10초를 표시한다. 휴식 완료(16:54:46) 이전에 찍힌 순서 차이로 보이며 제품 결함으로 판정하지 않는다.
- `routine-editor.png`의 영어 안내문 "Look away and enjoy a short pause."는 `src/Unfold.Desktop/SmokeDiagnostics.cs:92`가 주입한 진단 픽스처다. 제품 기본값이 아니므로 결함으로 보고하지 않는다. `settings.json`의 "Revised writing pause" 등도 같다.
- `weekly-review-expanded.png`는 `review-compatibility.png`와 동일 파일이다(620×650 펼침 상태 공용).

## 발견 표

| ID | 심각도 | 제목 | 근거 | 관찰 | 확신도 | 소스 후보 | 수정 방향 |
|---|---|---|---|---|---|---|---|
| EV-01 | P2 | 로그인하지 않은 상태에서 "로그아웃" 버튼이 표시·활성 | `settings-options-bottom.png`, `responsive-settings-bottom.png` | 계정 카드가 "로그인되지 않음"인데 오른쪽에 danger 스타일 로그아웃 버튼이 활성 상태로 나란히 있다. 소스에서 노출 조건이 세션이 아니라 `AccountSignOutPending`에만 묶여 있다 | 확인됨 | `src/Unfold.Desktop/SettingsWindow.Layout.cs:288,307-312` | 세션 null이면 버튼을 숨기거나 "로그인" 진입으로 대체 |
| EV-02 | P2 | 픽셀 에디터 UI 전체가 영어 | `editor.png` | 앱 전체가 한국어인데 이 창만 PIXEL EDITOR/Undo/Pencil/Eraser/Brush size/Onion skin/FRAMES/LAYERS/Save character로 전부 영어다. "Export Piskel"은 내부 도구명을 노출한다 | 확인됨 | `src/Unfold.Desktop/EditorWindow.cs:46-49` 및 같은 파일의 도구·패널 라벨 | 다른 화면과 같은 한국어 카피로 교체하고 Piskel은 형식명이 필요하면 ".piskel로 내보내기" |
| EV-03 | P2 | 긴 펫 이름이 말줄임 없이 카드 경계에서 잘림 | `home-minimum-long-name-error.png` | 2줄째가 "…새로운 고"에서 글자 중간이 잘린다. 말줄임표도 전체 노출도 없다 | 확인됨 | 홈 펫 카드 제목 TextBlock(`src/Unfold.Desktop/SettingsWindow.Layout.cs` 홈 구성부) | `TextTrimming=CharacterEllipsis` + 최대 2줄, 전체 이름은 ToolTip |
| EV-04 | P2 | 최소 크기 스크롤 검증의 시각 근거 없음 | `settings-minimum.png`와 `settings-minimum-scrolled.png`의 md5 동일(`e1a056df…`), smoke.json `settingsLayout.detailsScrollVerified:true` | "스크롤 후" 캡처가 "스크롤 전"과 바이트 단위로 같다. 스크롤이 일어나지 않았거나 캡처가 스크롤 전에 찍혔다 | 캡처 동일은 확인됨 / 제품 스크롤 실패 여부는 추정 | 진단 캡처 순서(`src/Unfold.Desktop/SmokeDiagnostics*.cs`), 해당 ScrollViewer | 스크롤 오프셋을 측정값으로 JSON에 기록하고, 오프셋 변화가 0이면 진단을 실패 처리 |
| EV-05 | P2 | 테마별 펫 빌더 캡처가 펫 목록 캡처와 동일(4테마 전부) | `theme-{MidnightBlue,OatLatte,Plum,Sage}-builder.png` = 같은 테마의 `-pets.png`, smoke.json `petManagementBuilderVerified:true` | 4쌍 모두 md5 동일. 빌더 화면이 캡처된 적이 없어 테마별 빌더 깨짐을 판정할 근거가 없다 | 확인됨(증거 공백) | 테마 캡처 루프(`src/Unfold.Desktop/SmokeDiagnostics*.cs`) | 빌더 탭으로 실제 전환 후 캡처하고, 두 캡처가 동일하면 진단 실패 처리 |
| EV-06 | P2 | 주간 회고 상세에 루틴 이름이 없음 | smoke.json `weeklyReview.routineNameVisible:false`, `weekly-review-expanded.png` | 펼친 행은 "16:54 완료 · 실제 휴식 3분 10초"만 보여준다. `break-history.json`에는 `routineName: "글쓰기 휴식"`이 저장돼 있는데 화면에 쓰이지 않는다 | 확인됨 | 주간 회고 상세 행 구성부(`src/Unfold.Desktop/` 회고 화면) | 저장된 routineName(과 profileName)을 상세 행에 표시 |
| EV-07 | P2 | `success=true` 아래에 미검증/실패 구분 없는 false 필드가 섞임 | smoke.json `nativeCaptionInputVerified:false`, `updaterInstalled:false`, `stopWhileOpening:false` | 세 값이 "검사했는데 실패"인지 "환경상 미실행"인지 JSON만으로 알 수 없다. 전체 success는 true라 하위 false가 묻힌다 | 확인됨 | smoke 결과 직렬화부(`src/Unfold.Desktop/SmokeDiagnostics*.cs`) | 불리언 대신 `verified/skipped/failed` 3상태와 사유 문자열로 기록 |
| EV-08 | P2 | 전체 테스트 2건이 제품 밖 경로 쓰기로 실패 | `final-full.trx`: `UiTests.EditorRendersAtDefaultAndMinimumWindowSizes`, `AccountWindowTests.ALayoutRendersLoginAndPurchaseWithSharedThemesAndFitsMinimumSize`, 메시지 `Access to the path '/private/artifacts' is denied` | 테스트가 `AppContext.BaseDirectory` 상위 5단계로 올라가 `/private/artifacts`를 만들려다 권한 오류. 제품 결함이 아니라 테스트 하네스가 출력 경로를 가정한 결함 | 확인됨 | `Tests/Unfold.Tests/UiTests.cs:81`, `Tests/Unfold.Tests/AccountScreenTests.cs:333` | 캡처 출력 경로를 환경변수나 `TestContext` 기준 상대 경로로 바꿔 빌드 위치에 의존하지 않게 수정 |
| EV-09 | P2 | 같은 날짜 검증 문서 두 건의 테스트 결과가 모순, 한쪽 근거 파일은 부재 | `2026-10-06-audit-followup.md`("748개 테스트 모두 통과") vs `2026-10-06-agency-execution.md`·`summary.json`(746/748, 실패 2) | followup이 인용한 `final-tests-v3.log`, `final-test-results-v3/final-release-v3.trx`, `published-smoke-v4.json`, `published-auto-snooze-v4.json`, `native-keyboard-observations.json`, `preservation-final.json`, `clean-publish-verification-v4.json`, `expiry-green.log`는 worktree 전체 `find`에서 하나도 나오지 않는다 | 확인됨 | `docs/validation/2026-10-06-audit-followup.md` | 두 문서의 실행 기준(HEAD·시점)을 명시해 분리하고, 인용 근거 파일을 커밋하거나 인용을 삭제 |
| EV-10 | P3 | 640×560에서 "펫 팩 만들기" 버튼 라벨이 말줄임 | `responsive-pet-create.png` | 하단 작업 버튼이 "펫 팩 만들기…"로 잘린다. 창 안에는 있으나 라벨 폭이 부족하다. 같은 캡처에서 "펫 선택" 라벨과 플레이스홀더가 동일 문구로 중복된다 | 확인됨 | 펫 만들기 하단 액션 버튼 폭 상수(`SettingsActionWidth` 계열) | 최소 폭에서 버튼이 라벨을 수용하도록 폭을 콘텐츠 기준으로 두거나 라벨 축약 |
| EV-11 | P3 | 대기 상태 타이머 숫자의 대비가 낮음(모든 테마 공통) | `theme-MidnightBlue-home.png`, `theme-Sage-home.png`(동일 패턴) | "휴식 대기 중" 상태에서 가장 큰 요소인 45:00이 배경과 가까운 회색이다. 진행 중(`home-default.png`)에서는 고대비다 | 가능성 높음(상태별 의도일 수 있음) | 타이머 숫자 전경색 토큰 | 대기 상태 전경색을 WCAG AA 대비까지 올리고, 상태 구분은 색이 아닌 배지로 유지. 실제 대비 측정은 접근성 QA가 확정 |
| EV-12 | P3 | 오류 대화상자가 오류임을 제목에서 알리지 않음 | `dialog-error.png` vs `dialog-confirm.png` | 오류 창의 머리글이 "UNFOLD / 확인", 제목이 "Unfold"로, 확인 대화상자와 동일한 틀이다. 본문만 "파일을 읽거나 저장하지 못했어요."다 | 확인됨 | 공용 대화상자 구성부 | 오류 변형에 전용 머리글/제목(과 danger 색 강조) 부여 |
| EV-13 | P3 | 펫 선택 목록의 이름 표기 혼용과 목록 길이 불일치 | `home-pet-picker.png`, smoke.json `characters:8` | 보리·펭귄·강아지·고슴도치(한국어) 사이에 "Mochi"만 영어다. 또 캐릭터 8개 중 5개만 보이고 스크롤 표시가 없다 | 이름 혼용은 확인됨 / 목록 잘림은 추정(캡처 시점에 3개가 아직 없었을 수 있음) | `src/Unfold.Core/CharacterLibrary.cs`, 펫 선택 팝업 | 기본 캐릭터 표시명을 한 언어로 통일하고, 팝업에 스크롤 표시 추가 |
| EV-14 | P3 | 펫 팩 창의 오류 상태가 약하게 표현되고 빈 공간이 과도 | `pack-error.png` | 520×850 중 약 400px가 빈 미리보기와 공백이다. 오류 문구는 좌하단 2줄로 접히고 색 강조가 없으며, 비활성 "저장" 버튼과 같은 줄에 있다 | 확인됨 | 펫 팩 창 레이아웃(`src/Unfold.Desktop/PetPackWindow.cs`) | 오류를 미리보기 영역 안 빈 상태로 승격하고 danger 색 적용 |
| EV-15 | P3 | 사용자 로그에 전체 스택과 빌드 절대 경로가 기록됨 | `unfold.log` 2행 이하, `CharacterPack.cs:31` / `PetPackWindow.cs:171` 경로 노출 | 손상 팩 처리 시 `InvalidDataException: Central Directory corrupt` 전체 스택이 빌드 머신 경로와 함께 남는다. 기능상 정상 경로(거부)지만 로그 내용이 과하다 | 확인됨 | `src/Unfold.Core/CharacterPack.cs:31`, `src/Unfold.Desktop/PetPackWindow.cs:171` | 예상된 거부 경로는 요약 1행으로 기록하고 전체 스택은 디버그 도구 활성 시에만 |

중복 해시 전체(참고): `settings-minimum`/`-scrolled`, `theme-*-builder`/`-pets`(4쌍), `settings-options-minimum`/`-top`, `app-updates`/`timer-refinement-updater`, `settings-review-tab`/`theme-Plum-review`, `review-compatibility`/`weekly-review-expanded`. 뒤 네 쌍은 같은 상태를 다른 이름으로 저장한 것으로 보여 결함으로 보고하지 않았다.

## 테스트 결과 분석

| TRX | 총 | 통과 | 실패 | skip | 비고 |
|---|---:|---:|---:|---:|---|
| `final-full.trx` (최신 입력 18a3a31, 격리 Release) | 748 | 746 | 2 | 0 | 실행 16:26~16:30. ResultSummary outcome=Failed |
| `latest-focused.trx` | 67 | 67 | 0 | 0 | GLB·Windows 연결·호버 집중 |
| `legacy-focused.trx` | 19 | 19 | 0 | 0 | release/mvp GLB·설정 |
| `legacy-pointer.trx` | 7 | 7 | 0 | 0 | 이전 동시 실행에서 실패했던 입력 항목 단독 재실행 |
| `pet-file-focused.trx` | 18 | 18 | 0 | 0 | 파일 연결·기존 인스턴스 전달 |
| `layout-standard.trx` | 2 | 2 | 0 | 0 | 실패 2건을 표준 빌드 출력 위치에서 재실행 |

- 실패 2건의 원인은 단일하다: 테스트가 `/private/artifacts` 디렉터리를 만들려다 `UnauthorizedAccessException`. 두 스택 모두 `Directory.CreateDirectory` 직후다. 표준 빌드 경로에서 같은 2건이 2/2 통과했으므로 제품 코드 결함으로 볼 근거는 없다(EV-08).
- skip 0, timeout 0, aborted 0, inconclusive 0, Error outcome 0(검색에 걸린 `outcome="Error"` 2건은 실패한 두 결과의 `RunInfo` 요소이며 테스트 카운터는 error=0).
- 비정상적으로 긴 테스트는 없었다. 실패 2건의 duration은 0.59초와 0.009초다.
- 간헐 실패 패턴: `agency-execution.md` 기록대로, 이전 작업본 동시 실행 중 공간 부족으로 완료 TRX를 얻지 못했고 그때 관찰된 실패 3건은 단독 실행 시 7/7 통과했다. 동시 부하가 원인이라고 확정된 바 없으므로 **미확정 간헐 실패**로 남는다. `pet-pack-ux` 전체 실행에는 미디어 도구 부재 skip이 있었다고 기록돼 있으나 해당 TRX는 이 폴더에 없다.
- 경계 요약: **오늘 "단일 전체 실행 748/748 통과" 근거는 없다.** 있는 것은 (a) 격리 Release 단일 전체 실행 746/748, (b) 실패 2건의 표준 경로 재실행 2/2, (c) 집중 검사 4건 전부 통과다. `audit-followup.md`의 "748개 모두 통과"는 다른 HEAD(c44ef12)를 가리키면서 근거 파일이 부재하므로 이 숫자를 채택하지 않는다(EV-09).

## 미검증 범위

이 수집에서 확인하지 못한 것:

- 실제 OS 창·입력·보조기술 동작. smoke 캡처는 off-screen 렌더링이며, 키보드 포커스 순서, VoiceOver/Narrator, 실제 클릭·드래그, 창 포커스는 이 증거로 판정할 수 없다.
- 실제 Windows 환경(클릭 통과, DPI, GPU, 다중 모니터).
- 장시간 성능·메모리 추세. 이번 smoke는 56.9초 단일 실행이며 `workingSetBytes` 162MB는 단일 시점 값이다. `managedBytes` 382MB는 누적 할당으로 읽히므로 누수 판정에 쓸 수 없다.
- 설치본 업데이트와 데이터 보존. smoke.json `updaterInstalled:false`이고 `app-updates.png`는 대화상자 캡처일 뿐이다.
- 실거래·OAuth. smoke.json `accountScreen.authenticationPerformed:false`, `paymentPerformed:false`.
- 애니메이션의 실제 타이밍·프레임 품질. `timer-refinement-*`는 정지 캡처와 계측 JSON뿐이다.
- 테마 캡처는 대표만 열람했다(MidnightBlue·Sage home, Plum review). OatLatte 전 화면과 각 테마의 settings/review/speech 다수는 미열람이며, 빌더 캡처는 EV-05로 근거 자체가 없다.
- 추가 진단(펫 팩 재생, GLB 진단)은 실행하지 않았다. 필요 시 `unfold-src-1a74cbf`에서 새 빈 `UNFOLD_DATA_DIR`로 `--review-original-pets` / `--review-pet-pack`을 순차 실행하면 EV-05·EV-14의 보강 근거를 얻을 수 있다.
