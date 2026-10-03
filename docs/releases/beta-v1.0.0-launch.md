# Beta v1.0.0 판매 시작 안내

2026-09-30 · 현재 상태: 판매용 로컬 후보 완성 중, Live 결제·공개 배포 서명 대기.

## 준비할 외부 설정

1. Lemon Squeezy 스토어의 운영 활성화·심사를 완료하고 Live 모드로 전환한다.
2. 한국용 일회성 상품을 게시한다. 구독·체험·자유 가격은 사용하지 않고, 한국 가격은 4,900원·세금 별도로 설정한다. 운영 Store ID, Product ID, 숫자 Variant ID와 Checkout Host를 확인한다. Test ID나 Checkout 공유 UUID를 대신 사용하지 않는다.
3. Live API 키를 발급하고 Supabase의 **Edge Functions → Secrets**에 `LEMONSQUEEZY_LIVE_API_KEY`로 등록한다. 비밀값을 채팅·Git·앱·웹 저장소에 넣지 않는다.
4. Lemon Live 모드에서 아래 주소의 웹훅을 등록하고 `order_created`, `order_refunded` 이벤트를 선택한다. 웹훅 Secret은 Supabase의 `LEMONSQUEEZY_LIVE_WEBHOOK_SECRET`에만 등록한다. Test 웹훅 Secret과 구분한다.

   `https://xrelgkdawkogrwxmwkcx.supabase.co/functions/v1/lemon-webhook`

현재 원격 프로젝트는 Test 구성이다. 이 안내의 서버 전환은 아직 실행하지 않았다. API 키를 공개키로 대체하거나 Test 결제 권한을 Live 권한으로 바꾸지 않는다.

## 서버 전환

권한이 있는 Supabase CLI 로그인 상태에서 프로젝트 루트에서 실행한다. 먼저 Checkout을 비활성화한 상태로 새 마이그레이션·함수를 배포한다.

```sh
supabase link --project-ref xrelgkdawkogrwxmwkcx
supabase secrets set UNFOLD_CHECKOUT_ENABLED=false
supabase db push
supabase functions deploy create-checkout
supabase functions deploy get-entitlement
supabase functions deploy lemon-webhook
```

`supabase/config.toml`의 `create-checkout`, `get-entitlement`는 사용자 인증을 유지한다. `lemon-webhook`은 JWT 대신 원문 HMAC 서명을 검증한다.

운영자의 SQL Editor에서 아래 `LIVE_...` 값을 실제 숫자 ID로 바꾼 뒤 실행한다. 아직 확인되지 않은 Live ID는 이 저장소에 지정하지 않았다. Host가 다르면 함께 교체한다.

```sql
begin;
insert into public.lemon_prices
  (price_id, environment, store_id, variant_id, product_id, checkout_host)
values
  ('unfold-kr', 'live', 'LIVE_STORE_ID', 'LIVE_VARIANT_ID', 'LIVE_PRODUCT_ID',
   'dokhustudio.lemonsqueezy.com')
on conflict (price_id, environment) do update
set store_id=excluded.store_id, variant_id=excluded.variant_id,
    product_id=excluded.product_id, checkout_host=excluded.checkout_host;
update public.prices set live_enabled=true
where id='unfold-kr' and product_id='unfold' and currency='KRW' and amount_minor=4900;
commit;
```

운영 키·웹훅 Secret 등록을 끝낸 뒤 환경을 전환한다.

```sh
supabase secrets set UNFOLD_ENVIRONMENT=live UNFOLD_CHECKOUT_ENABLED=true
```

서버 환경은 요청 본문이나 URL로 변경할 수 없다. 앱의 `Assets/Account/connection.json`은 이미 `live`·`KR`로 설정돼 있다. 해외 가격의 `live_enabled`는 false로 유지한다.

## 실제 구매 검증

- 새 테스트 계정으로 후보 앱을 실행한다. 기존 프로필에서도 홈·타이머·펫이 구매 전에는 나타나지 않아야 한다.
- Google 로그인 → 구매하기 → 실제 Live 결제창에 한국 상품·4,900원과 별도 세금이 표시되는지 확인한다. 실제 결제 승인은 운영자가 수행한다.
- 앱의 구매 확인을 누르면 Live 권한이 활성화되고 홈·펫·타이머가 시작돼야 한다. 주문 이메일만으로 이 단계를 통과한 것으로 간주하지 않는다.
- 중복 웹훅이 주문을 중복 처리하지 않고, 전액 환불 시 Live 권한이 취소되는지 확인한다. 앱은 다음 5분 주기 권한 확인에서 잠긴다.
- 재실행·자동실행·로그아웃·통신 단절·다시 확인을 각각 검증한다. 현재 베타는 매 실행 온라인 로그인이 필요하며 오프라인 사용을 제공하지 않는다.
- 실제 결제·환불은 비용과 고객 권한에 영향을 주므로 운영자가 지정한 검증 계정으로 진행하고, 확인된 주문 ID만 검증 기록에 남긴다. 비밀 토큰은 기록하지 않는다.

## 공개 배포 파일

로컬 ZIP 후보는 Apple Silicon, Intel Mac, Windows x64 세 가지이며 버전은 `1.0.0-beta`다. 라이선스·기본 펫 5종·브랜드 아이콘과 자체 포함 런타임을 포함한다.

macOS 공개 배포 전에 Apple Developer 계정의 **Developer ID Application** 인증서를 준비한다. 현재 호스트에는 Apple Development 인증서만 있으며 Developer ID 서명·공증을 실행하지 않았다.

```sh
UNFOLD_CODESIGN_IDENTITY='Developer ID Application: 발급된 실제 인증서 이름' \
  bash Scripts/make-macos-bundle.sh osx-arm64
xcrun notarytool submit artifacts/Unfold-v1.0.0-beta-osx-arm64.zip \
  --keychain-profile '운영자가 등록한 공증 프로필' --wait
xcrun stapler staple artifacts/Unfold.app
xcrun stapler validate artifacts/Unfold.app
spctl --assess --type execute --verbose=2 artifacts/Unfold.app
# 공증 티켓을 포함하도록 ZIP을 다시 만든다.
ditto -c -k --keepParent artifacts/Unfold.app artifacts/Unfold-v1.0.0-beta-osx-arm64.zip
```

Intel도 `osx-x64`로 별도 빌드·서명·공증한다. 두 빌드는 같은 `artifacts/Unfold.app`을 사용하므로 동시에 실행하지 않는다. 공증·서명 이후 ZIP과 tar.gz를 모두 다시 만들고 SHA-256을 갱신한다.

Windows는 실제 Windows에서 후보 설치·실행·로그인·결제를 검증한다. 공개 배포 신뢰성을 위해 코드 서명 인증서를 준비하고 `Unfold.exe`에 서명한 뒤 ZIP과 SHA-256을 다시 생성한다. 현재 후보는 서명되지 않았으며 SmartScreen 통과를 검증하지 않았다.

모든 운영 검증과 최종 파일 해시 확인을 끝낸 뒤 웹 다운로드 링크를 이 버전 파일로 교체한다. 이번 작업은 웹 사이트·GitHub Release에 파일을 게시하지 않는다.

공식 절차: [Lemon Test/Live](https://docs.lemonsqueezy.com/help/getting-started/test-mode),
[Lemon Checkout 생성](https://docs.lemonsqueezy.com/api/checkouts/create-checkout),
[웹훅 서명](https://docs.lemonsqueezy.com/help/webhooks/signing-requests),
[Supabase 함수 배포](https://supabase.com/docs/guides/functions/deploy),
[Apple 공증](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution).
