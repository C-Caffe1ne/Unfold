# Lemon Squeezy Live 연결 진행 상태

2026-10-11 · 앱 `1.1.3-beta`, `codex/remove-unused-features`, HEAD `17941db`와 기존 미커밋 수정 기준.

## 확인과 변경

- 사용자가 Live 스토어 승인 완료를 확인했다. 브라우저는 로그인 화면이므로 Live 상품 ID는 아직 확인하지 못했다.
- 앱 `Assets/Account/connection.json`은 이미 `live`·한국 시장이었다.
- 웹과 배포된 주문 서버, DB 상품 매핑은 Test 전용이었다.
- 별도 과거 작업 트리의 Live 구현을 최신 앱 소스에 복구했다. 배포된 웹훅의 소수 금액·환산 처리도 유지했다.
- 운영 DB에 `lemon_live_currency` 마이그레이션을 반영했다. Live 상품·판매 활성화는 하지 않았다.
- `create-checkout` v28, `lemon-webhook` v12에 Test/Live 지원을 배포했다. 기존 환경 비밀 설정은 변경하지 않았다.
- 주문 RPC 권한: anon/authenticated 실행 불가, service_role만 실행 가능.
- 배포 후 HTTP 확인: create-checkout 무인증 요청 401, lemon-webhook 무서명 요청 401 invalid_signature.
- 기존 앱 UI·펫 관리 등 미커밋 수정은 변경하지 않았다.

## 검증

- 서버 Node/PGlite 35개 테스트 통과: Live 주문·서명·중복·부분/전체 환불·지연 이벤트·환경 분리·환산 소수 금액.
- 웹 72개 테스트 통과, check:site 및 wrangler dry-run 통과.
- 테스트는 로컬 엔진/모의 외부 API 검사다. 실제 Live 청구·웹 OAuth·앱 구매 복원 완료를 뜻하지 않는다.

## 남은 연결

1. Live 스토어·상품·Variant ID와 실제 가격 확인 후 `lemon_prices`의 Live 매핑 등록.
2. Supabase Secrets에 `LEMONSQUEEZY_LIVE_API_KEY`, `LEMONSQUEEZY_LIVE_WEBHOOK_SECRET` 등록.
3. Live 웹훅 URL: `https://xrelgkdawkogrwxmwkcx.supabase.co/functions/v1/lemon-webhook`; 이벤트 `order_created`, `order_refunded`.
4. 키·웹훅·상품 확인 후 서버 `UNFOLD_ENVIRONMENT=live`, 한국 가격 `live_enabled=true`, `UNFOLD_CHECKOUT_ENABLED=true` 전환.
5. 웹 account-config를 Live로 전환하고 checkoutEnabled를 켠 뒤 공개 배포·실제 OAuth·체크아웃 확인.
6. 사용자 직접 결제 후 웹·앱 동일 Google 계정 권한 확인. 실결제와 환불은 별도 사용자 행동/승인을 거쳐 검증.

비밀값은 채팅·소스·공개 설정에 넣지 않는다. 해외 상품 매핑은 미확인 상태다.

## Live 전환 완료 — 2026-10-11

사용자가 Test 환불 검증을 생략하고 즉시 Live 전환을 요청했다. Unfold 스토어 488157의 상품 1429889를 Published로 전환했다. Live variant는 2233159이며 unfold-kr 가격만 live_enabled=true로 활성화했다. UNFOLD_ENVIRONMENT=live, UNFOLD_CHECKOUT_ENABLED=true를 설정했다. 초대 코드는 유지했다.

웹 배포 56c4b1c5-479c-480e-bf5b-ee113e31921a에서 Live 설정과 환경별 배지를 확인했다. 웹 검사 76/76 및 사이트 정적 검사 통과. 로그인 계정으로 실제 create-checkout 호출이 성공했고 unfoldpet.lemonsqueezy.com의 Live 체크아웃이 열렸다. 상품 기본 가격 4,900원, Test 배너 없음. 카드 입력과 결제 제출은 사용자가 수행한다. 실제 청구 및 Live 웹훅에 따른 권한 활성화, 앱 로그인 검증은 아직 미실시다. 세금과 최종 금액은 청구 국가/주소에 따라 결제창에서 확인한다.

## 부가세 포함 4,900원 — 2026-10-11

사용자 요청으로 Unfold Live 스토어의 Enable tax-inclusive pricing을 켜고 저장했다. 웹 안내도 부가세 포함 최종 4,900원으로 변경하고 배포했다(a31f363e-af91-4581-b11a-89532784e32d). 결제창에서 한국을 선택한 상태로 총계 4,900원 및 결제 버튼 4,900원을 확인했다. 우편번호와 카드 입력 및 실제 청구는 사용자에게 남겼다.

lemon-webhook은 세금 포함 주문의 세액 0 제한 대신 총액이 계약금액과 일치하도록 검증한다. 상품가격·통화·서명·소계+세액=총계 검증은 유지한다. 세금 포함 주문의 결제/전액환불 회귀 검사를 추가해 서버 38/38 통과 후 배포했다. 웹 76/76, 사이트 및 보안 검사 통과. 실제 Live 결제와 권한 활성화는 아직 확인 전이다.
