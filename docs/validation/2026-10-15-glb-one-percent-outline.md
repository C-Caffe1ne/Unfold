# GLB 요소별 외곽선 1% · 2026-10-15

기준: `codex/pet-overlay-hover`, HEAD `58d629949860ecac68e1ed1a48154710ec1693ca`,
`1.1.4-beta`와 직전 검증된 5% 요소별 외곽선 변경. 기존 반영 파일의 해시 일치를 확인했다.

사용자 요청에 따라 내부 고정값을 `OutlineWidthPercent = 1`로 줄였다. 192px 기준 약
0.1px이며, 메시 노드·재질별 요소 구분·가림·투명도·저장·재생·조절 UI 없음은 유지한다.
1px 미만의 바깥 선은 픽셀 커버리지를 두께에 비례해 줄여 작은 출력에서도 완전히
사라지지 않게 한다. 원본 GLB·manifest 형식은 변경하지 않았다.

격리 사본 `/private/tmp/unfold-glb-elements-wldv8iyv`, macOS arm64, .NET SDK 10.0.401에서 검증했다.
관련 회귀 230개 실행 중 229개 통과·1개 실패했다. 실패는 기존 검사에서 바깥쪽의 희미한 선까지
삼각형 내부 선으로 취급한 기대값이었다. 불투명한 본체 픽셀이 모두 흰색을 유지하는지 검사하도록
수정한 뒤 두 외곽선 클래스의 집중 17개가 모두 통과했다. 제품 렌더러는 첫 회귀 실행 이후 바꾸지 않았다.
Release 솔루션 빌드는 경고·오류 없이 통과했다. 실제 Kei의 192/384/576px PNG도 생성·확인했다.

```sh
dotnet test Tests/Unfold.Tests/Unfold.Tests.csproj -c Release --no-restore \
  --filter 'FullyQualifiedName~GlbOutlineTests|FullyQualifiedName~GlbElementOutlineTests'
dotnet build Unfold.slnx -c Release --no-restore
```

[요소별 외곽선 구현·5% 당시 증거](2026-10-15-glb-element-outline.md).
현재 반영 파일은 검사 사본과 SHA-256으로 대조했다. 설치 앱 교체·배포·실제 OS 입력 검사는 수행하지 않았다.
