# 반응형·메뉴·확인 동작 검증

## 환경과 범위

2026-09-21 · `release/mvp`, 기준 커밋 `8db9c02`에 변경을 적용한 작업 트리.
Windows 10.0.26200.0 · .NET SDK 10.0.301 / 런타임 10.0.9.
기존 팔레트와 알림·저장 변경을 보존했다. 모든 테스트와 진단은 새 `UNFOLD_DATA_DIR`을 사용했다.

| 요청 | 구현 |
|---|---|
| 펫 우클릭 숨기기 | ‘설정’ 아래 ‘펫 숨기기’ 추가. 표시 설정 저장 후 현재 말풍선과 함께 숨기고 진행 중 휴식은 유지한다. |
| 반응형 UI | 최소 640×560. 너비 860px 미만의 홈은 한 열로 전환하고 세로 스크롤한다. 페이지 객체·초안·포커스를 보존한다. |
| 알림 설정 배치 | 말풍선 위치 선택기를 오른쪽 정렬. 음량 슬라이더를 220px로 줄이고 퍼센트와 묶어 오른쪽 정렬한다. |
| 작은 알림 설정 | 내부 폭 480px 미만에서 라벨 위·입력 아래 배치로 전환한다. |
| 말풍선 텍스트 | 제목·타이머·버튼 텍스트를 가운데 정렬. 안내·완료는 세로 중앙에도 배치한다. |
| 내비게이션 | 타이머 → 펫 추가 → 기록 → 설정 순서로 변경. 최상단 장식 아이콘 제거. |
| 중지·종료 확인 | 홈/사이드바와 트레이에 확인 창 연결. 취소·창 닫기 시 실행하지 않으며 중복 요청을 막는다. |
| 펫 팩 열기 | 배경 선택 제거, 테마 배경 고정. ‘미리 볼 동작’을 미리보기 아래 왼쪽으로 옮기고 오른쪽 크기 선택은 유지한다. |

## 자동 검사

```powershell
dotnet test Unfold.slnx -c Release --no-restore --logger 'trx;LogFileName=final.trx' --results-directory artifacts/responsive-actions-results
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --smoke-test
dotnet build src/Unfold.Desktop/Unfold.Desktop.csproj -c Release --no-restore
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --review-pet-pack <격리 진단에서 만든 diagnostic-v1.unfoldpet>
git diff --check
```

- 전체 테스트: 종료 코드 0, **254개 통과 / 2개 건너뜀 / 0개 실패**. 건너뛴 검사는 FFmpeg 도구가 필요한 실제 MP4 변환 두 항목이다.
- Release 빌드: 종료 코드 0, 경고·오류 0.
- [Windows 화면 진단](2026-09-21-responsive-actions.json): 종료 코드 0, `success=true`, PNG **91장**.
  기본/기존 860×680 배치, 새 640×560의 홈·설정·기록·펫 탭과 두 확인 창을 포함한다.
- [펫 팩 진단](2026-09-21-responsive-actions-pack-review.json): 종료 코드 0, `success=true`, PNG **14장**.
  동작 선택·재생·일시정지·192/384px·저장·디스크 재열기·숨긴 상태 유지와 정상 종료를 확인했다.
- 새 종료 확인이 무인 진단을 막지 않도록 진단 전용 승인 응답을 주입했다. 화면 진단에서는 별도로 실제 확인 창을 생성하고 취소 버튼 이벤트를 실행한다.
- 팩 진단의 첫 실행은 출력 로그를 데이터 경로 안에 만들어 ‘새 빈 폴더’ 검사로 시작 전에 거부됐다(종료 코드 2).
  로그를 데이터 경로 밖에 기록하고 새 경로에서 재실행해 통과했다.
- `git diff --check`와 이 보고서의 로컬 링크 검사 통과.

## 경계 검증

- 숨김은 저장 성공 후에만 반영된다. 현재 휴식·완료 기록은 유지하고 일반 화면 갱신으로 다시 보이지 않는다.
  ‘휴식 알림으로 이동’, 새 알림 또는 명시적 미리보기에서 다시 표시할 수 있다.
- 중지의 승인·취소·창 닫기와 중복/교차 요청, 종료 취소 및 종료 승인 후 기존 편집 초안 보호를 headless로 검사했다.
- 640/760/859/860/1120px 전환에서 홈 입력 객체와 수정값, 설정 음량 초안, 펫 이름 초안이 유지된다.
  편집 중인 홈 입력은 좁은 배치로 전환해도 포커스와 화면 안 위치를 유지한다.
- 작은 창에서 모든 탭의 가로 넘침, 설정 저장·기록 내보내기·펫 저장·파일 추가의 도달 가능성을 검사했다.
- 알림 카드 760/468/340px에서 오른쪽 정렬과 슬라이더 너비·초안 유지, 말풍선 모든 상태의 텍스트 정렬을 검사했다.
- 배경 선택 UI 부재, 동작 선택기의 하단 이동, 크기/반응 선택·저장 초기화와 테마 반영을 검사했다.
  대체 밝은 배경 캡처는 진단에서 직접 주입하고 복원하며 제품의 설정 항목은 아니다.

## 캡처 확인

현재 코드가 Windows에서 생성한 off-screen 이미지다. 작은 창의 내비게이션·스크롤·하단 버튼과
기본 펫 미리보기, 넓은 설정의 오른쪽 정렬, 동작 선택기 위치, 확인 문구와 가운데 말풍선을 확인했다.

| 화면 | 캡처 |
|---|---|
| 작은 홈 위 / 아래 | [위](images/2026-09-21-responsive-actions/responsive-home-top.png), [아래](images/2026-09-21-responsive-actions/responsive-home-bottom.png) |
| 설정 860×680 / 640×560 | [넓은 배치](images/2026-09-21-responsive-actions/settings-options-minimum.png), [좁은 배치](images/2026-09-21-responsive-actions/responsive-settings-top.png) |
| 작은 펫 만들기 | [보기](images/2026-09-21-responsive-actions/responsive-pet-create.png) |
| 팩 미리보기 | [보기](images/2026-09-21-responsive-actions/pack-preview.png) |
| 중지 / 종료 확인 | [중지](images/2026-09-21-responsive-actions/confirm-stop.png), [종료](images/2026-09-21-responsive-actions/confirm-quit.png) |
| 안내 말풍선 | [보기](images/2026-09-21-responsive-actions/speech-five-minutes.png) |

## 미검증

실제 Windows 마우스·키보드 수동 조작, macOS 실기, 다중 모니터·DPI 전환, 보조기술과 장시간 사용은 미검증이다.
자동 화면 진단은 프로그램으로 이벤트를 발생시키며 네이티브 효과음은 억제한다.
배포물 생성·서명·게시는 이번 범위에 포함하지 않았다.
