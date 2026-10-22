# 펫 미디어 형식 조사·구현

2026-10-22. 기준은 `codex/pet-throw-media`의 `d9fdf23`(버전 1.2.1),
작업 경로는 `.worktrees/pet-throw-media`다. 시작 시 미커밋 변경은 없었다.
루트 `release/mvp`의 `7ef85c9` 이후 배경 제거 수정 두 건까지 포함한 작업트리를 사용했다.
이번 수정은 미커밋이며 설치 앱·공개 배포본은 교체하지 않았다.

## 선정

사용 빈도 순위나 점유율을 산정한 조사는 아니다. 웹 이미지·폰/편집기 영상에서 쓰는 형식,
투명 배경과 애니메이션 필요성, 현재 배포 도구의 지원 여부로 선정했다.

| 분류 | 형식 | 결정 |
|---|---|---|
| 정지 이미지 | PNG, JPG/JPEG, WebP, BMP | 기존 지원 유지 |
| JPEG 변형 | JFIF | JPEG 디코더로 추가 |
| 움직이는 이미지 | GIF | 기존 원본 보관 방식 유지 |
| 움직이는 이미지 | WebP, APNG (`.apng`, `.png`) | 프레임 합성 후 GIF로 가져오기 추가 |
| 영상 | MP4 | 기존 지원 유지 |
| 영상 | MOV, M4V | MP4와 같은 MOV 계열 디먹서로 추가 |
| 영상 | WebM, MKV | Matroska 디먹서로 추가 |
| 영상 | AVI | AVI 디먹서로 추가 |
| 후속 후보 | HEIC/HEIF, AVIF | 추가 디코더·배포 구성이 필요해 보류 |
| 이번 제외 | TIFF, SVG, RAW, PSD, ICO, WMV, FLV, MPEG/TS | 사진 편집·벡터·아이콘·구형/전문 영상 범위. 이번 펫 입력 우선순위에서 제외 |

공식 근거:

- [MDN 이미지 형식 안내](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Image_types): 정지·애니메이션 이미지 형식의 특성과 용도.
- [Google WebP 컨테이너 규격](https://developers.google.com/speed/webp/docs/riff_container): WebP의 알파·애니메이션과 프레임별 합성/시간.
- [W3C PNG 3판](https://www.w3.org/TR/png-3/): APNG의 acTL/fcTL/fdAT, 기본 이미지 제외, blend/dispose, 지연 시간.
- [MDN 미디어 컨테이너](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Containers): 확장자/컨테이너와 내부 코덱의 구분.
- [FFmpeg 형식 지원](https://ffmpeg.org/general.html): MOV/MP4, Matroska/WebM, AVI의 디먹싱 지원.

문서의 FFmpeg 전체 기능을 번들 지원으로 간주하지 않았다. 설치 앱에 동봉된 LGPL FFmpeg의
디코더·디먹서 목록을 읽어 H.264·HEVC·MPEG-4·VP8·VP9와 해당 컨테이너를 확인했다.
이 도구에는 PNG/APNG 디코더가 없다. APNG는 PNG 청크·CRC·프레임 순서·경계를 검증한 뒤
각 부분 프레임을 Skia PNG 디코더로 읽고 앱에서 합성한다. WebP는 Skia의 프레임 디코더를 사용한다.

## 변경

- 기존 두 파일 선택기가 공유하는 `PetMediaImporter.FilePatterns`를 확장했다.
- APNG는 확장자뿐 아니라 PNG 내부의 애니메이션 청크로 감지해 `.png`도 움직임을 유지한다.
- APNG의 부분 프레임·알파 합성·배경 복원·이전 프레임 복원과 별도 기본 이미지를 처리한다.
- WebP의 프레임별 지연과 투명 영역을 읽고, 기존 GIF 저장/편집 계약을 재사용한다.
- 불투명 프레임 뒤에 투명 영역이 생길 때 이전 색이 남던 GIF 변환도 보완했다.
  완성된 전체 프레임을 기록하고 프레임 사이에 캔버스를 비운다.
- 파일 크기·이미지 크기·프레임 수·디코딩 예산·영상 길이 제한, 취소와 임시 파일 정리를 유지했다.
- 오류 문구를 영상 전반에 맞추고 한국어·영어·일본어·중국어 번역을 반영했다.

입력 제한과 변환 특성은 [펫 팩 문서](../pet-packs.md)를 따른다.
새 애니메이션은 최대 2048px·512프레임·픽셀 128 MiB, 파일 32 MiB다.
GIF 변환은 프레임별 최대 256색·1단계 알파, 10ms 시간 단위(최소 20ms)다.
영상은 128 MiB·10초 이하를 최대 192px·12fps로 변환하며 오디오는 제외한다.
WebM 별도 알파 스트림은 현재 번들 디코더가 지원하지 않아 영상 투명도를 보장하지 않는다.
컨테이너를 지원하더라도 모든 코덱이나 DRM 영상이 지원된다는 의미는 아니다.

## 검증

Release 집중 검사 **116 통과, 실패 0, 건너뜀 0**. 전체 테스트·전체 OS 진단은 실행하지 않았다.
테스트 실행 시 새 `UNFOLD_DATA_DIR`와 설치 앱의 번들 FFmpeg 경로를 지정했다.
FFmpeg는 읽어서 변환 프로세스로 사용했으며 설치 앱의 파일은 변경하지 않았다.

```sh
UNFOLD_DATA_DIR=$(mktemp -d /tmp/unfold-media-final.XXXXXX) \
UNFOLD_FFMPEG_PATH=/Applications/Unfold.app/Contents/MacOS/Tools/ffmpeg \
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
dotnet test Tests/Unfold.Tests/Unfold.Tests.csproj -c Release --no-restore -m:1 /nodeReuse:false \
  --filter 'FullyQualifiedName~AnimatedPetMediaTests|FullyQualifiedName~PetVideoFormatTests|FullyQualifiedName~PetMediaProcessingTests|FullyQualifiedName~StaticPetMedia|FullyQualifiedName~PetMediaImporterTests|FullyQualifiedName~PetBuilderTests|FullyQualifiedName~PetDirectSaveTests|FullyQualifiedName~LocalizationTests' \
  --logger 'trx;LogFileName=media-formats.trx' --results-directory /tmp/unfold-media-format-results
```

- 실제 형식으로 인코딩된 MOV/M4V/MKV(H.264), WebM(VP9), AVI(MPEG-4 Part 2)를 번들 도구로 변환.
- APNG는 테스트용 합성 사례와 별도 FFmpeg 인코더가 만든 fixture를 모두 확인.
- 원본 삭제 후 가져온 내용 보존, 팩 내보내기·설치·편집 재열기, 프레임 픽셀·시간 비교.
- APNG CRC·순서·프레임 경계·메모리 예산·잘린 파일 거부, WebP 손상·취소 검사.
- 새 영상 형식의 11초·손상·128 MiB 초과 입력 거부.
- 헤드리스 Avalonia에서 `.apng`·애니메이션 `.png`·`.webp`를 기존 편집기로 열고,
  손상 파일로 교체를 시도해도 이전 클립·썸네일이 남는지 확인.
- 기존 정지 이미지, 직접 저장·적용, 펫 편집기, 배경 제거와 번역 회귀 포함.
- `git diff --check` 통과. 테스트 빌드에 포함된 Release 컴파일 통과.

처음 실행은 샌드박스의 MSBuild 로컬 소켓 제한으로 시작하지 못해 허용된 환경에서 재실행했다.
검사 중 발견한 GIF 투명도 회귀를 수정한 뒤 최종 116개가 통과했다. 이전 실행 결과를 합산하지 않는다.
TRX: `/tmp/unfold-media-format-results/media-formats.trx`.

실제 macOS 앱 창·OS 파일 선택기 입력, Windows 실행, 모든 변형 코덱, HEVC·AV1 실파일,
배포 패키지 재생성과 업데이트는 이번에 검증하지 않았다. 헤드리스/네이티브 변환 검사와 구분한다.
