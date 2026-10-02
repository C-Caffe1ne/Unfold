# 최초 실행 계정 화면 편집

2026-09-28 · 승인된 A안: 왼쪽 펫 패널, 오른쪽 Google 로그인·구매 화면.
신규 첫 실행 계정 화면에서는 로컬 앱으로 바로 넘어가는 하단 버튼을 제공하지 않는다. 구매 권한이
확인되면 자동으로 앱에 진입한다. 이미 안내를 닫은 프로필과 백그라운드 시작까지 막는 전역 권한 잠금은 별도 범위다.

## 편집 위치

| 변경 항목 | 파일 | 반영 방법 |
|---|---|---|
| 제목, 버튼 이름, 상태 문구, 브랜드 문구 | `Assets/Account/entry-screen.json`의 `copy`, `brand`, `companionText` | JSON 수정 후 빌드·재실행 |
| 표시 가격·통화 | 같은 파일의 `markets`, `defaultMarket` | KRW·USD를 각각 수정. 실제 청구액과 구매 권한은 변경되지 않음 |
| 왼쪽 펫 이미지 | `web/assets/pet-rest.png` | 현재 시안의 Mochi 원본. 빌드 시 `Assets/Account/companion.png`로 복사 |
| 다른 펫 이미지 | `Assets/Account/`에 PNG 추가 후 `companionImage` 변경 | 폴더 없이 파일명만 지정. 빌드·재실행 |
| 레이아웃·간격·글자 크기 | `src/Unfold.Desktop/AccountWindow.axaml` | XAML 수정 후 빌드 |
| 전체 색상·공통 버튼·모서리 | `DesignSystem.Themes.cs`, `DesignSystem.cs` | 기존 4개 테마를 공유. 계정 화면용 별도 팔레트 없음 |
| Supabase 프로젝트·공개 키·콜백 포트·환경·판매 시장 | `Assets/Account/connection.json` | `checkoutMarkets`로 노출 시장 관리. 비밀 키는 넣지 않음 |
| 화면 상태·버튼 동작 | `AccountScreenModel.cs` | 로그인·구매 확인과 시각 표현을 분리 |
| 브라우저 인증 연결 | `DesktopAccountService.cs` | 시스템 브라우저, 콜백 수신, Supabase 클라이언트 호출 |

예: `welcomeTitle`의 `\n`은 줄바꿈이다. `markets`의 `amountMinor`는 최소 통화 단위다.
KRW `4900` / `minorUnitDigits: 0` → `4,900원`, USD `399` / `minorUnitDigits: 2` → `US$3.99`.
`entry-screen.json`에는 후속 Global 표시값도 남아 있지만, 현재 `connection.json`의
`checkoutMarkets`는 `KR`만 허용하므로 실제 화면과 주문 생성에는 한국 가격만 나타난다.

JSON과 이미지가 출력·게시 폴더에 복사된다. 변경은 위 표의 원본 파일에서 하고 현재 체크아웃을
다시 빌드해 확인한다. 출력·이전 배포 폴더의 복사본을 수정하면 원본에 반영되지 않는다.
배포용 변경은 원본에서 하고 다시 빌드·서명한다.
잘못된 JSON·누락 필드·중복 시장·잘못된 이미지 경로는 내장 기본 문구로 복구한다.
이미지 파일 자체를 읽지 못하면 이미지를 생략하고 앱은 계속 사용할 수 있다.

## 현재 동작

- 새 로컬 프로필의 첫 일반 실행에서 계정 화면을 연다. 구매 확인이 완료되면 창을 닫고 홈에 진입하며,
  좌측 하단 `Unfold 종료`는 확인 후 타이머와 펫을 포함한 앱 전체를 종료한다.
- 이후에는 설정 탭 → 계정 → `로그인 · 구매`에서 다시 연다. 중복 창을 만들지 않는다.
- `account-welcome-seen`은 안내를 닫았다는 기록일 뿐, 로그인·구매 권한을 뜻하지 않는다.
  파일에는 현재 안내 화면의 버전이 저장된다. 로그인·결제 흐름처럼 최초 화면이 크게 바뀌면
  이전 버전의 종료 기록은 사용하지 않고 새 화면을 한 번 다시 표시한다.
- 백그라운드 자동실행에서는 계정 창을 띄우지 않는다. 현재 접근 제어는 적용하지 않는다.
- Google 버튼은 기본 브라우저로 PKCE 인증을 시작한다. 5분 시간 제한, 취소, 중복 클릭 방지,
  포트 점유 시 오류 복구가 있다. 콜백은 `127.0.0.1`에만 바인딩한다.
- 로그인 성공 후 서버에서 구매 권한을 조회한다. 조회 실패는 미구매로 간주하지 않고 `다시 확인`을 제공한다.
- 구매 권한 응답에는 본인에게만 보이는 서버 관리 역할도 포함된다. 구매가 확인된 계정의 사용자 ID,
  이메일과 역할만 앱 실행 중 메모리에 남기며, `admin` 역할에서만 설정의 디버그 도구를 표시한다.
- 미구매 상태의 `구매하기`는 인증된 `create-checkout`을 호출한다. 앱은 서버가 반환한 Lemon Squeezy
  HTTPS 호스트·Checkout ID·서명 쿼리를 검사한 뒤 시스템 브라우저로 연다.
- 결제창을 연 뒤 `구매 확인`으로 서버 권한을 다시 조회한다. 브라우저를 여러 번 열지 않으며,
  확인 전에는 앱 접근 권한을 임의로 활성화하지 않는다.
- 인증 토큰·이메일은 파일에 쓰지 않는다. 계정 창을 닫으면 토큰 세션을 폐기하므로 다시 열 때 로그인해야 한다.
  확인된 계정의 안전한 식별 정보는 앱을 종료할 때까지 런타임에만 유지한다.
  자동 로그인·OS 보안 저장소·오프라인 권한과 전역 사용 잠금은 후속 작업이다.

`connection.json`은 현재 테스트 환경이다. 공개 `sb_publishable_` 키만 허용한다.
`UNFOLD_SUPABASE_URL`, `UNFOLD_SUPABASE_PUBLISHABLE_KEY` 환경 변수가 있으면 해당 연결 값을 우선한다.
콜백 포트 변경 시 Supabase의 허용 반환 URL도 일치시켜야 한다. 현재 원격 Test mode에는
한국 Checkout과 Lemon 웹훅이 배포돼 있다.
Global을 추가할 때는 서버 카탈로그 매핑을 먼저 배포한 뒤 `checkoutMarkets`에 `GLOBAL`을 추가한다.

## 실행과 검증

```sh
dotnet run --project src/Unfold.Desktop -c Release
dotnet test Tests/Unfold.Tests -c Release --filter 'FullyQualifiedName~Account'
```

새 최초 실행 화면을 검증할 때는 기존 데이터를 지우지 않고 새 `UNFOLD_DATA_DIR`을 사용한다.
[구현·검증 기록](validation/2026-09-28-account-screen.md)에 검증 범위와 미검증 항목을 구분했다.

구현 참고: [Avalonia 데이터 바인딩](https://docs.avaloniaui.net/docs/data-binding/introduction-to-data-binding),
[Supabase PKCE](https://supabase.com/docs/guides/auth/sessions/pkce-flow).
