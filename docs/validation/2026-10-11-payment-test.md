# 2026-10-11 결제 종단 검증과 Live 준비

기준 앱: codex/remove-unused-features, HEAD 17941db, 1.1.3-beta 및 기존 미커밋 변경. 웹 소스 HEAD 6902da6 및 기존 변경 보존.

## 완료

- 공개 웹 Google 로그인 성공.
- 브라우저 fetch를 서비스 객체의 메서드로 호출해 발생한 연결 오류를 기본 fetch wrapper로 수정. 기본 브라우저 receiver 회귀 검사 추가.
- 현재 공개 자산을 읽어 보존한 별도 Test 배포본으로 fetch 수정과 보안 헤더 배포. Cloudflare 버전 934ccdf5-4e38-4338-b9cc-b979c965992e. 로컬에 준비된 Live/마케팅 변경은 배포하지 않음.
- 공개 웹 권한 조회 → 서버 생성 Test 체크아웃 → 공식 Test 카드 결제 성공 → 실제 서명 웹훅 → DB paid/active → 웹 이용 권한 확인 성공.
- 이번 서버 주문 ID f3547d9a-1918-4f36-b742-e19ca0d1d134, Lemon 주문 9723307 (#4851254). 실제 청구 없음.
- 실제 공개 응답에서 CSP/HSTS/X-Frame-Options 확인. 웹 검사 76개, 정적·보안 검사와 배포 dry-run 통과.

## 환불 한계

Test 전체 환불 UI를 실행했으나 주문은 Paid 상태를 유지했다. 이어 보낸 order_refunded 시뮬레이션은 refunded=false/refunded_amount=0인 원문이었다. 서버는 422 payment_mismatch로 차단했고 권한을 유지했다. 정상 환불로 오인해 권한을 취소하지 않는다. 해당 입력 회귀 검사를 추가했다. 실제 환불 완료의 종단 확인은 아직 통과하지 않았다. 모의 엔진의 부분/전체 환불·중복·환경 분리 검증과 구분한다.

## Live 연결 준비

기존 Test는 DOKHU 스토어 485125 / 상품 1393777 / Variant 2176689 / dokhustudio.lemonsqueezy.com을 사용한다. DOKHU는 심사 대기 표시다.

별도 Unfold 스토어는 Active, Store 488157 / Live 상품 1429889 / 기본 Variant 2233159 / unfoldpet.lemonsqueezy.com, KRW 4,900, 세금 별도다. 상품은 Draft다. 확인한 값으로 unfold-kr의 Live 매핑만 서버 DB에 저장하고 다시 확인했다. live_enabled=false와 서버 Test 환경을 유지했다.

서버 비밀 이름 목록에 LEMONSQUEEZY_LIVE_API_KEY와 LEMONSQUEEZY_LIVE_WEBHOOK_SECRET이 없다. 사용자에게 직접 Live 자격증명 생성·등록을 요청했다. 비밀값은 채팅/코드에 받지 않는다. Live 웹훅 URL은 https://xrelgkdawkogrwxmwkcx.supabase.co/functions/v1/lemon-webhook, 이벤트는 order_created/order_refunded.

정상 환불 검증·Live 비밀 설정 확인 후 서버 환경 전환, 상품 공개, 가격 활성화, 웹 checkoutHost를 unfoldpet.lemonsqueezy.com으로 맞춰 배포해야 한다. 앱은 현재 Live 권한을 사용하므로 Test 구매만으로 앱 이용을 허용하지 않았다. 실제 앱 구매 복원·Live 청구는 아직 확인하지 않았다. 무료 초대 코드·기존 계정 권한은 변경하지 않았다.

참고: https://docs.lemonsqueezy.com/guides/developer-guide/testing-going-live

## Live 비밀 등록 후 재확인

사용자의 등록 완료 이후 서버 비밀 이름에서 Live API·Live 웹훅 키를 확인했다. 그러나 기존 LEMONSQUEEZY_WEBHOOK_SECRET이 사라졌고 Live 웹훅 비밀의 해시는 이전 Test 값과 일치했다. 이름 변경으로 추정되므로 Test 항목 복원과 별도 Live Signing secret 등록을 사용자에게 요청했다. 원문 비밀값은 읽거나 출력하지 않았다. Unfold Live 웹훅 URL 등록은 브라우저에서 확인했다. 서버 환경 전환·판매 활성화는 수행하지 않았다.
