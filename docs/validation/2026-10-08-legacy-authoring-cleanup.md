# 과거 편집 기능 파일 정리 · 2026-10-08

## 기준과 소유 범위

- 최신 작업 경로: `/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features`.
- 브랜치 `codex/remove-unused-features`, 기준 HEAD `dda83e6750eef4ceceb8db85b82c49c778de34b8`, 버전 `1.1.1-beta`.
- 공개 배포 소스 `887d0127e57e5145062d0d558fb71b113284542d`와 `src/`·`Assets/` 차이가 없는 것을 확인했다. 다른 작업트리의 기존 변경은 보존한다.
- 사용자의 정리 요청에 따라 제거된 편집 기능의 문서·캡처와 해당 호환 코드만 정리한다. 현재 펫 관리·알림·타이머·계정·업데이트·기존 사용자 데이터는 변경하지 않는다.

## 사전 코드 검사와 위험

| 관련 파일 | 생산자·소비자 확인 | 결정 |
|---|---|---|
| Piskel 코덱·픽셀 모델 | `CharacterLibrary.Save/OpenForEditing` → 격리 진단과 펫 재생·복구 회귀 fixture | 저장·재열기 계약 보존, Compatibility로 분리 |
| 기존 루틴·업무 프로필 | AppSettings 읽기/복구/저장 → BreakSession/알림 → BreakHistory/CSV | 타입·JSON 이름·스냅샷·기존 선택 유지 |
| 과거 Swift/Piskel 편집 문서와 루틴·프로필 PNG | 프로젝트 빌드 입력과 현재 실행 코드에서 참조 없음 | 28개 제거, 연결된 문서 링크 정리 |
| `PiskelCodec.ImportPng` | 전체 소스·도구·테스트에서 호출 없음; 현재 미디어 입력은 ImageCodec/CustomPetDraft 사용 | 사용하지 않는 메서드 제거 |

코덱·설정 모델을 통째로 삭제하면 기존 진단과 저장·복구 검증이 깨진다. 해당 계약은 유지한다.
코드 검사에서 현재 문서의 “진단용 C# 편집기 유지” 및 루틴·프로필 편집 안내가 제거된 구현과 다른 것을 확인해 함께 수정했다.
삭제한 파일은 위 기준 커밋의 Git 이력에서 복구할 수 있다. 현재 `.piskel` 원본이나 사용자 라이브러리를 삭제하지 않는다.

## 변경

- 폐기된 편집 설계·계획·인수인계/분석 문서 14개와 루틴·업무 프로필 화면 이미지 14개 제거: 합계 901,335 bytes.
- PiskelCodec, PixelDocument, AppSettings의 루틴·프로필 검증을 `src/Unfold.Core/Compatibility/`로 이동. 타입·네임스페이스·저장 형식 유지.
- CharacterLibrary의 이전 픽셀 원본 저장·재열기·revision 연산을 같은 폴더의 partial 파일로 옮겼다. 메서드 본문은 동일하다.
- 참조가 없는 PNG 가져오기 메서드를 제거하고 호환 코드의 역할을 README에 기록했다.
- 현재 안내와 삭제된 캡처 링크를 갱신했다. 당시 검증 기록·현재 회고/CSV·활성 캐릭터 자산은 유지한다.

## 검증

- 사전 전체 Release 빌드·회귀 검사: **763/763 통과**, 실패·건너뜀 0, 1분 50초.
- 명령: `dotnet test Unfold.slnx -c Release --no-restore --logger 'trx;LogFileName=pre-cleanup.trx' --results-directory /private/tmp/unfold-legacy-cleanup-lnn12ua1/pre-results`.
- 테스트는 기존 TempDirectory/RuntimeScope의 격리 데이터 사용. 사용자 프로필에서 진단하지 않는다.
- 사전 입력 해시·삭제 목록·기존 문서 링크 상태: `/private/tmp/unfold-legacy-cleanup-lnn12ua1/pre-inventory.json`.
- 정리 후 전체 Release 빌드·회귀 검사: **763/763 통과**, 실패·건너뜀 0, 1분 39초. 경고를 오류로 처리한 빌드 포함.
- 정리 후 명령: 새 `UNFOLD_DATA_DIR=/private/tmp/unfold-legacy-cleanup-lnn12ua1/post-data`에서 `dotnet test Unfold.slnx -c Release --no-restore` 실행. TRX 원본은 같은 임시 폴더의 `post-results/post-cleanup.trx`.
- 코드 계약 검사 5개 통과: 픽셀 모델, 설정 모델, 사용하지 않는 메서드를 제외한 Piskel 코덱, 라이브러리 이전 원본 연산 및 현재 로딩 연산의 본문 동일성.
- 소유 파일 4개 외 빌드 입력 **245개 SHA-256 동일**: 기존 펫 자산·테스트·실행 스크립트·도구 보존.
- 로컬 Markdown 링크 **988개 확인**, 새로 깨진 링크 0. 사전부터 존재한 관련 없는 과거 링크 19개는 범위 밖으로 보존.
- `git diff --check` 통과.

## macOS native 통합 진단

새 `UNFOLD_DATA_DIR=/private/tmp/unfold-legacy-cleanup-lnn12ua1/native-data`에서 이 작업트리의
`src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --smoke-test`를 실행했다.
종료 코드 **0**, `smoke.json`의 **success: true**, PNG **100장**을 확인했다.

- 기존 루틴·업무 프로필 JSON 저장/복원과 이전 픽셀 펫 원본 저장/재열기 통과.
- 현재 설정·최소 창, 타이머·알림·숨김 펫, 완료 기록/CSV, 펫 팩 설치·업데이트·손상 복구 통과.
- macOS 실제 Avalonia native 백엔드의 off-screen 자동 진단이다. 실제 마우스/키보드 조작 증거는 아니며 `nativeCaptionInputVerified=false`를 유지한다.
- [최종 결과와 삭제 파일 원장](evidence/2026-10-08-legacy-authoring-cleanup/result.json)에 전후 수치·해시·계약 확인을 기록했다. 원본 TRX·smoke JSON·PNG는 위 임시 증거 폴더에 있다.

## Beta v1.1.2 개발 기준 전환

정리 검증 이후 사용자의 지시에 따라 후속 개발 버전을 `1.1.2-beta`로 지정했다.
프로젝트 버전과 현재 개발 안내·작업 지침을 갱신했으며, 이번 커밋에 정리 결과와 버전 전환을 함께 포함한다.
위 763개 전후 검사와 macOS native 진단 및 JSON 원장은 버전 전환 전 `1.1.1-beta`에서 수집한 증거다.
공개 설치본과 업데이트 채널은 `v1.1.1-beta`를 유지하며, 새 설치본 배포·설치된 앱 교체는 수행하지 않는다.

- 버전 전환 후 Release 빌드 및 `AppReleaseTests`·`AppUpdateTests`: **30/30 통과**, 실패·건너뜀 0.
- 빌드된 `Unfold.dll`에서 `AppRelease.Version`과 `DisplayVersion`을 실제로 읽어 **`1.1.2-beta` / `Beta v1.1.2`**를 확인했다. AssemblyVersion은 `1.1.2.0`이다.
- [버전 전환 검증 결과](evidence/2026-10-08-legacy-authoring-cleanup/version.json)에 추가 검사 결과와 증거 경로를 기록했다.


## 검증 경계

전체 자동 검사의 통과를 모든 OS에서 버그가 없다는 보장으로 해석하지 않는다.
실제 Windows 실행·물리 입력, Google 인증/결제, OS 파일 선택기와 장시간 자원 측정은 이번 파일 정리의 검증 범위에 포함하지 않는다.
