# 배경 지우기 실패 수정 계획

2026-10-22 작성. 작업 위치: `/Users/hwanghyeonseong/Developer/GitHub/Unfold/.worktrees/pet-throw-media`, 브랜치 `codex/pet-throw-media`. 시작 커밋 `7ef85c9`은 `release/mvp`(푸시됨)와 같다. 사용자 요청 없이 푸시하지 않는다.

## 재개 방법

1. 아래 **진행 상태**에서 첫 미완료 단계부터 이어서 한다. 단계를 마칠 때마다 커밋하고 이 표를 갱신한다.
2. 앱 확인은 `dotnet Unfold.dll`이 아니라 배포본과 같은 번들로 한다. `dotnet publish -r osx-arm64 --self-contained`, `/Applications/Unfold.app`의 `Info.plist`, 루트 `.tools/media-lgpl/osx-arm64/*` → `Contents/MacOS/Tools/`, 임시 서명 순서다. 새 서명마다 키체인 암호 창이 한 번 뜬다.

## 원인

| | 증상 | 원인 | 근거 |
|---|---|---|---|
| A | `배경을 지운 파일을 만들지 못했어요.` | 앱에 들어가는 LGPL FFmpeg 8.1.2에 `png` 디코더가 없는데, `PetMediaProcessor.EncodeGif`가 PNG 프레임을 `image2`로 입력한다. | 실행 로그 `no decoder found for: png`. `-decoders` 목록에 png 없음. `rawvideo` 디코더, `gif` 인코더·먹서, `split`·`palettegen`·`paletteuse`는 있음. |
| B | `배경을 지우면 빈 프레임이 생겨요.` (허용 범위를 바꿔도 반복) | `PetMediaProcessing.RemoveBackground`가 CbCr 거리만 쓴다. 배경이 흰색·검정·회색이면 키 색차가 (0,0)이고, 무채색 픽셀(흰 몸통, 검은 외곽선)이 모두 거리 0이라 허용 범위 0에서도 지워진다. | 수식으로 확인(r=g=b → Cb=Cr=0). 사용자 파일로는 미확인. 사용자가 Tenor 흰 배경 GIF를 보던 중이었다. |

검사가 이를 놓친 이유: 작업트리에 `.tools/media-lgpl`이 없어 테스트·개발 빌드가 PATH의 Homebrew 전체 FFmpeg(`/opt/homebrew/bin/ffmpeg`)를 썼다.

## 수정

**A. 원시 픽셀 입력**
- `EncodeGif`는 프레임을 PNG 대신 하나의 BGRA 원시 파일로 쓴다. `PixelImage`의 `uint` ARGB는 리틀엔디언 바이트로 B,G,R,A이므로 `bgra`다.
- FFmpeg 인자는 `-f rawvideo -pixel_format bgra -video_size {W}x{H} -framerate 100 -i frames.bgra`로 바꾸고, 팔레트 필터와 `GifTiming.RewriteDelays`는 그대로 둔다.
- 프레임 크기가 서로 다르면 가장 큰 캔버스로 왼쪽 위 기준 패딩한다. 먼저 `LoadFrames`가 같은 크기를 보장하는지 확인한다.

**B. 무채색 배경 키잉**
- 키 색차 크기 `sqrt(Cb²+Cr²)`가 작으면(초안 12 미만) 무채색 키로 본다. 그린스크린처럼 채도 높은 키는 지금 동작을 유지한다.
- 무채색 키:
  - 거리에 휘도 차를 포함한다(`sqrt(dCb²+dCr²+dY²)`).
  - 프레임 가장자리에서 시작한 4방향 flood fill로 이어진 키 색 픽셀만 지운다. 외곽선으로 둘러싸인 흰 몸통은 남는다.
- 빈 프레임 오류는 그대로 둔다. 개선 후에도 정말 빈 프레임만 걸리는지 확인한다.

## 검사 (먼저 실패하는 검사를 쓴다)

- A:
  - LGPL FFmpeg로 `EncodeGif`를 실행하는 검사를 둔다. 경로는 저장소 루트에서 위로 올라가며 `.tools/media-lgpl/<rid>/ffmpeg`를 찾아 `UNFOLD_FFMPEG_PATH`로 지정하고, 없으면 건너뛴다.
  - 현재 코드에서 실패하는지 먼저 확인한다.
  - 기존 지연 검사(80/90/80/250)를 이 FFmpeg로 다시 통과시킨다.
- B:
  - 흰 배경 위에 검은 외곽선의 흰 몸통이 있는 프레임을 쓴다. 기본 허용 범위에서 예외 없이 배경만 투명해지고 몸통·외곽선이 남아야 한다.
  - 기존 그린스크린·허용 범위 검사가 그대로 통과해야 한다.
- 마무리:
  - `PetMediaProcessingTests`, `PetMediaCleanupUiTests`, `PetActionFiles*`를 돌린다.
  - 전체 `dotnet test Unfold.slnx`를 돌린다.
  - Release 빌드는 경고 0이어야 하고, apphost로 `--smoke-test`도 실행한다.
- 실제 확인: 번들 앱으로 흰 배경 GIF와 그린스크린 영상에 배경 지우기를 적용한다. 사용자가 쓴 파일을 받으면 그 파일로도 확인한다.

## 진행 상태

| 단계 | 상태 | 커밋 |
|---|---|---|
| 1. 계획 작성 | 완료 | 이 문서 |
| 2. A 실패 검사 → 수정 → 통과 | 완료: 번들 FFmpeg 검사가 수정 전 `no decoder found for: png`로 실패, 수정 후 미디어·정리·여러 파일 20개 통과 | (이 커밋) |
| 3. B 실패 검사 → 수정 → 통과 | 완료: 흰·검정·회색 배경 3건이 수정 전 `빈 프레임` 오류로 실패, 수정 후 미디어·정리·여러 파일 23개 통과 | (이 커밋) |
| 4. 전체 검사·smoke·번들 실제 확인 | 완료: `d9fdf23` 전체 1,238개 통과, v1.3.0 번들 E2E(흰 스티커 GIF·그린스크린 MP4)·smoke 통과 | `0d648f1`, `d9fdf23` |
| 5. 검증 기록(`docs/validation/2026-10-21-pet-throw-media.md`에 절 추가)·사용자 보고 | 완료(v1.3.0 배포 뒤 기록) | (이 커밋) |

## 남은 확인

- 사용자가 실패한 원래 파일은 받지 못했다. 받으면 같은 번들로 다시 확인한다.
- 무채색 판정 기준(12)은 합성 프레임으로만 정했다.
