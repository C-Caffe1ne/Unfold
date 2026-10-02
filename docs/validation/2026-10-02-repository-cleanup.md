# 저장소 정리·검증 — 2026-10-02

기준: `release/mvp`의 기존 변경을 포함한 작업 트리, macOS arm64 / .NET SDK 10.0.401.
[사전 계획](../plans/2026-10-02-repository-cleanup.md)과
[삭제 경로·크기 목록](2026-10-02-repository-cleanup-deleted.json)을 함께 확인한다.

## 용량과 정리

- 정리 전 약 12GiB → 생성물 삭제 직후 1.2GiB → Release 출력 재생성 후 약 2.4GiB.
- 생성물 56개 경로의 논리 크기 합계 12,116,273,266 bytes 제거.
  `du`의 디스크 할당량과 논리 크기는 서로 다르므로 차이를 같은 수치로 해석하지 않는다.
- Swift 캐시·Xcode 사용자 상태·빈 Sources/Finder 메타데이터, 반복 생성된 C# bin/obj,
  진단 harness 출력·테스트용 앱, 풀어 둔 배포 앱, v0.1.1/v0.2.0/v0.2.1 압축본,
  재다운로드 가능한 media-downloads 캐시를 정리했다.
- v0.2.2 압축본과 체크섬, 검증 JSON·PNG·재현 소스, pet packs/rollbacks,
  `.tools/media-lgpl` 빌드 입력, 제작 원본, `.git`, 기존 코드·테스트 변경은 유지한다.
- 문서·코드 원본은 용량이 작다. 진단/회귀 검사 소비가 있는 숨겨진 편집기를
  죽은 코드로 분류하지 않았으며 제품 코드 삭제·성능 리팩터는 하지 않았다.

## 오류 원인과 최소 수정

동일 DataRoot의 단일 인스턴스 잠금이 소스 실행과 설치 앱을 구분하지 않는다.
기존 배포 앱이 실행 중이면 새 실행이 기존 앱을 활성화할 수 있다.
이번 작업은 이 사용자 데이터 보호 계약을 유지하고, 개발 런처 기본값을
실행마다 새 임시 `UNFOLD_DATA_DIR`로 바꿨다. 현재 checkout 절대 프로젝트 경로를
Release로 빌드·실행하고 소스·데이터 위치를 출력한다.
명시적으로 전용 개발 경로를 지정하면 해당 프로필을 재사용한다.

README/MVP의 과거 클릭·타이머·루틴/프로필 UI·계정 미연결 설명을 현재 코드에 맞췄다.
계정 JSON은 출력 복사본 대신 원본을 편집하도록 고쳤고 과거 우선순위는 이력으로 표기했다.
실제로 구버전 프로세스가 실행 중이었는지는 확인하지 못했다.
macOS 사용자 LaunchAgents 목록에는 Unfold 항목이 없었으며 Windows 자동시작은 검사하지 않았다.

## 자동 검증

| 확인 | 정리 전 | 정리 후 |
|---|---|---|
| Release 테스트 | 423 통과 / 실패·건너뜀 0 | 423 통과 / 실패·건너뜀 0 |
| Release 빌드 | 경고·오류 0 | 경고·오류 0 |
| 새 프로필 smoke | success true, PNG 93장 | 격리 런처 success true, PNG 93장 |
| Supabase Node | 35 통과 / 실패·건너뜀 0 | 관련 파일 변경 없음, 반복 안 함 |
| locked restore | 제한 환경 무응답 종료, 미판정 | clean restore 성공 |

제한 환경 테스트 시작은 로컬 소켓 Permission denied로 중단됐고, 허용된 환경에서
동일 테스트가 통과했다. 제품 실패와 구별한다. 원시 실행 로그는
`/tmp/unfold-cleanup-20261002/`에 있다(임시 경로이므로 영구 보존 보증 없음).

런처 계약 확인은 다른 cwd에서 현재 프로젝트 경로, Release 설정, 공백 포함 인수 전달,
기본 프로필의 실행별 분리, 명시적 프로필 재사용을 검사했다. bash 구문 검사도 통과했다.
PowerShell이 설치되지 않아 Windows 런처 실제 실행·구문은 미검증이다.

기존 변경 24개 파일의 SHA-256이 유지됐고, 보존 대상 4,731개 파일에서 승인한 문서·런처 수정 외 변경·누락은 없었다.
Markdown 로컬 링크 673개에서 누락 0(외부 URL·앵커 제외), `git diff --check` 통과.
[보존·런처 검사 결과](2026-10-02-repository-cleanup-integrity.json)를 기록했다.

정리 후 원시 smoke 결과는 [정리 전](2026-10-02-repository-cleanup-baseline-smoke.json)과
[정리 후](2026-10-02-repository-cleanup-post-smoke.json)에 보존했다.

## 실제 OS 한계

smoke는 네이티브 렌더링을 사용하는 자동 화면 밖 진단이다.
실제 마우스·키보드·파일 선택, 소리 청취, Windows UI, DPI·Space·sleep/wake,
로그인·결제·원격 배포·서명·공증, 장시간 CPU/메모리·사용성은 이번 작업에서 미검증이다.
빌드·테스트 통과가 이 항목의 통과를 뜻하지 않는다.
