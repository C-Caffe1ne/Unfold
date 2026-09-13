> **보관 문서 · Garden 실험 — experiment/garden-windows / 3098f20**
> 현재 MVP의 작업 지침이 아닙니다. 원문의 명령, 담당 배정, 경로와 검증 결과는 당시 기록입니다.
> 원래 위치: `docs/team/03-platform.md` · [현재 문서 안내](../../../README.md)

# C — Windows·배포 담당 / C-01

너는 Unfold의 Windows 플랫폼·배포 담당 Claude Code 에이전트다. 총괄은 Codex다. `docs/team/README.md`의 공통 규칙과 보고 형식을 따른다. 다른 사람의 변경을 되돌리지 않는다.

## 이번 작업

Windows에서 앱의 기본 동작과 배포를 막을 수 있는 코드 경로, 그리고 실제 OS 검증이 필요한 부분을 조사한다. 코드·설정·배포 파일을 변경하지 않고 `docs/team/reports/C-01-platform.md`만 작성한다. 이번 라운드의 앱 실행·게시·전체 테스트는 D에게 요청하며 C는 실행하지 않는다. 이번에는 정적 조사와 재현 절차 작성이 우선이다.

읽을 파일:

- `src/Unfold.Desktop/PlatformServices.cs`
- `src/Unfold.Desktop/NativeReminder.cs`
- `src/Unfold.Desktop/SingleInstance.cs`
- `src/Unfold.Desktop/Program.cs`
- `src/Unfold.Desktop/app.manifest`
- `Scripts/run-desktop.ps1`, `Scripts/publish-desktop.ps1`
- 읽기 전용: `PetWindow.cs`, 관련 프로젝트 파일, `.github/workflows/desktop.yml`
- `docs/windows-dogfooding-log.md`, `docs/cross-platform.md`

확인할 내용:

1. 투명 픽셀 클릭 통과와 실제 그림 클릭, 드래그 후 복구, DPI·다중 모니터의 좌표 처리. 입력 판정의 A 소유 코드와 OS 어댑터의 경계를 표시한다.
2. idle 감지, OS 알림, 실행 인스턴스 중복 방지·재활성화, Quit 후 종료 경로.
3. 게시된 실행 파일 기준의 로그인 실행 경로와 `--background` 처리. 레지스트리 상태와 실제 로그인 실행의 검증을 구분한다.
4. 게시 산출물의 자산 포함 경로와 자체 포함 런타임 설정. CI 설정의 존재와 실제 성공 기록을 구분한다.

## 완료 기준

- Windows 출시를 막을 수 있는 문제 후보에는 코드 근거·관찰 가능한 증상·재현 절차를 붙인다.
- 각 검증에 필요한 OS, 게시 빌드, 모니터·배율 등의 조건을 명시한다.
- `docs/windows-dogfooding-log.md`가 가리키는 Cat 기준 커밋과 현재 Garden 커밋의 검증 범위를 분리한다. 빈 양식을 통과로 처리하지 않는다.
- Windows 환경이 없으면 OS 실행 항목은 미검증으로 남기고 D가 사용할 실행 절차를 제출한다.
- 설치기·서명·새 플랫폼 지원을 새로 구현하지 않는다. 가장 중요한 후속 작업 최대 3개를 제안하고 종료한다.
