# 숨김 파일 포함 생성물 정리 — 2026-10-06

## 환경과 범위

Git worktree 4개를 브랜치·HEAD·미커밋 상태·프로젝트 버전과 함께 재확인했다.
최신은 `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold`,
`codex/glb-import-compat`, HEAD `a1bad96`, 1.1.0-beta와 기존 미커밋 변경이다.
`release/mvp` 0.2.2를 최신 개발본으로 취급하지 않았다.

주 폴더(내부 `.worktrees/pet-pack-ux` 포함), 6bca, beta-v1-1-0를 파일 시스템에서
숨김/ignored 파일까지 열거했다. 일반 파일 34,679개, 그중 숨김 경로 파일 6,161개를
크기와 경로 기준으로 조사했다. 심볼릭 링크를 따라 트리를 순회하거나 삭제하지 않았다.
내용을 확인한 범위는 빌드 입력·설정 사용 경로이며 모든 파일 내용의 코드 감사는 아니다.
비밀 설정 값은 출력하지 않았다. Git 내부 파일은 크기 조사만 했으며 변경하지 않았다.

## 실행한 명령과 결과

삭제 전에 최신 경로에서 `dotnet test Unfold.slnx -c Release --no-restore -m:1
-p:UseSharedCompilation=false`를 실행해 **754 통과, 실패·건너뜀 0**을 확인했다.
새 임시 `UNFOLD_DATA_DIR`의 앱 smoke가 종료 코드 0, success true, 이미지 106개로 성공했다.

원본·기존 변경과 교차 확인한 153개 생성물 경로를 삭제했다.
삭제 직전 실행 중인 Unfold/dotnet/testhost/MSBuild가 없음을 확인했다.
보존한 파일과 심볼릭 링크·macOS 자동시작 실행 경로가 삭제 대상에 의존하지 않는지도 확인했다.

| 범위 | 삭제 전 | 삭제 후 | 확보 |
|---|---:|---:|---:|
| 주 폴더(숨김 작업트리 포함) | 15.44GB | 8.07GB | 7.36GB |
| 6bca 이전 작업트리 | 18.55GB | 6.37GB | 12.19GB |
| 최신 beta-v1-1-0 작업트리 | 6.01GB | 5.02GB | 0.99GB |
| 합계 | 40.00GB | 19.46GB | **20.54GB** |

GB는 10억 bytes 기준이며 `du`의 할당 크기로 비교했다.
숨김 `.worktrees/pet-pack-ux`는 정리 후 약 0.84GB(801MiB)이다.
등록된 작업트리 자체와 브랜치는 모두 유지했다.

## 정리한 것과 보존한 것

- 이전 작업트리의 bin/obj, 진단 프로브와 테스트 harness의 bin/obj.
- 실행 중이지 않은 이전 artifacts의 생성된 macOS 앱 사본.
- 재다운로드 가능한 `.tools/media-downloads`, Finder `.DS_Store`.
- 최신 src/Tests/tools의 빌드 출력은 유지해 현재 개발 작업과 충돌하지 않게 했다.
- `.git`, `.config`, `.agents`, `.claude`, `.codex`, `.github`, `.specify`는 설정·이력으로 유지.
- `.superpowers` 설계/QA 기록과 `.tools/garden-dogfood` 검증 데이터는 유지.
- `.tools/media-lgpl`은 csproj의 실제 게시 입력이므로 유지.
- 원본 자산·소스·검증 보고서/JSON/PNG·재현 소스·롤백 자료·펫 팩을 유지.
- 업데이트 feed와 설치/배포 패키지는 보존했다. 원격 배포·설치된 앱은 수정하지 않았다.

[삭제 경로](2026-10-06-hidden-cleanup-deleted.json) ·
[용량 집계](2026-10-06-hidden-cleanup-summary.json) ·
[파일 보존 검사](2026-10-06-hidden-cleanup-integrity.json)

## 경계면 검증

원본·기존 작업·Git tracked 및 non-ignored 파일 **6,366개**의 SHA-256이 모두 동일하며
누락 0개다. 제품 코드 변경 없음. 정리 후 최신 Desktop Release 빌드가
종료 코드 0, 경고·오류 0으로 통과했다.
사전 검사 당시와 같은 소스이므로 전체 테스트는 근거 없이 반복하지 않았다.
정리 후 새 프로필의 smoke도 종료 코드 0, **success true, 이미지 106개**로 통과했다.
[정리 전 smoke](2026-10-06-hidden-cleanup-baseline-smoke.json) ·
[정리 후 smoke](2026-10-06-hidden-cleanup-post-smoke.json)

로그/전체 경로 inventory는 `/tmp/unfold-hidden-cleanup-20261006/`에 있다.
임시 경로의 영구 보존은 보증하지 않는다.

## 실제 OS와 한계

macOS 네이티브 렌더링을 사용하는 화면 밖 자동 진단이며,
실제 마우스·키보드·소리 청취·장시간 사용·Windows·로그인/결제·원격 배포는 이번에 검증하지 않았다.
이전 작업트리에서 앱을 다시 빌드하려면 bin/obj가 재생성된다.
다운로드 캐시를 다시 준비할 때에는 네트워크가 필요하다.
