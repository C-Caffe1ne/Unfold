# 최초 실행 계정 화면 편집

2026년 10월 8일 · Beta v1.1.2 기준 (배포 소스 `ce86c7b`). 최신 공개 배포는 Beta v1.1.2다.
승인된 A안은 왼쪽 펫 패널, 오른쪽 Google 로그인·구매 화면이다.
Google 로그인 또는 저장된 세션 복원 후 Live 구매 권한이나 계정의 무료 이용 권한이 확인되면 홈·펫·타이머를 시작한다.
테스터는 로그인 후 `코드 입력`에서 `admin`을 등록한다. 앱 로컬 우회가 아니라 Supabase 계정에 저장되는 무료 권한이다.

## 편집 위치

| 변경 항목 | 파일 | 반영 방법 |
|---|---|---|
| 제목, 버튼 이름, 상태 문구 | `Assets/Account/entry-screen.json`의 `copy` | JSON 수정 후 빌드·재실행 |
| 상단 브랜드 로고 PNG | `Art/Brand/unfold-lilac-v1/png/logo/logo-lilac-640.png`, `logo-light-640.png` | 빌드에 포함. 어두운 테마에서는 밝은 로고를 자동 사용 |
| 표시 가격·통화 | 같은 파일의 `markets`, `defaultMarket` | KRW·USD를 각각 수정. 실제 청구액과 구매 권한은 변경되지 않음 |
| 왼쪽 펫 이미지 | `web/assets/pet-rest.png` | 현재 시안의 Mochi 원본. 빌드 시 `Assets/Account/companion.png`로 복사 |
| 다른 펫 이미지 | `Assets/Account/`에 PNG 추가 후 `companionImage` 변경 | 폴더 없이 파일명만 지정. 빌드·재실행 |
| 레이아웃·간격·글자 크기 | `src/Unfold.Desktop/AccountWindow.axaml` | XAML 수정 후 빌드 |
| 전체 색상·공통 버튼·모서리 | `DesignSystem.Themes.cs`, `DesignSystem.cs` | 기존 4개 테마를 공유. 계정 화면용 별도 팔레트 없음 |
| Supabase 프로젝트·공개 키·콜백 포트·환경·판매 시장 | `Assets/Account/connection.json` | `checkoutMarkets`로 노출 시장 관리. 비밀 키는 넣지 않음 |
| 화면 상태·버튼 동작 | `AccountScreenModel.cs` | 로그인·구매 확인과 시각 표현을 분리 |
| 브라우저 인증 연결 | `DesktopAccountService.cs` | 시스템 브라우저, 콜백 수신, Supabase 클라이언트 호출 |
| 로그인 유지·OS 보안 저장 | `AccountSessionVault.cs`, `NativeAccountCredentialStore.cs` | 갱신 자격 증명 저장·교체·삭제, 프로젝트·데이터 프로필 격리 |

예: `welcomeTitle`의 `\n`은 줄바꿈이다. `markets`의 `amountMinor`는 최소 통화 단위다.
KRW `4900` / `minorUnitDigits: 0` → `4,900원`, USD `399` / `minorUnitDigits: 2` → `US$3.99`.
`entry-screen.json`에는 후속 Global 표시값도 남아 있지만, 현재 `connection.json`의
`checkoutMarkets`는 `KR`만 허용하므로 실제 화면과 주문 생성에는 한국 가격만 나타난다.
결제 화면에는 가격 텍스트만 표시하고 시장·통화 선택 드롭다운은 표시하지 않는다. 기본 가격과 주문 시장은 설정 파일의 `defaultMarket` 및 허용된 `checkoutMarkets`에서 정한다.
왼쪽 패널에는 Mochi 이미지만 중앙에 표시한다. 기존 `companionText` 필드는 호환성을 위해 유지하되 화면에 표시하지 않으며, `brand`는 로고의 접근성 이름으로 사용한다.

JSON과 이미지가 출력·게시 폴더에 복사된다. 개발 중 해당 출력 JSON을 직접 수정하면 재컴파일 없이
재실행으로 확인할 수 있지만, 다음 빌드에는 원본 파일을 사용한다. 배포용 수정은 원본에서 하고 다시 빌드·서명한다.
잘못된 JSON·누락 필드·중복 시장·잘못된 이미지 경로는 내장 기본 문구로 복구한다.
이미지 파일 자체를 읽지 못하면 이미지를 생략하고 앱은 계속 사용할 수 있다.

## 현재 동작

- 상단 영역을 끌어 로그인·구매 창을 이동한다. 좌측 하단 `Unfold 종료`는 확인 후 앱 전체를 종료한다.
- 일반 실행·자동실행 모두 로그인·구매 확인이 필요하다. 이전 `account-welcome-seen`은 판매용 접근 제어에 사용하지 않는다.
- 이용 권한 확인 전에는 창 닫기·트레이·홈 호출로 진입할 수 없다. 코드 입력은 Google 로그인 후 우측 하단에 표시한다.
- Google 버튼은 시스템 브라우저의 PKCE 인증과 `127.0.0.1` 콜백을 사용한다. 5분 제한·취소·중복 클릭 방지와 포트 점유 오류 처리를 유지한다.
- 로그인 후 `get-app-access`에서 Live 구매 또는 유효한 무료 이용 권한을 조회한다. 실패하면 `다시 확인`을 제공하고 잠금을 유지한다.
- `admin` 등록은 `redeem-access-code`가 Google 계정을 검증하고 저장한다. 저장 후 권한을 다시 조회해야 홈으로 진입한다. 이후 같은 계정으로 로그인하면 코드 재입력 없이 복원된다. 코드가 잘못됐거나 제한을 초과하면 모달에 오류를 표시한다.
- 미구매 상태의 `구매하기`는 서버가 선택한 한국 Live 상품 Checkout을 요청한다. 앱은 HTTPS 호스트·Checkout ID·서명 쿼리를 검사한 뒤 브라우저로 연다.
- `구매 확인` 또는 코드 등록 후 유효한 이용 권한이 확인되면 홈·타이머·펫을 시작한다. Test 구매는 이용 권한으로 인정하지 않는다.
- 설정에는 Google 아이콘·이메일과 `로그아웃`을 표시한다. 로그아웃은 실행 기능을 멈추고 메모리와 OS 보안 저장소의 로그인 정보를 지운 뒤 Supabase 현재 세션의 종료를 요청한다. 네트워크 실패도 로컬 삭제를 되돌리지 않는다. 보안 저장소 삭제 자체가 실패하면 별도 오류를 표시한다. 설정·기록·커스텀 펫은 삭제하지 않는다.
- 실행 중 5분마다 서버 권한을 다시 확인하고 필요하면 토큰을 갱신한다. 권한 취소나 조회 실패는 실행을 멈추고 구매 확인 화면으로 돌아간다. 잠금 동안 편집 중 초안은 메모리에 보존한다.
- 2026-10-01 작업본부터 Google 로그인 성공 직후 갱신 토큰과 계정 ID만 macOS Keychain 또는 Windows 자격 증명 관리자에 저장한다. 접근 토큰·이메일·이용 권한은 일반 파일에 저장하지 않는다. 종료 시 저장하는 방식에 의존하지 않는다.
- 앱 재실행·컴퓨터 재시작 후 저장된 갱신 토큰을 Supabase와 교환하고 `get-app-access`를 다시 확인한다. 유효한 구매·무료 권한이면 Google 로그인과 코드 입력 없이 시작한다. 자동실행은 복원 성공 시 설정 창을 열지 않는다.
- 시작 시 로그인 창을 만들기 전에 저장된 세션과 이용 권한을 확인한다. 복원 성공은 바로 앱을 시작하고, 로그인·구매·재시도가 필요할 때 계정 창을 표시한다. 복원 중 설정 열기 요청은 완료 후 처리하며, 이때 자동실행도 설정 창을 연다. 복원 중 트레이의 종료는 확인 창 없이 복원을 취소하고 종료한다.
- 교체된 갱신 토큰은 권한 조회 전에 즉시 보관한다. 복원·갱신·로그아웃을 직렬화하여 이전 토큰 재사용과 늦은 응답의 로그인 복구를 방지한다. 계정 변경도 저장 정보를 삭제한다.
- 통신 실패는 저장 정보를 유지하고 `다시 시도`로 복원을 재시도한다. 서버에서 세션 무효가 확인되면 저장 정보를 지우고 Google 로그인을 요구한다. 프로젝트·환경·데이터 프로필별로 저장 항목을 구분하고 평문 파일로 대체하지 않는다. 오프라인 이용 권한은 제공하지 않는다.
- 진단 모드는 격리된 프로필의 자동 검사에서만 사용한다. 정상 실행의 잠금을 대체하지 않는다.

`connection.json`의 환경은 `live`이며 클라이언트는 Test 연결 설정을 거부한다.
공개 `sb_publishable_` 키만 포함한다. 프로젝트 URL·공개키 환경 변수 재정의와 콜백 포트 계약은 유지한다.
무료 이용 권한은 결제 서버의 Test 모드에서도 동작한다. 현재 실제 판매 결제의 운영 전환은
[판매 시작 안내](releases/beta-v1.0.0-launch.md)를 따른다.

## 실행과 검증

```sh
dotnet run --project src/Unfold.Desktop -c Release
dotnet test Tests/Unfold.Tests -c Release --filter 'FullyQualifiedName~Account'
```

새 최초 실행 화면을 검증할 때는 기존 데이터를 지우지 않고 새 `UNFOLD_DATA_DIR`을 사용한다.
[구현·검증 기록](validation/2026-09-28-account-screen.md)에 검증 범위와 미검증 항목을 구분했다.
로그인 유지 수정은 [2026-10-01 검증](validation/2026-10-01-session-persistence.md)을 따른다.
재실행 때 로그인 창이 잠깐 나타나던 시작 순서 수정은 [2026-10-07 검증](validation/2026-10-07-login-startup.md)에 기록했다.
로고·Mochi 배치는 [계정 화면 브랜드 적용 검증](validation/2026-10-01-account-branding.md)을 따른다.
공개 Beta v1.1.1은 세션 보안 저장과 로그인 창 표시 전 복원을 포함한다.
Beta v1.1.2 지정만으로 설치된 앱이나 공개 업데이트 채널을 교체하지는 않는다.

구현 참고: [Avalonia 데이터 바인딩](https://docs.avaloniaui.net/docs/data-binding/introduction-to-data-binding),
[Supabase PKCE](https://supabase.com/docs/guides/auth/sessions/pkce-flow).

무료 이용 코드 발급·회수·배포 절차는 [계정별 무료 이용 코드](access-codes.md)를 따른다.
