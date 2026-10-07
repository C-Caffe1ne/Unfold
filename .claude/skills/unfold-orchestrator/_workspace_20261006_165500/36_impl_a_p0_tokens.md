# 구현 보고서 — A-P0 의미 토큰

- 날짜: 2026-09-17
- 브랜치: `release/mvp`
- 범위: C09–C11, C16–C21, B01, F01의 다크 토큰과 비활성 버튼 결함 교정
- 변경 후 격리 데이터: `/tmp/unfold-impl-a.MGim9I`
- 비교 baseline: `/tmp/unfold-redesign-baseline.JHBiAh/verification/`

## 원인

기존 공통 버튼의 비활성 상태는 `Surface`와 `Muted`를 지정한 뒤 컨트롤 전체에
`Opacity=.55`를 적용했다. 이 방식은 글자뿐 아니라 버튼 배경까지 뒤 표면과 합성해 라벨
판독성을 떨어뜨렸다. 또한 장식선·입력·Quiet 버튼이 `Outline` 또는 `Muted` 한 역할에
묶여 있었고, 성공·낮은 우선순위 글자·일관된 포커스 링의 의미 토큰이 없었다.

## 변경

### 의미 토큰과 적용 지점

| ID | 이름 | 다크 값 | 적용 지점 |
|---|---|---:|---|
| C09 | `TextTertiary` | `#9CA798` | 토큰 정의. 설정 10–11px 교정과 실제 호출부 이행은 후속 단계 소유다. |
| C10 | `OutlineSubtle` | `#4F5B51` | 기존 `Outline` 별칭, 일반·Danger 버튼 pressed 표면. 별칭을 쓰는 프레임·카드 장식선에도 적용된다. |
| C11 | `OutlineStrong` | `#849187` | TextBox·NumericUpDown·ComboBox의 외곽/템플릿 경계와 Quiet 버튼 경계. |
| C16 | `Error` | `#FFB4AB` | 기존 오류·Danger 호출부가 새 값으로 이어진다. |
| C17 | `Warning` | `#F2CD7D` | 기존 일시정지·초과 휴식·참고 호출부가 새 값으로 이어진다. |
| C18 | `Success` | `#9ED8AC` | 토큰 정의. 완료 메시지 호출부 이행은 소유 파일 밖 후속 작업이다. |
| C19 | `DisabledFill` | `#292F29` | 모든 `.unfold-action:disabled`의 presenter 배경. |
| C20 | `DisabledText` | `#929C91` | 모든 `.unfold-action:disabled`의 presenter 글자와 비활성 NumericUpDown 화살표. |
| C21 | `FocusRing` | `#8FD3FF` | 토큰 정의만 수행했다. UI-07 전까지 컨트롤에 적용하지 않는다. |
| B01 | `BorderSubtle` | `1px` | 장식 경계 두께 토큰 정의. 색은 `OutlineSubtle`이다. |
| B01 | `BorderStrong` | `1px` | 입력 경계와 입력 템플릿에 적용. 색은 `OutlineStrong`이다. |
| F01 | `FocusRingWidth` | `2` | 토큰 정의만 수행했다. |
| F01 | `FocusRingOffset` | `2` | 토큰 정의만 수행했다. |

비활성 버튼의 전체 opacity는 `.55`에서 `1`로 바꾸고 `DisabledFill`/`DisabledText`를
직접 적용했다. 입력의 배경·경계·두께는 hover, focus, focus-within, disabled 사이에서
계속 동일하며, `FocusAdorner=null` 두 곳은 지시대로 유지했다. 라이트 팔레트는 추가하지
않았고 글자 크기·간격·반경·컨트롤 크기·2px 버튼 경계도 바꾸지 않았다.

### 한 단계 유지한 기존 이름

| 호환 이름 | 유지 값/관계 |
|---|---:|
| `Cream` | `#DFE5D1` |
| `Muted` | `#B6BEB0` |
| `Surface` | `#2B2F2A` |
| `Raised` | `#363C33` |
| `Outline` | `OutlineSubtle`의 동일 brush 별칭 |
| `Ink` | `#252A23` |
| `Hover` | `#505A48` |

호출부 전면 치환은 하지 않았다.

## 소유 파일

- `src/Unfold.Desktop/DesignSystem.cs`: 다크 의미 토큰, 경계 토큰, 비활성 상태와 강한 입력 경계
- `Tests/Unfold.Tests/DesignSystemTests.cs`: 승인값·기하·별칭·비활성 대비·입력 경계 회귀 검사
- `docs/design-system.md`: 새 의미 역할, 호환 이름, opacity 1 계약 문서화
- `.claude/skills/unfold-orchestrator/_workspace/36_impl_a_p0_tokens.md`: 본 구현 보고서

사용자가 이미 수정한 다른 파일과 `DesignSystemTests.cs`의 기존 UI-04 변경은 되돌리거나
재작성하지 않았다.

## 테스트

### 추가·수정한 테스트 이름

- 추가: `ApprovedDarkSemanticTokensKeepLegacyValuesAndGeometry`
  - C09–C11, C16–C21의 정확한 다크 hex, 기존 호환값, `Outline` 동일 brush 별칭,
    `BorderSubtle/BorderStrong=1`, `FocusRingWidth/Offset=2`를 단언한다.
- 수정: `ButtonStatesKeepPrimaryTextLegibleAndExposeKeyboardFocus`
  - 비활성 presenter가 `DisabledFill`/`DisabledText`인지, 컨트롤 opacity가 1인지,
    두 비활성 색의 대비가 4.5:1 이상인지 단언한다.
- 수정: `InputsKeepTheirAppearanceWhileHoveredAndEdited`
  - TextBox·NumericUpDown·ComboBox의 `OutlineStrong`/`BorderStrong` 적용과 상태 간 표면
    불변 계약을 함께 단언한다.

## 검증

### `git diff --check`

```text
exit 0
출력 없음
```

### `dotnet test Unfold.slnx -c Release --filter FullyQualifiedName~DesignSystemTests`

```text
복원할 모든 프로젝트가 최신 상태입니다.
통과!  - 실패:     0, 통과:     9, 건너뜀:     0, 전체:     9, 기간: 2 s - Unfold.Tests.dll (net10.0)
```

### `dotnet test Unfold.slnx -c Release --no-restore`

```text
통과!  - 실패:     0, 통과:   175, 건너뜀:     0, 전체:   175, 기간: 15 s - Unfold.Tests.dll (net10.0)
```

### `dotnet build src/Unfold.Desktop -c Release --no-restore`

```text
빌드했습니다.
    경고 0개
    오류 0개
경과 시간: 00:00:01.15
```

### 격리 스모크

실행:

```sh
AFTER=$(mktemp -d /tmp/unfold-impl-a.XXXXXX)
UNFOLD_DATA_DIR="$AFTER" dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
```

실제 결과:

```text
AFTER=/tmp/unfold-impl-a.MGim9I
exit 0
smoke.json success=true
startupMs=1137.5145
totalMs=15147.6313
imageFiles=42
PNG 파일=42장
```

`settingsLayout.noHorizontalOverflow=true`, `sharedDesignDialogsVerified=true`,
`pinnedPageActionsVerified=true`도 확인했다.

### 캡처 비교

| 캡처 | 크기 baseline → after | 직접 비교 결론 |
|---|---:|---|
| `settings.png` | `1120×800 → 1120×800` | 입력 경계와 Quiet `기록·내보내기` 경계가 의미색으로 분리됐다. 위치·크기·줄바꿈은 같다. 펫 표정은 애니메이션 프레임 차이다. |
| `settings-minimum.png` | `860×680 → 860×680` | 기본 화면과 같은 경계 변화만 보이며 최소 창의 카드·타이머 배치는 같다. 펫 표정은 애니메이션 프레임 차이다. |
| `settings-notification-options.png` | `860×680 → 860×680` | TextBox·NumericUpDown·ComboBox 경계가 `OutlineStrong`으로 통일됐다. 두 카드의 폭·높이·줄바꿈은 같다. |
| `dialog-confirm.png` | `460×300 → 460×300` | Quiet 취소 경계가 더 분명하고 Error가 승인값으로 바뀌었다. 대화상자와 문장 배치는 같다. |
| `pack-installed.png` | `520×850 → 520×850` | 비활성 `설치 완료`가 불투명 `DisabledFill` 위 `DisabledText`로 렌더링돼 baseline보다 라벨이 분명하다. 배경이 뒤 표면과 섞이던 현상이 사라졌다. |
| `custom-pet-minimum.png` | `520×620 → 520×620` | 입력·구분선 경계색만 바뀌었고 스크롤·미리보기·하단 버튼 배치는 같다. |
| `speech-overtime.png` | `320×472 → 320×472` | 초과 시간 `Warning`이 승인된 더 선명한 금색으로 바뀌었다. 말풍선·꼬리·버튼 배치는 같다. |
| `weekly-review.png` | `620×650 → 620×650` | Quiet 경계와 비활성 `다음 7일`의 판독성이 좋아졌다. 목록·스크롤·하단 동작 배치는 같다. |

8개 모두 캔버스 크기가 같고, 직접 열어 비교했을 때 색 이외의 높이·폭·줄바꿈 회귀는
보이지 않았다. 자동 캡처는 96 DPI off-screen 렌더 결과이므로 실제 OS 시각 확인을 대신하지
않는다.

## 소유 파일 밖에서 발견했지만 고치지 않은 문제

- `Success`는 정의됐지만 `PersonalizationWindow.cs`와 `SettingsWindow.cs` 등의 완료 메시지는
  아직 `Muted`를 사용한다. 의미 호출부 이행은 이번 소유권 밖이라 수정하지 않았다.
- `TextTertiary`는 정의됐지만 설정 전용 10–11px 글자 교정과 적용은 A-P0-B 후속 작업이다.
- `FocusRing`/`FocusRingWidth`/`FocusRingOffset`은 정의만 됐다. TextBox,
  NumericUpDown, ComboBox의 `FocusAdorner=null`을 포함한 실제 포커스 표시는 UI-07 소유다.

## 미검증·위험

- 실제 macOS·Windows에서 마우스 hover/press와 키보드 Tab 포커스 링을 조작하지 않았다.
- Windows 100/125/150/200% DPI, High Contrast, Narrator와 macOS Retina,
  Increase Contrast, VoiceOver를 확인하지 않았다.
- 정적 PNG는 실제 시스템 글꼴 fallback, 제목 표시줄·파일 선택기·트레이 메뉴, overlay
  scrollbar, GPU 합성에서의 체감 대비를 증명하지 않는다.
- 라이트 테마는 사용자 결정으로 설계와 코드에서 제외했다.
