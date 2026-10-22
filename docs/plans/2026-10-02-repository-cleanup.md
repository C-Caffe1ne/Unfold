# 저장소 정리 계획 — 2026-10-02

기준: `release/mvp`의 현재 작업 트리. 기존 펫·Supabase 변경을 보존한다.
현재 C#/.NET 10·Avalonia 빌드 입력은 `Unfold.slnx`, `src/`, `Tests/`, `Assets/`다.

## 실행 순서와 게이트

1. 변경 전 Release 테스트·빌드, 새 `UNFOLD_DATA_DIR` smoke 진단을 실행하고 실패 원인을 분리한다.
2. 제품 실패가 있으면 삭제를 보류하고 수정·재검증한다. 환경 실패를 통과로 기록하지 않는다.
3. Git 추적 파일이 없는 아래 생성물만 정리한다. 원본·기존 변경·검증 보고서/캡처의 SHA-256을 확인한다.
4. 최신 v0.2.2 압축 배포본·체크섬, 진단 소스, JSON·PNG, rollback/pet-pack 원본,
   `.tools/media-lgpl` 실제 패키징 입력, `.git`, 제작 원장과 보관 문서는 유지한다.
5. 구버전 활성화 혼동을 줄이도록 개발 실행은 현재 프로젝트를 빌드하고 기본 데이터 폴더를 격리한다.
   README·MVP·계정 화면 안내의 과거 구현 설명을 현재 코드 근거로 정정한다.
6. 생성물을 제거한 상태에서 locked restore, Release 테스트·빌드, 새 프로필 smoke를 재실행한다.
   전후 용량·파일 보존·Markdown 로컬 링크·스크립트 구문·diff 형식을 확인한다.

## 선별한 생성물

다음 목록은 삭제 전 측정값이며 실제 할당 크기와 다를 수 있다.
소스/테스트 원본 합계는 1MiB 미만으로, 확실한 미사용 코드가 없는 상태에서 코드 삭제는 하지 않는다.
이름이 숨겨진 편집기도 진단·호환성 회귀 검사에서 사용한다.

| 경로 | 논리 크기 |
|---|---:|
| `.build` | 845.7 MiB |
| `.swiftpm` | 0.0 MiB |
| `Unfold.xcodeproj` | 0.0 MiB |
| `build` | 8.0 MiB |
| `.tools/media-downloads` | 75.5 MiB |
| `artifacts/Unfold-v0.1.1-osx-arm64.tar.gz` | 49.6 MiB |
| `artifacts/Unfold-v0.1.1-osx-arm64.zip` | 49.9 MiB |
| `artifacts/Unfold-v0.1.1-osx-x64.tar.gz` | 52.3 MiB |
| `artifacts/Unfold-v0.1.1-osx-x64.zip` | 52.7 MiB |
| `artifacts/Unfold-v0.1.1-win-x64.zip` | 77.8 MiB |
| `artifacts/Unfold-v0.2.0-osx-arm64.tar.gz` | 61.6 MiB |
| `artifacts/Unfold-v0.2.0-osx-arm64.zip` | 62.0 MiB |
| `artifacts/Unfold-v0.2.0-osx-x64.tar.gz` | 65.3 MiB |
| `artifacts/Unfold-v0.2.0-osx-x64.zip` | 65.7 MiB |
| `artifacts/Unfold-v0.2.0-win-x64.zip` | 91.9 MiB |
| `artifacts/Unfold-v0.2.1-osx-arm64.tar.gz` | 66.2 MiB |
| `artifacts/Unfold-v0.2.1-osx-arm64.zip` | 66.5 MiB |
| `artifacts/Unfold-v0.2.1-osx-x64.tar.gz` | 69.9 MiB |
| `artifacts/Unfold-v0.2.1-osx-x64.zip` | 70.2 MiB |
| `artifacts/Unfold-v0.2.1-win-x64.zip` | 96.3 MiB |
| `artifacts/Unfold.app` | 159.4 MiB |
| `artifacts/osx-arm64` | 159.9 MiB |
| `artifacts/osx-x64` | 154.5 MiB |
| `artifacts/win-x64` | 253.4 MiB |
| `Tests/Unfold.Tests/bin` | 1215.1 MiB |
| `Tests/Unfold.Tests/obj` | 2.5 MiB |
| `src/Unfold.Core/bin` | 0.5 MiB |
| `src/Unfold.Core/obj` | 0.7 MiB |
| `src/Unfold.Desktop/bin` | 1714.2 MiB |
| `src/Unfold.Desktop/obj` | 80.0 MiB |
| `tools/Unfold.MediaSetup/bin` | 0.1 MiB |
| `tools/Unfold.MediaSetup/obj` | 0.2 MiB |
| `artifacts/probes/pet-ghost/bin` | 590.3 MiB |
| `artifacts/probes/pet-ghost/obj` | 0.4 MiB |
| `artifacts/releases/v0.2.1/win-x64` | 258.2 MiB |
| `artifacts/releases/v0.2.2/win-x64` | 258.2 MiB |
| `artifacts/validation/2026-09-23-hover-click/Unfold Hover QA.app` | 592.7 MiB |
| `artifacts/validation/2026-09-23-mac-pet/Unfold Space QA.app` | 592.6 MiB |
| `artifacts/validation/2026-09-27-hover-jump/Unfold Pointer QA.app` | 0.0 MiB |
| `artifacts/releases/v0.2.1/verify-osx-arm64/Unfold.app` | 159.4 MiB |
| `artifacts/releases/v0.2.1/verify-osx-x64/Unfold.app` | 154.5 MiB |
| `artifacts/releases/v0.2.2/metadata-check/bin` | 0.1 MiB |
| `artifacts/releases/v0.2.2/metadata-check/obj` | 0.2 MiB |
| `artifacts/releases/v0.2.2/verify-osx-arm64/Unfold.app` | 159.4 MiB |
| `artifacts/releases/v0.2.2/verify-osx-x64/Unfold.app` | 154.5 MiB |
| `artifacts/validation/2026-09-23-hover-click/native-harness/obj` | 0.4 MiB |
| `artifacts/validation/2026-09-23-mac-pet/native-harness/bin` | 592.6 MiB |
| `artifacts/validation/2026-09-23-mac-pet/native-harness/obj` | 0.4 MiB |
| `artifacts/validation/2026-09-23-pet-hover/native-harness/bin` | 592.6 MiB |
| `artifacts/validation/2026-09-23-pet-hover/native-harness/obj` | 0.4 MiB |
| `artifacts/validation/2026-09-23-preview-sound/native-harness/bin` | 592.7 MiB |
| `artifacts/validation/2026-09-23-preview-sound/native-harness/obj` | 0.4 MiB |
| `artifacts/validation/2026-09-23-reminder-recovery/native-harness/bin` | 592.6 MiB |
| `artifacts/validation/2026-09-23-reminder-recovery/native-harness/obj` | 0.4 MiB |
| `artifacts/validation/2026-09-27-hover-jump/native-harness/bin` | 594.1 MiB |
| `artifacts/validation/2026-09-27-hover-jump/native-harness/obj` | 0.4 MiB |

## 구버전 혼동 조사

- 동일 DataRoot의 `.instance.lock`과 파이프는 소스 실행과 배포 앱을 같은 인스턴스로 취급한다.
  기존 앱이 켜져 있으면 새 소스 실행이 기존 앱을 활성화할 수 있다. 단일 인스턴스 보호는 유지한다.
- `artifacts`/`bin`의 복사본 대신 `src`, `Assets`, `web` 원본을 수정한다.
- 현재 csproj의 0.2.2는 작업 트리 변경까지 식별하지 못한다. 커밋과 dirty 상태를 함께 확인한다.
- macOS 사용자 LaunchAgents 목록에 Unfold 등록은 없었다. 실제 실행 프로세스·Windows 자동 시작은 미검증이다.

## 후속 정책

새 배포물을 만든 뒤 최신 배포 압축본만 유지하고, 진단 harness의 bin/obj 및 테스트용 앱 사본은
보고서·JSON·캡처·재현 소스를 남겨 정리한다. 현재 코드 최적화는 성능 측정 근거가 없는 상태라
별도 동작 변경을 하지 않는다. 실제 OS 입력·장시간 안정성·원격 배포는 이번 자동 검증과 구분한다.
