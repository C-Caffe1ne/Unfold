# 휴식 동료 1차 구현 검증

2026-09-13 · `release/mvp`, 기준 커밋 `97f9a84` 위의 현재 작업 트리.
기존 C# 전환·문서 정리 변경에 이번 기능을 적용했다. 커밋·외부 배포는 수행하지 않았다.
macOS arm64, .NET SDK 10, 게시 앱 런타임 10.0.12에서 확인했다.

## 구현과 확인 결과

| 대상 | 구현 | 확인 |
|---|---|---|
| 휴식 루틴 | 기본 3개, 직접 편집하는 내 루틴 1개(최대 3단계·10분), 다음 초대부터 적용 | 설정 저장·재로드, 잘못된 사용자 루틴의 기본값 복구, 실제 편집 UI 저장 테스트 |
| 세션 | 시작·진행·확인 대기·완료·미루기·건너뛰기 상태, 초대 중 작업 타이머 보류 | 중복 시작·중복 완료 거부, 세션의 루틴 사본 고정, 역행 시각·10초 초과 갭 처리 |
| 완료 기록 | 확인 버튼을 눌러야 기록, 오늘 횟수·계획 시간 합계, 로컬 원자적 저장 | 닫기·미루기 미기록, 중복 방지, 재로드, 날짜별 집계, 손상 파일 보존 |
| 펫 반응 | 초대 attention, 시작 stretch, 완료 celebrate, 클릭 click을 정확한 키로 선택 | 없는 키는 현재 동작 유지. 기존 Mochi는 idle/stretch만 제공 |
| 자산 검사 | 실제 디코더, 안전한 경로, 파일 해시, 프레임·투명도·메모리 검사 | 손상 GIF, 빈 디렉터리, 시트 디코딩 메모리 한도 테스트와 실제 Mochi 검사 |
| 게시 | macOS publish 디렉터리를 비워 제거된 펫이 다음 번들에 남지 않도록 수정 | 스크립트 문법 검사, 새 디렉터리로 자체 포함 ReadyToRun 게시 성공 |

Release 전체 테스트는 **56 통과, 실패 0, 건너뜀 0**이다. 작업 전 기준은 36개였다.
변경에 맞는 코어·UI 회귀 테스트 20개를 추가했다. 빌드 오류였던 Avalonia 12의
입력 힌트 속성은 `PlaceholderText`로 수정했다. 사용자 소스 자산은 변경하지 않았다.

## 실행 명령

SDK가 PATH에 없는 이 환경에서는 `dotnet` 대신 `/Users/hwanghyeonseong/.dotnet/dotnet`을 사용했다.
테스트 러너의 로컬 소켓과 네이티브 UI 서비스 접근이 가능한 환경에서 실행했다.

```sh
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  dotnet test Unfold.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false

dotnet publish src/Unfold.Desktop/Unfold.Desktop.csproj -c Release -r osx-arm64 \
  --self-contained true --no-restore -p:PublishReadyToRun=true \
  -p:UseSharedCompilation=false -m:1 -o <새-publish-디렉터리>

UNFOLD_DATA_DIR=<새-테스트-프로필> <게시-디렉터리>/Unfold --smoke-test
<게시-디렉터리>/Unfold --validate-characters Assets/Characters
```

## 게시 파일의 macOS 진단

격리된 새 프로필에서 게시 실행 파일의 진단이 종료 코드 0으로 끝났다.
[원본 JSON](2026-09-13-companion-smoke.json)의 `success`는 true다.

- 루틴 편집 UI의 저장 버튼으로 사용자 루틴을 저장하고 설정 파일을 다시 읽었다.
- 픽셀 에디터에서 진단 캐릭터를 저장하고 다시 열어 그림이 같은지 비교했다.
- 사용자 루틴으로 휴식 창을 열고, 중복 호출이 같은 창·세션을 유지하는지 확인했다.
- 시작 버튼 → 시간 전진 → 확인 버튼으로 완료 1건을 저장했다. 시간 만료만으로는 기록되지 않았다.
- 5분 미루기의 예약과 창 닫기의 미기록을 확인했다.
- Settings·펫·루틴 편집기·에디터·초대·완료 확인·완료 후 Settings의 PNG 7장을 생성했다.

처음에는 화면 밖 창이 macOS에서 1×1로 축소되는 진단 결함을 발견했다.
진단 창에 명시적 최소 크기를 적용하고 100px 미만 캡처를 실패 처리하도록 수정했다.
최종 캡처에서 Settings 600×850, 루틴 편집기 500×640, 휴식 창 440×565의 배치와
주요 컨트롤이 겹치지 않는 것을 확인했다. 스크롤 아래 내용까지 항상 한 화면에 들어간다는 뜻은 아니다.

로컬 원본 화면은 git에서 제외되는 `artifacts/verification/companion-stage1/`에 보관한다:
[설정](../../artifacts/verification/companion-stage1/settings-completed.png),
[내 루틴 편집](../../artifacts/verification/companion-stage1/routine-editor.png),
[완료 확인](../../artifacts/verification/companion-stage1/break-confirm.png).
이 파일들은 현재 작업 공간의 증거이며 저장소 복제본에는 포함되지 않는다.

**버튼 이벤트와 세션 시간을 프로그래밍 방식으로 진행한 off-screen 네이티브 진단**이다.
실제 마우스·키보드 입력, 실제 시간에 따른 모든 프레임 재생, OS 알림 전달,
사용자의 신체 활동을 증명하지 않는다. 진단 중 시스템 알림은 억제했다.

## 자산 검사

[실제 검사 JSON](2026-09-13-character-audit.json): 오류 0, 경고 5. 소스와 최종 게시물의
`character.json`, `spritesheet.png`, `stretch.gif` SHA-256이 모두 일치한다.

- idle: 8프레임, 384×384, 총 약 1.143초, 디코딩 픽셀 4,718,592바이트.
- stretch: 18프레임, 480×480, 총 4초, 디코딩 픽셀 16,588,800바이트.
- stretch에는 완전히 투명한 픽셀이 있는 프레임이 없다. idle과 크기도 다르다.
- attention·celebrate·click이 없다. 기존 패키지 호환성을 유지하기 위해 경고로 처리한다.

현재 Mochi는 새 판매용 팩의 완성 기준에 미달한다. 제작 원본·권리 증빙의 확인 상태와
위 품질 차이는 [제작 원장](../../Art/Characters/default-cat/resource.json)에 기록했다.
검사 성공을 판매 품질 승인으로 해석하지 않는다.

## 자원 사용 표본과 남은 검증

통합 진단은 시작·저장·화면 캡처·애니메이션 전환을 연속 수행한 직후 2초를 측정했다.
이 표본은 단일 코어 환산 CPU 약 67.1%, 작업 집합 약 204.3MB였다.
원인은 이 측정만으로 확정하지 않았다.

같은 게시 파일을 별도 새 프로필에서 `--background`, 알림 간격 240분으로 실행했다.
각각 5초 대기 후 10초간 `ps` 누적 CPU 시간의 차이와 RSS를 측정하고 해당 진단
프로세스만 종료했다. [원본 수치](2026-09-13-companion-steady-cpu.json):

| 상태 | 단일 코어 환산 CPU | RSS |
|---|---:|---:|
| 펫 표시·설정 창 숨김 | 약 6.7% | 약 118.3MB |
| 펫 숨김·설정 창 숨김 | 약 1.5% | 약 110.3MB |

이는 초기 진단의 높은 값이 일반 대기 상태에서 그대로 유지되지는 않는다는 짧은 표본이다.
변경 전 같은 조건의 비교값이 없으므로 성능 개선·회귀의 증거로 사용하지 않는다.
60–90분 동작, 배터리, 여러 모니터·고DPI, sleep/wake는 여전히 확인해야 한다.

Windows 실제 UI·설치, macOS 실제 알림·로그인·다운로드 신뢰, 배포 서명·공증,
한국어 UI·접근성, 7일 재사용과 실제 지불 의사는 이번 자동·진단 검사로 확인하지 않았다.
Plus 전체 기능·판매용 펫 제작·구매·복원은 [후속 계획](../development-plan.md)이다.
