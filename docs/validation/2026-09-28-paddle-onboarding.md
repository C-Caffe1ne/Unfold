# Paddle 샌드박스 설정·결제 검증

2026년 10월 8일 문서 점검: 아래 `artifacts/` 캡처는 현재 체크아웃에 포함되지 않아 원래 경로만 기록한다. 당시 검사 결과·버전·수치는 유지한다.

> 이 문서는 공급자 전환 전 검증 이력이다. 2026-09-28부터 현재 결제 경로는
> [Lemon Squeezy](2026-09-28-lemon-squeezy-transition.md)를 사용하며, 당시 로컬 Paddle 도구는 제거했다.

2026-09-28 · Unfold 샌드박스 대시보드와 macOS Chrome에서 확인했다.
실제 금액을 청구하지 않는 개발 테스트이며 운영 계정 `Dokhu`는 변경하지 않았다.

최종 Get started는 **2/4 완료(50%)**다. Create your catalog와 Build your pricing page and
checkout은 Complete, Handle fulfillment and provisioning은 Not started, Test your integration은
In progress로 표시됐다. 완료 단계 캡처 (`artifacts/paddle-onboarding/get-started-2026-09-28.png`; 현재 체크아웃 미포함).

## 완료한 설정

사용자가 확정한 **무료 체험 없음·일회성 구매·국내 4,900원 / 해외 US$3.99·세금 별도**를 반영했다.

| 항목 | 값 |
|---|---|
| 샌드박스 상품 | `pro_01m3k6t6spch04c1g8e6jk7dvn` / Unfold |
| 세금 분류 | 대시보드에서 제공하는 Standard digital goods (`standard`) |
| 국내 가격 | `pri_01m3k742g5tva3gj528wa89jmf` / KRW 4900 / `unfold-kr` |
| 해외 가격 | `pri_01m3k6zzdpecd6ej4b99tem7qj` / USD 399 cents / `unfold-global` |
| 두 가격 공통 | One-time, Excludes tax (`external`), 수량 최소·최대 1, 체험 없음, Active |
| 공개 토큰 이름 | Unfold local sandbox checkout |
| 저장된 기본 결제 링크 | `https://localhost:43822/` — 대시보드가 HTTPS로 정규화 |
| 실제 테스트 페이지 | `http://localhost:43822/` — Paddle.js 오버레이 직접 실행 |

저장 후 두 가격의 Edit 화면을 다시 열어 결제 주기·세금·수량을 확인했다.
상품·가격 캡처 (`artifacts/paddle-onboarding/catalog-2026-09-28.png`; 현재 체크아웃 미포함).

## 실제 브라우저 검증

| 검사 | 결과 |
|---|---|
| 한국 가격 조회·결제창 | 원금 ₩4,900 + 세금 ₩490 = 합계 ₩5,390 |
| 한국 테스트 결제 | 공식 정상 테스트 카드로 성공, 완료 이벤트 및 Dashboard Complete 확인 |
| 미국 가격 조회·결제창 | 미국 우편번호 97205 기준 원금 US$3.99 + 세금 US$0.00 = 합계 US$3.99 |
| 해외 결제 실패 | 공식 거절 카드로 Paddle 오류 및 로컬 페이지 거절 상태 확인 |
| 해외 재시도 | 같은 결제창에서 정상 테스트 카드로 성공, 완료 이벤트 및 Dashboard Complete 확인 |
| 환경 제한 | 설정은 sandbox/test_ 공개 토큰, 로컬 호스트만 허용 |
| 정적 검사 | JS 구문, JSON, 가격·앱 ID 매핑, DOM 참조, 서버 비밀키 미포함, 공백 검사 통과 |

완료된 테스트 거래:

- KR: `txn_01m3k870nqt0ash87fw1prse1a`
- USD: `txn_01m3k8gtnfsb9f0pty0yyh65q6`

두 거래 완료 화면 (`artifacts/paddle-onboarding/transactions-2026-09-28.png`; 현재 체크아웃 미포함),
한국 결제창 (`artifacts/paddle-onboarding/checkout-kr-2026-09-28.png`; 현재 체크아웃 미포함),
해외 결제창 (`artifacts/paddle-onboarding/checkout-us-2026-09-28.png`; 현재 체크아웃 미포함),
거절 상태 (`artifacts/paddle-onboarding/payment-declined-us-2026-09-28.png`; 현재 체크아웃 미포함).
캡처는 Git에서 제외되는 로컬 산출물이다.

검증 중 고객 정보를 넘길 때 이메일 없이 주소만 넣으면 Paddle이 거절하는 점을 확인해,
실제 사용자 정보가 아닌 `unfold-sandbox@example.com`을 테스트 설정에 넣었다.
미국 주소에는 우편번호를 같이 전달해 예상 세금과 결제창을 일치시켰다.
결제 거절 이벤트는 공식 계약인 `checkout.payment.failed`로 처리했다.

## 변경 파일

- 당시 `scripts/paddle-sandbox/`에 별도 HTML/CSS/JS 도구를 두었으나 공급자 전환 때 제거했다.
- `docs/plans/2026-09-27-paid-launch.md`: 세금 별도 결정과 Paddle 진행 상태.
- `docs/README.md`, `supabase/README.md`, `supabase/.env.example`: 현재 단계와 연결 경계.
- 앱 런타임·계정 화면·기존 DB 마이그레이션은 이번 작업에서 변경하지 않았다.

## 남은 연결과 한계

1. **Supabase 서버 연결**: 현재 `orders.provider`는 `toss`, `lemon`만 허용한다. Paddle용 후속
   마이그레이션, 로그인 사용자와 서버 주문 연결, 거래 생성 API, 웹훅 서명 검증이 필요하다.
2. **세금 계약**: 현재 `apply_verified_payment`는 주문 금액과 정확히 비교한다. Paddle 원금·세금·합계를
   구분하고 상품·수량·통화·환경을 검증해야 한다. 세금 포함 합계를 기존 원금 인자에 넣지 않는다.
3. **인증 정보**: 대시보드에 서버 키 레코드는 있으나 실행 프로세스에는 사용 가능한 sandbox 비밀키가 없다.
   이전에 전달된 `apikey_...`는 레코드 ID다. 실제 `pdl_sdbx_apikey_...` 값은 채팅·소스 대신
   서버 Secret에 등록한다. Supabase 관리 인증도 현재 실행 환경에서 확인되지 않았다.
4. **앱 권한**: 브라우저 완료 이벤트로 권한을 부여하지 않는다. 서명 검증된 웹훅 → DB 권한 →
   앱 조회·복원을 연결해야 한다. 지금은 기존 결정대로 앱 사용을 잠그지 않는다.
5. **운영 전환**: 정식 도메인, 판매자·사이트 승인, 라이브 상품·가격·키·웹훅, 실제 통화·국가 분류,
   환불·중복 알림·재시도·권한 복원 검증이 남았다. Sandbox ID를 운영에서 사용하지 않는다.
6. **기본 링크**: HTTPS로 정규화된 기본 링크를 직접 여는 테스트는 하지 않았다. 현재 로컬 서버는 HTTP다.
   API의 checkout URL 연동에는 접근 가능한 HTTPS 호스트 또는 로컬 TLS 구성이 별도로 필요하다.
7. C# 변경이 없어 앱 빌드·전체 .NET 테스트는 재실행하지 않았다. Supabase 배포, 앱부터 구매 권한까지의
   통합 테스트, 다른 OS, 실제 결제·환불은 미검증이다.

로컬 Paddle 도구는 현재 소스에 포함하지 않는다.

## 공식 문서

- [샌드박스의 분리·테스트 카드](https://developer.paddle.com/sdks/sandbox/)
- [Paddle.js 공개 클라이언트 토큰](https://developer.paddle.com/paddle-js/about/)
- [가격 조회](https://developer.paddle.com/paddle-js/methods/paddle-pricepreview/)
- [결제 열기·고객 정보](https://developer.paddle.com/paddle-js/methods/paddle-checkout-open/)
- [결제 실패 이벤트](https://developer.paddle.com/paddle-js/events/checkout-payment-failed/)
- [기본 결제 링크](https://developer.paddle.com/build/transactions/default-payment-link/)
