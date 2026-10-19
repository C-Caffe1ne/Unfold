# GIF·사진 바로 저장·적용과 펫 팩 기본 파일명

2026-10-18 · 요청한 저장 흐름과 관련 회귀만 확인했다.

## 기준과 범위

- 작업트리: `/Users/hwanghyeonseong/Developer/GitHub/Unfold/.worktrees/pet-overlay-hover`
- 브랜치·시작 HEAD·프로젝트: `codex/pet-overlay-hover`, `5d0f86e`, `1.2.0`
- 원격 추적 브랜치와 시작 HEAD 차이: `0 / 0`. 공개 최신 정식 릴리스는 GitHub에서 `v1.2.0`, 제품 소스 `99db377`로 확인했다.
- 모든 후보 작업트리의 브랜치·HEAD·미커밋 변경·csproj 버전을 비교했다. 루트 `release/mvp`는 `0.2.2`이며 편집하지 않았다.
- 기존 로그인 관련 수정·미추적 파일을 보존했다. 실행 중인 `/Applications/Unfold.app/Contents/MacOS/Unfold`는 교체하거나 재시작하지 않았다.

## 원인과 변경

신규 GIF·사진은 UI와 `CustomPetDraft.Save` 모두 기존 설치본 편집만 허용하여 바로 저장할 수 없었다. 신규 초안도 검증된 팩을 앱 라이브러리에 설치하고 기존 `SelectInstalledCharacter` 콜백으로 적용하도록 연결했다. 이름과 필수 기본 동작이 있어야 저장할 수 있다. 외부 파일 저장 창이나 추가 설치 확인은 열지 않는다.

최초 저장 후에는 설치본의 ID·revision을 가진 편집 초안으로 전환한다. 재저장은 같은 펫을 갱신하며 콘텐츠 버전을 올린다. 적용 콜백이 성공한 뒤 변경 상태를 해제하므로, 설치 또는 적용에 실패해도 초안을 유지하고 재시도할 수 있다. 편집·삭제 버튼은 저장된 펫에만 표시한다.

팩 내보내기 기본 파일명은 GIF·사진의 현재 이름 입력과 GLB 초안의 현재 이름에서 만든다. 예를 들어 `모찌`는 `모찌.unfoldpet`으로 제안한다. 한글·공백을 보존하고 경로 구분자·금지문자·후행 점·Windows 예약명 및 중복 확장자를 보정한다.

수정 파일:

- `src/Unfold.Core/CustomPetDraft.Editing.cs`
- `src/Unfold.Core/CustomPetDraft.cs`
- `src/Unfold.Desktop/CustomPetWindow.cs`
- `src/Unfold.Desktop/GlbPetView.cs`
- `Tests/Unfold.Tests/PetDirectSaveTests.cs`

## 자동 검사

.NET SDK `10.0.401`, Release 빌드, 새 임시 `UNFOLD_DATA_DIR` 및 각 테스트의 격리 프로필을 사용했다. 최초 샌드박스 실행은 MSBuild 로컬 파이프 권한 오류로 중단되어 동일 범위를 허용된 환경에서 실행했다. 새 테스트의 using/내부 메서드 접근 컴파일 오류는 테스트 코드에서 수정했다.

| 검사 | 최종 결과 |
|---|---|
| 신규 회귀 `PetDirectSaveTests` | 고유 22개 통과 |
| 기존 저장·편집·팩 내보내기·최소 창 관련 회귀 | 고유 59개 통과 |
| 합계 | 고유 81개 통과, 실패·건너뜀 0 |
| Release 빌드 | Core·Desktop·Tests 빌드 성공 |
| `git diff --check` | 통과 |

신규 22개 중 16개가 첫 실행에서 통과했다. 나머지 6개는 이름 변경 직후 Avalonia의 `TextChanged` 이벤트 처리 전에 버튼 활성화를 검사해 실패했다. 실제 이벤트 처리를 기다리도록 테스트를 수정하고 해당 6개와 기존 회귀 59개를 실행하여 65개가 통과했다. 이미 통과한 16개는 반복하지 않았다.

검사 범위:

- GIF·PNG·JPG·JPEG·WebP·BMP 신규 가져오기 → 저장 → 실제 런타임 선택 콜백 및 `settings.json` 선택 ID 저장.
- 픽셀·프레임 수·프레임 시간·핑퐁 설정, 원본 파일 보존, 목록 즉시 갱신과 선택.
- 같은 ID 재저장과 콘텐츠 버전 `1.0.1`, 저장 후 변경 상태 해제.
- 라이브러리 잠금으로 설치 실패 후 재시도, 적용 콜백 실패 후 중복 설치 없이 재시도.
- 공용 파일명 변환 12개, 기존 GIF·GLB 내보내기와 취소·실패 시 초안 보존, 최소 창 배치.

실행 결과는 `/tmp/unfold-pet-save-results/pet-direct-save.trx`와 `/tmp/unfold-pet-save-results/pet-save-regression.trx`에 있다. 앞 파일에는 수정 전 6개 실패가 포함되며, 두 번째 파일의 해당 6개 통과가 이를 대체한다.

## 검증 경계

실제 macOS·Windows 파일 선택 창의 표시/입력, 실제 데스크톱 창 조작, 전체 테스트·앱 OS 진단, 설치 앱 교체와 배포는 수행하지 않았다. 자동 UI 검사는 Avalonia headless에서 실행했다. 기본 파일명은 공용 함수 검사와 두 편집기의 picker 인수 연결을 검토했으며, 네이티브 저장 창을 직접 조작한 증거는 아니다.

코드 변경은 미커밋 상태다.
