# Supabase 공개키 연결 검사

2026-09-28 · `release/mvp` · 프로젝트 `xrelgkdawkogrwxmwkcx`

## 변경

사용자가 제공한 URL과 Publishable key를 Git에서 제외되는 `supabase/.env.client`에 저장했다.
키 값은 문서·검사 출력에 기록하지 않았다. 파일 권한은 소유자 읽기·쓰기(0600)다.
공유 가능한 빈 키 예시는 `supabase/client.env.example`에 추가했다.
데스크톱 앱의 자동 설정 로딩이나 로그인 잠금을 구현한 것은 아니다.

## 원격 관찰

제공된 프로젝트에 공개키를 `apikey` 헤더로 전달했다. 모든 요청은 GET이며 리디렉션을 따르지 않았다.
상품·가격은 공개 카탈로그의 `unfold` 항목만 조회했다. 사용자·주문 데이터는 조회하지 않았다.

| 요청 | 실제 응답 | 해석 |
|---|---|---|
| `/auth/v1/health` | 200, GoTrue | 공개키가 수용되며 Auth health endpoint 응답 |
| `/auth/v1/settings` | 200 | Google 비활성, 이메일 활성, 익명 로그인 비활성 |
| `/rest/v1/products` | 404, `PGRST205` | 공개 REST 스키마 캐시에서 `public.products`를 찾지 못함 |
| `/rest/v1/prices` | 404, `PGRST205` | 공개 REST 스키마 캐시에서 `public.prices`를 찾지 못함 |
| `/functions/v1/get-entitlement` | 404, `NOT_FOUND` | 해당 조회 함수 엔드포인트 없음. 사용자 토큰 없이 조회했으며 권한 판정 검사가 아님 |

앞선 키 없는 요청의 401과 달리, 이번에는 공개키를 이용한 Auth 응답까지 확인했다.
REST의 404만으로 전체 DB 구조를 조사했다고 보지 않는다. 관리자 연결이 없어 DB 내부는 미확인이다.

## 다음 연결과 검증 한계

- Google OAuth Client ID/Secret 및 Supabase Google provider 활성화·반환 URL 등록이 필요하다.
- DB 마이그레이션·함수 배포에는 CLI 또는 대시보드의 별도 관리 계정 인증이 필요하다.
- 원격 프로젝트의 개발/운영 용도, 실제 Google 로그인, 사용자 토큰 기반 구매 권한 조회, RLS는 미검증이다.
- 원격 데이터·Auth 설정을 수정하거나 배포하지 않았다. 실제 결제도 수행하지 않았다.
- 앱 코드를 변경하지 않아 C# 빌드·전체 회귀 검사는 반복하지 않았다.

## 로컬 검사

- `.env.client`가 Git 제외 대상이고 추적되지 않는지 확인.
- URL·Publishable key 형식, 파일 권한, 예시에 키 값이 없는지 확인.
- `git diff --check`, 변경된 Markdown의 상대 파일 링크·공백 검사.

설정 순서는 [Supabase 연결 안내](../../supabase/README.md)를 따른다.
