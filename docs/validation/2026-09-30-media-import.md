# 이미지·사진 및 MP3 가져오기 구현 보고서

## 원인

펫 파일 선택과 가져오기는 GIF·MP4만 허용했고, 커스텀 팩은 모든 동작을 GIF로 저장했다.
알림 효과음은 WAV 선택·PCM 검증만 제공했다. 선택 필터만 늘리면 실제 디코딩·팩 저장·재생이 실패하므로 각 저장 경로까지 확장했다.

## 변경

- 펫의 다섯 행동 카드에서 PNG·JPG/JPEG·WEBP·BMP 정지 이미지와 기존 GIF·MP4를 선택할 수 있다.
- 이미지 입력은 32 MiB·각 변 8192px·16,777,216픽셀 이하로 제한한다. EXIF 회전·반전을 적용하고, 비율과 투명도를 유지해 긴 변을 최대 512px로 줄인다. 작은 이미지는 확대하지 않는다.
- 정지 이미지는 1프레임·1초의 동작으로 사용한다. 팩에서는 중앙 정렬한 PNG 시트와 기존 프레임 인덱스 계약을 사용하므로 런타임 manifest 버전은 1이다. 이미지와 GIF·MP4를 함께 배정할 수 있고 원본 파일을 삭제해도 초안·저장 팩이 유지된다.
- 스트레칭·완료 효과음 모두 WAV·MP3를 선택할 수 있다. MP3는 기존 동봉 LGPL FFmpeg로 로컬에서 모노·44.1 kHz·16비트 PCM WAV로 변환한다. 원본과 결과는 각각 5 MiB 이하, 재생 시간은 30초 이하이며 긴 소리를 잘라 저장하지 않는다.
- 변환한 효과음도 기존 파일명 표시, 미리듣기·볼륨, 저장·취소·교체·미사용 사본 정리를 따른다. 창을 폐기하면 진행 중 변환 프로세스도 취소한다.
- 기존 GIF 픽셀·프레임 시간, MP4 제한, WAV 바이트, 파일 교체 확인과 저장 경계를 유지했다. HEIC·TIFF·SVG와 움직이는 WEBP는 이번 지원 대상이 아니다.

## 소유 파일

- Core: `ImageCodec.cs`, `CustomPetDraft.cs`, `ReminderSounds.cs`.
- Desktop: `PetMediaImporter.cs`, `CustomPetWindow.cs`, `ReminderSoundImporter.cs`, `MediaConversion.cs`, `SettingsWindow.Notifications.cs`, `SettingsWindow.cs`의 소리 변환 취소 연결.
- Tests: `StaticPetMediaTests.cs`, `Mp3SoundTests.cs`, 합성 MP3 두 파일과 fixture 제작 기록.
- 문서: 펫 팩·효과음·설정·플랫폼 안내의 지원 형식과 제한 업데이트. 기존 체크아웃의 다른 변경은 보존했다.

## 검증

- Desktop Release 빌드: 경고 0, 오류 0.
- 집중 검사: **56/56 통과**, 건너뜀 0. 이미지 형식·사진 방향 8종·투명도·비율·손상·크기 제한, GIF와 혼합한 팩 저장·설치, MP3 변환·원본 삭제·중복 저장·볼륨·초과 시간 거부, 설정의 두 효과음 슬롯·미리듣기·저장·재열기·취소·교체 실패·변환 중 창 폐기를 확인했다.
- 전체 Release 검사: **464/464 통과**, 건너뜀 0. 실제 macOS FFmpeg MP4·MP3 변환도 실행했다. 결과는 `Tests/Unfold.Tests/TestResults/media-focused.trx`, `media-full.trx`다.
- macOS Avalonia 통합 진단: `success: true`, 93개 화면 밖 캡처, 펫 팩 설치·업데이트·복구 검사 통과. 새 `UNFOLD_DATA_DIR=/tmp/unfold-media-smoke-1790757375143`을 사용했다.
- macOS 네이티브 소리 확인: 실제 `ReminderSoundImporter.Import` → `ReminderSoundPlayer.Preview` → `/usr/bin/afplay` 경로를 별도 임시 프로필에서 실행했다. 합성 MP3 변환 후 0.7836734초 PCM 소리가 15% 음량으로 오류 없이 재생 완료됐다. 이 확인은 청취 품질 평가가 아니다.
- `git diff --check` 통과. 기존 미디어 준비 도구의 SHA-256 검증 후 osx-arm64 FFmpeg를 준비했고, 정상 빌드가 `Tools/`로 복사하는 경로를 사용했다. 실행 중 다운로드나 외부 업로드는 추가하지 않았다.

## 미검증·위험

- 실제 macOS 파일 선택창의 물리 입력과 Windows 파일 선택·MP3 변환·PlaySound 재생은 미검증이다.
- 사진의 배경은 유지한다. 자동 배경 제거와 동작 생성은 포함하지 않는다.
- 개발 환경에 미디어 도구가 없으면 MP3·MP4 변환을 사용할 수 없다. 배포 스크립트는 기존처럼 해당 OS 도구를 준비·동봉한다.
