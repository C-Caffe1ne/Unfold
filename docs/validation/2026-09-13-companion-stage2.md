# 개인화 데모 2차 검증

2026-09-13 · `release/mvp`, 기준 커밋 `97f9a84` 위의 기존 정리·1차 구현에 적용한 작업 트리.
macOS arm64, .NET SDK 10, 게시 앱 런타임 10.0.12. 커밋·공개 배포는 수행하지 않았다.

## 변경과 결과

| 대상 | 확인 내용 | 결과 |
|---|---|---|
| 기존 설정 | 단일 개인 루틴, 알림 값, 펫 표시·좌표를 보존한 라이브러리/프로필 저장·재로드 | 자동 검사 통과 |
| 루틴 라이브러리 | 여러 루틴, 기본 루틴 편집·삭제 금지, ID 충돌·개수 한도, 참조 중 루틴 삭제 거부 | 자동 검사 통과 |
| 업무 프로필 | 저장·적용·편집·삭제, 수동 설정 시 적용 표시 해제, 실제 편집 UI의 이름·루틴·시간 저장 | 자동·네이티브 진단 통과 |
| 세션과 기록 | 열린 세션의 루틴·프로필 이름 고정, 나중에 편집해도 과거 완료가 바뀌지 않음 | 자동·네이티브 진단 통과 |
| 주간 회고 | 완료 당시 현지 날짜별 7일 집계, 빈 날짜 포함, 구간 밖 기록 제외, 이전/다음 구간 | 자동 검사 통과 |
| CSV | 현재 표시 구간의 사본, 한글 BOM, 따옴표·쉼표, 수식 시작 문자 보호, 원자적 파일 저장 | 자동·네이티브 진단 통과 |
| 저장 한도 | 최대 2,000건에 각각 긴 한글 루틴·프로필 이름을 넣은 저장·재로드 | 자동 검사 통과 |
| 하위 호환 | 이름·프로필 필드가 없는 기존 history version 1 읽기·CSV, 기존 설정 왕복 | 자동 검사 통과 |

Release 테스트 **71 통과, 실패 0, 건너뜀 0**. 1차의 56개에 코어 10개·UI 5개를 추가했다.
첫 실행에서 새 목록 필드가 비어 있을 때 기존 설정 레코드 왕복 동등성 검사가 실패했다.
빈 목록의 표현을 일치시키는 수정 후 기존 검사를 포함해 통과했다. 기존 테스트의
검증 조건을 약화하지 않았다.

라이브러리 삭제 확인창을 기다리는 동안 바뀐 다른 설정을 덮어쓰지 않도록, 실제 삭제
저장은 확인 후 최신 설정에서 계산한다. 회고 화면의 단수 표기도 화면 검수 후 수정했다.
NuGet 의존성·잠금 파일과 기존 캐릭터 자산은 이번 작업에서 변경하지 않았다.

## 명령과 게시 파일

이 환경의 SDK 실행 파일은 `/Users/hwanghyeonseong/.dotnet/dotnet`이다. 테스트 러너의
로컬 소켓과 macOS 네이티브 서비스 접근이 허용된 환경에서 실행했다.

```sh
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  dotnet test Unfold.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false

dotnet publish src/Unfold.Desktop/Unfold.Desktop.csproj -c Release -r osx-arm64 \
  --self-contained true --no-restore -p:PublishReadyToRun=true \
  -p:UseSharedCompilation=false -m:1 -o <새-publish-디렉터리>

UNFOLD_DATA_DIR=<새-테스트-프로필> <게시-디렉터리>/Unfold --smoke-test
```

실행 중인 사용자 데이터는 사용하지 않았다. 게시 파일과 진단 데이터는 이번 작업 전용
임시 디렉터리에서 생성했다. [진단 원본 JSON](2026-09-13-companion-stage2-smoke.json)에
실행 수치와 결과가 있다.

## macOS 네이티브 진단과 화면

게시 앱의 진단은 `success: true`, 종료 코드 0이었다. 새 프로필에서 다음을 확인했다.

1. 기존 개인 슬롯과 추가 루틴을 편집 UI의 저장 버튼으로 저장했다.
2. 업무 프로필 편집 UI에서 45분 간격을 저장하고 관리 화면의 Apply로 적용했다.
3. 수동 Pause가 유지되고 Settings의 알림 간격 표시가 45분으로 바뀌는 것을 검사했다.
4. 적용한 루틴·프로필로 휴식을 연 뒤 루틴 이름과 시간을 변경했다. 열린 휴식은 원래
   20초 루틴을 유지했고, 완료 기록에도 원래 이름과 프로필이 남았다.
5. 완료는 1건만 저장됐고, 미루기·닫기는 추가 완료로 기록되지 않았다.
6. 주간 회고의 Export CSV 버튼으로 해당 1건을 파일에 저장했다.
7. PNG 12장을 생성하고 새 관리·프로필 편집·회고·Settings 화면에서 주요 컨트롤 배치를 확인했다.

로컬 원본 화면·CSV·테스트 로그는 git 제외 경로 `artifacts/verification/companion-stage2/`에
보관한다. 저장소 복제본에는 포함되지 않는다.

- [루틴 라이브러리](../../artifacts/verification/companion-stage2/routine-library.png)
- [업무 프로필](../../artifacts/verification/companion-stage2/work-profiles.png)
- [프로필 편집](../../artifacts/verification/companion-stage2/profile-editor.png)
- [주간 회고](../../artifacts/verification/companion-stage2/weekly-review.png)
- [완료 후 Settings](../../artifacts/verification/companion-stage2/settings-completed.png)

Settings 하단의 펫 옵션은 세로 스크롤로 접근한다. 새 관리 화면은 640×620,
프로필 편집기는 500×520, 회고는 620×650에서 확인했다.

## 검증의 한계

이것은 프로그램이 버튼 이벤트를 발생시키는 off-screen 네이티브 진단이다.
세션 시간은 직접 전진시켰고 CSV 목적지도 주입했다. 실제 사용자가 파일 선택 창에서
경로를 선택·취소·덮어쓰기 하는 과정과 실제 키보드·마우스 조작은 검증하지 않았다.
단위 검사와 별개로 Windows 실기, OS 알림·로그인·sleep/wake·전체 프레임 재생도 남아 있다.

진단 JSON의 CPU는 여러 창·저장·캡처 직후 2초 표본이다. 장시간 대기 성능이나
배터리 사용, 변경 전후 성능 차이를 증명하지 않는다. 원래 사용자 데이터와 실제
구매·결제 시스템은 접근하지 않았다.

전체 한국어 UI·스크린리더 검수, 시간대 자동 프로필 전환·회의 감지, 판매용 펫 제작과
팩 설치·결제·복원, 실제 7일 재사용·지불 의사는 완료 범위가 아니다.
현재 데모 사용법과 다음 범위는 [개인화 안내](../personalization.md), [개발 계획](../development-plan.md)에 있다.
