# 로그인 후 브라우저 확인 화면

2026-10-18 · 기준 `codex/pet-overlay-hover`, HEAD `5d0f86e`, 앱 `1.2.0`.
작업 경로는 `/Users/hwanghyeonseong/Developer/GitHub/Unfold/.worktrees/pet-overlay-hover`다.
시작 시 변경 없음, 원격 브랜치와 HEAD 일치, 최신 공개 `v1.2.0` 제품 소스 `99db377`을 확인했다.

## 원인과 변경

Google 로그인 반환 시 로컬 콜백이 안내 문장만 `text/plain`으로 보냈다.
이를 기존 Unfold 로고·펫 그림, 라일락 배경, 제목과 보조 안내로 구성한 반응형 HTML로 바꿨다.
문구의 첫 줄은 제목, 다음 줄은 보조 안내다. 줄바꿈 없는 사용자 편집 문구도 표시한다.
한국어·영어·일본어·중국어와 문서 언어 태그를 연결했다.

그림은 어셈블리 리소스에서 data URI로 넣으며 외부 요청·스크립트·버튼은 없다.
구성 문구를 HTML 인코딩하고 스타일의 정확한 SHA-256 해시만 CSP에 허용한다.
로그인 자격 증명이나 쿼리는 HTML에 넣지 않는다. 취소도 같은 중립적인 앱 복귀 안내를 표시한다.
잘못된 요청의 400 응답과 인증 코드 교환·저장·구매 확인 흐름은 유지한다.

## 소유 파일

- `src/Unfold.Desktop/AccountBrowserReturnPage.cs`: HTML·CSS·리소스·CSP
- `src/Unfold.Desktop/DesktopAccountService.cs`: 콜백의 HTML 응답
- `src/Unfold.Desktop/Unfold.Desktop.csproj`: 기존 그림 두 개를 리소스로 포함
- `Assets/Account/entry-screen.json`, `src/Unfold.Core/Localization.Strings.cs`: 안내 줄바꿈과 번역
- `Tests/Unfold.Tests/AccountBrowserReturnPageTests.cs`, `AccountScreenTests.cs`: 관련 검사

## 검증

- 새 임시 `UNFOLD_DATA_DIR` 사용. Release 테스트가 Desktop·Core 빌드도 수행했다.
- 첫 집중 실행: `AccountBrowserReturnPageTests|AccountLoopbackTests|AccountClientTests` **40/40 통과**.
- 제목·안내 분리 후: `AccountBrowserReturnPageTests|AccountLoopbackTests|LocalizationTests`와
  `AccountPresentationTests.EditedCopyAndIndependentCurrencyPricesLoadWithoutCodeChanges` **26/26 통과**.
- 최초 sandbox 실행은 MSBuild 로컬 소켓 권한 때문에 시작하지 못했다. 권한이 허용된 실행의 위 결과와 구별한다.
- `git diff --check` 통과. 독립 읽기 전용 코드 검토에서 조치가 필요한 결함 없음.
- 컴파일된 렌더러로 만든 HTML과 실제 CSP 헤더를 로컬 브라우저에 표시해 로고·그림·스타일을 확인했다.
  한국어·영어 320px 프레임에서도 잘림을 확인하지 못했다. 프레임 검수용 서버에서만
  `frame-ancestors`를 `self`로 바꾸었으며 제품은 `none`을 유지한다.

## 미검증·위험

실제 Google OAuth 로그인 전체 흐름, Windows 브라우저, 보조기술은 이번에 실행하지 않았다.
전체 테스트·앱 OS 진단·설치 앱 교체·배포는 수행하지 않았다.
