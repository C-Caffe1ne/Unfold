# 배포 Beta v1.0.1 상태 표시줄·트레이 브랜드 아이콘 감사

2026년 10월 1일. 사용자 요청에 따라 현재 웹 다운로드에 연결한 설치 ZIP 3종을 검사했다. 제품 코드·아이콘·배포 파일·웹사이트는 수정하지 않았다.

## 결과

| 대상 | 배포 파일 검사 결과 | 실제 OS 화면 확인 |
|---|---|---|
| macOS Apple Silicon 메뉴 막대 | 브랜드 아이콘 미적용. 주황색 원과 어두운 U를 코드로 그린다. | 앱 실행 확인. 메뉴 막대 아이콘 실화면 미검증. |
| macOS Intel 메뉴 막대 | Apple Silicon과 같은 기존 U 아이콘 생성 코드. | Intel 호스트 없음, 미검증. |
| Windows 알림 영역/트레이 | Mac과 같은 기존 U 아이콘 생성 코드. | Windows 호스트 없음, 미검증. |
| Mac 앱 파일 아이콘 | 두 DMG의 `Unfold.icns`가 라일락 브랜드 ICNS 원본과 동일. | Finder 아이콘 실화면 미검증. |
| Windows 앱·설치 파일 아이콘 | 앱 EXE와 설치 마법사 EXE에 라일락 브랜드 ICO 7개 해상도 이미지가 동일하게 포함. | Explorer·작업 표시줄 실화면 미검증. |

## 원인과 경계면

`src/Unfold.Desktop/AppRuntime.cs`의 `BuildTray()`는 32×32 픽셀 배열을 만들고 원 색 `0xFFF4B860`, U 색 `0xFF141820`를 채운다. 이 비트맵을 `TrayIcon.Icon`에 넣는다. Mac과 Windows가 이 생성 경로를 공유한다.

프로젝트의 `ApplicationIcon`과 macOS 번들의 `CFBundleIconFile`은 앱 파일 아이콘을 설정한다. 이 설정이 `BuildTray()`의 수제 비트맵을 대체하지 않는다. Windows 설치 마법사의 `APP_ICON`도 트레이 아이콘과 별도다.

## 실행한 검사

- 배포 ZIP 3종의 SHA-256이 직전 공개 검증의 값과 일치한다. 각 ZIP 안의 EXE·DMG가 원본 설치 파일과 바이트 단위로 같다.
- 두 DMG를 읽기 전용으로 마운트해 `Info.plist`, ICNS, 앱 DLL을 기존 설치 검증 앱과 비교했다. 세 파일 모두 동일하고 번들 버전은 1.0.1이다. ICNS SHA-256은 `d1726022d14939cea630b385ed2ff1a12827a696a72822ae7c4882f2827aa853`이다.
- Windows 앱 EXE와 DLL은 설치 파일 제작 입력 목록의 SHA-256과 일치한다. 앱 EXE·설치 마법사 EXE의 PE `RT_GROUP_ICON` 및 `RT_ICON`을 읽어 ICO 원본의 각 이미지와 비교했다. 각각 7개 표현이 모두 일치한다.
- 제품 소스를 다시 빌드하지 않고 배포 DLL 3종의 `BuildTray()` IL을 읽었다. 모두 두 U 아이콘 색상 상수를 포함한다. 메서드 IL SHA-256은 세 플랫폼 모두 `c4e68e65ddf01a65669d5ef288b71361aea9f30f33c9122ee67766c3e0411d46`로 동일하다.
- 위 메타데이터 검사 전용 도구 빌드는 종료 코드 0, 경고·오류 0이다. 제품 회귀 테스트는 제품 변경이 없어 재실행하지 않았다.

근거: `artifacts/validation/2026-10-01-status-icons/package-icons.json`, `tray-il.json`, 검사 도구 소스.

## 실제 macOS와 미검증

배포 Apple Silicon 앱을 새 임시 `UNFOLD_DATA_DIR`로 실행해 로그인 창이 정상 표시되는 것을 확인했다. 네이티브 앱 캡처는 로그인 창만 제공했다. 메뉴 막대 호스트 관찰 요청은 시간 초과로 끝났으므로 실제 메뉴 막대 픽셀을 확인했다고 주장하지 않는다. 감사에 사용한 앱 프로세스와 DMG 마운트는 종료·해제했다.

Windows와 Intel Mac 실행 화면은 현재 환경에서 확인하지 않았다. 배포 DLL의 명시적인 아이콘 생성 코드로 미적용을 판정했으며, 이를 실기기 UI 검증과 구분한다.

## 필요한 수정

공통 `BuildTray()`에서 수제 U 비트맵을 브랜드 리소스로 교체해야 한다. Windows 트레이용 크기와 macOS 메뉴 막대용 작은 아이콘의 가독성을 각각 검사하고, 반영한 앱을 재패키징해야 공개 다운로드에도 적용된다. 이번 요청은 확인 작업이므로 수정·재배포하지 않았다.
