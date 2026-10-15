# 휴식 타이머 애니메이션 구현 보고서

## 원인
휴식 말풍선만 180ms·3px의 전체 텍스트 이동/페이드인 AnimatedTimeText를 사용했다. 홈·펫 하단의 공통 AnimatedCountdown 숫자별 수직 전환과 달라 같은 움직임을 표시하지 않았다.

## 변경
휴식 PetBreakTimer를 공통 AnimatedCountdown으로 연결했다. 380ms 숫자별 전환·40px 폰트·48px 고정 줄 높이를 사용한다. 남은 시간 ceiling·초과 시간 floor와 +표시를 보존하고 숨김·완료·중지·detach에 motion을 멈춘다. 펫 창과 오버레이 배치 경로는 바꾸지 않았다.

## 소유 파일
생산: PetSpeechBubble.cs, SmokeDiagnostics.TimerRefinements.cs. 테스트: TimerRefinementTests.cs, BreakReminderTests.cs, PetHoverTests.cs, PetTimerHoverTests.cs, PetOverlayWindowTests.cs.

## 검증
최신 제품 v1.1.4-beta/4c64bfc와 문서 HEAD4a668fd를 재확인하고 격리 클론·새 UNFOLD_DATA_DIR에서 검사했다. 수정 전 핵심 회귀3개 실패로 공통 숫자 컨트롤 미연결을 확인했다. 수정 후 15개 통과, 숨은 창 초기 구성을 logical tree로 검사하도록 보완한1개도 통과해 고유16개 모두 통과했다. Release 프로젝트 빌드 및 git diff --check 통과. 샌드박스의 테스트 실행기 통신 차단은 필요한 권한의 동일 집중 실행으로 해결했다.

## 미검증·위험
전체 테스트·앱 OS 진단·실기 입력·앱 재실행·재배포는 수행하지 않았다. 진단 코드의 타입/380ms 기대만 정렬했고 진단 자체는 실행하지 않았다.
