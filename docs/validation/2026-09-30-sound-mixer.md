# 전체·개별 소리 크기 구현 보고서

## 원인

기존 알림 소리 크기는 하나의 공통 값만 사용했고, 미리듣기는 파일 선택 버튼 옆에 있었다.
스트레칭·완료 소리를 따로 조절하려면 설정 저장·초안·미리듣기·실제 재생에 같은 개별 값을 연결해야 했다.

## 변경

- 설정의 알림 섹션에 **전체 소리 → 스트레칭 알림 → 완료 알림** 순서로 0~100% 슬라이더를 배치했다.
- 스트레칭·완료 각각의 % 오른쪽에 **미리듣기 / 정지**를 두고 전체 소리에는 미리듣기를 두지 않았다. 파일 가져오기·기본 버튼은 개별 슬라이더 위에 오른쪽 정렬했다. 알림 효과음 사용 체크박스는 완료 알림 아래에 유지했다.
- 실제 음량은 전체 값 × 해당 개별 값이다. 전체 50%·스트레칭 25%는 12.5%, 전체 50%·완료 80%는 40%로 재생한다. 소수점 음량을 정수 %로 반올림하지 않고 PCM 샘플에 적용한다.
- 전체 0%는 두 소리를 모두 끄고 개별 0%는 해당 소리만 끈다. 저장 전 미리듣기에는 초안의 세 값, 실제 알림에는 저장된 세 값을 사용한다. 어느 슬라이더를 변경해도 현재 미리듣기는 중지한다.
- 기존 `reminderVolumePercent`는 전체 소리 값으로 유지한다. 새 `reminderSoundVolumePercent`, `completionSoundVolumePercent`는 누락 시 100%로 읽어 기존 음량을 보존한다. 손상 값은 해당 필드만 복구하고 잘못된 값을 저장하면 기존 파일을 보존하며 거부한다.
- 저장·취소·미저장 탭 이동 확인에 두 개별 값을 포함했다. WAV·MP3 원본, 선택한 효과음, 운영체제 음량은 변경하지 않는다.
- 좁은 카드에서 세 슬라이더를 같은 폭으로 줄이고 %·미리듣기를 카드 안에 유지한다. 기능 설명 문구는 추가하지 않았다.

## 소유 파일

- Core: `AppSettings.cs`, `AppSettings.Recovery.cs`, `ReminderSounds.cs`.
- Desktop: `SettingsWindow.Preferences.cs`, `SettingsWindow.Notifications.cs`, `ReminderSoundPlayer.cs`.
- Tests: `SoundVolumeTests.cs`, `SettingsPreferencesTests.cs`, `NotificationLayoutTests.cs`.
- 문서: 설정 UI·말풍선 효과음 안내와 이 검증 기록. 기존 다른 변경은 보존했다.

## 검증

- Release 빌드와 집중 검사 **73/73 통과**, 건너뜀 0. 이전 설정 호환·독립 저장/복구·PCM 소수점 음량·각 알림 무음·저장/취소·미리듣기 초안·변경 시 재생 중지·기존 MP3·사본 수명을 포함한다.
- 전체 Release 검사 **474/474 통과**, 건너뜀 0. 최종 결과는 `Tests/Unfold.Tests/TestResults/mixer-focused.trx`, `mixer-full.trx`다.
- headless UI: 640·760·860·1120px 창에서 % 오른쪽 미리듣기와 가로 넘침 없음을 확인했다. 알림 카드 자체를 340·468·760px로 줄였다 넓혀도 세 슬라이더·버튼의 폭과 오른쪽 배치를 유지한다.
- macOS Avalonia 진단: 새 `UNFOLD_DATA_DIR=/tmp/unfold-mixer-smoke-final-1790759532921`, `success: true`, 화면 밖 캡처 93장. 최종 기본 화면은 `verification/settings-options-default.png`, 작은 화면은 `verification/responsive-settings-top.png`다.
- macOS 실제 재생: 합성 MP3를 앱 가져오기 경로로 WAV 변환 후, 전체 50%·스트레칭 25%·완료 80% 설정으로 `ReminderSoundPlayer.Preview` → `/usr/bin/afplay`를 각각 실행했다. 두 재생 모두 오류 없이 완료했다. 재생 시간은 각 0.7836734초이며 음량 계산은 별도 PCM 샘플 검사로 확인했다.
- `git diff --check` 통과.

## 미검증·위험

- 실제 Windows 재생·물리적 슬라이더 입력과 사람의 청취 품질 평가는 미검증이다. 화면 밖 렌더링과 이벤트 검사는 실제 마우스 조작 검증을 대신하지 않는다.
- 실행 중인 일반 앱은 재시작해야 새 화면과 설정 필드를 읽는다. 별도 진단 프로필을 사용해 실제 사용자 설정은 변경하지 않았다.
