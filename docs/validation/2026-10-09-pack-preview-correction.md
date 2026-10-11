# 홈 미리보기 복원·펫 팩 미리보기 제거 정정

2026-10-09 · Beta v1.1.3, `codex/remove-unused-features`, 기준 커밋 `17941db`.
작업 경로: `/Users/hwanghyeonseong/Documents/GitHub/Unfold/.worktrees/remove-unused-features`.

## 원인

사용자가 제거하려던 화면은 첨부 사진의 별도 **펫 팩 확인** 창이다.
앞선 작업은 이를 홈 타이머의 펫 미리보기로 해석하여 제거 범위를 잘못 적용했다.
이번 작업은 사용자의 정정에 따라 홈 미리보기를 복원하고 사진의 기능을 제거한다.

## 변경

- 홈 AnimationView, 펫 크기 표시·스크롤, 펫 전환 시 비동기 로드, 실패 재시도, 탭/창 표시·숨김 수명 처리를 복원했다.
- 별도 `PetPackWindow` / `PetPackView`와 미리보기 재생·일시정지·다시 재생·행동 선택·100/150/200% 크기 선택을 제거했다.
- `.unfoldpet` 가져오기와 미디어/GLB 내보내기를 유지했다. 파일·해시·재생 자산 검증 후 이름·버전이 담긴 저장 확인창을 사용한다.
- 취소 시 설치 파일과 현재 펫 선택을 유지한다. 확인창이 열린 동안 다른 작업이 수정한 설치 파일을 덮어쓰지 않도록 기존 revision 검사를 유지한다.
- 기존 설치 펫, 홈 펫 선택·크기, 실제 데스크톱 펫과 미디어·GLB 편집 미리보기는 유지한다.
- 이미 승인·완료한 루틴/프로필 및 Piskel 제거·이전은 유지한다.
- 실제 사용자 데이터는 건드리지 않았다. 검증에는 새 `UNFOLD_DATA_DIR`를 사용했다.

## 소유 파일

`SettingsWindow` 미리보기·레이아웃·AppRuntime 표시 연결, `PetManagementView`, `PetPackImport`, 제거한 팩 창과 관련 테스트·진단·현재 기능 문서.
다른 체크아웃의 기존 변경은 보존했다. 이전 커밋을 reset/revert하지 않고 필요한 홈 기능만 복원했다.

## 검증

- 수정 전 관련 검사 30개 통과.
- 집중 검사 52개 통과: 홈 미리보기 표시·펫 전환·크기·재시도·숨김/복구, 저장 확인 취소·업데이트·복구·충돌, 현재 편집·반응형·디자인 시스템.
- 전체 Release 회귀 검사 **778개 통과, 실패 0, 건너뜀 0**.
- Release 빌드 오류·경고 없음.
- macOS 네이티브 진단: 종료 코드 0, `success: true`, 화면 캡처 95장. 홈 펫 표시를 직접 확인했다.
- 실제 팩 가져오기 UI에서 취소·저장·업데이트·재설치·손상 파일 거부를 실행했다. 확인창에 `PackPreview`가 없는지도 검사했다. 타이머 Pause와 기존 설정을 유지했다.
- 결과 파일: `/private/tmp/unfold-pack-preview-correction-20261009-smoke/verification/smoke.json`.
- 제거한 클래스와 동작 제어에 대한 실행 소스 참조가 남지 않았음을 확인했다. `git diff --check` 통과.

![복원한 홈 미리보기](/private/tmp/unfold-pack-preview-correction-20261009-smoke/verification/timer-running.png)

![미리보기 없는 저장 확인창](/private/tmp/unfold-pack-preview-correction-20261009-smoke/verification/pack-import-저장.png)

## 미검증·위험

macOS 진단과 headless 검사는 물리적 마우스·키보드 입력이나 Windows 실제 실행의 증거가 아니다.
공개 웹과 배포 설치 파일은 변경하지 않았다. 이 정정 작업은 아직 커밋하지 않았다.
