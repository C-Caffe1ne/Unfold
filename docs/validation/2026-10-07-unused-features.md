# 미사용 기능 제거 검증 — 2026-10-07

## 환경과 기준

최신 제품 코드인 `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold`,
`codex/glb-import-compat`, `bbdb4a8416aec232f50a13c4e58e555b5bfa2f9a`, **1.1.1-beta**를 기준으로 했다.
시작 시 후보 작업트리 4개의 HEAD·브랜치·미커밋 변경·csproj 버전을 확인했다.
`release/mvp`의 더 최근 커밋 `ce0ccda`는 검증 문서만 변경했으며 제품 버전은 0.2.2다.
GitHub releases API에서 확인한 최신 공개 배포는 `v1.1.0-beta`다.
설치된 `/Applications/Unfold.app`의 1.0.2를 이번 검증 대상으로 사용하지 않았다.

기존 작업을 보존하도록 위 제품 커밋에서 독립 브랜치 `codex/remove-unused-features`를 만들었다.
작업·실행 경로는 `/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features`다.
진행 중 다른 작업트리에 생긴 `AGENTS.md` 변경은 수정·커밋하지 않았다.
최신 제품 커밋/배포 버전을 매번 확인한다는 사용자 요청은 기억 업데이트와 이 브랜치의 `AGENTS.md`에 반영했다.

## 제거 영향과 변경

일반 사용자 화면에는 이미 루틴·프로필 설정과 픽셀 에디터 진입점이 없었다.
다만 다음 6개 제품 컴포넌트가 컴파일되고 진단·테스트에서 생성되고 있었다.

- `RoutineEditorWindow`, `ProfileEditorWindow`, `PersonalizationWindow`.
- `EditorWindow`, `PixelCanvas`, Core의 `EditorSession`.

위 컴포넌트와 `AppRuntime.OpenEditor`·편집기 상태·저장 콜백을 제거했다.
종료·업데이트·구매 잠금의 미사용 편집기 호출도 함께 정리했다.
루틴/프로필 생성·수정·삭제·적용 API 6개(`SaveRoutine`, `RemoveRoutine`, `ApplyReminder`,
`SaveProfile`, `RemoveProfile`, `ApplyProfile`)도 일반 런타임 소비자가 없음을 확인한 뒤 제거했다.

현재 앱이 사용하는 경로는 유지했다.

- 기존 루틴·업무 프로필의 저장 필드, 검증/복구, 선택된 루틴 실행과 세션 스냅샷.
- 휴식 완료 기록·계획/실제 시간·CSV와 과거 루틴/프로필 이름.
- PNG/Piskel·기존 펫 패키지 로딩, 픽셀 데이터 모델과 코덱.
- GLB/GIF/MP4 펫 관리·가져오기·행동 배정·저장·삭제.
- 저장하지 않은 펫 초안의 종료/업데이트 취소 보호, 공통 확인/이름/오류 대화상자.
- 타이머·알림·소리·계정/구매 잠금·업데이트의 현재 기능.

호환성 smoke는 폐기한 편집기 대신 이전 설정 fixture의 읽기/재저장과
이전 픽셀 펫 파일의 저장/재열기를 검사한다. 진행 중 세션의 루틴/프로필 스냅샷도 계속 검증한다.
삭제 기능 전용 테스트 **21개**를 정리했다. 현재 기능의 종료·업데이트 초안 검사를 펫 관리 화면으로
전환했고, 삭제 대상 파일에 섞여 있던 기록 탐색/CSV 검사 1개를 `BreakReviewTests`로 옮겨 유지했다.
공통 테스트 부트스트랩과 기존 데이터/회고 검사는 별도 파일로 보존했다.

## 자동 검사

| 검사 | 결과 |
|---|---|
| 제거 전 최신 소스 Release 전체 검사 | 755 통과, 실패 0, 건너뜀 0, 종료 0 |
| 제거 전 새 데이터 폴더의 macOS smoke | success true, PNG 106개, 종료 0 |
| 최종 Release 전체 검사 | 734 통과, 실패 0, 건너뜀 0, 종료 0 |
| 최종 Desktop Release 빌드 | 경고 0, 오류 0, 종료 0 |
| 제거 후 새 데이터 폴더의 macOS smoke | success true, PNG 100개, 종료 0 |
| 일반 번들 도구를 쓰는 미디어 집중 검사 | 5 통과, 실패/건너뜀 0, 종료 0 |
| 삭제 타입의 소스 참조·컴파일된 메타데이터 | 남은 참조/타입 0 |
| 핵심 로더·데이터·현재 펫 관리 파일 15개 | 기준 커밋과 내용 동일 |
| Assets·Art·web 파일 575개 | 기존 최신 소스와 SHA-256 동일, 누락 0 |
| 제거 전후 홈 PNG | 파일 bytes 동일 |
| 변경 문서의 로컬 링크·git diff --check | 누락/공백 오류 0 |

테스트 수 차이는 삭제한 기능 전용 21개다. 테스트 이름 변경과 현재 회고 검사 이동을 대응시켜
누락된 현재 기능 검사가 없음을 확인했다. 테스트 이관 중의 호출 연결·취소 반환값 불일치를
수정한 뒤 최종 전체 검사에 반영했다.

독립 작업트리의 ignored `.tools/media-lgpl`은 기준 작업트리의 실제 미디어 도구를
심볼릭 링크로 읽는다. 정상 Release 빌드에 ARM64 `Tools/ffmpeg`가 복사되는 것을 확인했다.
미디어 집중 검사는 `UNFOLD_FFMPEG_PATH`를 비운 상태에서 통과했다.
이 로컬 링크는 Git에 포함되지 않는다. 별도 클론에서는 기존 MediaSetup 절차로 도구를 준비한다.

재현 명령(이 작업 폴더에서 실행):

```sh
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet restore Unfold.slnx --locked-mode
UNFOLD_DATA_DIR=/tmp/<new-empty-test-directory> AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet test Unfold.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build src/Unfold.Desktop/Unfold.Desktop.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false
UNFOLD_DATA_DIR=/tmp/<new-empty-smoke-directory> dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --smoke-test
```

초기 제한 환경의 restore가 진행하지 않아 해당 프로세스만 종료하고 허용된 환경에서 복원했다.
Avalonia 빌드 로그·테스트 소켓·네이티브 렌더링 접근이 허용된 환경에서 검증했다.
`<new-empty-...>`는 예시이며 실행마다 새 빈 경로로 바꾼다.

## 실제 OS와 한계

macOS에서 네이티브 렌더링을 사용하는 화면 밖 자동 진단과 캡처를 실행했다.
홈과 설정·펫 관리의 정상/최소 크기, 휴식 완료·미루기·중지, 팩 설치/업데이트/복구,
기록/CSV와 소리 요청을 자동 검증했다. 제거 후 smoke는 루틴/프로필 편집기와 픽셀 에디터
창을 생성하지 않는다. 홈 캡처를 직접 확인했고 전후 파일도 동일했다.

실제 마우스·키보드 입력, 소리 청취, 장시간 사용, 실제 Windows, 실 OAuth/결제,
설치/업데이트 패키지와 원격 배포는 이번 검증 범위에 포함하지 않았다.
설치 앱과 공개 배포는 변경하지 않았다.

## 증거

[제거 전 smoke](2026-10-07-unused-features-baseline-smoke.json) ·
[제거 후 smoke](2026-10-07-unused-features-post-smoke.json) ·
[테스트 수와 보존 검사](2026-10-07-unused-features-tests.json) ·
[소스/자산/타입 무결성](2026-10-07-unused-features-integrity.json)

[제거 전 홈](2026-10-07-unused-features-home-before.png) ·
[제거 후 홈](2026-10-07-unused-features-home-after.png)

TRX와 격리된 진단 데이터는 `/tmp/unfold-unused-20261007/`에 있다.
임시 경로의 영구 보존은 보증하지 않는다.
