# Beta v1.0.4 GLB 통합·검증 — 2026-10-04

## 수정 기준

이전 GLB 작업은 기본 체크아웃 `release/mvp`의 `0.2.2`를 기준으로 구현됐다.
사용자의 최신 수정본 지시에 따라 Git worktree·브랜치·프로젝트 버전·미커밋 변경을 확인하고 아래 소스에 다시 통합했다.

- 소스: `/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold`
- 브랜치: `codex/beta-v1.0.4`
- 시작 HEAD: `f915738e5799f636e109b1a7efba3f146b40c663`
- 프로젝트 및 실행 파일: `1.0.4-beta`, `Unfold Beta v1.0.4 (1.0.4-beta)`
- 실행 파일: 위 소스의 `src/Unfold.Desktop/bin/Release/net10.0/Unfold`
- 이번 변경은 위 HEAD 위의 로컬 미커밋 변경이다. 설치 앱·공개 릴리스는 교체하지 않았다.

현재 공개 배포본은 별도 `Beta v1.0.3` 계열이며, 이전에 설치된 `/Applications/Unfold.app` 확인값은 `1.0.2 / 1000002`였다.
설치 앱 버전을 이번 기능의 실행 근거로 사용하지 않았다.
[소스·실행 파일 근거](2026-10-04-beta-glb-pets/provenance.json)에 경로·버전·어셈블리 SHA-256을 보관했다.

두 작업 폴더의 `AGENTS.md`에 앞으로의 모든 구현에서 마지막 수정본을 확인하도록 기록했다.
현재 대화 경로를 최신 소스로 가정하지 않고 worktree, HEAD, 미커밋 변경, 프로젝트 버전과 실행 경로를 먼저 확인한다.
Beta 문서 인덱스의 오래된 `release/mvp` 설명도 수정했다.

## 적용 내용

[GLB 펫 사용법](../glb-pets.md)의 가져오기·행동 지정·저장·재편집을 최신 **펫 추가 → GLB 펫**에 연결했다.

- 모델 내 애니메이션을 마우스·휴식 등 14개 상황에 연결하고 반복·속도를 지정한다.
- 루트의 이동·회전을 대기 첫 자세로 고정하고 하위 관절·표정은 재생한다. 이동 방향에 따른 이미지 반전도 차단한다.
- 투명 픽셀 클릭 판정, 창 드래그, 들어 올리기·착지, 말풍선과 숨김 동작을 기존 런타임에 연결한다.
- GLB는 사용자 라이브러리의 펫 팩 검증·설치 경로로 저장한다. 원본 파일은 변경하지 않는다.
- 작업 스레드에서 최대 20fps로 렌더링한다. 첫 프레임은 고정 캐시하고 최근 프레임 한 장만 추가로 보관한다.
- 일시정지·시각 트리 이탈 후 진행 중이던 렌더의 완료 이벤트를 버리도록 보완했다.
- STEP 보간이 정확한 키프레임 시각에 다음 값을 적용하도록 보완했다.

최신 펫 제작 화면·SVG 아이콘·설정 즉시 적용·계정/접근 코드·Velopack 업데이트 시작 처리·macOS Dock 위치 보정은
최신 소스를 유지했다. 기존 Supabase 및 브랜드 작업 파일 22개는 작업 전후 SHA-256이 모두 같았다.
기존 파일 전체를 구버전 사본으로 덮어쓰지 않고 GLB 변경 부분만 적용했다.

## 자동 검사

- 변경 전 최신 Beta 전체 Release: **587/587 통과**, 실패·건너뜀 0.
- 최초 GLB·설정·펫 제작·업데이트 집중 검사: **65개 통과**.
- 최종 기능 코드 전체 Release: **596/596 통과**, 실패·건너뜀 0.
- 루트 고정, 투명 렌더링, 모프·STEP 전환, 매핑 저장·수정·재열기, 원본 보존, 미러링 차단,
  클릭·드래그 단계·놓기·휴식 중 걷기·숨김 유지, 일시정지 중 렌더 완료 폐기 등을 확인했다.
- 기존 드래그 검사에서 실제 타이머와 수동 진행이 경합해 자세값 비교가 한 차례 실패했다.
  같은 파일의 다른 검사와 동일하게 실제 타이머를 멈춰 수동 진행 검사를 결정적으로 만들었으며 이후 전체 검사가 통과했다.

최종 TRX: `/tmp/unfold-beta-glb-results/beta-glb-final-v2.trx`.
전체 검사에는 최신 Dock 위치·펫 제작·업데이트·설정 검사도 포함된다.

## 기존 Beta 네이티브 통합 진단

기존 smoke 진단의 펫 탭 기대값이 두 개였으므로 GLB를 포함한 세 개의 이름과 가져오기 버튼을 검사하도록 갱신했다.
진단 코드 변경 후 Release 빌드는 **경고 0·오류 0**이었고, 새 프로필
`/tmp/unfold-beta104-smoke-glb-final-20261004`의 통합 진단이 **성공·종료 코드 0**으로 끝났다.
[smoke JSON](2026-10-04-beta-glb-pets/smoke.json)과 PNG **100장**을 생성했다.
타이머, 설정, 최신 펫 제작, 펫 팩 설치·수정·복구, 업데이트 대화상자, 숨김·말풍선·최소 크기 등을 검사한다.
실제 업데이트 다운로드·설치나 OS 포인터 입력 검증을 뜻하지 않는다.
최종 `git diff --check`도 통과했다.

## macOS 네이티브 GLB 진단

새 프로필 `/tmp/unfold-beta104-glb-review-final-20261004`로 현재 Beta 실행 파일을 구동했다.
[결과 JSON](2026-10-04-beta-glb-pets/result.json)의 `appVersion`은 `1.0.4-beta`, `success`는 `true`이며 프로세스 종료 코드는 0이다.

- 사용자 파일 `Kazusa.glb`: 애니메이션 24개, 삼각형 11,466개.
- 파일 경로를 주입한 가져오기 버튼, 저장·적용, 디스크 재열기 통과.
- idle/click/pickup/held/land/walk 6개 클립의 재생·일시정지·완료 1회 통과.
- 숨김 상태에서 반응을 요청해도 펫이 다시 나타나지 않음을 확인.
- 독립 편집 화면 480×560·860×680, 통합 설정 화면 1120×800·860×680·640×560의 실제 ClientSize와 가로 넘침 없음 확인.
- 초기 통합 화면 진단의 탭 탐색 실패는 화면 생성·레이아웃 대기를 추가해 해결했다.

[최신 설정 화면](2026-10-04-beta-glb-pets/settings-glb-1120-800.png) ·
[최소 크기 화면](2026-10-04-beta-glb-pets/settings-glb-640-560.png) ·
[최소 크기 행동 지정](2026-10-04-beta-glb-pets/settings-mappings-640-560.png) ·
[펫 렌더링](2026-10-04-beta-glb-pets/live-pet.png)

## 확인 범위

네이티브 진단은 macOS 그래픽 세션의 화면 밖 창에서 실행한 버튼 이벤트와 렌더링 검사다.
실제 OS 마우스로 창을 드래그한 결과, Windows 실기, 다중 모니터·DPI, 장시간 CPU·메모리는 이번에 검증하지 않았다.
24개 모든 클립의 외형 검수나 모든 GLB 형식 지원을 의미하지 않는다. 지원 형식과 렌더링 제한은 사용법에 명시했다.
Kazusa 원본 GLB는 저장소·배포물에 추가하지 않았다.

## 재실행

반드시 위 최신 소스 폴더에서 실행한다.

```sh
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  dotnet test Unfold.slnx -c Release --no-restore -m:1

# 각각 새 빈 프로필을 지정하고 macOS 그래픽 세션에서 실행한다.
UNFOLD_DATA_DIR=/tmp/unfold-glb-new \
  src/Unfold.Desktop/bin/Release/net10.0/Unfold --review-glb /path/to/model.glb
UNFOLD_DATA_DIR=/tmp/unfold-smoke-new \
  src/Unfold.Desktop/bin/Release/net10.0/Unfold --smoke-test
```
