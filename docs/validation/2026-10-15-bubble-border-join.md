# 말풍선 본체·꼬리 접합부 보더 · 2026-10-15

기준은 `codex/pet-overlay-hover`, HEAD `58d629949860ecac68e1ed1a48154710ec1693ca`,
앱 `1.1.4-beta`와 미커밋 GLB 1% 외곽선 변경이다. 현재 소스·검사를 격리 사본과 대조했다.
사용자가 첨부한 이미지의 본체와 꼬리 사이 수직 보더를 제거하는 범위다. 화면에 문구나 조작 요소를 추가하지 않는다.

## 원인과 수정

기존 꼬리 밑변은 그리지 않았지만, 뒤에 있는 본체 Border가 접합부를 그대로 그렸다.
`PetOverlayWindow`는 이제 둥근 본체와 꼬리를 `CombinedGeometry`의 Union으로 합친 뒤
하나의 Path에 채움·외곽선을 적용한다. Bubble 내용은 기존 border inset과 padding을 유지하고
별도의 배경·보더를 그리지 않는다. 공통 Canvas에서 불투명도를 적용해 반투명 겹침도 방지한다.

펫·말풍선·타이머 창 분리, 말풍선 크기·배치·내용·입력 버튼은 유지한다. 기존 GLB 1% 설정은 수정하지 않는다.

## 검증

macOS arm64, .NET SDK 10.0.401, 격리 사본 `/private/tmp/unfold-glb-elements-wldv8iyv`에서 실행했다.

| 검사 | 결과 |
|---|---|
| 수정 전 기존 알림·배치·불투명도·테마 | 37 통과, 실패·건너뜀 0 |
| 수정 전 새 접합부 픽셀 검사 | 8 실패: 모든 테마·방향에서 본체 색과 접합부 색의 차이를 재현 |
| 수정 후 기존 검사 + 새 픽셀 검사 | 45 통과, 실패·건너뜀 0 |
| 새 픽셀 검사 내부 조합 | 4 테마 × 좌우 2 × 알림 상태 4 × 불투명도 2 × 배율 3 = 192 렌더링 |
| 최종 Release 빌드 | 경고 0·오류 0 |

픽셀 검사는 접합부와 본체의 ARGB를 직접 비교하며, 불투명도 37/100%, 배율 1/1.25/2,
사전·초대·휴식·완료 상태를 확인한다. 수정 전후 PNG는 같은 실제 말풍선 컨트롤을
Avalonia Headless + Skia RenderTargetBitmap으로 그렸다. 실제 OS 창 캡처·물리 입력 검사는 아니다.

```sh
dotnet test Tests/Unfold.Tests/Unfold.Tests.csproj -c Release --no-restore \
  --filter 'FullyQualifiedName~PetSpeechSurfaceTests|FullyQualifiedName~PetOverlayWindowTests|FullyQualifiedName~BreakReminderTests|FullyQualifiedName~NotificationLayoutTests|FullyQualifiedName~ThemeTests'
dotnet build Unfold.slnx -c Release --no-restore
```

[수치·소스 SHA-256](evidence/2026-10-15-bubble-border-join/verification.json),
[전체 말풍선·접합부 확대 비교](evidence/2026-10-15-bubble-border-join/comparison.png).
반영 파일을 검사 사본과 대조하고 기존 미커밋 변경을 보존했다.
전체 테스트 스위트·실제 Windows/macOS 입력·설치 앱 교체·배포는 수행하지 않았다.
