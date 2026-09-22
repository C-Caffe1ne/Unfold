# Mochi·보리 확장 행동 구현·검증

2026-09-22 · `release/mvp` 작업 트리 · macOS arm64 · .NET 10 / Avalonia.
기존 미커밋 UI·BM 문서 변경을 보존하고 승인된 펫 상호작용과 리소스를 구현했다.

## 구현 결과

- Mochi(고양이)·보리(토끼)를 심플한 선화 PNG로 재제작하고 기본 목록에 포함했다.
  팩마다 256px 셀과 열 개 클립을 사용한다. [원본·타임라인](../../Art/Characters/original-companions-v2/README.md)
- 평상시 20~40초의 유효 대기 후 잠자기·두리번거리기·하품을 무작위로 재생한다.
  포인터 호버, 알림, 다른 반응 중에는 발생하지 않고 같은 행동을 연속 선택하지 않는다.
- 누르는 동안 0.1초 스퀴시, 놓으면 0.44초 바운스와 놀람 → 두리번거리기를 재생한다.
  드래그·포인터 캡처 취소는 클릭 반응을 발생시키지 않는다.
- 세 번째 연속 미루기에서 삐지기를 한 번 재생한다. 휴식 시작·완료·중지 시 횟수를 초기화한다.
- 실제 휴식 중 스트레칭 완료 이벤트 이후 초당 28 논리 px로 현재 모니터 안을 걷는다.
  말풍선을 포함한 창을 작업 영역 안에 유지하며 호버·드래그·메뉴·반응 중에는 멈춘다.
  중지·완료·숨김·선택 변경은 이동을 끝낸다. 자동 이동은 저장된 수동 위치를 덮어쓰지 않는다.
  스트레칭 도중 드래그하면 놓은 뒤 스트레칭을 다시 마치고 이동한다.
- `behaviorProfile: unfold-original-v1`과 열 개 클립이 모두 있어야 새 행동을 사용한다.
  커스텀 제작기는 기존 다섯 슬롯을 유지하고 이 프로필을 출력하지 않는다.
- 기존 설치 폴더와 같은 기본 ID가 있으면 파일은 보존하고 목록에 기본 펫만 한 번 표시한다.

## 자동 검사

| 검사 | 결과 |
|---|---|
| `dotnet build Unfold.slnx -c Release --no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false` | 종료 0, 경고·오류 0 |
| `dotnet test Unfold.slnx -c Release --no-build --no-restore` | 종료 0, 268 통과, 실패·건너뜀 0 |
| 새 상태·입력·이동 회귀 검사 | 유효 미루기 횟수, 무작위 간격, 스퀴시/바운스, 드래그 후 복구, stretch → walk, 숨김·전환·중지, 커스텀 분기 통과 |
| 이동 계산 검사 | 음수 원점 모니터, 1/1.5/2배 배율, 96/192/288px 펫, 말풍선 4방향, 큰 시간 간격 통과 |
| 원본 팩 자산·왕복 검사 | 두 팩 각각 열 클립 디코딩, 경계 alpha <26, 빈 프레임 없음, ZIP 생성·열기·설치·재열기 통과 |
| `--validate-characters Assets/Characters` | 종료 0, 오류·경고 0; Mochi 18.75 MiB, 보리 18.5 MiB 디코딩 픽셀 |
| `.unfoldpet` 산출물 | 제작 CLI 검증, ZIP CRC와 `pack.json`의 각 파일 SHA-256 일치 |
| `git diff --check` | 통과 |

새 `OriginalCompanionTests`는 11개다. 입력 검사는 Avalonia headless 입력 API를 사용하며 실제 마우스 검증이 아니다.
테스트·진단은 사용자 데이터와 분리된 새 임시 프로필에서 실행했다.

## macOS 네이티브 자동 진단

```sh
UNFOLD_DATA_DIR=/private/tmp/unfold-original-pets-20260922-b \
  dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --review-original-pets
UNFOLD_DATA_DIR=/private/tmp/unfold-original-smoke-20260922 \
  dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --smoke-test
```

- 두 실행 모두 종료 0. 원본 펫 진단은 각 열 클립의 실제 dispatcher 재생·idle 복귀,
  눌림/해제 포즈, 세 번째 미루기, 스트레칭 후 walk 전환, 완료·숨김 중단을 확인했다.
- 원본 펫 PNG 46장, 기존 smoke PNG 91장. 렌더링 캡처에서 기본 자세, 클릭 방향 전환,
  스트레칭·걷기와 말풍선 배치를 확인했다. 캡처는 2배 픽셀이며 원본 해상도를 뜻하지 않는다.
- smoke는 타이머, 설정, 기록, 말풍선, 팩 설치·갱신·복구, 커스텀 제작과 최소 창 검사를 통과했다.
- 새 진단은 화면 밖 창과 코드 입력을 사용한다. 이동 상태 전환은 확인하지만 실제 바탕화면
  이동은 진단 모드에서 억제한다. 이동 범위·속도는 별도의 자동 계산 검사 근거다.

원본 결과: [펫 재생](2026-09-22-original-pets.json), [전체 smoke](2026-09-22-original-pets-smoke.json),
[자산 검사](2026-09-22-original-pets-assets.json), [팩 체크섬](2026-09-22-original-pets-packages.json).
캡처는 `artifacts/verification/2026-09-22-original-companions/`에 보관했다.

## 산출물과 남은 실기 확인

- `artifacts/pet-packs/Mochi-2.0.0.unfoldpet` — 1,863,293 bytes.
- `artifacts/pet-packs/Bori-0.2.0.unfoldpet` — 1,789,947 bytes.
- 두 펫은 현재 앱에 포함되어 홈에서 바로 선택한다. 기본 ID 보호에 따라 같은 ID로 재설치하지 않는다.

실제 macOS 포인터 조작·여러 모니터 이동·DPI 변경·장시간 자원 사용과 Windows 실기는
이번 검증에서 수행하지 않았다. 연속 프레임의 자연스러움과 사용자 최종 아트 승인은 별도다.
보리의 제외 셀과 두 자세 걷기는 제작 원장에 기록했다. 자동 성공을 유료 판매용 아트 승인으로 해석하지 않는다.
