# GLB 5% 요소별 외곽선 · 2026-10-15

## 기준과 요청

최신 작업트리 `codex/pet-overlay-hover`, HEAD `58d629949860ecac68e1ed1a48154710ec1693ca`,
앱 `1.1.4-beta`와 직전 미커밋 10% 외곽선 구현을 기준으로 한다. 직전 반영 파일 11개의
SHA-256이 검증 사본과 일치함을 확인했다. 당시 관련 검사 222개는 모두 통과했다.

요청은 두께를 5%로 줄이고 모델 내부의 요소별 경계에도 외곽선을 적용하는 것이다.
기존 화면과 diff를 확인했으며 조절 UI·새 문구·설정 항목을 추가하지 않는다.

## 구현

- 내부 고정값 `OutlineWidthPercent = 5`. 192px 출력 기준 약 0.5px이며 DPI·확대·2×2 안티앨리어싱에 비례한다.
- 래스터에 실제 표시된 메시 노드·재질 조합을 요소 ID로 기록한다. 같은 노드·재질의 primitive와 삼각형은 같은 요소다.
- 전체 바깥 경계는 얇은 선으로 유지하고, 서로 다른 보이는 요소 사이에는 경계 중앙에 선을 추가한다.
- 내부 선은 기존 픽셀의 알파를 유지하면서 RGB만 어둡게 한다. 깊이 검사로 가려진 면, alpha 0, MASK로 버린 픽셀은 경계를 만들지 않는다.
- 내부 경계 시드를 반 픽셀 좌표로 기록하고 두 방향 거리 스캔으로 선을 계산한다. 투명 배경은 내부 거리 계산에서 제외한다.
- 기존 시드 버퍼를 재사용하며 요소 ID 버퍼 하나를 추가한다. 최대 2048px 래스터에서 색·깊이·요소 ID·시드 버퍼 합계는 64 MiB다.
- 프레임마다 요소 ID를 초기화하여 애니메이션·회전·크기 변경 때 이전 경계가 남지 않는다.

요소는 GLB가 실제로 나눈 노드·재질을 기준으로 한다. 한 노드·한 재질에 합쳐진 영역의
텍스처 무늬나 의미상 부위를 자동으로 별도 요소로 분류하지 않는다. 원본 GLB와 manifest
형식은 변경하지 않으며 최초 가져오기·편집 미리보기·썸네일·저장 후 펫에 공통 적용한다.

## 검증

격리 사본 `/private/tmp/unfold-glb-elements-wldv8iyv`, macOS arm64, .NET SDK 10.0.401.

| 검사 | 결과 |
|---|---|
| 외곽선 집중 검사 | 17 통과, 실패·건너뜀 0 |
| 최종 GLB·CharacterPack·CharacterLibrary·AnimationView | 230 통과, 실패·건너뜀 0 |
| Release 솔루션 빌드 | 성공, 경고 0·오류 0 |
| 실제 Kei 렌더링 | 19,057 삼각형·32 애니메이션, 192/384/576px PNG 생성 및 10% 전체 테두리와 5% 요소별 선 비교 |

새 요소별 검사 8개는 노드 경계·재질 경계, 동일 요소의 삼각형/primitive 선 억제,
가려진/투명/마스크 요소의 선 억제 3개, 반투명 알파 보존, 저장·재열기·회전 초기화를 다룬다.
기존 두께·투명도 검사는 변경된 5% 및 서브픽셀 표시를 기준으로 갱신했다.
기존 버퍼 재사용 검사도 최종 실행에서 통과했다.

```sh
dotnet restore Unfold.slnx --locked-mode -p:NuGetAudit=false
dotnet test Tests/Unfold.Tests/Unfold.Tests.csproj -c Release --no-restore \
  --filter 'FullyQualifiedName~Glb|FullyQualifiedName~CharacterPack|FullyQualifiedName~CharacterLibrary|FullyQualifiedName~AnimationView'
dotnet build Unfold.slnx -c Release --no-restore
```

[수치·소스 SHA-256](evidence/2026-10-15-glb-element-outline/verification.json),
[실제 Kei 비교 이미지](evidence/2026-10-15-glb-element-outline/comparison.png),
[직전 10% 구현 이력](2026-10-15-glb-outline.md).

렌더링 도구의 짧은 처리 시간 표본은 장시간 성능·전체 앱 메모리 검증을 대신하지 않는다.
실제 OS 입력·Windows 실행·전체 테스트 스위트·설치 앱 교체·배포는 수행하지 않았다.
반영 시 기존 파일 해시를 먼저 확인하고 검증 사본과 동일한 파일만 복사한다.
