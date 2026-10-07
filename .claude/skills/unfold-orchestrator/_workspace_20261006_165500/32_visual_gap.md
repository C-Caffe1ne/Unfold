# 32. 화면별 시각 격차와 재설계 회귀 위험

- 감사일: 2026-09-17
- 브랜치: `release/mvp`
- 역할: 워커 B · 화면별 시각 격차
- 기준 입력: `.claude/skills/unfold-orchestrator/_workspace/30_ui_redesign_input.md:1-35`
- 후속 대조: `.claude/skills/unfold-orchestrator/_workspace/31_design_system_redesign.md:128-187`
- 현재 소스 기준 baseline: `/tmp/unfold-redesign-baseline.JHBiAh/verification/` PNG 42장, `smoke.json`의 `success=true`, `imageFiles=42`, `os=Unix 26.6.2`, `.NET 10.0.12
- 직전 비교 캡처: `/tmp/unfold-phase2-smoke-final.j5Uen8/verification/` PNG 42장
- 감사 방식: 새 baseline 42장을 화면군별로 모두 열고 현재 소스와 대조했다. 직전 캡처와의 목록 차이 및 바이트 차이도 확인했으며, 제품 코드·테스트는 수정하거나 다시 실행하지 않았다.

## 결론

1. Phase 2 이전의 실제 겹침 결함은 해소됐다. `settings-pet-create-minimum.png`와 `custom-pet-minimum.png`에서 본문 오른쪽과 세로 스크롤바 사이에 8px 거터가 보이고, 입력·버튼을 관통하던 트랙이 사라졌다. 공통 구현은 `src/Unfold.Desktop/Ui.cs:49-66`이다.
2. 라이브러리 하단의 “닫기만 둘째 줄” 문제도 해소됐다. `routine-library.png`와 `work-profiles.png`에서 관리 3개는 왼쪽, 주 작업과 닫기는 오른쪽 한 행에 놓인다(`src/Unfold.Desktop/PersonalizationWindow.cs:67-97`).
3. 고정 경고 요약은 지정된 `pack-*.png` 5장 모두 경고가 없는 진단 팩이라 **시각적으로 검증할 수 없다**. 구현과 자동 검사 근거만 있다(`src/Unfold.Desktop/PetPackWindow.cs:45-49,90-108,141-153`).
4. 가장 큰 잔여 위험은 토큰 변경 자체보다 고정 폭·고정 높이와 비래핑 행이다. 워커 A안은 10–11px→12px, Caption 12→13px/행간 18, Section 17→18px/행간 24, 일부 14px 간격→16px를 제안한다(`31_design_system_redesign.md:134-155`). 설정 860×680, 개인화 600×580, 펫 팩 480×560, 커스텀 펫 520×620, 고정 320×268 말풍선은 이 작은 증가만으로도 줄바꿈·세로 스크롤 임계점을 넘을 수 있다(`src/Unfold.Desktop/SettingsWindow.cs:30-37`, `PersonalizationWindow.cs:10-16`, `PetPackWindow.cs:14-18`, `CustomPetWindow.cs:14-18`, `PetSpeechBubble.cs:13-37`).
5. **캡처-소스 불일치는 새 baseline으로 해소됐다.** 새 42장은 현재 진단의 4개 사이드바 탭, 페이지 헤더 제거, `settings-notification-options.png`·`settings-timer-options.png` 출력과 일치한다(`src/Unfold.Desktop/SmokeDiagnostics.cs:392-410,476-497`). 단, 두 설정 옵션 PNG는 MD5 `4831c841fd9f3d8e1663a92c714db55a`로 완전히 같아 서로 다른 스크롤 상태를 증명하지 못한다. 두 카드가 860×680 뷰포트에 모두 들어가 `BringIntoView()`가 no-op인 현재 레이아웃에서는 화면 결함이 아니지만, 진단 이름과 증거의 의미는 바로잡아야 한다(`src/Unfold.Desktop/SmokeDiagnostics.cs:456-491`).

## Phase 2 항목 대조

| 항목 | 전 캡처의 문제 | Phase 2 후 판정 | 남은 시각 확인 |
|---|---|---|---|
| 스크롤바 예약 트랙 + 8px 거터 | 이름 입력·파일 가져오기 위에 스크롤바가 겹침(`08_ui_visual_audit.md:30-35,60-64`) | **해소** — `settings-pet-create-minimum.png`, `settings-pet-create-minimum-scrolled.png`, `custom-pet-minimum.png`, `custom-pet-minimum-scrolled.png`에서 트랙과 컨트롤이 분리됨. 기본 폭도 `settings-pet-create-tab.png`, `custom-pet-editor.png`에서 불필요한 상시 인셋이 없음 | 트랙은 좁은 창에서 존재감이 크지만 콘텐츠를 침범하지 않아 현재 토큰에서는 어색하지 않다. Windows 125~200% DPI의 실제 트랙 폭은 미검증 |
| 펫 팩 열기 버튼 모서리 | 최소 폭에서 트랙과 버튼이 거의 맞닿음(`08_ui_visual_audit.md:25-28`) | **해소** — `settings-pet-open-minimum.png`에서 버튼 오른쪽과 트랙 사이 공간이 보임 | 현재 소스의 480×560 독립 창과 384px 미리보기 조합은 별도 재캡처 필요(`src/Unfold.Desktop/PetPackWindow.cs:14-18,37-41,83-86`) |
| 고정 경고 요약 | 경고가 본문 아래 접혀 설치 버튼만 보일 수 있었음 | **자동 검사 근거만 있음** — 요약·이동 버튼은 고정 영역에 연결됨(`src/Unfold.Desktop/PetPackWindow.cs:91-108,141-153`) | `pack-preview.png`, `pack-installed.png`, `pack-reinstall.png`, `pack-update.png`, `pack-error.png`에는 경고 요약이 한 번도 나타나지 않아 높이·줄바꿈·설치 버튼과의 균형은 미검증 |
| 현재 알림 설정 그룹 | 값만 있어 선택기 설명으로 읽힘 | **현재 제품 UI 아님** — 직전 `settings.png`, `settings-minimum.png`의 중첩 “현재 알림 설정” 그룹은 새 baseline에서 제거됐고, 최신 대시보드는 `시간 설정`·`오늘의 작은 쉼`만 둔다(`settings.png`; `src/Unfold.Desktop/SettingsWindow.Layout.cs:26-43,126-151`) | 제거된 그룹은 회귀 기준에서 제외한다. 알림 설정의 현재 근거는 별도 설정 탭의 `settings-notification-options.png`다 |
| 하단 동작 그룹 | 5번째 “닫기”가 둘째 줄에 홀로 배치됨(`08_ui_visual_audit.md:47-52`) | **해소** — `routine-library.png`, `work-profiles.png`에서 한 행과 좌우 역할 분리가 명확함 | 새 구조는 `Grid("Auto,*,Auto")`와 비래핑 `Ui.Row`라 토큰 확대 시 이전처럼 자연스럽게 줄바꿈하지 않고 충돌할 위험이 있음(`src/Unfold.Desktop/PersonalizationWindow.cs:87-97`) |
| 접힌 알림 배지 | 접은 뒤 알림 상태가 보이지 않음 | **표시는 추가됨** — `speech-folded.png` 우상단에 대기 링이 보이고 `pet.png`에는 평시 배지가 없음 | 22px 외곽 안의 10px 링은 펫과 시각적으로 떨어져 로딩 표시처럼 읽힐 수 있다. 실제 화면 배경·배율에서 체감 크기와 상태 구분은 미검증(`src/Unfold.Desktop/PetWindow.cs:22-35,153-168`) |

### 워커 A 토큰안이 만드는 실제 레이아웃 변화

- `Body`는 14px로 유지되고 공통 버튼 최소 높이 38px도 이 보고서에서 확대 제안되지 않았다. 따라서 전 화면이 일괄 확대되는 것이 아니라, 설정의 10–11px 직접값과 공통 Caption/Section이 주로 커진다(`31_design_system_redesign.md:134-141,160-173`).
- 세로 위험은 `LabelSmall 12`, `Caption 13/18`, `Section 18/24`와 6·7→8, 10→12, 14→16 간격 정규화가 한 카드에 누적될 때 발생한다(`31_design_system_redesign.md:134-150`). 우측 300px 설정 열, 고정 푸터가 있는 펫 팩, 320px 말풍선을 우선 재캡처한다.
- 반경은 Control 12→10, Card 24/28→16/20, 일반 Frame 32→24로 줄어든다. 이는 외곽 크기를 키우지 않으므로 잘림 위험보다 중첩 카드의 깊이와 버튼 형태가 갑자기 달라지는 시각 회귀를 확인한다(`31_design_system_redesign.md:153-157`).
- 아래의 16px 본문·44px 버튼 조건은 워커 A의 제안값이 아니라 OS 글꼴/접근성 확대까지 포함한 **스트레스 상한**이다. A안 자체의 1차 수용은 위 실제 값으로 판정하고, 스트레스 상한은 워커 D와 실제 OS QA에서 확인한다.

## 화면별 잔여 격차

표의 우선순위는 “현재 캡처에서 즉시 막힘”뿐 아니라 워커 A의 토큰 재설계가 적용될 때 깨질 가능성을 포함한다. `유지`는 새 제안이 아니라 회귀 방지 대상이다.

| 화면 | 확인한 캡처 파일명 | 관찰된 정렬·간격·밀도·잘림·시각 계층 | 소유 파일·의존성 | 우선순위 | 시각 수용 기준 | 검증 |
|---|---|---|---|---|---|---|
| 설정 대시보드 · 기본/상태 | `settings.png`, `timer-paused.png`, `timer-stopped.png`, `settings-completed.png` | 새 baseline은 페이지 헤더와 중첩 알림 요약이 없어져 펫→타이머→오른쪽 `시간 설정`/`오늘의 작은 쉼` 계층이 명확하고 1120×800에서 잘림이 없다. 잔여 격차는 고정 300px 우측 열에 10~17px 레이블·도움말·입력이 모여 큰 펫 카드보다 문자 밀도가 높은 점이다(`settings.png`) | `SettingsWindow.Layout.cs:26-43,67-151`; 공통 토큰 `DesignSystem.cs:15-22`; `Ui.cs:24-45` | P1 · VG-01 | 1120×800에서 각 카드 제목·입력·상태가 겹치지 않고, Primary가 화면당 하나의 명확한 종결 동작으로 읽힌다. A안의 LabelSmall 12, Caption 13/18, Section 18/24를 적용해도 우측 카드의 한글 레이블이 잘리지 않는다 | V1, V6 |
| 설정 대시보드 · 최소/스크롤 | `settings-minimum.png`, `settings-minimum-scrolled.png` | 860×680에서 펫·타이머는 남고 우측 300px 열만 스크롤되며, 처음/끝 캡처 모두 컨트롤 잘림은 없다. 두 PNG는 서로 달라 실제 스크롤 이동을 보여 주며, 8px 거터도 콘텐츠를 침범하지 않는다. 다만 작은 10~11px 설명과 고정 2열 숫자 입력은 토큰 확대 시 높이가 먼저 증가한다(`settings-minimum.png`, `settings-minimum-scrolled.png`) | `SettingsWindow.cs:30-37`; `SettingsWindow.Layout.cs:34-43,126-151`; `SmokeDiagnostics.cs:456-475`; `Ui.cs:49-66` | P1 · VG-01 | 860×680 처음/끝 위치에서 적용·기록 버튼이 완전히 보이고, 스크롤 트랙과 카드 사이 8px가 유지되며, 확대된 글자에서 입력·설명이 서로 침범하지 않는다 | V1, V6 |
| 설정 · 알림 옵션 | `settings-notification-options.png` | 카드 제목·우측 적용, 전폭 말풍선 위치, 체크박스, 좌측 상태/우측 파일·기본 동작의 정렬은 일관되고 잘림이 없다. 반면 10px 간격에 캡션·두 효과음 행·WAV 제한·상태가 연속되어 가장 조밀하다. `적용`은 활성 Primary로 보이고 코드에도 비활성 조건이 없어 **비활성 적용 버튼 가독성은 이 화면에서 검증할 상태 자체가 없다**(`settings-notification-options.png`; `SettingsWindow.Notifications.cs:22-34`; `DesignSystem.cs:67-69`) | `SettingsWindow.Notifications.cs:13-75`; `SettingsWindow.Layout.cs:179-187`; `DesignSystem.cs:58-69` | P1 · VG-09 | 860×680에서 방향·체크·효과음 두 행·상태가 겹치지 않는다. 제품이 비활성 적용 상태를 도입하면 활성 Primary와 구분되면서도 레이블을 읽을 수 있어야 하고, A안 Caption 13/18·간격 12/16 뒤에도 카드가 도달 가능하다 | V1, V6 |
| 설정 · 타이머 옵션 | `settings-timer-options.png` | 두 숫자 필드의 시작선·폭과 상단 우측 적용 정렬은 안정적이고 현재 잘림은 없다. 하지만 `*,12,*` 2열 안의 10~11px 레이블/도움말은 작고 조밀하다. 이 PNG는 알림 옵션 PNG와 바이트 동일하므로 타이머 카드 단독 도달이나 스크롤 상태를 증명하지 못하고, 활성으로 보이는 `적용`만 있어 비활성 적용 가독성도 판정할 수 없다(`settings-timer-options.png`; `SettingsWindow.Layout.cs:105-123`; `SmokeDiagnostics.cs:485-491`) | `SettingsWindow.Layout.cs:105-123,179-187`; 진단 `SmokeDiagnostics.cs:476-497`; 공통 비활성 스타일 `DesignSystem.cs:67-69` | P1 · VG-09/VG-10 | A안 LabelSmall 12·Caption 13/18·간격 12/16 후 2열 레이블/도움말이 겹치지 않는다. 콘텐츠가 뷰포트를 넘으면 상·하단 캡처와 실제 양의 스크롤 오프셋이 함께 확인되고, 넘지 않으면 하나의 overview로 정직하게 기록한다 | V1, V6 |
| 펫 추가 · 열기 탭 | `settings-pet-open-tab.png`, `settings-pet-open-minimum.png` | 이전 버튼-트랙 근접은 해소됐다. 남은 문제는 선택 전의 큰 빈 미리보기 표면이 제목·열기 동작보다 높은 시각 비중을 갖는 점이다. 최소 화면에서는 거의 빈 본문 때문에 스크롤 트랙이 더 두드러진다 | `PetManagementView.cs:8-35`; `PetPackWindow.cs:37-108`; `Ui.cs:49-85` | P2 · VG-03/VG-08 | 빈 상태에서는 “펫 팩 열기…”가 첫 시선과 키보드 순서의 주 행동이며, 미리보기 표면은 선택 후에만 지배적이 된다. 최소 폭에서 버튼·트랙은 8px 이상 분리된다 | V2, V6 |
| 펫 추가 · 만들기 탭 | `settings-pet-create-tab.png`, `settings-pet-create-minimum.png`, `settings-pet-create-minimum-scrolled.png` | 새 baseline은 현재 소스의 가로 5칸 행동 스트립과 일치하고, 겹침 없이 하단 만들기 버튼도 고정된다. 그러나 860×680 최소 화면에서는 이름/미리보기와 행동 스트립이 세로 스크롤로 분리되고, 각 행동 카드 안 아이콘형 조작이 조밀해 현재 작업 위치 비교가 어렵다(`settings-pet-create-minimum.png`, `settings-pet-create-minimum-scrolled.png`) | `CustomPetWindow.cs:28-45,63-68,137-166`; `PetManagementView.cs:8-35`; `Ui.cs:49-85` | P1 · VG-02 | 일반 설정 폭에서는 5개 행동 카드 비교가 가능하고, 860×680에서는 가로·세로 스크롤 중 어느 축도 버튼을 가리거나 이중 스크롤을 만들지 않는다. 이름/파일 동작의 오른쪽 경계는 트랙에서 8px 이상 떨어진다 | V2, V6 |
| 커스텀 펫 제작 | `custom-pet-editor.png`, `custom-pet-minimum.png`, `custom-pet-minimum-scrolled.png`, `custom-pet-preview.png` | 기본/최소 모두 겹침은 해소됐다. 기본 660×880도 모든 행동을 한 화면에 보여 주지 못하고, 520×620에서는 미리보기와 행동 편집이 분리되어 현재 진행 위치를 기억해야 한다. 고정 320px 이름, 220px 미리보기, 640px 행동 스트립은 토큰 확대에 취약하다 | `CustomPetWindow.cs:14-18,36-45,63-68,92-116,137-166,330-346`; `Ui.cs:49-85` | P1 · VG-02 | 660×880/590×750/520×620에서 이름·미리보기·5개 행동·고정 만들기 버튼이 모두 도달 가능하고, A안 Caption 13/18과 8·12·16 간격에서도 가로 넘침·컨트롤 겹침이 없다. 행동 스트립의 첫/마지막 카드가 스크롤로 완전히 보인다 | V2, V6 |
| 펫 팩 열기·설치·재설치·업데이트·오류 | `pack-preview.png`, `pack-installed.png`, `pack-reinstall.png`, `pack-update.png`, `pack-error.png` | 520×850에서 제목→미리보기→조작→상태→주 행동 계층은 안정적이고 상태별 버튼도 구분된다. 오류 화면도 잘림은 없다. 다만 5장 모두 경고 0개라 새 고정 경고 행의 밀도·두 줄 높이·설치 버튼과의 균형은 확인 불가하다. 선택 전 큰 빈 미리보기 표면은 열기 동작보다 시각 비중이 높다 | `PetPackWindow.cs:14-18,37-50,78-108,141-153`; `Ui.cs:68-92` | P1 · VG-03, P2 · VG-08 | 480×560 경고 0/1/3개, 설치/재설치/업데이트/오류 상태를 각각 캡처해 요약·“참고 사항 보기”·주 버튼이 푸터 안에서 겹치지 않는다. 경고 3개에서도 요약은 최대 2줄, 설치 버튼은 완전 노출, 본문 이동 후 상세가 보인다 | V2, V6 |
| 루틴 편집 | `routine-editor.png`, `additional-routine.png` | 전후 바이트 동일. 단계 카드와 고정 저장 영역의 위계·간격은 안정적이고 현재 잘림은 없다. 다만 500×700 비리사이즈 창과 125px 숫자 입력은 글자·컨트롤 토큰 확대 시 본문 스크롤량과 행 높이가 즉시 증가한다 | `RoutineEditorWindow.cs:10-31,32-52`; `Ui.cs:68-96` | P2 · VG-06 | 500×700에서 3단계, 180자 한글, 오류 2줄, 44px 버튼을 동시에 넣어도 하단 저장/취소가 고정되고 각 필드는 스크롤로 완전히 도달 가능하다 | V3, V6 |
| 업무 프로필 편집 | `profile-editor.png` | 새 baseline에서도 단일 카드와 고정 저장 영역은 정돈되어 있고 현재 잘림이 없다. 500×520 비리사이즈, 2열 숫자 필드(`*,12,*`)는 큰 글자·긴 레이블에서 가장 먼저 압축된다 | `ProfileEditorWindow.cs:10-24,27-47`; `Ui.cs:68-96` | P2 · VG-06 | 500×520에서 필드 레이블이 겹치거나 말줄임되지 않고, 오류 2줄이 나타나도 저장/취소가 창 안에 남는다. 필요하면 2열→1열 전환 기준을 둔다 | V3, V6 |
| 루틴·프로필 라이브러리 | `routine-library.png`, `work-profiles.png` | 독립 호환성 진단 창의 하단 그룹은 한 행으로 정돈되어 균형이 좋고, 목록 230px와 좌우 액션이 계층을 명확히 한다. 비래핑 좌우 행은 토큰 확대 시 충돌 위험이 크다. 직전 baseline에만 있던 `settings-routines-tab.png`는 **현재 제품 UI 아님**이며 회귀 기준에서 제외한다(`src/Unfold.Desktop/SmokeDiagnostics.cs:395-405`) | `PersonalizationWindow.cs:10-16,25-29,67-97`; 현재 일반 UI 제거 근거 `docs/settings-ui.md:49-50` | P1 · VG-04 | 진단 호환 창 600×580에서 관리 3개와 주 작업·닫기가 겹치지 않고 한 행 또는 의도된 2행으로 배치된다. 현재 일반 설정에는 루틴·프로필 탭이 다시 나타나지 않는다 | V3, V6 |
| 주간 기록 | `weekly-review.png`, `settings-review-tab.png` | 요약→7일 표→이동→설명→내보내기 계층은 유지되고, 새 `settings-review-tab.png`에는 중복 페이지 헤더가 없다. 예약 트랙은 카드와 분리돼 어색하지 않다. 560px 최소 폭의 `*,100,110` 고정 숫자 열과 620×650 기본 높이는 토큰 확대에 취약하다 | `BreakReviewWindow.cs:11-23,35-70,74-90`; 현재 탭 헤더 제거 검사 `SmokeDiagnostics.cs:392-410` | P1 · VG-07 | 현재 소스의 독립 560×600과 내장 860×680에서 날짜·횟수·시간 3열이 겹치지 않고, 긴 상태/경고 2줄 뒤에도 내보내기 버튼이 고정된다. 내장 탭에는 중복 페이지 헤더가 없다 | V1, V6 |
| 펫 말풍선 4방향 | `speech-top.png`, `speech-bottom.png`, `speech-left.png`, `speech-right.png` | 꼬리 방향, 펫과 말풍선 간격, 두 버튼 계층은 네 방향 모두 안정적이며 잘림이 없다. 그러나 모든 방향이 고정 320×268 말풍선과 320×472/524×268 캔버스를 사용해 타이포·간격 토큰 변경 여유가 거의 없다 | `PetSpeechBubble.cs:11-38,73-91`; `PetWindow.cs:129-150` | P1 · VG-05 | 네 방향에서 4줄 안내, 2개의 44px 버튼, 16px 본문을 넣어도 잘림·말줄임·꼬리 분리가 없고 작업 영역 안에서 고정된다 | V4, V6 |
| 펫 말풍선 상태·접힘 | `speech-five-minutes.png`, `speech-completed.png`, `speech-overtime.png`, `speech-folded.png`, `pet.png` | 5분 전/완료/초과의 제목과 상태 색은 명확하다. 다만 행동·타이머가 없는 `speech-completed.png`와 `speech-five-minutes.png`도 268px 고정 높이를 써 본문과 하단 힌트 사이에 과도한 빈 공간이 남는다. 접힘 배지는 보이지만 펫과 떨어진 작은 링이라 로딩 표식으로 읽힐 수 있고, 캡처는 대기 링만 포함해 진행 중 채운 점 비교가 없다 | `PetSpeechBubble.cs:20-38,40-69`; `PetWindow.cs:22-35,153-168`; 동적 높이 접점 `33_ux_flow_redesign.md:178-195` | P1 · VG-05 | 완료·5분 전은 내용 기반으로 초대/진행보다 짧아지고, 초대/진행은 버튼·타이머가 잘리지 않는다. 대기 링·진행 점·평시 없음 3장을 같은 배경/배율로 비교해 100%와 200% 모두 형태가 구분되고 얼굴·귀를 가리지 않는다 | V4, V6 |
| 확인·입력·오류 대화상자 | `dialog-confirm.png`, `dialog-prompt.png`, `dialog-error.png` | 전후 바이트 동일. 제목→메시지 카드/입력→구분선→동작 순서와 Danger/Quiet/Primary 역할은 안정적이며 잘림이 없다. 고정 460/420px 폭과 입력 280px은 긴 번역·큰 글자에서 위험하다 | `Ui.cs:108-126,128-166`; 공통 토큰 `DesignSystem.cs:41-73` | P2 · VG-06 | 200% 배율 또는 16px 본문에서 제목·2줄 메시지·버튼이 겹치지 않고, 확인 창은 취소·삭제가 한눈에 구분된다. 입력창은 최소 280px 또는 가용 폭 전체를 유지한다 | V1, V6 |
| 진단용 픽셀 에디터 | `editor.png` | 42장 완전성 확인을 위해 열람했다. 조밀한 영문 도구 UI지만 현재 MVP 일반 UI와 화면 재설계 범위가 아니며, 기존 감사도 제외했다(`08_ui_visual_audit.md:3-6`) | `src/Unfold.Desktop/EditorWindow.cs` 계열; 공통 스타일만 `DesignSystem.cs` | P2 · 유지 | 공통 토큰 변경 후에도 진단 도구의 캔버스·레이어·내보내기 버튼이 잘리지 않는지만 스모크로 확인하고 별도 재설계는 하지 않는다 | V6 |

직전 baseline에만 있던 `settings-routines-tab.png`와 `settings-speech-options.png`는 **현재 제품 UI 아님**으로 분류하고 회귀 기준에서 제외한다. 새 baseline의 파일 목록에는 없으며, 현재 진단은 루틴 진입점 부재를 검사하고 설정 탭에서 알림/타이머 카드를 직접 캡처한다(`src/Unfold.Desktop/SmokeDiagnostics.cs:395-405,476-491`).

## 변경 제안 목록

### 검증 명령 키

- **V1 · 공통/설정/대화상자**
  `dotnet test Unfold.slnx -c Release --no-restore --filter "FullyQualifiedName~DesignSystemTests|FullyQualifiedName~SettingsDashboardTests|FullyQualifiedName~SettingsUiTests"`
- **V2 · 펫 제작/팩**
  `dotnet test Unfold.slnx -c Release --no-restore --filter "FullyQualifiedName~CustomPetWindowTests|FullyQualifiedName~PetPackWindowTests"`
- **V3 · 편집기/라이브러리**
  `dotnet test Unfold.slnx -c Release --no-restore --filter "FullyQualifiedName~RoutineEditorTests|FullyQualifiedName~PersonalizationWindowTests"`
- **V4 · 말풍선/접힘**
  `dotnet test Unfold.slnx -c Release --no-restore --filter "FullyQualifiedName~PetReminderTests|FullyQualifiedName~BreakReminderTests|FullyQualifiedName~BreakSessionTests"`
- **V5 · 전체 자동 검사**
  `dotnet test Unfold.slnx -c Release --no-restore`
- **V6 · 새 격리 네이티브 스모크**
  `UNFOLD_DATA_DIR=/tmp/unfold-ui-redesign-<새-빈-디렉터리> dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test` (`docs/verification.md:69-90`)

### baseline 재현 절차

2차 구현 담당은 토큰 변경 전후에 아래 절차를 각각 실행한다. 진단 출력은 `$run_root/verification/`에 생기며, `smoke.json` 성공과 PNG 개수를 함께 보존한다(`src/Unfold.Desktop/SmokeDiagnostics.cs:14-20`; `docs/verification.md:69-90`).

```sh
dotnet build Unfold.slnx -c Release
run_root="$(mktemp -d /tmp/unfold-redesign-after.XXXXXX)"
UNFOLD_DATA_DIR="$run_root" dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
jq -e '.success == true and .imageFiles == 42' "$run_root/verification/smoke.json"
test "$(find "$run_root/verification" -maxdepth 1 -name '*.png' | wc -l | tr -d ' ')" = 42
md5 "$run_root/verification/settings-notification-options.png" "$run_root/verification/settings-timer-options.png"
```

마지막 MD5 비교는 “같아야 한다”는 수용 기준이 아니라 현재 진단의 중복 증거를 탐지하는 임시 가드다. VG-10 적용 뒤에는 콘텐츠가 넘칠 때 두 해시가 달라야 하고, 넘치지 않을 때는 중복 파일을 만들지 않아야 한다(`src/Unfold.Desktop/SmokeDiagnostics.cs:485-491`).

| ID | 제안 | 소유 파일 | 의존성 | 우선순위 | 수용 기준 | 검증 명령 |
|---|---|---|---|---|---|---|
| VG-01 | 설정의 300px 보조 열을 새 타이포/간격 토큰에 맞춰 반응형으로 조정한다. 카드 안 카드와 2열 필드는 최소 폭에서 한 열 전환 후보로 둔다 | `SettingsWindow.Layout.cs`, `SettingsWindow.cs` | 워커 A의 Title/Body/Caption, Space/Gap/Inset 토큰; 워커 C 정보 구조 | **P1** | 1120×800, 990×740, 860×680에서 펫·타이머 고정, 우측만 스크롤, 잘린 글자/버튼/입력 0, 마지막 기록 동작 도달 | V1, V6 |
| VG-02 | 펫 제작의 이름·미리보기·5행동 스트립을 토큰 확대에도 한 축씩만 스크롤하도록 조정한다 | `CustomPetWindow.cs`, `PetManagementView.cs`, 공통 `Ui.cs` | 워커 A 토큰; 현재 가로 640px 스트립 계약; 초안 보존 | **P1** | 660×880/590×750/520×620 및 설정 1120×800/860×680에서 5개 행동과 고정 만들기 버튼 도달, 이중 스크롤·겹침 0 | V2, V6 |
| VG-03 | 경고 0/1/3개를 포함한 펫 팩 상태 캡처를 추가하고 고정 경고 행의 최대 높이·줄바꿈을 명시한다 | `PetPackWindow.cs`, `Ui.cs`, `SmokeDiagnostics.cs` | 워커 A Caption/버튼 높이; 480×560 최소; `BringIntoView` 계약 | **P1** | 요약 최대 2줄, “참고 사항 보기”와 설치 버튼 완전 노출, 본문 이동 후 상세 경고 완전 노출, 경고 리셋 뒤 빈 푸터 공간 0 | V2, V6 |
| VG-04 | 라이브러리 좌우 동작 그룹에 폭 부족 시의 명시적 전환 규칙을 둔다. 현재 한 행을 우선하되 충돌 전에 의도된 2행으로 바꾼다 | `PersonalizationWindow.cs`, 필요 시 `Ui.cs` | 워커 A 버튼 패딩/글자 크기; 호환성 진단 창 600×580 | **P1** | 600×580에서 관리/주 작업이 겹치지 않고 역할 순서 유지. 일반 설정 UI에는 제거된 진입점이 복귀하지 않음 | V3, V6 |
| VG-05 | 고정 말풍선을 내용 기반 높이로 바꾸고 접힘 배지를 새 토큰에 맞춰 함께 재조정한다. 배지는 링/점의 의미가 펫 장식·로딩과 혼동되지 않게 크기·배경·부착 위치를 비교한다 | `PetSpeechBubble.cs`, `PetWindow.cs` | 워커 A Section 18/24·Caption 13/18·반경 토큰; 워커 C의 최소 높이 176/anchor 보존 사양(`33_ux_flow_redesign.md:178-195`); 워커 D 접근성 이름/대비 | **P1** | 4방향, 5분 전, 완료, 초과, 대기 링, 진행 점, 평시 상태에서 잘림 0. 완료/5분 전은 초대보다 짧고, 100/200%에서 링과 점 구분, 얼굴/귀 가림 0 | V4, V6 + 실제 OS |
| VG-06 | 비리사이즈 편집기와 고정 폭 대화상자를 큰 글자/긴 한글 스트레스 시나리오로 고정한다. 필요한 경우 2열 필드를 한 열로 전환한다 | `RoutineEditorWindow.cs`, `ProfileEditorWindow.cs`, `Ui.cs` | 워커 A 본문/버튼/Inset; 워커 D 키보드 포커스 | **P2** | 고정 창에서 본문은 스크롤 가능하고 하단 동작은 고정. 오류 2줄·긴 이름·긴 레이블에서도 잘림/겹침 0 | V1, V3, V6 |
| VG-07 | 주간 기록의 고정 숫자 열을 최소 폭·큰 글자에 맞춰 조정하고 내장/독립 화면의 중복 헤더 계약을 분리한다 | `BreakReviewWindow.cs`, `SettingsWindow.Layout.cs` | 워커 A 표 글자/간격; 현재 헤더 제거 계약 | **P2** | 560×600/860×680에서 날짜·횟수·시간 열 겹침 0, 스크롤바 거터 유지, 내장 탭 중복 헤더 0 | V1, V6 |
| VG-08 | 선택 전 빈 미리보기 표면의 시각 비중을 줄이거나 명시적 빈 상태 안내를 넣는다 | `PetPackWindow.cs`, `CustomPetWindow.cs` | 워커 C 빈 상태 문구/행동; 워커 A Surface/Raised 계층 | **P2** | 파일 선택 전에는 열기/파일 선택이 가장 강한 시각 요소이고, 선택 후에는 미리보기와 재생 조작이 우선한다. 빈 사각형이 오류나 로딩으로 오해되지 않는다 | V2, V6 |
| VG-09 | 알림/타이머 설정 카드의 직접 10~11px 레이블과 10px 세로 간격을 A 토큰으로 치환한다. 제품 사양에 비활성 적용 상태가 생길 때만 해당 상태 캡처를 추가하고, 현재처럼 항상 적용 가능하면 활성 상태로 명시한다 | `SettingsWindow.Layout.cs`, `SettingsWindow.Notifications.cs`, `DesignSystem.cs` | 워커 A LabelSmall 12, Caption 13/18, Gap 12/16, Disabled 토큰; VG-10 진단 상태 | **P1** | 860×680에서 두 카드의 읽기 순서·정렬·도달성을 유지한다. 비활성 상태가 존재하면 레이블이 읽히고 활성 Primary와 혼동되지 않으며, 2열 타이머 필드는 겹치지 않는다 | V1, V6 |
| VG-10 | 설정 옵션 진단이 실제 스크롤 상태를 정직하게 증명하도록 바꾼다. `Extent.Height <= Viewport.Height`면 중복 두 장 대신 `settings-options-overview.png` 한 장과 `settingsOptionsScrollRequired=false`를 기록한다. 넘치면 `ScrollToHome()`/`ScrollToEnd()` 후 상·하단을 캡처하고 양의 오프셋·서로 다른 해시·마지막 카드 노출을 단언한다. 항상 두 장이 필요하면 제품 최소 높이보다 작은 진단 전용 창을 쓰되 파일명에 `diagnostic-stress-top/bottom`을 명시한다 | `SmokeDiagnostics.cs` | 워커 A T01 적용 전·후 동일 분기; T01의 10~11px→12px와 행간/간격 증가가 `Extent > Viewport`로 분기를 바꿀 수 있음(`31_design_system_redesign.md:134-150`) | **P1** | 캡처 파일명이 실제 상태와 일치한다. 스크롤 필요 시 오프셋이 0→양수이고 두 PNG가 다르며 타이머 카드가 완전히 보인다. 불필요 시 중복 이미지로 도달성을 주장하지 않는다 | V1, V5, V6 |

## 토큰 재설계 적용 시 회귀 위험

| 위험 | 근거 | 영향 화면 | 방어 기준 | 우선순위 |
|---|---|---|---|---|
| **baseline 재현성·진단 의미** | 새 baseline으로 소스 불일치는 해소됐지만 설정 옵션 두 PNG가 같은 MD5다. 두 카드가 860×680에 함께 들어 `BringIntoView()` 두 번이 모두 no-op이기 때문이다(`settings-notification-options.png`, `settings-timer-options.png`; `SmokeDiagnostics.cs:456-491`) | 설정 알림/타이머 옵션, T01 전후 비교 | 위 격리 명령으로 before/after를 생성한다. T01 후 `Extent > Viewport`면 상·하단 오프셋과 서로 다른 PNG를 확인하고, 아니면 overview 한 장과 `scrollRequired=false`로 기록한다. 구 `settings-routines-tab.png`·`settings-speech-options.png`는 비교 목록에서 제외 | P1 |
| **설정 고정 골격** | 창 860×680, 레일 64, 간격 16, 우측 300 고정(`SettingsWindow.cs:30-37`; `SettingsWindow.Layout.cs:35-43`) | 기본/최소/스크롤/설정 탭 | A안 LabelSmall 12, Caption 13/18, Section 18/24에서 가로 넘침 0; 300px 열을 유지할 수 없으면 breakpoint 명시. 16px 본문/44px 컨트롤은 후속 접근성 스트레스 | P1 |
| **펫 팩 최소 폭 + 고정 조작 폭** | 480×560, 클립 260, 배경/크기 각각 최소 130, 미리보기 최대 384(`PetPackWindow.cs:14-18,37-41,83-101`) | 열기/설치/재설치/업데이트/오류/경고 | 480×560에서 384px 미리보기 상태까지 가로 넘침 0; 경고 푸터 추가 후 설치 버튼 노출 | P1 |
| **커스텀 펫 고정 폭** | 이름 320, 행동 스트립 최소 640, 미리보기 220/최소높이 230, 창 520×620(`CustomPetWindow.cs:14-18,36-45,63-68,137-166`) | 설정 만들기 탭, 독립 제작 | 세로 본문과 가로 행동 스트립의 스크롤바가 컨트롤을 가리지 않고 마지막 카드 도달 | P1 |
| **라이브러리 비래핑 액션** | 600×580 최소, `Grid("Auto,*,Auto")` 안 두 `Ui.Row`(`PersonalizationWindow.cs:10-16,67-97`) | 루틴·프로필 라이브러리 | 관리/주 작업 그룹의 필요 폭 합이 가용 폭을 넘기 전에 breakpoint로 2행 전환 | P1 |
| **말풍선·펫 캔버스 고정 크기** | 말풍선 320×268, 캔버스 320×472/524×268, 펫 192, 배지 22/표식 10(`PetSpeechBubble.cs:13-37,73-91`; `PetWindow.cs:25-41`) | 4방향, 5분, 완료, 초과, 접힘 | 장문·큰 글자에서 말줄임 없이 수용하거나 크기 계산을 토큰에 연동; 실제 작업 영역 clamp 재검증 | P1 |
| **기록 고정 열** | `*,100,110`, 창 최소 560×600(`BreakReviewWindow.cs:19-20,35-38`) | 주간 기록/내장 기록 탭 | 날짜/횟수/시간의 최소 폭을 측정해 열 충돌 0, 좁을 때 합리적 축약 또는 단일 행 전환 | P2 |
| **비리사이즈 편집기** | 루틴 500×700, 프로필 500×520, 둘 다 `CanResize=false`(`RoutineEditorWindow.cs:14-15`; `ProfileEditorWindow.cs:14-15`) | 루틴 편집, 업무 프로필 | 큰 글자·오류 메시지에서 본문 스크롤과 고정 푸터가 공존하고 OS 제목 표시줄 포함 실제 창에서도 하단 잘림 0 | P2 |
| **고정 폭 대화상자** | 확인 460, 입력 420/입력 최소 280(`Ui.cs:108-124,157-165`) | 확인·입력·오류 | 긴 한글 제목/메시지·200% 배율에서 수평 잘림 0, 버튼은 의미 순서 유지 | P2 |

## 자동 확인과 실제 OS 미검증 구분

### 이번 보고서에서 확인한 것

- 새 baseline의 PNG 42장을 모두 열어 화면별로 확인했다. 목록은 위 표에서 42장 전부 소진한다.
- 직전 baseline과 공통인 40장 중 11장은 바이트 동일, 29장은 변경됐다. 새로 생긴 파일은 `settings-notification-options.png`·`settings-timer-options.png`, 제거된 파일은 `settings-routines-tab.png`·`settings-speech-options.png`다.
- `settings-pet-create-minimum*.png`와 `custom-pet-minimum*.png`에서 트랙-컨트롤 분리를, `routine-library.png`·`work-profiles.png`에서 하단 한 행을, 새 `settings.png`에서 제거된 중첩 알림 그룹과 현재 대시보드 구성을 확인했다.
- 새 `smoke.json`은 `success: true`, `imageFiles: 42`, `Unix 26.6.2`, `.NET 10.0.12`를 보고하며 제공된 Release 빌드 결과는 경고 0이다. 이는 화면 밖 네이티브 렌더러와 프로그램 방식 입력 결과이지 사람의 실제 조작 증거가 아니다(`docs/verification.md:69-90`).
- 두 설정 옵션 PNG의 MD5가 `4831c841fd9f3d8e1663a92c714db55a`로 같음을 자동 비교했고, 현재 소스의 연속 `BringIntoView()`와 일치함을 정적으로 확인했다(`SmokeDiagnostics.cs:485-491`).
- 이번 후속 워커는 빌드·테스트·스모크를 다시 실행하지 않았다. 제공된 새 baseline의 전수 시각 감사, 파일 비교, 현재 소스 정적 대조만 수행했다.

### 미검증

- 비활성 `적용` 버튼의 실제 가독성. 두 설정 옵션 캡처의 적용 버튼은 활성 Primary이고 현재 카드 생성 코드에도 비활성 조건이 없으므로, 비활성 적용은 제품 상태가 아니라 공통 스타일 차원의 미검증 항목이다(`SettingsWindow.Notifications.cs:22-34`; `SettingsWindow.Layout.cs:105-110`; `DesignSystem.cs:67-69`).
- 설정 옵션이 T01 적용 후 실제로 세로 스크롤을 요구하는지, 요구할 때 타이머 카드 끝까지 도달하는지. 현재 중복 두 장은 이 사실을 증명하지 않는다(`SmokeDiagnostics.cs:485-491`; `31_design_system_redesign.md:134-150`).
- 경고가 있는 실제 펫 팩의 고정 경고 요약, “참고 사항 보기” 전후 화면, 480×560 최소 창. 지정 `pack-*.png`에는 경고가 없다.
- 실제 비진단 펫 팩 아트의 192/288/384px 품질. 지정 팩 미리보기는 주황 윤곽 진단 고정물이다(`pack-preview.png`, `pack-installed.png`, `pack-reinstall.png`, `pack-update.png`).
- 대화상자/설정/라이브러리의 마우스·키보드 포커스 링, 탭 순서, VoiceOver, Narrator.
- macOS 실제 제목 표시줄·메뉴바·Retina, Windows 100~200% DPI, 다중 모니터, 펫 창 클릭 통과와 작업 영역 clamp.
- 접힌 알림의 진행 중 채운 점 비교, 실제 배경 위 체감 크기·대비, 트레이에서 복구하는 사용자 흐름.
- 장문 현지화, OS 글꼴 대체, 200% 텍스트 확대에서의 재배치. 정적 1x PNG만으로 판단하지 않는다.

## 접점

- **워커 A · 디자인 시스템:** 본 보고서의 VG-01~VG-10은 토큰 값을 다시 정하지 않고 A의 `LabelSmall 12`, `Caption 13/18`, `Section 18/24`, 8·12·16·20 간격과 10/16/20/24·28 반경을 소비한다(`31_design_system_redesign.md:134-158`). 특히 `Space2=8`을 유지하는 A안과 맞춰 `ScrollGutter` 8px를 보존한다(`31_design_system_redesign.md:147-150`; `Ui.cs:49-66`). T01이 설정의 직접 10~11px 값을 12px로 올리면 현재 한 뷰포트에 들어가는 알림·타이머 카드가 스크롤 임계점을 넘을 수 있으므로, VG-10의 `Extent/Viewport` 분기와 함께 적용한다(`settings-notification-options.png`, `settings-timer-options.png`; `SettingsWindow.Layout.cs:105-123`; `SettingsWindow.Notifications.cs:71-75`). A의 Success/Disabled/표면 분리는 `pack-installed.png`, `settings.png`, `weekly-review.png`를 재캡처해 계층을 확인한다(`31_design_system_redesign.md:162-173`).
- **워커 C · UX 흐름:** VG-08 빈 미리보기, 펫 제작의 작업 위치 인지, 접힌 배지의 복구 가능성은 정보 구조·빈 상태·다음 행동 설계와 겹친다. 특히 C의 말풍선 고정 높이 제거·최소 높이 176·anchor 보존 사양을 VG-05의 시각 수용 기준으로 채택한다(`33_ux_flow_redesign.md:178-195`). 문구와 흐름 자체는 이 보고서에서 확정하지 않는다.
- **워커 D · 접근성:** 배지 링/점의 비색상 구분, 버튼 44px 스트레스 기준, 대화상자 포커스, 큰 글자/스크린리더는 접근성 구현 사양과 겹친다. D 보고서는 작성 시 A 보고서가 없었다고 기록했으므로(`34_a11y_implementation_spec.md:18-20,355-362`), 구현 전에 A의 `FocusRing #8FD3FF/#005FCC`, 2px/offset 2와 이름을 단일 출처로 맞춘다(`31_design_system_redesign.md:158,200,244-249`). 정적 PNG의 대비 인상으로 통과를 선언하지 않는다.
- **현재 제품 범위:** 일반 설정에서 루틴·업무 프로필 진입점은 제거됐다(`docs/settings-ui.md:49-50`). `routine-library.png`, `work-profiles.png`, 편집기 캡처는 저장 데이터 호환성 진단 화면으로만 유지하고 제품 정보 구조에 다시 넣는 제안으로 해석하지 않는다.
