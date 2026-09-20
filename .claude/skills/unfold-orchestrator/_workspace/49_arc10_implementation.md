# 구현 보고서 — ARC-10 설정 손상 복구 비대칭

## 원인

`AppSettings.Load`가 필드마다 다르게 반응했다. `Theme`, `CustomRoutine`, `BreakRoutineId`,
`ActiveProfileId`는 잘못된 값을 개별적으로 되돌리고 나머지를 보존했지만,
`Validate()`가 검사하는 8개 필드는 `InvalidDataException`을 던졌다.
그 예외는 `AppRuntime.cs:75-77`의 catch로 떨어져 `Settings = new()`,
즉 사용자의 루틴 라이브러리·업무 프로필·펫 선택·창 위치까지 공장 초기값이 됐다.

예: `PetScalePercent`가 55로 손상되면 저장된 루틴이 전부 사라졌다.

`docs/personalization.md:11-12`는 저장된 사용자 루틴·업무 프로필을 삭제하거나 덮어쓰지
않는다고 약속한다. 전체 초기화를 의도로 명시한 문서는 없다.

## 변경

- `src/Unfold.Core/AppSettings.cs` — `Load`의 `Validate(value)`를 `Recover(value)`로 교체.
  8개 필드가 각각 기본값으로 자체 복구한다. 손상된 사운드 식별자는 표시 이름까지 함께
  비워 둘이 어긋나지 않게 한다.
- `Save`는 기존대로 `Validate(this)`를 유지한다. 잘못된 값이 파일에 기록되는 경로는 없다.

## 소유 파일

- `src/Unfold.Core/AppSettings.cs`
- `Tests/Unfold.Tests/SettingsRecoveryTests.cs` (신규)
- `Tests/Unfold.Tests/SettingsReliabilityTests.cs`

## 변경한 기존 테스트 계약

세 개가 "손상된 값이면 Load가 던진다"를 고정하고 있어 새 동작에 맞게 다시 썼다.
이름과 단정을 바꿨을 뿐 검사 대상은 유지했다.

| 기존 | 변경 후 |
|---|---|
| `LoadWithOutOfRangeIntervalThrowsInvalidDataException` | `LoadWithOutOfRangeIntervalRecoversThatFieldAlone` — 간격만 기본값, `IdleMinutes` 보존 |
| `LoadWithNullSelectedCharacterIdThrowsInvalidDataException` | `LoadWithNullSelectedCharacterIdRecoversThatFieldAlone` |
| `InvalidSettingsValuesDoNotPreventStartup` (2개 InlineData) | `UnreadableSettingsDoNotPreventStartup` — 읽을 수 없는 `null`만 남김. 복구 가능한 값은 더 이상 격리·초기화 대상이 아니다 |

## 검증

| 검사 | 결과 |
|---|---|
| `dotnet test Unfold.slnx -c Release` | **220 통과 / 0 실패 / 0 건너뜀** |
| 신규 `SettingsRecoveryTests` | 손상 필드 8종 각각 복구 + 동반 필드 보존, 루틴 라이브러리 생존, `Save`는 여전히 거부 |
| 신규 `ARecoverableValueStartsUpHealedWithoutDiscardingTheFile` | 시작 시 손상 필드만 복구, `.invalid-*` 격리 파일 없음 |

`ADamagedScalarDoesNotDiscardTheSavedRoutineLibrary`는 직렬화 이름이 바뀌면 검사가
조용히 무력해지므로, 치환 토큰을 찾지 못하면 실패하도록 가드를 넣었다.

## 의도적으로 남긴 것

`ValidatePersonalization()` 실패는 여전히 던진다. 루틴·프로필은 사용자가 만든 내용이라
조용히 버리는 것보다 `AppRuntime`의 `.invalid-<timestamp>` 백업 후 초기화가 안전하다고
판단했다. 스칼라·열거형 필드는 되돌릴 기본값이 분명해 복구해도 잃는 내용이 없다.

## 미검증

- 실제 손상 파일을 가진 사용자 환경에서의 업그레이드 경로는 확인하지 않았다.
- `AppRuntime`의 백업 복사 실패 경로(`AppRuntime.cs:82-83`, 로그만 남김)는 그대로다.
