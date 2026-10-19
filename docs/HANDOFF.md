# Unfold 최신 작업 인계 진입점

2026년 10월 14일 · 이 문서는 이전 루트 체크아웃에서 최신 작업트리를 찾기 위한 안내다.

**다른 세션·IDE에서 이어갈 기준 문서는 [최신 작업 인계](../.worktrees/pet-overlay-hover/docs/HANDOFF.md)다.**
최신 [문서 색인](../.worktrees/pet-overlay-hover/docs/README.md),
[작업 규칙](../.worktrees/pet-overlay-hover/AGENTS.md),
[최신화 기록](../.worktrees/pet-overlay-hover/docs/validation/2026-10-14-handoff-refresh.md)을 함께 확인한다.

| 구분 | 확인한 상태 |
|---|---|
| 현재 루트 | `release/mvp`, `ce0ccda`, csproj `0.2.2` |
| 최신 개발 경로 | `/Users/hwanghyeonseong/Developer/GitHub/Unfold/.worktrees/pet-overlay-hover` |
| 최신 개발 | `codex/pet-overlay-hover`, `f9953a060deb76ea089e9442b9cf94ea12a78c73`, `1.1.4-beta` |
| 확인한 원격 | `codex/pet-overlay-hover`의 `4a668fd` |
| 최신 공개 | [v1.1.4-beta](https://github.com/C-Caffe1ne/Unfold/releases/tag/v1.1.4-beta), 제품 소스 `4c64bfc` |

`f9953a0`는 배포 이후 휴식 카운트다운 숫자 전환을 수정한 **로컬 전용 커밋**이다.
이번 인계 문서와 증거도 미커밋이다. 새로 clone하거나 다른 컴퓨터로 옮길 때
현재 작업트리의 로컬 커밋·문서·미추적 증거를 함께 보존한다.
이 루트의 `src/`에서 최신 제품을 구현하거나 루트의 과거 기능 설명을 새 앱의 상태로 해석하지 않는다.

같은 Mac에서는 아래 경로를 IDE 폴더로 열고 해당 폴더의 `Unfold.slnx`를 사용한다:

```sh
cd /Users/hwanghyeonseong/Developer/GitHub/Unfold/.worktrees/pet-overlay-hover
git worktree list
git branch --show-current
git rev-parse HEAD
git status --short
dotnet --version
```

전체 재개 명령·PowerShell·데이터 계약·코드 지도·검증 수치·보류 항목은 최신 인계에 있다.
경로·SHA는 확인 당시의 값이므로 다음 작업에서는 다시 대조한다.
다른 컴퓨터에 `.worktrees/`가 없으면 최신 브랜치와 로컬 전용 제품 커밋을 먼저 확보한 뒤
그 소스 안의 `docs/HANDOFF.md`를 읽는다.
