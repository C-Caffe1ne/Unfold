# Codex·Claude Code 스킬과 지침 검사 — 2026-09-13

이 문서는 이 컴퓨터의 설치 상태를 기록한 **검사 보고서**다. 현재 작업 지침이나
설정 변경 승인이 아니다. 검사 중 앱 코드, 스킬, 전역 설정, 훅, 메모리는 수정하지 않았다.

## 판단 기준과 범위

사용자가 지정한 [OpenAI 글](https://developers.openai.com/blog/rethinking-skills-and-prompts-for-gpt-6-astra)을 읽고 다음 기준을 적용했다:
스킬의 실행 조건을 좁히고, 필요한 내용을 선택해서 읽도록 구성하며, 반복 승인과
고정 절차를 작업의 실제 위험·규모에 맞춘다. 완료 조건은 명확히 두되 불필요한 중간
정지를 줄인다. Astra에 대한 권고를 Claude 모델의 성능 보장으로 확대하지 않는다.

대상은 Unfold와 상위 경로의 `AGENTS.md`·`AGENTS.override.md`·`CLAUDE.md`·
`CLAUDE.local.md`, 사용자 스킬 경로, Codex·Claude 플러그인 캐시, 관련 설정·명령·훅이다.
다른 프로젝트 전체, 원격 호스트, 모든 과거 대화와 캐시의 모든 보조 문서는 대상이 아니다.

| 조사 항목 | 확인 결과 |
|---|---|
| Unfold 프로젝트 지침 | `AGENTS.md`와 `CLAUDE.md` 없음. 조사한 상위 경로에도 적용할 본문 없음 |
| Codex 사용자 지침 | `~/.codex/AGENTS.md`는 0바이트. `AGENTS.override.md` 없음 |
| Claude 사용자 지침 | `~/.claude/CLAUDE.md`와 사용자 `rules/` 없음 |
| 일반 검색으로 수집한 `SKILL.md` | `.agents/skills` 77, `.codex/skills` 7, `.claude/skills` 5, Codex 캐시 85, Claude 캐시 20: 총 194개 경로 |
| 무시된 생성본까지 포함한 파일 목록 | 총 671개 경로. 추가 477개는 gstack 안의 9개 도구별 생성 트리 각 53개 |
| 실제 상세 검사 | 194개 파일의 이름·설명·분량·강제 표현을 정적으로 분석하고, 문제 후보 본문과 연결 경로를 확인. 추가 생성본은 목록·경로 참조를 확인 |
| 도구 버전 | 로컬 CLI: Codex 0.154.0, Claude Code 2.1.270. 데스크톱 호스트 버전과 동일하다고 가정하지 않음 |

**파일 수는 활성 스킬 수나 시작 시 로드되는 본문 수가 아니다.** 플러그인 예제,
다른 클라이언트의 복사본, 무시된 생성본이 포함된다. Codex의 기본 목록은 이름·설명·경로를
제공하고 선택된 스킬의 본문을 읽는다. 동일 이름도 합쳐지지 않을 수 있다.
[Codex 스킬 문서](https://learn.chatgpt.com/docs/build-skills)

## 우선 수정할 항목

### 1. gstack 실행 경로 불일치 — 높음, 파일 검사로 확인

일반 검색의 gstack 스킬 58개 중 **52개**에 `~/.Codex/skills/gstack` 참조가 있다.
현재 설치 위치는 `~/.agents/skills/gstack`이며, `~/.Codex/skills/gstack`와
`~/.codex/skills/gstack` 모두 없다. 대소문자 문제만으로 설명되지 않는다.

예: [review/SKILL.md:36](/Users/hwanghyeonseong/.agents/skills/gstack/review/SKILL.md:36)의
업데이트 확인, 설정 조회, 타임라인 기록 등이 없는 경로를 사용한다. 오류를 숨기는
fallback이 많아 기능 누락이 정상 기본값처럼 보일 수 있다. 명령은 실행하지 않았다.

권고: 실제 설치 위치를 사용하는 경로 해석을 한 곳에서 관리한다. 파일에 자동 생성
표시가 있고 `SKILL.md.tmpl`·`scripts/gen-skill-docs.ts`가 존재하므로 생성 경로부터
수정하고 재생성한다. 52개 출력 파일만 일괄 치환하면 재생성 때 되돌아갈 수 있다.

### 2. GSD의 안내·명령과 본문 설치가 분리됨 — 높음, 파일 검사로 확인

Codex에 노출된 `source-command-gsd-*` 스킬은 9개다.
[workflow 안내:19](/Users/hwanghyeonseong/.agents/skills/source-command-gsd-ns-workflow/SKILL.md:19)는
16개 대상 스킬로 연결하지만, 해당 이름의 대상은 조사한 Codex 스킬 경로와 현재
세션의 스킬 목록에 없다. 마지막에는 이 환경에 없는 이름인 `Skill tool`을 요구한다.

Claude에는 `~/.claude/commands/gsd-*.md` 71개가 남아 있다. 그중 **58개 명령**에서
없는 `~/.claude/gsd-core/` 파일을 가져오며, 누락된 고유 import 대상은 **95개**다.
예: [gsd-execute-phase.md:35](/Users/hwanghyeonseong/.claude/commands/gsd-execute-phase.md:35),
[gsd-plan-phase.md:33](/Users/hwanghyeonseong/.claude/commands/gsd-plan-phase.md:33).
이 컴퓨터에는 양쪽의 GSD 에이전트 정의도 각 34개 남아 있다.

권고: GSD를 계속 쓸 경우 명령·본문·도구 연결을 같은 설치 기준으로 복구하고 각
클라이언트에서 확인한다. 쓰지 않을 경우 노출된 진입점부터 비활성화한다.
명령 파일이 있다는 이유로 설치가 완전하다고 판정하거나, 보호 훅까지 함께 지우지 않는다.

### 3. Superpowers의 광범위한 적용·승인·TDD 강제 — 높음, 예상 동작 영향

- [using-superpowers:10](/Users/hwanghyeonseong/.codex/plugins/cache/superpowers-marketplace/superpowers/6.3.0/skills/using-superpowers/SKILL.md:10):
  적용 가능성이 1%여도 스킬을 사용하도록 요구하고 모든 응답 이전에 적용한다.
- [brainstorming:14](/Users/hwanghyeonseong/.codex/plugins/cache/superpowers-marketplace/superpowers/6.3.0/skills/brainstorming/SKILL.md:14):
  작은 변경과 타당성 조사까지 구현 전 승인을 요구한다. 애매하면 절차를 늘리고 줄이지 않는다.
- [test-driven-development:18](/Users/hwanghyeonseong/.codex/plugins/cache/superpowers-marketplace/superpowers/6.3.0/skills/test-driven-development/SKILL.md:18):
  리팩터링까지 항상 TDD를 요구하고 설정 파일 예외도 질문하게 한다. 테스트보다 먼저
  작성한 코드를 삭제하고 다시 시작하라는 절차도 있다.

이 규칙들은 이번 요청에서 사용자가 밝힌 검사를 넘어 실행하지 않았다. 지침의 존재와
반복 승인·불필요한 재작업 가능성은 확인되지만, 실제 지연 시간은 측정하지 않았다.

양쪽 Superpowers 패키지의 `hooks/hooks.json`에는 SessionStart 연결이 있고,
`hooks/session-start`는 `using-superpowers` 본문을 읽어 문맥에 넣는다. 특히 Claude는
사용자 설정에서 플러그인이 활성화돼 있다. 따라서 스킬의 자동 발동 메타데이터만
바꾸는 것으로 시작 시 지침 주입까지 해제된다고 보장할 수 없다. 이번 검사에서는
새 Claude 세션을 실행해 실제 주입을 추적하지 않았다.

권고: 설계 탐색·TDD·단계별 검토는 명시적으로 선택하는 작업 방식으로 둔다.
요청받은 가역적 수정과 영향 범위 검증은 완료까지 이어가고, 중요한 미정 요구사항이나
아직 승인되지 않은 외부 영향에서 질문한다. 실패를 재현하는 회귀 테스트와 검증 증거는 유지한다.

### 4. 큰 본문과 긴 설명 — 중간, 분량 확인·성능 영향 미측정

| 스킬 | 본문 규모 | 개선 방향 |
|---|---:|---|
| [gstack/spec](/Users/hwanghyeonseong/.agents/skills/gstack/spec/SKILL.md) | 2,275줄 / 117,208바이트 | 공통 시작 절차, 질문 형식, 단계별 가이드를 분리 |
| [gstack/review](/Users/hwanghyeonseong/.agents/skills/gstack/review/SKILL.md) | 1,810줄 / 100,866바이트 | 리뷰 실행과 업데이트·설정 소개·질문 형식을 분리 |
| [hatch-pet](/Users/hwanghyeonseong/.codex/skills/hatch-pet/SKILL.md) | 923줄 / 85,522바이트 | 생성·방향 QA·수리·패키징을 선택해 읽는 구조로 분리 |

긴 파일 자체가 결함인 것은 아니다. gstack의 리뷰 본문은 실제 리뷰 외에도 업데이트,
텔레메트리 선택, 기능 소개, 긴 질문 양식을 포함한다. 이들은 반복 사용 시 분리할 가치가 크다.

`orchestration` 설명은 원문 893자, `orca-cli`는 661자, `archify`는 652자다.
Unity 스킬 설명에도 900자 이상이 여러 개 있다. 문자 수는 토큰 수와 다르다.
발동 조건을 앞부분 한두 문장으로 두고 옵션·출력 형식·유사 도구 비교는 본문으로 옮긴다.
현재 앱 작업과 관련 없는 전문 스킬은 삭제보다 명시 호출 또는 프로젝트별 노출을 고려한다.

### 5. 이름과 클라이언트 계약 충돌 — 중간, 파일 검사로 확인

두 파일이 모두 `name: qa`다:
[gstack/qa](/Users/hwanghyeonseong/.agents/skills/gstack/qa/SKILL.md:2)는 웹 앱을 검사하고 수정하며,
[개인 qa](/Users/hwanghyeonseong/.agents/skills/qa/SKILL.md:2)는 HTML·CSS·JS 정합성 검사다.
이번 Codex 세션에도 두 항목이 같은 이름으로 보인다. `web-qa-static`처럼 역할을
드러내는 이름과 서로 다른 실행 조건을 권한다.

[web-orchestrator:3](/Users/hwanghyeonseong/.agents/skills/web-orchestrator/SKILL.md:3)는
일반적인 수정·업데이트 표현을 넓게 받는다. 단일 파일 수정 예외는 있지만 기능 추가에는
4개 에이전트를 기본 배정한다. [63행](/Users/hwanghyeonseong/.agents/skills/web-orchestrator/SKILL.md:63)의
`TeamCreate`·`TaskCreate`와 고정 `opus` 모델은 현재 Codex 도구 계약과 맞지 않는다.
명시적인 팀 개발 요청으로 범위를 좁히고, 모델·도구 연결은 클라이언트별 어댑터로 분리한다.

`orca-cli`·`orchestration`·`computer-use`는 Orca 실행 파일에 의존한다. 이번 셸에는
`orca`가 없고 `ORCA_CLI_COMMAND`·`ORCA_DEV_REPO_ROOT`도 없다. Orca 전체가 제거됐다는
증거는 아니지만 현재 환경에서 일반적인 인계·컴퓨터 조작 요청에 자동 선택할 근거는 약하다.
Orca를 명시한 경우에만 선택하도록 설명을 좁힌다.

### 6. Unfold의 펫 형식과 Codex 펫 형식 구별 — 중간, 예방 목적

[hatch-pet:10](/Users/hwanghyeonseong/.codex/skills/hatch-pet/SKILL.md:10)은 Codex v2 펫의
8×11 아틀라스·9개 동작·16개 시선 방향을 요구한다. Unfold의 현재 캐릭터 소비 계약은
`Assets/Characters/default-cat/character.json`과 `src/Unfold.Core/CharacterLibrary.cs`에 있다.
두 형식은 같은 계약이 아니다.

권고 설명: “Codex 앱용 v2 펫 생성·수리·검증에 사용한다. 다른 앱의 캐릭터 자산은
해당 앱의 로더와 manifest를 먼저 확인한다.” Unfold 자산 제작에 16개 시선 방향을
의무로 가져오면 제작 부담이 불필요하게 커질 수 있다. 과거 제작 어려움의 원인으로
확정한 것은 아니다. 형식 검사·투명도 확인·실제 애니메이션 QA는 유지한다.

## AGENTS.md·CLAUDE.md 권고

현재는 지침 파일이 비대하지 않다. 필요한 것은 **짧은 프로젝트 진입점**이다.
앞선 문서 정리로 `docs/README.md`에 역할이 정리돼 있으므로 이를 다시 복제하지 않는다.
Codex는 비어 있는 사용자 지침 파일을 건너뛴다.
[Codex 지침 로딩 문서](https://learn.chatgpt.com/docs/agent-configuration/agents-md)

아래는 **미적용 초안**이다. 실제 `AGENTS.md`를 만들거나 설정하지 않았다.

```markdown
# Unfold 작업 안내

- 현재 체크아웃의 코드와 빌드 입력으로 구현 상태를 확인한다.
- 문서 위치는 docs/README.md에서 찾는다. archive/와 reference/는 현재 실행 지침이 아니다.
- 제품 범위가 필요한 작업은 docs/mvp.md, 설치·게시 작업은 docs/cross-platform.md를 참고한다.
- 검증 방법과 한계는 docs/verification.md를 참고한다.
- 캐릭터 자산을 바꿀 때는 현재 CharacterLibrary와 manifest 계약을 확인한다.
- 요청받은 변경은 필요한 검증과 발견된 회귀 수정까지 완료한다.
- 로컬 검증은 격리된 테스트 데이터를 사용한다. 진단 실행에는 새 UNFOLD_DATA_DIR을 쓴다.
- 변경 범위에 맞는 검사를 선택하고, 새 근거 없이 통과한 검사를 반복하지 않는다.
- 실제 OS에서 확인한 사실과 자동 검사 결과를 구별해 보고한다.
- 사용자의 기존 변경을 보존한다. 중요한 미정 요구사항과 승인되지 않은 외부 영향은 질문한다.
```

Claude는 `AGENTS.md`를 직접 읽지 않으므로, 적용한다면 `CLAUDE.md`에는
`@AGENTS.md` 한 줄로 공유할 수 있다. 문서 전체를 각각 복사하지 않는다.
[Claude 지침 문서](https://code.claude.com/docs/en/memory#agentsmd)

## 유지할 것과 설치 차이

- 로컬 `code-review`·`doc-writing`·`qa`·`web-dev`는 70~103줄이며 검사 대상이 비교적
  구체적이다. 강한 발동 문구와 이름은 손보되 검사 내용을 전부 없앨 이유는 없다.
- `.agents/skills`와 `.claude/skills`의 개인 웹 스킬 5개는 내용이 동일하다. 서로 다른
  클라이언트 경로에 있다는 사실만으로 이중 실행이라고 판단하지 않는다. 수정 시 동기화 정책이 필요하다.
- Superpowers는 Codex 활성 캐시가 6.3.0, Claude 설치 레지스트리는 5.1.0이다.
  Claude의 5.1.0도 승인 절차가 강하다. 업데이트만으로 절차 충돌이 해결된다고 보지 않는다.
- `~/.codex/config.toml` 기본 모델은 `gpt-5.6-sol`이다. 현재 작업에 실제 사용되는
  모델을 이 설정만으로 추정하지 않았고, Astra로 자동 변경하지 않았다.
- 전역 Claude 설정의 GSD graphify·worktree 보호 훅은 실제 파일이 존재한다.
  graphify는 프로젝트 설정의 명시적 opt-in, worktree 보호는 작업 트리 밖 쓰기 차단이라는
  구체적 조건이 있다. 글의 간소화 권고를 이유로 함께 제거할 대상이 아니다.
- 홈의 `.claude/settings.local.json`은 존재하지만 일반적인 Unfold 세션의 사용자
  설정과 같은 것으로 보지 않는다. 사용자 설정은 `~/.claude/settings.json`, 프로젝트
  로컬 설정은 프로젝트의 `.claude/settings.local.json`이다.
  [Claude 설정 범위](https://code.claude.com/docs/en/settings)

## 적용 순서와 검증 방법

1. gstack 경로와 GSD 누락 import부터 복구하거나 사용하지 않는 진입점을 비활성화한다.
2. Superpowers의 자동 적용 범위와 반복 승인 경계를 정하고, 웹 팀 작업의 고정 도구·모델을 분리한다.
3. `qa` 이름을 구별하고, 긴 설명·본문을 작업별로 나눈다.
4. 프로젝트에 위와 같은 짧은 지침과 Claude 공유 import를 적용한다.
5. 같은 작업을 새 세션에서 비교해 불필요한 스킬 호출·질문·재검사 수와 결과 품질을 확인한다.

대표 비교 작업은 README 오타 수정, 재현 가능한 타이머 버그 수정, 캐릭터 manifest 검토,
명시적인 설계 토론이다. 작은 수정에서는 팀·설계 문서·반복 승인이 불필요하게 생기지
않는지, 버그 작업은 실제 재현·검증까지 끝나는지, 설계 요청에는 필요한 질문이 남는지 본다.
이 비교 실험은 아직 실행하지 않았다.

설정 변경 시에는 클라이언트별 공식 방식을 사용한다. Codex는 `[[skills.config]]`로
파일을 삭제하지 않고 비활성화하거나 `agents/openai.yaml`의
`policy.allow_implicit_invocation: false`로 명시 호출만 허용할 수 있다.
[Codex 스킬 제어](https://learn.chatgpt.com/docs/build-skills)
Claude는 `disable-model-invocation: true` 또는 지원되는 `skillOverrides`로
호출 범위를 제한할 수 있다. 플러그인 시작 훅은 스킬 메타데이터와 별도로 확인해야 한다.
[Claude 스킬 제어](https://code.claude.com/docs/en/skills)

검사는 읽기와 정적 파일 비교로 수행했다. 감사 대상 스킬·훅·GSD 명령을 실행하지
않았고, 다른 에이전트를 시작하거나 플러그인을 설치·삭제하지 않았다.
