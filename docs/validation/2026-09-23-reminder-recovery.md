# 펫 파일 오류 시 알림 복구 — 2026-09-23

## 범위와 원인

`release/mvp`, 기준 커밋 `d9267bc`의 기존 미커밋 변경을 보존하고
[후속 감사](2026-09-23-followup-audit.md)의 **FOLLOW-01**만 수정했다.
홈 미리보기 복구(FOLLOW-02), 미사용 효과음 정리(FOLLOW-03), 패키징·버전·패치노트는 이번 범위에 포함하지 않았다.

기존 `ShowReminder()`는 idle 애니메이션을 읽은 후 휴식 세션과 효과음을 생성했다.
따라서 이미 설치된 펫의 GIF가 손상되거나 접근에 실패하면 해당 회차 알림까지 누락됐다.

## 변경

- 알림 세션 생성·효과음 요청을 애니메이션 로딩보다 먼저 실행한다. 같은 세션의 재요청은 중복 알림·효과음을 만들지 않는다.
- 알림 중 파일 읽기에 실패하면 이미 검증·로딩한 펫 시트의 첫 칸을 정지 이미지로 표시한다. 선택 ID, 원본 파일, 저장된 펫 표시 설정은 유지한다.
- 말풍선의 미루기·휴식 시작·완료와 기록 저장은 계속 동작한다. 클릭 반응 후 손상된 idle로 복귀하는 경우에도 정지 이미지를 유지한다.
- 다음 알림 또는 트레이의 알림 이동 요청에서 파일을 다시 읽는다. 성공하면 애니메이션으로 돌아오고, 무제한 자동 재시도는 만들지 않는다.
- 늦은 로딩 실패가 중지·숨김·종료한 창이나 이미 복구된 재생 상태를 덮어쓰지 않도록 검사한다. 로딩 중 휴식을 시작했으면 뒤늦게 초대 애니메이션으로 바꾸지 않는다.

## 변경 파일

- `src/Unfold.Desktop/AppRuntime.cs`: 알림 생성 순서, 파일 오류 복구, 종료 검사.
- `src/Unfold.Desktop/PetWindow.cs`: 시트 기반 정지 이미지, 오래된 로딩 실패 무시.
- `src/Unfold.Desktop/PetWindow.Companion.cs`: 반응 종료 후 알림 중 정지 이미지 유지.
- `Tests/Unfold.Tests/ReminderRecoveryTests.cs`: 8개 회귀 검사.
- `docs/stretch-notifications.md`, `docs/verification.md`, 이 보고서: 동작과 검증 기록.

## 자동 검사

macOS arm64 / .NET 10 / Avalonia 12.1.2에서 새 임시 데이터 프로필을 사용했다.

| 검사 | 결과 |
| --- | --- |
| 수정 전 손상 idle 관련 재현 | 최초 회귀 검사 6개 중 5개 실패; 알림 누락 확인 |
| 오래된 실패가 복구된 펫을 덮는 추가 재현 | 정상 4프레임이 정지 이미지 1프레임으로 덮이는 실패 확인 후 수정 |
| 초기 집중 검사 | 알림 복구·펫 재생 복구·중지/종료 확인 21개 통과 |
| 최종 전체 Release 테스트 | **329 통과 / 실패 0 / 건너뜀 0**, 종료 코드 0 |
| 앱·네이티브 진단 빌드 | 경고 0 / 오류 0, 종료 코드 0 |
| 변경 형식 | `git diff --check` 통과 |

새 회귀 검사는 손상 GIF에서도 초대·미루기·시작·완료·기록 유지, 중복 세션·효과음 방지,
다음 초대에서 복구된 GIF 재시도, 과거 실패와 새 재생의 경합,
반응 후 정지 이미지 복귀, 로딩 중 중지·숨김·종료, 종료 후 알림 요청을 확인한다.

실행 명령:

```sh
dotnet test Unfold.slnx -c Release --no-restore \
  --logger 'trx;LogFileName=full.trx' \
  --results-directory artifacts/validation/2026-09-23-reminder-recovery
```

전체 결과: `artifacts/validation/2026-09-23-reminder-recovery/full.trx`.

## macOS 네이티브 렌더링 진단

화면 밖의 실제 Avalonia 창에서 보리 시트를 복사한 임시 사용자 팩의 idle GIF만 손상시켰다.
초대·휴식 진행·완료·파일 복구 화면 4장을 생성했다. 앞의 세 장에서 펫 정지 이미지와
말풍선·조작 영역이 함께 표시됨을 확인했다. 프로그램이 버튼 이벤트를 실행해 미루기 후 숨김,
완료 기록 1건, 중지 후 숨김, 정상 GIF 복구 후 4프레임 재생을 확인했다.
설정 파일은 진단 전후 동일했다. 효과음 요청은 새 초대 3회·완료 1회이며 실제 소리는 재생하지 않았다.

- 결과: `artifacts/validation/2026-09-23-reminder-recovery/native-profile-20260923-205229/result.json`, `success: true`, 종료 코드 0.
- 같은 폴더의 `invitation.png`, `resting.png`, `completed.png`, `repaired.png`에 캡처 저장.
- 진단 코드: `artifacts/validation/2026-09-23-reminder-recovery/native-harness/`.

## 미검증·한계

- 실제 macOS 마우스 조작·효과음 청취·긴 작업 간격 전체를 기다린 자동 알림은 이번에 검증하지 않았다. 화면 밖 렌더링과 프로그램의 버튼 이벤트 검사를 실제 사용 검증으로 간주하지 않는다.
- Windows·Intel Mac 실행은 미검증이다.
- 이번 복구는 라이브러리에 로딩된 유효한 manifest·시트가 있는 펫의 애니메이션 읽기 실패를 대상으로 한다. manifest·시트까지 손상되어 팩 자체가 로딩되지 않는 경우는 기존 라이브러리 처리에 따른다.
- 홈 미리보기 실패 후 재시도는 별도 FOLLOW-02로 남아 있다. 기존 v0.2.1 배포 ZIP은 다시 만들지 않았다.
