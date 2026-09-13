> **보관 문서 · Garden 실험 — experiment/garden-windows / 3098f20**
> 현재 MVP의 작업 지침이 아닙니다. 원문의 명령, 담당 배정, 경로와 검증 결과는 당시 기록입니다.
> 원래 위치: `docs/team/reports/B-01-presentation.md` · [현재 문서 안내](../../../../README.md)

# B-01 — 사용자 경험과 자산 계약 점검

```text
작업 ID / 담당: B-01 / B — UI·자산
OS: macOS (Darwin 25.6.0, arm64) — Windows 실기 없음
브랜치: experiment/garden-windows
기준 커밋: 3098f2057bca28f7024aa2a9413556111b3dea09
시작 시 변경 상태: `?? docs/team/` (미추적 디렉터리) 외 없음
검증 성격: 전부 `정적 확인`. 빌드·테스트·앱 실행은 하지 않았다(D 전담).
          화면을 띄우지 않았으므로 시각 검증 항목은 모두 `미검증`이다.
```

기준 확인 명령과 출력:

```
$ git branch --show-current   → experiment/garden-windows
$ git rev-parse HEAD          → 3098f2057bca28f7024aa2a9413556111b3dea09
$ git status --short          → ?? docs/team/
```

자산 실측은 앱을 실행하지 않고 파이썬 표준 라이브러리로 PNG를 직접 디코딩해
계산했다(스크래치패드의 일회용 스크립트, 저장소에 남기지 않음). 사용한 판정
기준은 코드와 동일하다: 알파 ≥ 26을 불투명으로 보고(`AnimationView.cs:70`),
히트 테스트 관용 반경은 `radius = ceil(2 * 384 / 192) = 4` 소스 픽셀
(`AnimationView.cs:67`).

---

## 1. 확인한 현재 동작

### 1.1 화면/위치 → 현재 표시 → 실제 동작 → 문제 여부

| # | 화면 / 위치 | 현재 표시 | 실제 동작 (코드 근거) | 문제 여부 |
|---|---|---|---|---|
| 1 | 설정 창 제목 | `Unfold` | 창 제목 (`SettingsWindow.cs:21`) | 없음 |
| 2 | 설정 헤더 | `UNFOLD` / `Make room for a small break.` | 정적 텍스트 (`SettingsWindow.cs:56`) | 없음 (break 용어 일관) |
| 3 | 설정 카운트다운 블록 | `NEXT BREAK` + `mm:ss` | `Clock.Remaining` 1 Hz 갱신 (`SettingsWindow.cs:58,75`) | 없음 |
| 4 | 설정 상태 줄 | `Paused by you` / `Paused while you're away` / `Counting active time` / 활동 오류 메시지 | `Clock.Paused`, `Clock.IdlePaused`, `ActivityError` (`SettingsWindow.cs:76`) | 낮음 — 활동 API 오류가 원문 예외 메시지 그대로 사용자에게 노출된다 |
| 5 | 설정 `Pause` 버튼 | `Pause` ↔ `Resume` | `runtime.TogglePause` (`SettingsWindow.cs:25,77`) | 없음 |
| 6 | 설정 `Reset` 버튼 | `Reset` | `Clock.Reset` (`AppRuntime.cs:106`) | 없음 |
| 7 | 설정 `Break now` 버튼 | `Break now` | `AppRuntime.ShowReminder()` (`SettingsWindow.cs:58`, `AppRuntime.cs:186`) | **중간** — 휴식을 해도 카운트다운이 리셋되지 않는다(§2.5) |
| 8 | 설정 간격 입력 | `Remind me every (min)` 5–240 | `IntervalMinutes` (`SettingsWindow.cs:23,59`) | 없음 |
| 9 | 설정 유휴 입력 | `Pause when away (min)` 1–60 | `IdleMinutes` (`SettingsWindow.cs:24,59`) | 없음 |
| 10 | 설정 적용 버튼 | `Apply reminder settings` | 두 숫자만 저장. 체크박스·캐릭터는 즉시 저장이므로 이 버튼과 무관 (`SettingsWindow.cs:26-30`) | 낮음 — "설정 적용"이 일부만 적용한다는 사실이 화면에 없다 |
| 11 | 설정 캐릭터 구역 | `YOUR COMPANION` | 캐릭터 선택 + 120×120 미리보기 (`SettingsWindow.cs:60`) | **중간** — 같은 대상이 `companion` / `pet` / `character` 세 단어로 불린다(§2.1) |
| 12 | 설정 캐릭터 미리보기 | 선택 캐릭터의 `idle` 루프 | `runtime.Clip("idle")` — 시각(時刻)과 무관하게 항상 낮 프레임 (`SettingsWindow.cs:85`) | **중간** — 밤에 데스크톱 식물은 밤 팔레트, 설정 미리보기는 낮 팔레트(§2.3) |
| 13 | 설정 체크박스 | `Show desktop pet` | `Settings.ShowPet` (`SettingsWindow.cs:31`) | 중간 — 용어(`pet`) 혼재 |
| 14 | 설정 체크박스 | `Launch at login` | `PlatformServices.SetStartAtLogin` (`SettingsWindow.cs:38-45`) | 없음 |
| 15 | 설정 하단 | `Closing this window keeps Unfold in the tray.` + `Quit` | `Closing`을 취소하고 숨김 (`SettingsWindow.cs:61,63`) | 없음 |
| 16 | 설정 `Edit` / `Delete` 버튼 | **표시되지 않음** | 생성되지만 레이아웃에 넣지 않음. `Refresh()`는 계속 상태를 갱신 (`SettingsWindow.cs:52-55,81`) | 낮음 — 의도된 MVP 숨김(주석에 명시). 사용자 영향 없음 |
| 17 | 트레이 아이콘 툴팁 | `Unfold · mm:ss[ · paused/ · away]` | 1 Hz 갱신 (`AppRuntime.cs:99`) | 없음 |
| 18 | 트레이 메뉴 1행 | `Next break: mm:ss` (비활성) | 상태 표시용 (`AppRuntime.cs:100,217`) | 없음 |
| 19 | 트레이 메뉴 | `Settings` | 설정 창 (`AppRuntime.cs:219`) | 없음 |
| 20 | 트레이 메뉴 | `Hide Pet` ↔ `Show Pet` | `ShowPet` 토글. **헤더는 1 Hz Tick에서만 갱신**되므로 클릭 직후 최대 1초간 이전 문구 (`AppRuntime.cs:102,220-221`) | 낮음 |
| 21 | 트레이 메뉴 | `Pause` ↔ `Resume` | `TogglePause` (`AppRuntime.cs:101,222`) | 없음 |
| 22 | 트레이 메뉴 | `Reset timer` | `Reset` (`AppRuntime.cs:223`) | 없음 |
| 23 | 트레이 메뉴 | `Break now` | `ShowReminder()` (`AppRuntime.cs:223`) | #7과 동일 |
| 24 | 트레이 메뉴 | `Quit Unfold` | `Quit()` (`AppRuntime.cs:224`) | 없음 |
| 25 | 트레이 메뉴 | 에디터 항목 **없음** | `BuildTray`에 에디터 항목이 없다 (`AppRuntime.cs:208-227`) | 문서 불일치(§2.2 #5) |
| 26 | 식물 우클릭 메뉴 | `Settings` / `Break now` | 두 항목뿐 (`PetWindow.cs:32-34`). 변수명만 `stretch` | 없음 (내부 이름은 대상 외) |
| 27 | 앱 내 휴식 창 제목 | `Time for a break · Unfold` | `AppRuntime.cs:195` | 없음 |
| 28 | 앱 내 휴식 창 본문 | `A little room to breathe.` / (애니메이션) / `Look away for a moment.` / `I'm back` | `AppRuntime.cs:197-198` | 없음 (용어 일관) |
| 29 | 앱 내 휴식 창 애니메이션 | 캐릭터의 `stretch` 클립 | sprout 1.000초 재생 후 **마지막 프레임에서 정지**. `loop:false`를 매니페스트에서 읽음 (`AppRuntime.cs:194`, `character.json` stretch) | 낮음 — 창이 열려 있는 대부분의 시간 동안 정지 이미지(§2.6) |
| 30 | 데스크톱 식물 (자동/수동 휴식 시) | `stretch` 1회 재생 후 idle 복귀 | `AppRuntime.cs:206` → `PetWindow.React("stretch")` → `PetWindow.cs:104` | **중간** — 밤에도 낮 팔레트로 재생(§2.4) |
| 31 | 데스크톱 식물 클릭 | sprout: `click` 1회(333 ms) / Mochi: 무반응 | `PetWindow.cs:94` — `click` 키가 없으면 그대로 idle | **중간** — Mochi 사용자에게는 클릭이 완전 무응답이며 이를 설명하는 문구가 UI 어디에도 없다(§2.7) |
| 32 | 데스크톱 식물 드래그 | 불투명 픽셀에서만 시작 | `PetWindow.cs:37` + `AnimationView.OpaqueAt` | **높음** — 잡을 수 있는 면적이 창의 17%뿐(§3.3) |
| 33 | Windows OS 알림 | 제목 `Time for a break`, 본문 `Look away for a moment.`, 툴팁 `Unfold` | `NativeReminder.cs:30` | 없음 (앱 내 문구와 일치) |
| 34 | macOS OS 알림 | 제목 `Unfold`, 본문 `Look away for a moment.` | `NativeReminder.cs:41` | 낮음 — Windows와 제목이 다르다(§2.1 #4) |
| 35 | 배포 zip 안의 `README.md` | `Tray | Settings, editor, timer controls, **stretch now**, quit …` | `docs/cross-platform.md:59`를 `publish-desktop.ps1:22`가 `README.md`로 복사해 배포 | **높음** — 사용자에게 배포되는 문서에 `stretch now`가 남아 있고, 존재하지 않는 `editor` 항목도 안내한다(§2.2) |
| 36 | 저장소 랜딩 페이지 | `stretches with you`, `stretches when you click it`, `Stretch reminders` | `README.md:6,34,35` | **높음** — 사용자 눈에 보이는 최상위 문서(§2.2) |

### 1.2 sprout 자산 계약 (실측 대조)

| 항목 | 매니페스트 / 코드 기대 | PNG 실측 | 일치 |
|---|---|---|---|
| 시트 파일 | `spritesheet.png` | 존재, 95,890 바이트 | O |
| 시트 크기 | `columns 8 × frameWidth 384` = 3072, `rows 4 × frameHeight 384` = 1536 | 3072 × 1536, 8bit RGBA(color type 6), 비인터레이스 | O — `CharacterLibrary.cs:80`의 치수 검사 통과 |
| 그리드 한도 | columns/rows ≤ 256, 프레임 ≤ 2048, 열·행 총합 ≤ 4096 (`CharacterLibrary.cs:75-76`) | 3072 ≤ 4096, 1536 ≤ 4096 | O |
| `idle` | frames 0–7, fps 6, loop true | 8프레임 모두 그려짐 (row 0) | O — 총 1,333 ms 루프 |
| `idle-day` | frames 0–7, fps 6, loop true | `idle`과 **동일 프레임**(중복 정의) | O — 의도됨 (`generate-plant-placeholder.py:333-335`) |
| `idle-night` | frames 8–15, fps 6, loop true | 8프레임, 밤 팔레트 (화분 RGB 122,74,51) | O — 총 1,333 ms |
| `stretch` | frames 16–23, fps 8, loop false | 8프레임 | O — 총 1,000 ms, 마지막 프레임이 휴식 자세로 복귀 |
| `click` | frames 24–29, fps 18, loop false | 6프레임 | O — 총 333 ms. `React`의 하한 클램프 200 ms를 넘김 (`PetWindow.cs:101`) |
| 슬롯 30–31 | 미참조 | 완전 투명 | O — 낭비 1.2 MB(메모리), 디스크는 압축됨 |
| `renderStyle` | 없음 | — | `pixel`이 아니므로 `HighQuality` 보간 (`SettingsWindow.cs:86`, `PetWindow.cs:77`) |
| `thumbnailSymbol` | 없음 (Mochi는 `cat.fill`) | — | C# 경로에서는 미사용 |
| 코드가 부르는 키 | `idle`, `IdleKey`(=`idle-day`/`idle-night`), `stretch`, `click` (`AppRuntime.cs:24,192`, `SettingsWindow.cs:85`, `PetWindow.cs:71,94`) | 5개 모두 존재 | O — 계약 충족 |

### 1.3 불투명 픽셀 분포 실측 (알파 ≥ 26)

프레임 0 (idle-day, 정지 자세) 기준. 프레임 크기 384 × 384 = 147,456 px.

| 항목 | 소스 픽셀 | 프레임 대비 | 192 DIP 창 환산 |
|---|---|---|---|
| 불투명 픽셀 총합 | 20,398 | **13.83 %** | — |
| 화분 (y ≥ 236) | 15,752 | 10.68 % | 불투명 픽셀의 **77.2 %** |
| 화분 위 (줄기+잎, y < 236) | 4,646 | 3.15 % | 불투명 픽셀의 **22.8 %** |
| 내용 경계 상자 | (103,99)–(280,343) = 178 × 245 | 폭 46 %, 높이 64 % | **89 × 123 DIP** |
| 히트 테스트 실효 면적 (±4 px 팽창 후) | 25,042 | **16.98 %** | 창 192 × 192 중 약 158 × 158 DIP²에 해당하는 면적 |
| └ 그중 화분 | 17,706 | — | 잡을 수 있는 면적의 **70.7 %** |
| └ 그중 줄기+잎 | 7,336 | — | 잡을 수 있는 면적의 **29.3 %** |

줄기 굵기(수평 방향 불투명 구간):

| 소스 y | 불투명 폭 | 팽창 후 폭 | 192 창 환산 |
|---|---|---|---|
| 190 | 17 px | 38 px | 19.0 DIP |
| 192 (최소) | **8 px** | 18 px | **9.0 DIP** |
| 200 / 210 / 220 / 230 / 236 | 10 px | 18 px | 9.0 DIP |
| 240–260 (화분 상단) | 174–178 px | — | 87–89 DIP |

전 프레임 불투명률 범위: 13.80 % (idle-day f2) ~ 14.81 % (stretch 최대 개방 f20).
화분 부분은 전 프레임에서 15,752–15,753 px로 사실상 불변 — 화분은 애니메이션되지 않는다.

Mochi 비교 (동일 기준, 시트 3072 × 1152, 프레임 0):

| | Sprout | Mochi |
|---|---|---|
| 불투명 픽셀 | 20,398 (13.83 %) | **54,782 (37.15 %)** |
| 경계 상자 | 178 × 245 → 89 × 123 DIP | 291 × 296 → **145 × 148 DIP** |
| 잡을 수 있는 면적 배율 | 1.0 | **약 2.7배** |

---

## 2. 발견 사항

심각도 → 관찰 사실 → 파일:줄 → 재현 여부 → 영향 순.

### 2.1 [중간] 같은 대상을 부르는 사용자 문구가 네 갈래다 — `pet` / `companion` / `character` / (알림 제목)

관찰 사실:

1. 설정 구역 제목은 `YOUR COMPANION` (`SettingsWindow.cs:60`).
2. 바로 아래 체크박스는 `Show desktop pet` (`SettingsWindow.cs:31`).
3. 트레이 항목은 `Hide Pet` / `Show Pet` (`AppRuntime.cs:102,220`).
4. 삭제 확인 대화상자는 `Delete character?` / `Delete {name} from your library?`
   (`AppRuntime.cs:182`), 편집 오류는 `Create a new character to edit your own pixel art.`
   (`AppRuntime.cs:159`).
5. OS 알림 제목이 Windows는 `Time for a break`, macOS는 `Unfold`
   (`NativeReminder.cs:30` vs `NativeReminder.cs:41`).

재현: 정적 확인 (문자열 리터럴 대조).
영향: Garden 프로토타입에서 대상은 식물이므로 `pet`은 Cat 시절 어휘다. 사용자가
설정 창 한 화면 안에서 `companion`과 `pet`을 동시에 본다. 기능 결함은 아니지만
"내 식물"이라는 정체성이 성립하지 않는다.

### 2.2 [높음] `stretch`가 **사용자에게 배포되는 문서**에 남아 있다 — 앱 UI에는 남아 있지 않다

먼저 확실히 해둔다: **앱이 실제로 그리는 문자열 중 `stretch`/스트레칭은 하나도
없다.** 설정 창, 트레이 메뉴, 식물 우클릭 메뉴, 앱 내 휴식 창, Windows·macOS OS
알림을 전부 훑었고 모두 `break` 계열로 통일돼 있다(표 §1.1 #2,3,7,18,23,26,27,28,33,34).
남아 있는 `StretchClock`, `stretch` 클립 키, `ShowReminder`, `PetWindow`의 `var stretch`
변수는 전부 내부 이름이라 대상 밖이다.

새는 곳은 문서다. 심각도 순:

| 위치 | 현재 문구 | 왜 사용자에게 보이나 |
|---|---|---|
| `docs/cross-platform.md:59` | `Tray \| Settings, editor, timer controls, **stretch now**, quit …` | `Scripts/publish-desktop.ps1:22`가 이 파일을 배포 폴더의 `README.md`로 복사한다. Windows zip을 푼 사용자가 첫 번째로 여는 문서다 |
| `README.md:6` | `an animated desktop pet that **stretches** with you` | GitHub 저장소 첫 화면 |
| `README.md:34` | `Draggable desktop companion that idles on your desktop and **stretches when you click it**.` | 위와 같음. 게다가 **동작 설명 자체가 틀렸다** — 클릭은 `click` 클립(잎 흠칫)을 재생하고 `stretch`는 휴식 전용이다(`PetWindow.cs:94`). Mochi는 클릭에 아무 반응도 없다 |
| `README.md:35` | `**Stretch reminders** with an animated character` | 위와 같음 |
| `docs/mvp.md:16-19,38-44,53-57` | `stretch timer`, `**Stretch now**`, `dismissed with **I'm refreshed**` | 저장소 문서. 실제 버튼은 `Break now`, `I'm back` |
| `docs/windows-dogfooding-log.md:99,162,169-172` | `Settings / **Stretch now**`, `### H. Stretch`, `**I'm refreshed** closes the reminder` | 검증자가 화면에서 찾을 수 없는 라벨을 체크하게 된다 |

추가로 같은 배포 문서의 사실 오류 두 건:

- `docs/cross-platform.md:59`의 트레이 목록에 `editor`가 있으나 `BuildTray`
  (`AppRuntime.cs:208-227`)에는 에디터 항목이 없다. MVP에서 의도적으로 숨긴 것이
  배포 문서에는 반영되지 않았다.
- `docs/mvp.md:52-57`의 `Stretch reminder | … dismissed with **I'm refreshed**`는
  실제 버튼 `I'm back`(`AppRuntime.cs:198`)과 다르다.

재현: 정적 확인.
영향: 배포본 README는 최종 사용자용이다. 여기서 "stretch now"를 찾은 사용자는
트레이에서 그 항목을 찾지 못한다. 문구 통일 커밋이 코드만 바꾸고 배포 문서를
빠뜨렸다.

**주의: 이 문서들은 총괄 조정 대상이거나 다른 담당 소유다. B는 수정하지 않았다.**

### 2.3 [중간] 설정 미리보기는 항상 낮, 데스크톱 식물은 밤 — 같은 순간에 다른 그림

관찰 사실: `SettingsWindow.Refresh()`는 `runtime.Clip("idle")`을 부른다
(`SettingsWindow.cs:85`). `PetWindow.SetCharacter()`는 `runtime.IdleKey`
(=`idle-day`/`idle-night`)를 부른다(`PetWindow.cs:71`). 매니페스트에서 `idle`은
낮 프레임 0–7의 별칭이다. 18:00–06:00 사이에는 데스크톱의 식물이 밤 팔레트,
설정 창의 미리보기가 낮 팔레트로 동시에 보인다.

추가로 미리보기는 `previewCharacter != selected`일 때만 다시 로드하므로
(`SettingsWindow.cs:82`), 설정 창을 열어 둔 채로 18:00을 넘겨도 미리보기는
절대 갱신되지 않는다.

재현: 정적 확인 — 두 호출 경로 대조. 실제 화면 대조는 미검증(§5).
영향: "이게 지금 내 식물이 맞나"라는 의심을 만든다. 낮/밤 기능 자체의 가치가
설정 창에서 부정된다. `generate-plant-placeholder.py:333-335`의 주석은 `idle`을
"낮/밤을 모르는 소비자용 안전망"으로 남긴다고 적었는데, 설정 미리보기가 바로
그 "낮/밤을 모르는 소비자"로 남아 있다.

### 2.4 [중간] 밤에 휴식/클릭이 낮 팔레트로 번쩍인다

관찰 사실: 픽셀 샘플링으로 확인했다. 좌표 (192,300)(화분 몸통)의 RGBA는

- `idle-day` f0 → (192,114,74,255)
- `idle-night` f8 → (122,74,51,255)
- `stretch` f16 → (192,114,74,255) — 낮과 동일
- `click` f24 → (192,114,74,255) — 낮과 동일

즉 시트의 row 2(stretch)와 row 3(click)은 DAY 팔레트로만 그려져 있다.
생성기 자신이 이를 명시한다: `Scripts/generate-plant-placeholder.py:27-29`
("Known placeholder limit: stretch and click are drawn in the day palette only,
so playing either at night flashes warm for its duration. Deliberate for now").

재현: 정적 확인 (픽셀 샘플).
영향: 밤에 휴식이 발동하면 데스크톱 식물이 1.000초 동안, 클릭하면 0.333초 동안
낮 색으로 번쩍였다가 밤 색으로 돌아간다. 어두운 화면에서 가장 눈에 띄는 순간에
색이 튄다. 플레이스홀더의 알려진 한계이므로 결함이라기보다 **다음 자산 작업의
1순위**로 기록한다. 코드 수정 없이 시트만 다시 생성하면 해결된다(row 2·3을
낮/밤 두 벌로 나누려면 매니페스트에 `stretch-night`/`click-night` 키가 필요하고,
그건 계약 변경이므로 제안만 한다).

### 2.5 [중간] `Break now`로 쉬어도 카운트다운이 리셋되지 않는다

관찰 사실: `AppRuntime.ShowReminder()`(`AppRuntime.cs:186-207`)는 `Clock`을
건드리지 않는다. 자동 발동 경로만 `StretchClock.Tick`이 `Remaining = Interval`로
되돌린다(`StretchClock.cs:32`). 세 개의 수동 진입점(설정 `Break now`, 트레이
`Break now`, 식물 우클릭 `Break now`)은 모두 `ShowReminder()`만 부른다
(`SettingsWindow.cs:58`, `AppRuntime.cs:223`, `PetWindow.cs:33`).

재현: 정적 확인 — 호출 그래프 대조. 실제 타이머 관측은 D 몫.
영향: 59분째에 `Break now`로 쉬고 자리에 돌아오면 1분 뒤 자동 휴식이 또 뜬다.
사용자는 방금 쉬었는데 앱이 그걸 모른다. 설정 창에서 `Break now`가 `Reset`
바로 옆에 있어(`SettingsWindow.cs:58`) "쉬면 타이머도 리셋된다"는 기대가 더 강해진다.
`AppRuntime.cs`는 A 소유이므로 §4에 요청으로 남긴다.

### 2.6 [낮음] 휴식 창은 1초 뒤 정지 이미지가 된다

관찰 사실: `stretch`는 8프레임 × (1/8초) = 정확히 1.000초, `loop:false`. 휴식 창은
매니페스트의 loop 값을 그대로 쓴다(`AppRuntime.cs:194`). `AnimationView.Advance`는
`!loop && ms >= totalMs`에서 마지막 프레임에 멈춘다(`AnimationView.cs:49`). 창은
사용자가 `I'm back`을 누를 때까지 닫히지 않는다(`AppRuntime.cs:198`).
Mochi도 `stretch.gif`가 `loop:false`라 동일하다.

재현: 정적 확인.
영향: "Look away for a moment"를 읽고 시선을 돌렸다 돌아오면 화면에는 멈춘 그림이
있다. 휴식이 진행 중이라는 신호가 없다. 설계 의도(1회 재생)와 창 수명(무기한)이
어긋난 것으로, 자산이 아니라 재생 정책의 문제다.

### 2.7 [중간] Mochi 프로필에서는 클릭이 완전 무응답이고, 그 사실이 UI 어디에도 없다

관찰 사실:

- `default-cat/character.json`의 클립은 `idle`(프레임 0–7)과 `stretch`(GIF) 둘뿐이다.
  `click`, `idle-day`, `idle-night` 없음.
- `PetWindow.React()`는 `click` 키가 없으면 `return`한다(`PetWindow.cs:94`).
  주석도 "The cat has none, so it stays idle"라고 적혀 있다.
- 낮/밤도 `LoadAnimation`의 `idle` 폴백으로 흡수된다(`CharacterLibrary.cs:27`),
  즉 Mochi는 하루 종일 같은 그림이다.
- `README.md:34`는 반대로 "**stretches when you click it**"이라고 광고한다.

재현: 정적 확인.
영향: Mochi를 쓰던 기존 사용자는 (a) 클릭해도 아무 일이 없고, (b) 낮/밤 변화가
없으며, (c) README는 클릭하면 반응한다고 말한다. 앱은 이 차이를 설명하지 않는다 —
캐릭터 선택 콤보박스는 이름만 보여준다(`SettingsWindow.cs:13`, `CharacterLibrary.cs:19`).
`AppSettings.cs:9-11`의 주석대로 기존 `settings.json`은 Mochi를 유지하므로,
업그레이드한 사용자는 **정원 프로토타입의 신규 기능을 하나도 못 본 채** 문구만
바뀐 앱을 쓰게 된다. 검증 체크리스트도 이 점을 질문으로 남겨 두었다
(`docs/windows-dogfooding-log.md:236` — "Mochi has no `click` clip, so a plain click
does nothing. Calm, or dead?"). 아직 답이 비어 있다.

### 2.8 [높음] 잡을 수 있는 면적이 창의 17 %, 그중 71 %가 화분이다

관찰 사실(§1.3 실측):

- 192 × 192 DIP 창에서 알파 ≥ 26인 픽셀은 프레임의 13.83 %.
- `OpaqueAt`의 ±4 소스 픽셀 관용을 적용해도 16.98 %.
- 그 실효 면적의 **70.7 %가 화분**(y ≥ 236), 29.3 %만이 줄기와 잎.
- 줄기의 가장 얇은 지점은 소스 8 px = 창에서 **4.0 DIP**. 관용 반경을 더해도
  **9.0 DIP**.
- 남은 83 %는 완전 투명이다.

`Scripts/generate-plant-placeholder.py:9`는 줄기를 "about 5 px of a 384 px frame"로
설명하지만, 이는 캡슐의 **반지름**(`generate-plant-placeholder.py:217`의 `radius = 5.0 - 2.6*t`)
이고 실측 불투명 **폭**은 8–10 px다. 문서가 실제의 절반을 적고 있다.

재현: 정적 확인 (PNG 픽셀 계산). 실제 마우스로 잡히는지는 미검증(§5).
영향과 해석 — 마지막 커밋의 질문("사용자가 실제로 무엇을 잡을 수 있는가")에 대한
**수치상의 답**:

1. 화분이 사실상 유일한 드래그 핸들이다. 71 % 대 29 %는 "잡을 수 있다/없다"가 아니라
   "화분 말고는 잡을 이유가 없다"에 가깝다.
2. 줄기 단독 9.0 DIP는 마우스로 불가능하지 않다(Windows 기본 커서 히트 1 px). 다만
   Fitts 관점에서 88 DIP 폭의 화분과 9 DIP 폭의 줄기가 같은 창 안에 있으면 사용자는
   항상 화분을 노린다. `docs/windows-dogfooding-log.md:241`의 "would a plant need an
   opaque pot to be draggable at all?"에 대한 정적 답은 **"필요하다, 그리고 이미
   그렇게 되어 있다"**.
3. 부수 효과: 클릭 반응(`click` 클립)도 같은 히트 영역을 쓴다. 즉 사용자가 잎을
   쓰다듬으려 해도 실제로는 화분을 눌러 잎이 흠칫하게 된다. 인터랙션의 원인과
   결과가 공간적으로 어긋나 있다.
4. **미검증**: Windows 클릭스루가 실제로 이 마스크와 일치하는지, 경계를 넘나들 때
   지연이 느껴지는지는 실기에서만 알 수 있다(`docs/windows-dogfooding-log.md:89-100`은
   여전히 빈 체크박스다).

### 2.9 [중간] macOS에서는 투명 영역 83 %가 클릭을 삼킨다

관찰 사실: 클릭스루는 `UpdateClickThrough()`에서 `OperatingSystem.IsWindows()`
가드로 Windows 전용이다(`PetWindow.cs:123`). macOS에는 대응 경로가 없다.
`animation.PointerPressed`는 불투명하지 않으면 `e.Handled`를 세우지 않고 반환하지만
(`PetWindow.cs:37`), 창 자체는 여전히 그 자리에 있는 실제 창이라 아래 앱으로
이벤트가 내려가지 않는다.

재현: 정적 확인. macOS 실기 클릭 확인은 미검증.
영향: macOS에서 식물 근처(투명 여백 포함 192 × 192 전체)를 클릭하면 아무 일도
일어나지 않고 밑의 앱도 반응하지 않는다. 죽은 사각형이 화면 구석에 상주한다.
Sprout는 여백이 83 %라 Mochi(63 %)보다 체감이 훨씬 나쁘다. 이미 알려진 한계로
문서화돼 있다(`docs/mvp.md:46`, `docs/cross-platform.md:67-68`).

### 2.10 [낮음] Sprout는 Mochi보다 화면에서 40 %쯤 작아 보인다

관찰 사실: 같은 192 DIP 창에 Sprout는 89 × 123 DIP, Mochi는 145 × 148 DIP로
그려진다(§1.3). 두 캐릭터 모두 프레임은 384 × 384로 같지만 프레임 안에서
차지하는 비율이 다르다. 또 Sprout의 내용은 y = 343에서 끝나 프레임 아래에 41 소스
px(20.5 DIP)의 빈 공간이 남는다. 기본 배치는 작업 영역 우하단에서 24 DIP를
띄우므로(`PetWindow.cs:61`), 화분은 실제로는 화면 아래 가장자리에서 약 44 DIP 떠
있는 것처럼 보인다.

재현: 정적 확인.
영향: 캐릭터를 바꾸면 크기가 눈에 띄게 점프한다. 자산 규격이 "프레임 크기"만
정하고 "프레임 안에서 캐릭터가 차지해야 할 비율/바닥선"은 정하지 않아서다.
계약 확장 후보이며 이번 배정에서는 제안만 한다.

### 2.11 [낮음] 내장 캐릭터 두 개의 시트가 메모리에 상주한다

관찰 사실: `LoadPackage`가 검증을 위해 `package.Sheet`를 강제 평가하고
(`CharacterLibrary.cs:79`), `CharacterPackage`는 `Lazy<PixelImage>`로 시트를
영구 보유한다(`CharacterLibrary.cs:17-23`). `PixelImage`는 `uint[]`
(`ImageCodec.cs:5`). 시작 시 두 내장 캐릭터가 모두 로드된다(`AppRuntime.cs:76`).

- sprout: 3072 × 1536 × 4 B = **18.9 MB**
- default-cat: 3072 × 1152 × 4 B = **14.2 MB**
- 합계 약 33 MB. 여기에 클립 캐시(sprout `idle` 8프레임 = 4.7 MB 등)가 더 붙는다
  (`AppRuntime.cs:34,129`).

재현: 정적 확인 (산술). 실제 RSS는 D의 스모크 리포트에서 확인해야 한다
(`SmokeDiagnostics.cs`가 프로세스 통계를 남긴다).
영향: "tiny desktop companion"(`README.md:3`) 치고 상주 메모리가 크다. 384 px
소스는 192 DIP 창에 200 % 배율까지만 필요하다. 자산 규격 논의 시 같이 볼 항목.

---

## 3. 원인 가설과 추가 확인 방법

| 발견 | 원인 가설 | 추가 확인 방법 |
|---|---|---|
| §2.2 | 문구 통일 커밋이 `src/` 문자열만 바꾸고 `README.md`/`docs/`를 대상에 넣지 않았다 | `git log -p --all -S "Break now" -- src docs README.md`로 어느 커밋이 무엇을 바꿨는지 대조 |
| §2.3 | `IdleKey`가 `AppRuntime`에 나중에 추가되었고 `SettingsWindow`는 그 이전의 `"idle"` 하드코딩을 유지했다 | `git log -L 85,85:src/Unfold.Desktop/SettingsWindow.cs` |
| §2.4 | 생성기가 row 2·3을 DAY 고정으로 렌더한다(`clip_frames`의 `stretch`/`click` 분기가 `DAY`를 인자로 넘김, `generate-plant-placeholder.py:242,262`) | 이미 픽셀로 확정. 남은 건 밤 변형을 시트에 넣을지의 계약 결정 |
| §2.5 | 자동 발동만 `Tick` 내부에서 리셋하고, 수동 진입점은 `ShowReminder`만 공유한다 | D가 간격 5분으로 두고 4분 시점에 `Break now` → 1분 뒤 자동 발동 여부 관측 |
| §2.8 | 의도된 실험 설계(생성기 docstring). 다만 "5 px" 기재는 반지름/지름 혼동 | 실기에서 §5의 시나리오 K-1~K-4 수행 |
| §2.9 | macOS 클릭스루 미구현(문서화된 한계) | macOS에서 식물 아래에 텍스트 편집기를 두고 투명 영역 클릭 |

---

## 4. 다른 담당자에게 필요한 요청

### A (런타임 — `AppRuntime.cs`, `PetWindow.cs`, `AnimationView.cs`)

| # | 파일 | 이유 | 바꿀 계약 |
|---|---|---|---|
| A-req-1 | `SettingsWindow.cs:85` **(B 소유이나 `AppRuntime.IdleKey`에 의존)** | 설정 미리보기가 낮/밤을 따라가지 않는다(§2.3) | `runtime.Clip("idle")` → `runtime.Clip(runtime.IdleKey)`. 추가로 `Changed`에서 위상 변화 시 미리보기를 다시 로드하려면 `AppRuntime`이 위상 변경을 알릴 수단이 필요하다. **A가 `IdleKey` 노출 방식을 확정해 주면 B가 `SettingsWindow.cs`를 고친다.** 코드 계약 변경이므로 이번 라운드에서는 실행하지 않았다 |
| A-req-2 | `AppRuntime.cs:186-207` | 수동 휴식이 타이머를 리셋하지 않는다(§2.5) | `ShowReminder()`가 실제로 창을 연 경우에 한해 `Clock.Reset(monotonic.Elapsed)`을 부를지 결정. 자동 발동은 이미 `Tick`이 리셋하므로 이중 리셋이 되지 않게 진입점 구분 필요 |
| A-req-3 | `AppRuntime.cs:194` + `AnimationView.cs:49` | 휴식 창이 1초 뒤 정지 이미지가 된다(§2.6) | 휴식 창 한정으로 `stretch` 후 `idle`(또는 `IdleKey`)로 넘어가게 할지 결정. 매니페스트의 `loop` 의미(=클립 자체의 반복 여부)는 건드리지 말 것 — 데스크톱 식물의 1회 재생 계약이 여기에 걸려 있다 |
| A-req-4 | `PetWindow.cs:94` | Mochi 클릭 무응답을 사용자가 이해할 방법이 없다(§2.7) | 코드 변경 요청 아님. **동작을 유지할지**만 판정해 달라. 유지한다면 B가 설정 화면 쪽 안내 문구를 제안한다 |
| A-req-5 | `PetWindow.cs:123` | macOS 투명 영역이 클릭을 삼킨다(§2.9) | 신규 기능이므로 이번 배정 밖. 후보로만 기록 |

### C (Windows·배포 — `NativeReminder.cs`, 배포 스크립트)

| # | 파일 | 이유 | 바꿀 계약 |
|---|---|---|---|
| C-req-1 | `NativeReminder.cs:41` | macOS 알림 제목이 `Unfold`, Windows는 `Time for a break`로 다르다(§2.1 #5) | macOS `display notification`에 `subtitle`/제목을 Windows와 맞추는 문구 제안: 제목 `Time for a break`, 본문 `Look away for a moment.`, 앱 이름은 osascript가 자동으로 붙인다. **문구만 제안이며 C가 적용 여부를 판단한다** |
| C-req-2 | `Scripts/publish-desktop.ps1:22` + `docs/cross-platform.md:59` | 배포 zip의 `README.md`에 `stretch now`와 존재하지 않는 `editor` 트레이 항목이 실려 나간다(§2.2) | 표의 Tray 행을 실제 메뉴(`Settings / Hide Pet / Pause / Reset timer / Break now / Quit Unfold`)로 교정. `docs/cross-platform.md`는 총괄 조정 대상 문서일 수 있으므로 C가 총괄과 확인 후 진행 |

### 총괄

- `README.md:6,34,35`의 `stretch` 문구와 "stretches when you click it"이라는 **동작
  오기재**(§2.2) — 공통 문서라 소유자 지정이 필요하다.
- `docs/mvp.md`, `docs/windows-dogfooding-log.md`의 `Stretch now` / `I'm refreshed`
  라벨(§2.2). 특히 검증 로그는 D가 사용할 문서이므로 D의 체크 항목이 실제 화면과
  맞지 않는다.
- 자산 계약 확장 제안(§2.10, §2.4): 프레임 내 캐릭터 점유 비율·바닥선 규격,
  `stretch`/`click`의 밤 변형. 둘 다 캐릭터 ID·clip 계약 변경이라 제안만 한다.

---

## 5. 검증

| 항목 | 상태 |
|---|---|
| `dotnet build` / `test` / `run` | **미실행** — D 전담(공통 규칙 5) |
| 앱 화면 실행·스크린샷 | **미실행** |
| PNG 자산 실측 (크기·프레임·불투명 분포) | `정적 확인` — 파이썬 표준 라이브러리로 PNG를 직접 디코딩, 종료 코드 0. 스크립트는 세션 스크래치패드에만 두고 저장소에 남기지 않았다. 재현 방법은 아래 |
| 문자열·호출 경로 대조 | `정적 확인` |

자산 실측 재현 방법(누구나 재현 가능하도록 절차만 기록):

1. `Sources/Unfold/Resources/Characters/sprout/spritesheet.png`를 zlib·struct로
   디코딩한다(필터 타입 0–4 전부 처리 필요. 이 파일은 생성기가 필터 0으로만
   쓴다 — `generate-plant-placeholder.py:299`).
2. 384 × 384 격자로 잘라 프레임별 알파 ≥ 26 픽셀 수와 경계 상자를 센다.
3. 히트 실효 면적은 알파 마스크에 체비쇼프 반경 4의 최대값 필터를 적용해 센다
   (`AnimationView.cs:67-71`의 사각 이웃 탐색과 동치).
4. 화분/줄기 분리 기준은 y = 236 (생성기의 화분 상단 rim이 y = 238부터,
   `generate-plant-placeholder.py:208`).

### D에게 전달할 시각·입력 검증 시나리오

`docs/windows-dogfooding-log.md`의 빈 항목 중 이번 실측이 **기댓값을 확정해 준
것들**이다. 값은 정적 계산이므로 실기 결과와 다르면 그 자체가 발견이다.

| ID | 시나리오 | 기대 결과 (정적 계산 기준) |
|---|---|---|
| B-D-1 | 100 % 배율에서 식물 창 위에 커서를 올려 화분 몸통(창 중앙에서 아래로 약 30–70 DIP)을 드래그 | 잡힌다. 폭 약 88 DIP |
| B-D-2 | 같은 배율에서 줄기 중간(창 중앙 x, 중앙에서 위로 약 0–20 DIP)만 드래그 | 폭 9 DIP 이내에서만 잡힌다. 놓치면 §2.8 확정 |
| B-D-3 | 잎 끝(창 중앙에서 좌상 약 −30, −25 DIP 부근)을 드래그 | 잡히거나 놓친다 — 어느 쪽이든 기록. 잎은 실효 면적의 29 %에 포함 |
| B-D-4 | 식물 아래에 텍스트 편집기를 두고 창 네 모서리(투명 영역) 클릭 | Windows: 밑의 편집기에 캐럿이 생겨야 한다. macOS: 아무 일도 일어나지 않는다(§2.9, 알려진 한계) |
| B-D-5 | 125 % / 150 % 배율에서 B-D-1~B-D-3 반복 | 히트 판정은 소스 좌표계라 배율과 무관해야 한다. 시각 크기만 커진다. 어긋나면 신규 결함 |
| B-D-6 | 시스템 시계를 17:59 → 18:01로 넘기고 1초 이상 대기 | 데스크톱 식물이 밤 팔레트로 바뀐다. **동시에 설정 창 미리보기는 낮 팔레트 그대로** → §2.3 확정 |
| B-D-7 | 밤(18:00–06:00)에 `Break now` | 식물이 1.000초 동안 낮 색으로 번쩍인 뒤 밤 색 idle로 복귀 → §2.4 확정 |
| B-D-8 | 밤에 식물 클릭 | 0.333초 동안 낮 색 흠칫 → §2.4 확정 |
| B-D-9 | 휴식 창을 열고 3초 이상 방치 | 1초 후 정지 이미지 → §2.6 확정 |
| B-D-10 | 간격 5분, 4분 시점에 `Break now` → `I'm back` → 대기 | 약 1분 뒤 자동 휴식이 또 뜬다 → §2.5 확정 |
| B-D-11 | 캐릭터를 Sprout ↔ Mochi로 전환 | 화면상 캐릭터 크기가 89 × 123 DIP ↔ 145 × 148 DIP로 점프 → §2.10 확정 |
| B-D-12 | Mochi 선택 후 식물(고양이) 클릭 | 아무 반응 없음. idle 유지 → §2.7 확정 |
| B-D-13 | Mochi 선택 후 시계를 18:00 넘김 | 아무 변화 없음(낮/밤 없음) → §2.7 확정 |
| B-D-14 | 스모크 실행 후 `verification/smoke.json`의 프로세스 통계 확인 | 상주 메모리에 시트 약 33 MB가 포함되는지 → §2.11 대조 |

---

## 6. 변경한 파일

`docs/team/reports/B-01-presentation.md` (이 파일) 하나뿐이다.
제품 코드·이미지·테스트·기존 문서는 읽기만 했다. `git checkout`/`reset`/`commit`/
`stash`/브랜치 변경을 하지 않았다. 임시 계산 스크립트는 세션 스크래치패드에만
만들었고 저장소에는 남기지 않았다.

---

## 7. 미검증 항목과 환경 제약

환경 제약: 작업 머신은 macOS이고 Windows 실기가 없다. 공통 규칙 5에 따라 빌드·
테스트·앱 실행을 하지 않았으므로 **아래는 전부 `미검증`이며, 정적 계산이
"통과"를 뜻하지 않는다.**

1. 실제 화면에서의 정렬·잘림 — 휴식 창(390 × 435, `AppRuntime.cs:195`)의 내용
   높이는 산술상 약 403 DIP로 들어가지만, Avalonia에서 `Window.Height`가 장식
   포함인지 클라이언트 영역인지에 따라 `I'm back` 버튼이 잘릴 수 있다.
2. 설정 창(600 × 790, `MinWidth 550`)에서 `Ui.Row`는 줄바꿈하지 않는다
   (`Ui.cs:25`). `Closing this window keeps Unfold in the tray.` + `Quit` 행이
   최소 폭에서 잘리는지 미확인. `ScrollViewer`는 세로만 담당한다(`SettingsWindow.cs:62`).
3. 선명도 — sprout/Mochi는 `renderStyle`이 없어 `HighQuality` 보간으로 축소된다
   (`SettingsWindow.cs:86`, `PetWindow.cs:77`). 384 → 192/240/288 px 축소의
   실제 화질, 특히 8 px 줄기가 배율에서 어떻게 보이는지 미확인.
4. 히트 영역과 그림의 일치 — 계산상 배율 무관이지만 실측 필요(B-D-5).
5. Windows 클릭스루의 경계 전환 지연(`PetWindow.cs:121-130`, 40 ms 폴링) 체감.
6. 낮/밤 전환 순간의 시각적 팝(크기 변화는 없도록 설계됨,
   `generate-plant-placeholder.py:55-57`) 확인.
7. Windows 트레이 풍선 알림의 실제 전달 여부와 Focus Assist 상호작용
   (`NativeReminder.cs:29-36`) — C·D 몫.
8. 밝은/어두운 배경 위에서의 가장자리 품질(`docs/windows-dogfooding-log.md:238-239`).
9. 상주 메모리 실측(§2.11).

`docs/windows-dogfooding-log.md`는 여전히 전 항목이 빈 체크박스다. 즉 §K의
"고양이로 먼저 측정한 뒤 식물을 설계한다"는 전제가 실행되지 않은 채 식물이
그려졌다. 이번 보고서의 §1.3은 그 §K가 요구한 수치를 **정적으로** 채운 것이며,
실기 관찰(B-D-1~B-D-5)을 대체하지 않는다.

---

## 8. 다음 작업 제안 (최대 3개)

1. **배포되는 문서의 `stretch` 제거와 사실 교정** (§2.2). 특히
   `docs/cross-platform.md:59`는 Windows zip에 `README.md`로 실려 나가므로
   사용자에게 직접 도달한다. `README.md:34`의 "stretches when you click it"은
   문구가 아니라 동작 오기재라 우선순위가 높다. 소유자 지정 필요.
2. **설정 미리보기를 `IdleKey`에 맞추기** (§2.3). 한 줄 수정에 가깝고, 낮/밤
   기능이 설정 화면에서 부정되는 상태를 없앤다. `AppRuntime`의 위상 알림
   방식만 A가 확정해 주면 B가 `SettingsWindow.cs`에서 처리한다.
3. **`stretch`/`click`의 밤 팔레트 변형** (§2.4). 생성기 자신이 알려진 한계로
   적어 둔 항목이고, 밤에 가장 눈에 띄는 순간에 색이 튄다. 시트 재생성 +
   매니페스트 키 추가(`stretch-night`/`click-night`)는 clip 계약 변경이므로
   총괄 판정 후 착수.

후속 후보로만 기록(이번 배정 밖): 프레임 내 캐릭터 점유 비율·바닥선 자산 규격
(§2.10), 소스 프레임 384 px의 메모리 비용 재검토(§2.11), macOS 클릭스루(§2.9),
캐릭터별 기능 차이(클릭 반응·낮/밤 유무)를 선택 화면에서 알리는 방법(§2.7).
