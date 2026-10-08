# 소리 설정 수정 · 2026년 10월 8일

## 원인

시작 기준은 `codex/remove-unused-features`, HEAD `2715fd183c42a25badb29cbf15fc3a017b468464`, **Beta v1.1.2**다. 작업·실행 경로는 `/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features`다. 모든 후보의 브랜치·HEAD·미커밋 변경·프로젝트 버전을 비교했다. 안내에 적힌 `codex/glb-import-compat`의 `bbdb4a8`/`1.1.1-beta`보다 이 개발본이 11커밋 앞선다. 최신 공개 릴리스 API는 `v1.1.1-beta`, 소스 `887d0127e57e5145062d0d558fb71b113284542d`를 반환했다.

- 전체·스트레칭·완료 음량 슬라이더는 아이콘만 표시하고 숫자 %를 표시하지 않았다.
- 파일 선택과 변환 경로는 WAV·MP3만 허용했다.
- 어느 음량 슬라이더를 바꿔도 `stopSoundPreview`가 현재 미리듣기를 취소했다. 새 회귀 검사에서 실제 취소를 재현했다.
- Windows는 비동기 `PlaySound`를 실행한 뒤 WAV의 계산된 길이만큼 기다리고 강제로 중지했다. 장치 시작·출력 지연을 기다리지 않아 끝부분을 자를 수 있는 계약 불일치가 있었다.
- macOS는 미리듣기마다 외부 `afplay` 프로세스를 생성했다. 앱 내부 재생 객체가 자연 종료까지 데이터를 소유하도록 변경했다. 사용자가 들은 macOS 잘림의 단일 원인을 계측으로 확정한 것은 아니며, 특정 출력 장치의 청취 결과는 아래 경계와 구분한다.

## 변경

- 세 음량 슬라이더 오른쪽에 **0~100%**를 표시한다. 입력·자동 저장·복원·음량 아이콘과 함께 갱신한다. 좁은 화면에서도 슬라이더·%·재생 버튼을 같은 줄에 유지한다.
- `.ogg`/`.OGG`의 Vorbis·Opus를 지원한다. 기존 동봉 FFmpeg로 모노·44.1 kHz·16비트 PCM WAV로 변환한다. 원본을 수정하지 않고 5 MiB·30초·취소·잘못된 파일 검사와 설정 자동 저장을 유지한다. 긴 소리를 자동으로 잘라 저장하지 않는다.
- 볼륨을 바꿔도 이미 재생 중인 미리듣기는 기존 크기로 끝까지 재생한다. 바꾼 값은 자동 저장되고 **다음 미리듣기·알림부터** 적용된다. 명시적 중지·다른 소리 미리듣기·파일 교체·페이지 이동·창 숨김은 기존 중지 동작을 유지한다.
- macOS는 앱 내부 [NSSound](https://developer.apple.com/documentation/appkit/nssound)의 재생 상태로 종료를 확인한다. 재생 객체를 종료까지 유지하고 취소 시 해당 객체만 중지·해제한다.
- Windows는 waveOut 버퍼의 [WHDR_DONE](https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/ns-mmeapi-wavehdr) 상태로 실제 종료를 기다린다. 모든 WAV `data` 청크와 마지막 샘플을 포함한다. 이전 재생의 정리는 이전 장치·버퍼에만 적용하며 새 재생을 중지하지 않는다.
- PCM 음량 계산·원본 보관·재생 파일 임대·임시 파일 삭제 계약은 유지한다. 재생 데이터와 임시 파일은 네이티브 재생 종료·명시적 중지 후 해제한다.

## 소유 파일

- [SettingsWindow.Notifications.cs](../../src/Unfold.Desktop/SettingsWindow.Notifications.cs): % 표시·볼륨 조절 시 미리듣기 유지.
- [ReminderSoundImporter.cs](../../src/Unfold.Desktop/ReminderSoundImporter.cs): OGG 선택·변환.
- [ReminderSoundPlayer.cs](../../src/Unfold.Desktop/ReminderSoundPlayer.cs), [NativeSoundPlayback.cs](../../src/Unfold.Desktop/NativeSoundPlayback.cs): 네이티브 자연 종료·취소·버퍼 수명.
- [OggSoundTests.cs](../../Tests/Unfold.Tests/OggSoundTests.cs): 새 검사 7개. 합성 OGG fixture 3개는 기존 FFmpeg의 Vorbis·Opus 인코더로 생성했다.
- 기존 `SettingsPreferencesTests`·`NotificationLayoutTests`·`Mp3SoundTests`를 변경된 동작·표시·입력 범위에 맞춰 갱신했다.
- 현재 설정·말풍선 효과음·플랫폼 안내와 이 기록을 갱신했다. 기존 문서·펫 관리 수정 및 다른 작업트리의 변경을 보존했다.

## 검증

- 수정 전 관련 검사 **65/65 통과**.
- 요구사항을 반영한 재현 검사 2개가 기존 코드에서 실패했다: 볼륨 변경의 취소 토큰, % 표시 누락.
- 최종 집중 검사 **72/72 통과**, 실패·건너뜀 0개. OGG 두 코덱·대문자 확장자·원본 보존·재가져오기·잘못된 컨테이너·과대/장시간 입력·취소·설정 두 슬롯 저장/재열기·교체 실패 복구·WAV 전체 버퍼·볼륨 변경 시 재생 유지를 포함한다.
- 최종 Release 전체 검사 **784/784 통과**, 실패·건너뜀 0개. 빌드 경고·오류 0개. 결과는 `/private/tmp/unfold-sound-settings.cfmp0e/sound-full.trx`다.
- headless UI는 640·760·860·1120px 창과 340·468·760px 알림 카드에서 슬라이더·%·버튼의 배치, 동일 슬라이더 폭과 가로 넘침을 확인했다.
- macOS 실제 Avalonia/AppRuntime + NSSound 실행은 매번 새 `UNFOLD_DATA_DIR`을 사용했다. Vorbis를 스트레칭 슬롯, Opus를 완료 슬롯에 가져오고 두 소리를 실제 네이티브 장치에서 재생했다.
- 약 **1.25초** PCM의 자연 재생 4회가 모두 완료됐다. 관측 호출 시간은 최초 약 1.776초, 이후 약 1.513~1.516초였다. 볼륨 3개 변경 중에도 재생을 유지했고, 명시적 중지·다른 슬롯 전환 2회는 정상 취소했다. 오래된 재생의 정리가 새 재생을 중지하지 않았으며 재시작도 통과했다.
- 실제 창 크기 **1120×800 / 640×560**에서 세 % 표시가 창 안에 있고 가로 넘침이 없음을 검사했다.

[Native 실행 결과 JSON](images/2026-10-08-sound-settings/native-result.json)

| 기본 창 | 최소 창 |
|---|---|
| [1120×800](images/2026-10-08-sound-settings/sound-settings.png) | [640×560](images/2026-10-08-sound-settings/sound-settings-minimum.png) |

## 미검증·위험

- macOS native 재생 상태·경과 시간 검증과 물리적 출력의 사람 청취 평가는 구분한다. 사용자의 특정 스피커·헤드폰·Bluetooth 경로에서의 청취 판정과 물리 슬라이더 입력은 미검증이다.
- Windows waveOut 실기 재생은 미검증이다. macOS에서 실행한 공통 회귀 검사·버퍼 검사가 Windows 오디오 장치 검증을 대신하지 않는다.
- 볼륨 조절은 현재 소리의 크기를 실시간으로 바꾸지 않는다. 중단 없이 완료하고 다음 재생에 적용한다.
- 실행 중인 기존 앱은 재시작해야 새 화면·재생 방식을 적용한다. 이번 진단은 사용자 설정·효과음 원본을 변경하지 않았다.
- 이번 작업은 로컬 v1.1.2 개발본 수정이다. 커밋·푸시·배포·설치 앱 교체는 수행하지 않았다.
