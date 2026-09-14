# 펫 제작 원장

`<stable-id>/resource.json`에 콘텐츠 버전, 제작 원본, 제작자, 권리 증빙, 출시 상태를
기록한다. 기본 번들에 배포할 파일은 `Assets/Characters/<stable-id>/`에 둔다.
별도 설치 팩 후보는 `<stable-id>/runtime/<stable-id>/`에 두고 `.unfoldpet`으로 내보낸다.
이 `Art/` 디렉터리는 앱 번들에 포함되지 않는다.

- [보리 후보 0.1.0](bori-rabbit/README.md): 원본 PNG·반응 5종·생성 출처·픽셀 측정과 로컬 팩.

규격·상태·검증 방법은 [펫 리소스 관리](../../docs/pet-resources.md)를 따른다.
Mochi의 원본 제작 파일과 권리 증빙은 이번 코드 확인만으로 확정하지 않았다.
확인하지 않은 제작자나 라이선스를 임의로 채우지 않는다.
