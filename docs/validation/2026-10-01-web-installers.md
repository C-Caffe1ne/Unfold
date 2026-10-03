# Beta v1.0.1 설치 ZIP과 웹 다운로드 배포

2026년 10월 1일. 사용자가 요청한 설치 파일 압축·기존 웹 다운로드 교체를 완료했다.

## 결과

- [공개 다운로드 화면](https://unfoldpet.dokhustudio.com/#install)의 기본 다운로드·계정 미리보기 다운로드를 Beta v1.0.1 설치 ZIP으로 교체했다.
- Windows ZIP은 설치 마법사 EXE, Apple Silicon·Intel Mac ZIP은 해당 기종의 DMG를 포함한다. 각 ZIP에는 `INSTALL.txt`와 `RELEASE-NOTES.md`도 들어 있다.
- [공개 릴리스](https://github.com/C-Caffe1ne/Unfold/releases/tag/v1.0.1-beta)는 테스터용 prerelease다. 기존 Beta v1.0.0 파일과 URL은 유지했다.
- 웹사이트 커밋 `55a9cf7`을 main에 푸시했고 기존 Cloudflare 자동 배포의 실제 반영을 확인했다. Worker·DNS·Custom Domain을 변경하지 않았다.

## ZIP 파일

| 플랫폼 | 파일 | 바이트 | SHA-256 |
|---|---|---:|---|
| Apple Silicon | `Unfold-v1.0.1-beta-osx-arm64-installer.zip` | 74,185,135 | `48ecbbbed8fea7050429a6cd9fa7568fccf1bd378e220c4da3f7a7b3e6e6f135` |
| Intel Mac | `Unfold-v1.0.1-beta-osx-x64-installer.zip` | 78,278,126 | `5c535546789c8bed11ed6124cb3ea23da85a99ea880e25369e7840b418785664` |
| Windows x64 | `Unfold-v1.0.1-beta-win-x64-installer.zip` | 77,450,278 | `bc4625209070b8887d92207864255d6a3309f58fa6cf2449b3da6c849772a642` |

ZIP 체크섬은 `Unfold-v1.0.1-beta-ZIP-SHA256SUMS.txt`로 별도 제공한다. 기존 포터블 ZIP·앱 번들 ZIP과 혼동하지 않도록 설치 ZIP 이름에 `-installer`를 붙였다. 원본 EXE·DMG와 기존 로컬 출력은 보존했다.

## 구현

- `Scripts/package-installers-zip.py`: 프로젝트 버전에서 이름을 만들고 EXE·DMG 파일 형식을 검사한다. ZIP을 만든 뒤 CRC와 내부 설치 파일 SHA-256을 확인하며 ZIP 체크섬을 생성한다.
- 웹사이트의 다운로드 링크·표시 버전·용량과 `_redirects` 네 규칙을 추가·갱신했다. 기존 네 다운로드 주소는 유지한다.
- 링크·ARIA 검사, 다운로드 테스트와 HTTP 경로 검사를 새 버전·이전 버전 양쪽에 맞췄다. 경로 검사 URL을 별도 로컬 포트로 지정할 수 있으며 공개 호스트는 거부한다.
- ZIP과 내부 검증 목록을 웹 Git이나 Cloudflare 정적 자산에 추가하지 않았다. 공개 Release에는 ZIP 세 개·ZIP 체크섬만 업로드했다. API 키와 사용자 데이터는 포함하지 않았다.
- 앱 저장소의 기존 미커밋 변경을 커밋·푸시하지 않았다. GitHub 태그는 기존 기준 커밋 `37c6de354ceb390500d4bcb586bde1a74d16e205`을 가리키므로 자동 Source code 아카이브와 설치 바이너리의 전체 작업본이 다르다는 안내를 릴리스에 명시했다.
- 작업 중 웹 저장소에서 추가 스타일 변경을 발견했다. 해당 `public/styles/pages.css` 변경과 `.claude/`는 이 작업의 커밋에 포함하지 않고 보존했다. 실제 배포 자산 비교는 현재 미커밋 작업 폴더가 아닌 배포한 커밋을 기준으로 했다.

## 검증

### 로컬 자동 검사

- 설치 ZIP 세 개의 CRC·내부 설치 파일 SHA-256: 통과. [원본 설치 파일 검증](2026-10-01-installers.md)의 EXE·DMG와 동일하다.
- 웹 테스트 **44개 통과**, 실패·건너뜀 0.
- 정적 검사: HTML 2개·로컬 참조 78개, 링크·ID·ARIA·이미지 비율 검사 통과.
- 로컬 HTTP 검사 **77개 통과**. 이전 페이지 주소, 공개 자산 내용, 내부 파일 404와 다운로드 302 규칙을 확인했다.
- Cloudflare dry run: `public/` 자산 54개만 읽었으며 배포 준비 성공.
- Python 구문 검사와 `git diff --check`: 통과.

기존 로컬 서버의 자동 새로고침 코드 삽입 때문에 최초 파일 바이트 비교가 실패했다. 기존 서버를 종료하지 않고 4181 포트에 별도 서버를 실행해 `--live-reload=false`로 재검사했다. 제품 파일의 문제는 아니었다.

### 실제 공개 서버 검사

- 릴리스 초안 업로드 네 파일의 크기·SHA-256이 로컬과 일치한 뒤 공개했다.
- 공개 릴리스 네 자산의 인증 없는 HTTP 응답 200과 파일명·크기를 확인했다.
- 공개 HTML·CSS·JS **5개**가 배포한 커밋의 파일과 바이트 단위로 일치했다.
- 공개 HTML SHA-256: `acfca1f8b00698df9dd45a5e93a4bed9088c43a2717585c6171cdd494e30ab73`.
- 새 네 주소와 기존 네 주소, **다운로드 302 규칙 8개**의 대상 URL이 모두 정확했다.
- Mac ZIP 두 개를 웹사이트 주소에서 로그인 없이 전체 다운로드했다. ZIP 크기·해시, 세 항목 구성, 내부 DMG 해시가 모두 일치했다.
- ZIP 체크섬 파일도 웹사이트 주소에서 받아 로컬 원문과 일치함을 확인했다.

기본 Python HTTP 클라이언트가 공개 파일 요청에서 403을 받아 동일 검사를 curl로 수행했다. API 자격 증명을 사용하거나 서버 보안 설정을 변경하지 않았다.

### 실제 브라우저 확인

- 공개 페이지에서 Mac·Windows 탭의 링크와 Beta v1.0.1 표시를 확인했다.
- Windows 다운로드 버튼을 클릭해 실제 다운로드 이벤트와 저장된 ZIP을 확인했다. 저장 위치: `~/Downloads/Unfold-v1.0.1-beta-win-x64-installer.zip`.
- 내려받은 Windows ZIP의 크기·SHA-256·CRC와 내부 EXE 해시가 원본과 모두 일치했다.
- 데스크톱 1280px·모바일 375px에서 탭 전환과 가로 넘침 없음 확인. 임시 뷰포트는 복원했다.
- 공개 Windows 데스크톱과 Mac 모바일 화면을 기록했다.

## 근거와 남은 범위

검증 근거는 `artifacts/validation/2026-10-01-web-installers/`에 있다. 주요 파일은 `release-draft.json`, `release-anonymous-head.json`, `public-verification.json`, `windows-browser-download.json`, `web-tests-final.log`, `web-static-final.log`, `web-routes.log`, `web-dry-run.log`, `public-windows-desktop.jpg`, `public-mac-mobile.jpg`다.

이번 검증은 ZIP 다운로드와 배포 연결을 확인한다. Windows 설치·제거·재부팅, 실제 Intel Mac 실행, 실제 Google 로그인 복원과 Windows 코드 서명·macOS Developer ID 서명·공증은 이전 설치 파일 보고서의 미검증·미완료 상태를 유지한다.
