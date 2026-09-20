# UI 설명 문구 제거 검증

## 변경

- 홈 타이머는 진행·일시정지·중지·자리 비움·휴식 대기·휴식 중의 상태명만 표시한다.
- 오늘 기록의 위로 문구와 로컬 저장 안내, 시간 입력 아래 사용법 문구를 제거했다.
- 설정 탭의 타이머 도움말, 앱 동작 즉시 적용 안내, 디버그 설명, 효과음 형식 안내를 제거했다.
- 기록 탭의 집계·저장 방식 설명과 빈 기록 위로 문구를 제거했다.
- 펫 팩 열기·만들기의 빈 미리보기 안내, 사용법과 파일 형식 설명을 제거했다.
- 말풍선의 완료 안내와 +60분 안내 문구를 제거했다.
- 항목명, 현재 값, 파일 메타데이터, 상태명과 실제 저장·내보내기·설치 성공·실패 피드백은 유지했다.
- 아이콘 버튼의 도구 설명과 접근성 이름은 유지했다.

## 검증

| 구분 | 결과 |
|---|---|
| Release 빌드 | 성공, 경고 0·오류 0 |
| UI 문구 집중 테스트 | 13 통과, 실패·건너뜀 0 |
| 테마 후속 테스트 | 4 통과, 실패·건너뜀 0 |
| Release 전체 테스트 | 215 통과, 실패·건너뜀 0 |
| 격리 스모크 진단 | `success: true`, PNG 82장 |
| 레이아웃 | 1120×800·860×680에서 가로 넘침 없음, 설정 본문·기록·펫 추가 고정 하단 유지 |

자동 캡처:

- [홈](images/2026-09-20-ui-copy-cleanup/home-default.png)
- [중지 타이머](images/2026-09-20-ui-copy-cleanup/timer-stopped.png)
- [설정 상단](images/2026-09-20-ui-copy-cleanup/settings-options-top.png)
- [설정 하단](images/2026-09-20-ui-copy-cleanup/settings-options-bottom.png)
- [기록](images/2026-09-20-ui-copy-cleanup/review-default.png)
- [펫 팩 열기](images/2026-09-20-ui-copy-cleanup/pet-open.png)
- [펫 팩 만들기](images/2026-09-20-ui-copy-cleanup/pet-create.png)

구조화 결과는 [JSON](2026-09-20-ui-copy-cleanup.json)에 있다.

## 미검증

- 캡처는 macOS 호스트의 off-screen Avalonia 자동 진단이다. 실제 macOS·Windows의 포인터·키보드 조작과 OS 배율별 화면은 이번 변경에서 수동 확인하지 않았다.
- 아이콘 도구 설명과 화면 읽기용 접근성 이름의 실제 OS 전달은 자동 테스트 범위 밖이다.

