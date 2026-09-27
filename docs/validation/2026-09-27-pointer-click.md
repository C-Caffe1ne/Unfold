# 포인터 누름 유지와 클릭 분리 — 2026-09-27

## 원인

`BeginPointerArt`가 모든 포인터 다운에서 즉시 `pickup`을 시작했다. 포인터 업에서는
누른 시간과 관계없이 드래그가 아니면 클릭으로 분류하고, 바운스·복귀 뒤 `click`까지
재생했다. 따라서 짧은 클릭도 공 모양·매달림과 착지 모션을 거쳤다.

## 변경

- 0.22초 미만의 짧은 클릭: 기존 `click` 반응만 재생한다.
- 0.22초 이상 누름 유지: 고슴도치의 공 모양 또는 나머지 펫의 매달림을 시작한다.
- 기존 드래그 기준인 5px 이상 이동: 대기 시간을 채우지 않아도 즉시 들어 올리기를 시작한다.
- 누름 유지·드래그 후 놓기: 한 번 튕기고 복귀하며, 클릭 반응을 덧붙이지 않는다.
- 누름 대기 중 숨김·펫 변경·입력 캡처 해제는 대기를 취소한다.
- 새 프레임을 비동기로 읽는 동안 입력 상태가 바뀌면 오래된 재생 요청을 무효화한다.
- 알림 보류·재누름·스트레칭 후 이동 흐름과 기존 펫별 자산은 유지한다.

## 소유 파일

- `src/Unfold.Desktop/PetWindow.cs`: 클릭 시간 분류, 드래그 시작 연결.
- `src/Unfold.Desktop/PetWindow.PointerArt.cs`: 누름 대기와 클릭·유지·착지 분기.
- `src/Unfold.Desktop/PetWindow.Companion.cs`: 누름 시간 시작점과 이전 팩의 착지 분기.
- `Tests/Unfold.Tests/OriginalCompanionTests.cs`: 5종 실제 자산 및 입력 경계 회귀 검사.
- `docs/pet-generation-guide.md`, `Art/Characters/pointer-interactions-v1/README.md`: 현재 재생 계약.

## 검증

macOS에서 .NET 10 Release 빌드와 자동 테스트를 실행했다. 런타임 테스트는 테스트별
임시 `UNFOLD_DATA_DIR`을 사용했다.

| 검사 | 결과 |
|---|---|
| OriginalCompanion·PetHover·AnimationRendering 집중 검사 | 48 통과, 종료 코드 0 |
| 누름 대기 취소 3개 사례를 추가한 전체 Release 검사 | 366 통과 / 0 실패 / 0 건너뜀, 종료 코드 0 |
| `git diff --check` | 통과 |

기본 펫 5종의 짧은 클릭·누름 유지·착지, 연속 클릭, 시간 경계, 빠른 드래그,
캡처 해제·숨김·펫 변경, 재누름과 알림 충돌을 검사했다. 기존 호버·렌더링 검사도 통과했다.

```sh
dotnet test Unfold.slnx -c Release --no-restore
```

결과: `artifacts/validation/2026-09-27-pointer-click/tests/pointer-click-focused.trx`,
`pointer-click-full.trx`.

## 미검증·위험

Avalonia Headless 입력 검사는 실제 마우스·트랙패드 조작 검증이 아니다.
이번 변경의 macOS 실제 포인터 조작 및 Windows 실기 검증은 수행하지 않았다.
