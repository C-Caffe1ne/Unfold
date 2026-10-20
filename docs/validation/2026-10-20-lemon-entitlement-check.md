# Lemon 결제 → 앱 권한 확인 (실제 결제 없음)

2026-10-20 · 앱 `1.2.1`(`09c9e72`), 운영 Supabase `xrelgkdawkogrwxmwkcx`. 실제 결제·환불·Lemon 대시보드 조작은 하지 않았다.
운영 DB는 읽기 전용 조회만 했다. 운영에 보낸 요청은 서명 없는 웹훅 1건(401)과 인증 없는 권한 조회 1건(401)이다.

## 경로

앱·웹 `create-checkout`(GLOBAL, 서버 가격 299센트) → Lemon 결제 → `lemon-webhook`(서명·스토어·상품·variant·통화·항목가·세금 검사)
→ `apply_lemon_event` → `apply_verified_payment`(Live 권한 `active`) → 앱 `get-app-access` → DB `get_app_access` → 앱 잠금 해제.

## 확인 결과

| 항목 | 결과 |
|---|---|
| 운영 가격·상품 | `unfold-global` USD 299 Live 활성, `unfold-kr` Live 비활성. Live 연결은 스토어 488157 / 상품 1429889 / variant 2233159 |
| 운영 결제창 생성 | USD 전환 뒤 10/16 12:47 KST에 실제 `create-checkout` 200, 주문 USD 299·체크아웃 연결. 그 뒤 웹훅 호출이 없어 결제하지 않은 주문으로 판단 |
| 배포 함수 | `lemon-webhook` v21·`create-checkout` v34. 저장소 결제 코드는 v21 배포 커밋 `a61ee4f` 이후 변경 없음, 웹훅 핵심 로직 육안 대조 일치 |
| 운영 DB 함수 | `apply_lemon_event`·`apply_verified_payment`·`get_app_access`·`reserve_lemon_checkout`(2개)·`bind_lemon_checkout` 정의가 저장소 마이그레이션으로 만든 DB와 정규화 MD5까지 동일 |
| 운영 웹훅 주소 | 서명 없는 요청에 `invalid_signature` 401. Live 비밀값이 설정돼 함수가 정상 기동했다는 근거 |
| 기존 Live 결제 | KRW 주문 1건(10/11)이 Live 권한 `active`로 남아 있다 |
| 서버 자동 검사 | Node/PGlite 44/44 통과 |
| 운영 상품 정보로 한 전 과정 모의 | 운영과 같은 함수·결제 코드, 테스트 서명 비밀값. USD 세금 포함·세금 별도·세금 없음 모두 웹훅 200 → `get_app_access` `active`. 같은 이벤트 재전송은 중복 처리, 다른 통화·테스트 모드·할인·다른 variant는 422·`unowned`, 위조 서명 401, 전액 환불 `revoked` |
| 앱 | `get-app-access` 응답(스키마 1·같은 사용자·`unfold`·`live`)만 받아들이고 `active`면 잠금 해제. 1.2.1 전체 테스트 1,107개에 상태별 검사 포함 |

## 발견한 위험

- **Lemon 공개 스토어에서 직접 구매하면 권한이 생기지 않는다.** `unfoldpet.lemonsqueezy.com`에 $2.99 상품과 바로 구매 링크가 공개돼 있다.
  이 경로의 주문에는 Unfold 주문 ID가 없어 웹훅이 200으로 무시한다. 10/13~10/20 로그에는 그런 웹훅 호출이 없었다.
  해결 구현: [스토어 직접 구매 → 권한 연결](2026-10-20-lemon-store-purchase.md)(운영 적용, 웹훅 v22).
- 실제 Lemon이 이 스토어의 USD 주문에 보내는 세금 방식·필드는 확인하지 못했다. 세 가지 세금 방식을 모두 모의했지만, 실제 결제 1건이 최종 확인이다.
- Lemon 쪽 웹훅 구독은 10/12 KRW 주문 재전송으로 확인한 상태이며 이번에 다시 확인하지 못했다(대시보드 접근 없음).

검증 스크립트는 저장소 밖 임시 폴더에서 실행했고 제품 코드는 바꾸지 않았다.
