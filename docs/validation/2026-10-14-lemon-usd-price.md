# Lemon US$2.99 가격 전환 준비

2026-10-14 · 앱 `codex/pet-overlay-hover` HEAD `dcaa0ca`(작업 중 다른 세션이 커밋한 로그인 언어 선택 포함), 웹 `dokhustudio/unfoldpet-web` `main` `4ec29c6` 기준.
앱·웹 저장소에 각각 로컬 커밋했다. 운영 DB 적용과 앱·웹 배포·푸시는 하지 않았다. 운영 DB 적용 시도는 자동 실행 권한 검사에서 거부됐다.

## 원인

- 사용자가 Live 스토어 488157의 같은 variant 2233159 가격을 ₩4,900에서 US$2.99로 바꿨다. 공개 스토어 표시 `$2.99`를 확인했다.
- 서버는 Lemon 대시보드 가격이 아니라 DB `prices`로 `custom_price`를 만든다. 운영 DB는 `unfold-kr` KRW 4,900만 Live로 판매하고, 앱 1.1.4와 웹은 `KR`만 보낸다.
- 코드 기준 예상 결과: USD 스토어에 `custom_price=490000`($4,900.00) 체크아웃이 생성된 뒤 `preview.currency` 검사(USD≠KRW)로 422가 되어 구매할 수 없다. 변경 후 24시간 로그에 결제·웹훅 호출은 0건이었다.

## 변경

| 위치 | 내용 |
|---|---|
| `supabase/migrations/20261014113000_lemon_usd_price.sql` | `unfold-global` 399→299. Live KR 매핑이 있으면 같은 variant를 `unfold-global`로 옮기고 Live 활성 상태를 승계. `unfold-kr` Live 비활성 |
| `Assets/Account/connection.json`, `entry-screen.json` | 결제 시장 `GLOBAL`, 기본 시장 `GLOBAL`, 표시 US$2.99 |
| 웹 `public/scripts/account-service.mjs`, `public/index.html` | 결제 시장 `GLOBAL`. 가격 카드·결제 모달·안내·약관의 가격을 US$2.99로 변경. v1.1.3 변경 이력의 4,900원은 당시 기록이라 유지 |
| 문서 | 현재 가격을 적은 BM·제품 방향·MVP·계정 화면·Supabase·웹 결제 문서 |

기존 KRW 주문은 주문 행에 통화·금액·variant를 저장하므로 영수증·환불 검증은 그대로다.
`create-checkout`·`lemon-webhook` 코드는 USD를 이미 지원하므로 재배포하지 않는다.

## 검증

- Supabase Node/PGlite: 변경 전 41/41, 변경 후 44/44. 신규 `lemon-usd-price.test.mjs` 3개는 운영 상태(KR Live 매핑·KRW 결제 1건)를 재현한 뒤 마이그레이션을 적용한다. USD 299 Live·같은 variant·KR 차단, `GLOBAL` 체크아웃 `custom_price=299`·세금 포함 USD 영수증의 권한 활성·항목가 399 거절, 기존 KRW 주문의 전액 환불을 확인했다. 마이그레이션 내용을 비우면 2개가 실패한다.
- 앱: Release 빌드, 계정·결제·다국어 집중 94개 통과. 신규 `ProductionCheckoutShowsTheLiveServerPrice`가 배포 설정의 시장·통화·금액을 서버 Live 가격과 대조한다. 전체 테스트·OS 진단은 실행하지 않았다.
- 웹: `npm test` 77/77, 공개 비밀 검사 통과. 로컬 wrangler에서 가격 카드 `$2.99 USD`·`부가세 포함` 렌더링을 확인했다. `check:site`·`check:routes`는 변경 전 HEAD에서도 v1.1.4 다운로드 리다이렉트 허용 목록·개수로 실패하며 이번 변경과 무관하다.

## 운영 적용 순서

사용자 승인 후 진행한다.

1. 운영 DB에 마이그레이션을 적용하고 `prices`·`lemon_prices`를 다시 조회한다. 현재도 KR 결제는 422로 막혀 있으므로, 적용 후 Lemon 호출 전 `checkout_not_ready`로 막히는 편이 낫다.
2. 웹을 배포한다. 이 시점부터 웹 구매가 US$2.99로 열린다.
3. 앱 버전을 올려 배포한다. 1.1.4 사용자는 업데이트 전까지 앱 안에서 구매할 수 없다.
4. 실제 결제 1건으로 USD 웹훅 영수증과 권한 활성을 확인한다.

## 미검증·위험

- Lemon 스토어 통화와 세금 포함 설정은 API로 직접 조회하지 못했다. 공개 스토어 표시와 사용자 설명을 근거로 삼았다.
- 세금 포함 USD 웹훅은 실제 이벤트가 아니라 공급자 형식을 따른 fixture로 검사했다.
- 웹 약관의 "세금은 별도로 계산됩니다"와 가격 카드의 "부가세 포함"은 변경 전부터 서로 다르다. 이번에는 가격만 바꿨다.
- 공개 스토어에서 앱·웹을 거치지 않고 직접 구매하면 주문 ID가 없어 권한이 생기지 않는다. 기존 문제다.
- 테스트 스토어 485125와 `unfold-kr` Test 매핑은 바꾸지 않았다.
