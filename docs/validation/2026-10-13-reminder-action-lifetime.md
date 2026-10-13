# 알림·스트레칭·완료 행동 수명 수정

## 기준과 원인

`codex/pet-overlay-hover`, `09853c7`, `1.1.3-beta`와 앞선 펫 재생·타이머 오버레이 수정을
이어 작업했다. 현재 최신 개발 브랜치와 HEAD가 같고, 이전 미커밋 변경을 보존했다.
다른 작업트리의 소스와 사용자 프로필은 수정하지 않았다.

기존 한 번 재생 attention은 완료 후 idle, stretch는 완료 후 walk로 복귀했다.
celebrate는 반복 설정과 완료 말풍선의 5초 수명에 묶여 반복하거나 중간에 끊길 수 있었다.
펫 숨김 설정에서는 말풍선 만료와 함께 창을 숨겨 긴 celebrate가 중단되는 별도 경로도 있었다.

## 변경

- attention은 버튼 선택·자동 미루기까지, stretch는 완료·중지까지 같은 상태를 유지한다.
  반복 설정은 계속 재생하고, 한 번 설정은 전체 클립 재생 후 끝 자세를 유지한다.
  stretch 완료 후 또는 미지정 stretch의 대안으로 walk를 자동 실행하지 않는다.
- celebrate는 반복 설정과 관계없이 전체 클립을 한 번 재생한 뒤 idle로 복귀한다.
  Completed → None의 자연스러운 말풍선 만료는 클립을 취소하지 않는다.
- 기존 알림 취소 revision을 함께 확인하여 중지·리셋은 여전히 즉시 취소한다.
  펫 숨기기, 캐릭터 변경, 미리보기 source 교체·닫기도 기존 generation 취소를 유지한다.
- 알림 때문에 임시 표시된 펫은 긴 celebrate가 끝날 때까지 유지하고, 종료 후 표시 설정에 따라 숨긴다.
- 클릭·들기·놓기 후에는 현재 source·session·캐릭터와 일치하는 알림 행동을 재개한다.
  지속되는 상태 반응은 별도 비동기 작업으로 시작하여 클릭·놓기 작업을 무기한 대기시키지 않는다.
- 읽기 실패는 현재 상태의 클립을 재귀 재시도하지 않고 idle로 복귀한다.

주 소유 파일은 `PetWindow.cs`, `PetWindow.Companion.cs`, `PetWindow.PointerArt.cs`, `AppRuntime.cs`다.
감독자가 제품 코드·기존 검사·관련 진단을 수정하고, 테스트 워커는 새
`PetReminderActionLifetimeTests.cs`만 소유했다. 구조 워커는 읽기 전용으로 검토했다.
펫·말풍선·타이머의 별도 창 구조, 저장 형식·애니메이션 파일·버전은 유지했다.

## 검증 범위

수정 전 관련 94개 검사가 통과했다. 정책 변경으로 기존 걷기 전환·완료 반복 기대를 바꾸고,
새 행동 수명 29개와 source·캐릭터 취소 6개 사례를 추가했다.
새 검사에서 중지 상태 타이머의 Remaining 보존, 반복 GLB lift의 Held 상태,
기본 펫을 로드하지 않은 fixture의 선택 조건을 발견해 검사 준비를 현재 계약에 맞췄다.

검사는 이미지·영상, 원본 2D, GLB의 반복·한 번 재생, 자동 미루기, 사전 알림 만료,
완료 전체 재생·긴 완료의 자연 만료와 중지 취소, 임시 표시 펫의 완료 후 숨김,
클릭·들기 후 상태 재개, 늦은 디코딩과 source·캐릭터 교체를 포함한다.
긴 2D 완료 검사 일부는 기존 terminal 경계 테스트 방식을 사용하고, 긴 GLB 검사는
6초 클립의 말풍선을 수동 만료시킨 뒤 취소를 확인한다. 자연 6초 재생 영상으로 해석하지 않는다.
TRX 원본은 `/tmp/unfold-reminder-actions-20261013/results/`에 보관했다.

| 최종 검사 | 결과 |
|---|---|
| 전체 Release 테스트 | 979 통과, 실패·건너뜀 0 |
| Release 빌드 | 성공, 경고·오류 0 |
| macOS 앱 자체 진단 | 종료 코드 0, `success: true`, PNG 110장 |
| 기본 펫 macOS 재생 진단 | 종료 코드 0, 기본 5종 통과, PNG 120장 |
| 공백 검사 | `git diff --check` 통과 |

[테스트 요약](2026-10-13-reminder-action-lifetime/test-summary.json),
[앱 자체 진단](2026-10-13-reminder-action-lifetime/smoke.json),
[기본 펫 재생 진단](2026-10-13-reminder-action-lifetime/original-pets.json)에 결과를 보관했다.

macOS의 `--review-original-pets`를 새 격리 프로필로 실행해 종료 코드 0,
`success: true`, 기본 펫 5종과 PNG 120장을 확인했다. 각 펫의 실제 dispatcher 재생에서
stretch 유지·완료 복귀와 고정된 캔버스를 확인했다. 이 진단은 `offScreen: true`,
`physicalInput: false`이므로 실제 마우스 클릭이나 Windows 실기 확인과 구분한다.
결과 원본은 `/tmp/unfold-reminder-actions-20261013/original-native/verification/`에 있다.

![격리 macOS 렌더링 진단에서 유지된 보리 스트레칭](2026-10-13-reminder-action-lifetime/bori-stretch.png)

Windows 실기 입력, 긴 GLB 완료의 자연 재생 영상, 모든 terminal 포즈의 실제 화면 제시는
이번 자동 검사·격리 진단으로 확인하지 않았다. 커밋·배포는 수행하지 않았다.
