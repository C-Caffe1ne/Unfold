# Phase 2 통합 QA — UI-04, UI-05, UI-06, UI-12, UI-13

2026년 09월 17일 · `release/mvp` `2f02e8a` 기반 공유 작업 트리 · C#/.NET 10 Avalonia.

## 판정

**자동 검사와 macOS off-screen 네이티브 진단 기준 PASS. 릴리스 차단 회귀 없음.**

Claude `release-qa` 워커가 diff 검토, 집중 36개, 전체 175개 테스트와 첫 스모크 실행까지 수행했다.
첫 스모크 실패 조사 중 Claude 세션 한도에 도달해 감독자가 실패 원인을 분리하고 진단 회귀를 수정한 뒤
집중·전체 테스트, 빌드, 새 데이터 디렉터리 스모크와 캡처 검토를 완료했다.

## 수용 기준

| 항목 | 판정 | 근거 |
|---|---|---|
| UI-04 스크롤바 거터 | PASS | 트랙 예약 + 8px 거터. 660×880/590×750/520×620 커스텀 펫과 1120×800/990×740/860×680 설정에서 좌표 비교 통과. 네이티브 캡처에서 트랙이 보이고 컨트롤과 분리됨. |
| 480px 펫 팩 384px 미리보기 | PASS | `EnlargedLightPreviewFitsTheMinimumWidthAndDoesNotInstall` 통과. 펫 팩 프레임 인셋 10px로 가로 넘침 없이 보존. |
| UI-05 접힌 알림 상태·복구 | PASS | 대기/진행 트레이 문구, 링/채운 점 배지, 세션·경과 시간 보존 복구 테스트 통과. `speech-folded.png`에서 대기 링 확인. |
| UI-06 고정 경고 요약 | PASS | 실제 경고 3개 팩으로 480×560 고정 요약·본문 이동·상태 초기화 검사 통과. 진단 팩은 경고가 없어 네이티브 경고 화면은 미캡처. |
| UI-12 현재 알림 설정 그룹 | PASS | 선택기 아래 별도 표면, 제목·값 갱신과 최소 설정 창 검사 통과. `settings.png` 확인. |
| UI-13 하단 동작 그룹 | PASS | 600×580에서 관리 3개와 주 작업·닫기가 같은 행을 유지. 독립·설정 내장 화면 캡처 확인. |
| 고정 작업 영역·초안·가로 넘침 | PASS | 전체 회귀와 `smoke.json`의 `pinnedPageActionsVerified`, `petDraftPreserved`, `noHorizontalOverflow`가 true. |

## 실행 결과

| 검사 | 결과 |
|---|---|
| `git diff --check` | 통과 |
| Phase 2 집중 테스트 | 36 통과, 실패 0, 건너뜀 0 |
| `dotnet test Unfold.slnx -c Release --no-restore` | 175 통과, 실패 0, 건너뜀 0 |
| `dotnet build src/Unfold.Desktop -c Release --no-restore` | 경고 0, 오류 0 |
| 격리 Release `--smoke-test` | `success: true`, PNG 42장 |

최종 스모크 데이터는 `/tmp/unfold-phase2-smoke-final.j5Uen8`, 결과는
`verification/smoke.json`과 같은 폴더의 PNG에 있다. 진단은 `Unix 26.6.2`, .NET `10.0.12`를
기록했다.

## 조사한 실패

첫 격리 스모크는 `Scrollbar covers AssignPetMedia`로 실패했다. 이 버튼은 GIF를 동작에 배정한 뒤
부모 카드가 접혀 화면에 없었지만 Avalonia가 이전 `Bounds`를 보존해 진단 헬퍼가 과거 좌표를 읽었다.
집중 좌표 테스트는 버튼이 실제로 보이는 초기 상태에서 이미 거터를 검사한다. 네이티브 스모크는
지속해서 보이는 이름·파일 가져오기·동작 슬롯 버튼을 검사하도록 고쳤다. 이후 전체 스모크가 통과했다.

초기 UI-04 구현은 본문 폭을 덜 줄이려고 스크롤바를 프레임 인셋 쪽으로 이동했으나 정적 네이티브
캡처에서 트랙이 보이지 않았다. 플랫폼 테마에 안정적인 예약 트랙 + 8px 거터로 단순화하고,
480px 펫 팩만 프레임 인셋을 줄여 384px 미리보기를 보존했다. 최종 캡처에서 트랙을 확인했다.

## 검증 경계

- macOS 렌더러를 사용한 자동 off-screen 창과 프로그램 방식 버튼 이벤트다.
- 실제 macOS 메뉴 막대에서 트레이 항목을 클릭하거나 사용자가 마우스·키보드로 흐름을 수행하지 않았다.
- Windows, 100~200% DPI, 다중 모니터, VoiceOver, Narrator는 실행하지 않았다.
- 배지의 실제 체감 대비와 Windows 클릭 통과는 미검증이다.

따라서 Phase 2 자동 수용 기준은 충족하지만, 실제 OS 입력·보조 기술 항목은 감사 문서의 3단계와
OS 실기 체크리스트에서 별도로 확인해야 한다.
