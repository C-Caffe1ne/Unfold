# 2026-10-11 백엔드 데이터 보안 결과

기준: codex/remove-unused-features, HEAD 17941db, 1.1.3-beta. 기존 미커밋 변경을 보존했다. 대상 Supabase 프로젝트: xrelgkdawkogrwxmwkcx. 웹은 별도 unfoldpet-web 저장소다.

## 운영 DB 반영 완료

20261011061634_backend_security.sql을 적용했다. get_app_access()의 SECURITY DEFINER를 제거해 호출자 RLS를 적용하고, postgres가 앞으로 만드는 함수·public 테이블의 기본 클라이언트 권한을 제거했다. 기존 명시적 권한과 데이터는 보존했다.

운영 DB에서 관리자 실행 해제, 임의 인증 사용자의 unowned 응답, 타 사용자 주문·이용 권한·무료 등록 조회 차단을 롤백 트랜잭션으로 확인했다. 14개 테이블 RLS와 서버 전용 변경 권한을 확인했다. advisor의 SECURITY DEFINER 경고는 해소됐다. 서버 전용 8개 테이블의 정책 없음 INFO는 의도된 접근 차단이다. leaked password protection 경고는 남아 있다.

초대 코드는 사용자 요청대로 유지했으며 기존 이용 권한과 신규 등록 정책을 바꾸지 않았다.

## 웹 소스 반영 — 공개 배포 미반영

public/_headers에 CSP·프레임 차단·MIME 보호·referrer 제한·권한 제한·HSTS·계정 설정 no-store를 추가했다. 스크립트는 self만 허용하고 현재 UI의 인라인 스타일은 허용한다. tools/check-security.py와 npm run check:security, 회귀 검사를 추가했다. 탐지 오류는 비밀값 원문을 출력하지 않는다.

미완료 Live 결제 설정을 함께 공개하지 않도록 실제 웹 배포는 수행하지 않았다. 브라우저 OAuth·결제 이동·CSP 위반 검증은 배포 단계에 남아 있다.

## 검증 결과와 한계

| 검사 | 결과 |
|---|---|
| 백엔드 Node/PGlite | 36/36 통과; 본인 권한·타 계정 차단·새 객체 기본 권한 포함 |
| 웹 Node | 75/75 통과; 헤더·비밀값 탐지 포함 |
| 정적 사이트 검사 | 자산 참조 85개 및 GIF/WebP 통과 |
| Wrangler dry-run | 자산 62개 통과 |
| 앱 공개 계정 자산 비밀값 검사 | 텍스트 2개 통과 |
| 변경 공백 검사 | 두 저장소 통과 |

앱 C# 런타임은 이번 변경 대상이 아니며 새 OS 실행 검증은 수행하지 않았다. 전체 Git 이력 감사·침투/부하 검사·백업 복구를 대체하지 않는다. 실제 Live 결제·환불은 [결제 연결 기록](2026-10-11-lemon-live-connection.md)의 미검증 상태를 유지한다.

[보안 운영 절차](../operations/backend-security.md)에 키 교체·로그·백업 복구 목표·사고 대응을 정리했다. 백업/PITR·MFA·보존 기간의 실제 설정 확인은 남아 있으며 적용 완료로 표시하지 않았다.
