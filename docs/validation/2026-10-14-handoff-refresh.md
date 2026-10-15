# 세션·IDE 작업 인계 문서 최신화

2026년 10월 14일 · 제품 기준 `f9953a0`, 브랜치 `codex/pet-overlay-hover`, `1.1.4-beta`.

## 확인 범위

- 등록된 작업트리 8개 각각의 브랜치·전체 HEAD·변경·프로젝트 버전과 커밋 차이를 확인했다.
- 최신 개발 `f9953a0`, GitHub 원격 `4a668fd`, 공개 태그 제품 `4c64bfc`를 구분했다.
- GitHub 읽기 전용 API에서 최신 공개 prerelease `v1.1.4-beta`, 공개 시각과 자산 10개를 확인했다.
- 실제 실행 경로·설치 앱 plist와 기존 Release DLL을 확인했다.
- 현재 코드의 홈 시간·중지·GLB 편집·창 분리·숫자 전환·데이터 계약을 기능 문서와 대조했다.
- 기존 TRX와 JSON·보고서를 확인해 최신 고유 집중 16개와 이전 전체 979개·배포 집중 63개의 적용 소스를 구분했다.
- 최신 수정의 원본 증거를 임시 경로에서 저장소로 복사하고 7개 수정 파일의 해시 일치를 기록했다.

## 실행 앱 확인

읽기 전용 프로세스 확인에서 `/Applications/Unfold.app/Contents/MacOS/Unfold`가 실행 중이었다.
설치 plist는 `CFBundleIdentifier=app.unfold.desktop`, `CFBundleShortVersionString=1.1.4`,
`CFBundleVersion=100100400`이었다.

| DLL | SHA-256 | 수정 시각 KST |
|---|---|---|
| 설치 앱 `Contents/Resources/Managed/Unfold.dll` | `031f7e438ac90bcfcdbda928c6fecf817842654cb5341fa094ff1842a68de843` | 2026-10-14 15:34:18 |
| 작업트리 `src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll` | `ffbcc637e0aa9f0b2240452eab37ac99732ab3007c0b890e3a0b5ebd69f3810c` | 2026-10-14 02:24:11 |

설치 경로의 `Contents/MacOS/Unfold.dll`은 위 Managed DLL로 연결된다.
두 파일이 다르고 기존 빌드 시각도 제품 수정 전이므로, 설치 앱이나 `--no-build` 실행을
현재 `f9953a0` 실행 증거로 취급하지 않는다. 앱을 재빌드·재시작·교체하지 않았다.
GUI 읽기는 `Avalonia Application / Window`만 확인할 수 있었고 Unfold 앱 목록은 비어 있었다.
실제 화면 상태·물리 입력·애니메이션의 새 통과 결과는 없다.

## 갱신한 문서

[작업 인계](../HANDOFF.md)를 추가하고 최신 작업트리의 README·AGENTS·CLAUDE와
문서 색인을 연결했다. 제품 범위·개발 계획·설정·말풍선·GLB·펫 팩·계정·제품 방향·BM·
Windows 양식·검증/배포 안내의 현재 버전과 구현 차이를 정정했다.
루트 `release/mvp`에는 최신 인계로 가는 안내를 넣고 해당 소스의 `0.2.2`를 명시했다.
과거 날짜별 검증·릴리스 노트와 다른 작업트리의 미커밋 문서는 보존했다.

## 이번 문서 검사

Markdown 26개·로컬 링크 577개와 코드/증거 경로를 확인했고 누락 링크·잘못된 앵커는 0개다.
기준 SHA·버전, 코드 블록, 두 체크아웃의 Git diff 공백과 변경 범위 검사를 통과했다.
제품 코드 변경은 없으며 이전 작업트리의 수정 문서 12개와 루트의 기존 미추적 폴더도 유지됐다.
[결과 JSON](evidence/2026-10-14-handoff-refresh/checks.json)에 실제 결과를 저장했다.
앱 테스트·빌드·smoke·설치/업데이트 진단은 다시 실행하지 않았다.

문서·증거는 로컬 미커밋이며 `f9953a0`도 아직 로컬 전용 커밋이다.
이번에는 커밋·푸시·태그·외부 문서 수정·서버 배포를 하지 않았다.
