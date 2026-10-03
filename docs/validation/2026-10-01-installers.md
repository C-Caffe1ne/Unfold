# Beta v1.0.1 설치 파일 검증

2026년 10월 1일. Windows 설치 마법사 EXE, Apple Silicon·Intel Mac DMG를 생성했다.
직전 작업의 로그인 유지 수정도 포함한다. 공개 웹사이트에 연결하는 작업은 아직 수행하지 않았다.

후속 작업에서 설치 ZIP을 만들고 웹사이트 연결을 완료했다. 현재 공개 다운로드 결과는 [설치 ZIP·웹 배포 검증](2026-10-01-web-installers.md)에 있다. 아래 내용은 ZIP 게시 전 설치 파일 제작 단계의 검증 기록이다.

## 환경과 범위

- macOS Apple Silicon, .NET 10, Avalonia 12.1.2, NSIS 3.12, 7-Zip 26.03.
- 프로젝트 버전 `1.0.1-beta`, 표시 버전 `Beta v1.0.1`, Mac 빌드 번호 `1000001`.
- 현재 작업 폴더의 코드와 리소스로 게시했다. Git HEAD `37c6de354ceb390500d4bcb586bde1a74d16e205`에는 기존 미커밋 작업이 있으므로 HEAD만으로 이 파일을 재현할 수 없다. 기존 변경을 보존했고 커밋·푸시·GitHub Release 게시를 수행하지 않았다.
- 자체 포함 .NET 런타임, 기본 펫 5종, 폰트·아이콘, LGPL FFmpeg와 라이선스를 포함한다.

## 설치 파일

| 플랫폼 | 파일 | 바이트 | SHA-256 |
|---|---|---:|---|
| Windows x64 | `artifacts/Unfold-v1.0.1-beta-win-x64-setup.exe` | 77,443,999 | `43c0880387916a05421b183f25621d5d40c7051b30f42ff11749f511d2f20cce` |
| Apple Silicon | `artifacts/Unfold-v1.0.1-beta-osx-arm64.dmg` | 75,761,904 | `abc251d4ecc8b323bdbb4abb7e03922c2e4f5682f2855804043514154ed2a368` |
| Intel Mac | `artifacts/Unfold-v1.0.1-beta-osx-x64.dmg` | 79,837,673 | `ed24a38e2981f62882ac9c13ca2994f7b7ce728b9f0d13e8db315d20eb7a0d60` |

공개 업로드 대상은 위 세 파일과 `artifacts/Unfold-v1.0.1-beta-SHA256SUMS.txt`다.
`*-installer-inputs.json`과 `artifacts/validation/`은 내부 검증 근거이므로 공개 릴리스에 올리지 않는다.
기존 ZIP·tar.gz 출력도 유지했다.

## 구현과 경계면 검증

### Windows

- `Packaging/Unfold.Windows.nsi`: 한국어·영어 설치 마법사. 현재 사용자 계정의 `%LOCALAPPDATA%\Programs\Unfold`에 설치한다. 관리자 권한을 요청하지 않는다.
- 시작 메뉴 바로가기와 Windows 앱 제거 항목을 등록한다. 바탕화면 바로가기는 선택 항목이다.
- 앱의 `Program.Main`과 동일한 데이터 경로·`.instance.lock`을 독점으로 열고 설치·제거 동안 유지한다. 앱 실행 중 파일 교체를 막도록 설계했다. 취소·잘못된 플랫폼은 오류 종료 코드를 반환한다.
- 기존 자동실행이 활성화된 경우 인용부호가 포함된 새 실행 경로로 갱신한다. 새로운 자동실행 항목을 임의로 켜지 않는다.
- 제거 프로그램은 NSIS의 임시 실행 스텁을 고려해 `$INSTDIR`을 검증한다. 관리되는 앱 폴더만 제거하고 설정·기록·커스텀 펫·OS 계정 자격 증명은 보존하도록 설계했다.
- `Scripts/package-windows-installer.py`가 게시 입력, AMD64 실행 파일, 라이선스·미디어 파일을 확인하고 NSIS를 경고도 실패로 처리하는 옵션으로 실행한다.
- `Scripts/verify-windows-installer.ps1`을 Windows CI에 연결했다. 격리 호스트에서 설치, 전체 파일 해시, 시작 메뉴, 실행 파일 버전, 실행 중 설치 거부, 재설치·자동실행 경로 갱신, 제거·사용자 데이터 보존을 검사하도록 작성했다. 기존 앱 또는 자동실행 항목이 있으면 거부한다. **이 스크립트는 이번 로컬 macOS 작업에서 실제 실행하지 않았다.**

### macOS

- `Scripts/make-macos-bundle.sh`에서 기존 앱·ZIP·tar.gz 이후 `Scripts/package-macos-dmg.sh`를 호출한다.
- DMG에는 `Unfold.app`, `/Applications` 바로가기, 한국어 설치 안내가 있다. 앱을 Applications에 드래그한 뒤 실행하고 디스크를 추출한다.
- 버전과 실행 파일 아키텍처를 검사하고 압축 이미지 무결성을 확인한다. 번들 빌드 번호는 이전 값 6보다 큰 `1000001`이다.
- 기존 ad-hoc 서명 경로와 선택적 Developer ID 서명 경로를 유지했다. 새 entitlement를 추가하지 않았다.

## 실행한 검사와 결과

### 자동 검사

- NSIS 설치 파일 컴파일: 성공, 경고 0, 오류 0.
- 7-Zip 설치 파일 전체 추출·CRC 검사: 성공. 추출한 앱 파일 **262개**의 SHA-256이 원본 게시 입력과 전부 일치했다. 기본 펫 5종과 설치 안내·릴리스 노트·라이선스도 확인했다.
- 두 DMG의 `hdiutil verify`: 성공. 실행 파일 아키텍처, 버전, 설치 안내, Applications 링크를 확인했다.
- 로그인·이용 권한 회귀 검사: **101개 통과**, 실패·건너뜀 0. 새 `UNFOLD_DATA_DIR`로 Release 빌드에서 실행했다.
- 기본 펫 검사: **5종·65개 클립**, 오류·경고 0.
- Bash 구문 검사, Python 컴파일 검사, `git diff --check`: 통과.

### 실제 macOS에서 실행한 자동 진단

- Apple Silicon DMG를 읽기 전용으로 마운트하고 앱을 별도 검증 폴더에 복사했다. 마운트를 해제한 뒤 복사된 앱에서 검사했다.
- 복사 전후 `codesign --verify --deep --strict`: 성공.
- 설치된 실행 파일의 `--version`: `Unfold Beta v1.0.1 (1.0.1-beta)`.
- 새 테스트 프로필의 `--smoke-test`: 종료 코드 0, `success: true`, 오류 0, **PNG 93장**.
- 진단은 화면 밖 창과 프로그램으로 호출한 동작을 사용한다. Finder의 실제 드래그, Google 로그인, 키보드·마우스 조작이나 다른 Mac의 최초 실행 승인을 검증한 결과는 아니다.

근거는 `artifacts/validation/2026-10-01-installers/`에 저장했다. 주요 파일은 `account-release.trx`, `windows-installer-final.log`, `windows-extraction.log`, `windows-package.json`, `arm64-contents.json`, `intel-contents.json`, `character-report.json`, `macos-smoke-summary.json`, `release-assets.json`이다.

## 통과·미검증과 배포 상태

- 통과: 세 설치 파일 생성·무결성, Windows 게시 입력과 아카이브 일치, Apple Silicon 앱 실행·진단, 계정 관련 회귀 검사.
- 실제 Windows 설치·업그레이드·제거·자동실행·재부팅: **미검증**. CI 구현을 현재 체크아웃에서 원격 실행한 결과도 없다.
- 실제 Intel Mac 실행: **미검증**. Intel 바이너리와 DMG·서명 검사는 통과했으나 Intel 기기에서 실행하지 않았다.
- 사람의 설치 마법사 조작·Finder 드래그 검수: **미검증**.
- Windows Authenticode 서명, macOS Developer ID 서명·공증: **미완료**. SmartScreen·Gatekeeper 경고가 나타날 수 있다.
- 공개 웹사이트의 다운로드 링크는 기존 Beta v1.0.0 파일을 유지한다. 이번 Beta v1.0.1 설치 파일은 로컬 배포 후보이며 웹사이트 게시 완료를 의미하지 않는다.

## 다음 검증 순서

1. 격리된 Windows 환경에서 설치·재설치·제거 검사를 실행하고 실제 마법사 화면을 확인한다.
2. 실제 Intel Mac에서 DMG 설치와 실행을 확인한다. 각 플랫폼에서 Google 로그인 후 앱 종료·재실행·재부팅의 계정 복원을 확인한다.
3. 공개 배포에 필요한 서명·공증을 준비한다. 새 릴리스에 세 설치 파일과 체크섬을 업로드하고 파일 해시를 검증한다.
4. 웹사이트 다운로드 버튼을 Windows EXE·Mac DMG URL과 새 버전·크기로 바꾼 뒤 배포된 주소에서 실제 다운로드를 검증한다.
