# GLB 내부 고정 외곽선 · 2026-10-15

## 기준과 요청

- 최신 제품 기준: `codex/pet-overlay-hover`, `58d629949860ecac68e1ed1a48154710ec1693ca`, `1.1.4-beta`.
- 실제 작업트리: `/Users/hwanghyeonseong/Developer/GitHub/Unfold/.worktrees/pet-overlay-hover`.
- 새 GLB를 처음 열 때 외곽선 10%를 자동 적용하고 사용자 조절 UI는 추가하지 않는다.
- 기존 화면·diff 점검: GLB 편집기에 외곽선 설정은 없었다. 화면·문구·조작 요소를 추가하지 않았다.

## 구현

`GlbModel.Outline.cs`가 불투명·반투명 렌더링이 끝난 뒤 투명한 경계에 검은 외곽선을 추가한다.
내부 기준은 192px 출력에서 100% = 10px이며, 고정 10%는 약 1px다. 출력 해상도에
비례하여 DPI·확대·2×2 안티앨리어싱에도 같은 비율로 적용한다.

두 차례의 거리 스캔과 재사용 버퍼로 프레임 크기에 비례하는 작업량을 유지한다.
모델 본체의 기존 색·알파는 보존하고 투명한 샘플에만 선을 그린다. 면의 삼각형 경계에는
선을 추가하지 않으며, 내부 틈·MASK로 버린 픽셀·완전히 투명한 재질도 유지한다.
새 버퍼는 기존 모델 렌더링 잠금 안에서 재사용하며 최대 2048px 래스터에서
색상·깊이·외곽선 시드 합계 48 MiB로 제한된다.

새 가져오기 미리보기·썸네일·실제 펫·정지/마지막 자세·기존 설치 GLB 모두 같은
렌더러를 사용한다. 원본 GLB·재질·manifest 형식은 변경하지 않는다.

## 검증

원본 작업트리의 기존 변경을 보존하기 위해 `/private/tmp/unfold-glb-outline-61f3zvzq`의
격리 사본에서 .NET SDK 10.0.401, macOS arm64로 검사했다.

| 검사 | 결과 |
|---|---|
| 변경 전 GLB 검사 | 193 통과, 실패·건너뜀 0 |
| 새 외곽선 검사 | 9 통과: 두께/해상도 4, 투명도 3, 최초 가져오기·저장·재열기·팩 1, 자세·버퍼 재사용 1 |
| 최종 GLB·CharacterPack·CharacterLibrary·AnimationView 검사 | 222 통과, 실패·건너뜀 0 |
| 최종 Release 솔루션 빌드 | 성공, 경고 0·오류 0 |
| 실제 Kei 렌더링 | 19,057 삼각형·32 애니메이션, 192/384/576px 적용 전후 PNG 생성·시각 확인 |

첫 변경 후 실행은 195 통과·7 실패였다. 실패는 외곽선까지 모델 본체 크기로 측정하던
기존 크기 검사였다. 흰색 fixture의 본체 커버리지를 측정하도록 수정한 뒤 크기 검사
16개와 최종 222개가 모두 통과했다. 반투명 면 검사도 외곽선을 제외한 기존 색을 검증한다.

실행 명령:

```sh
dotnet restore Unfold.slnx --locked-mode -p:NuGetAudit=false
dotnet test Tests/Unfold.Tests/Unfold.Tests.csproj -c Release --no-restore \
  --filter 'FullyQualifiedName~Glb|FullyQualifiedName~CharacterPack|FullyQualifiedName~CharacterLibrary|FullyQualifiedName~AnimationView'
dotnet build Unfold.slnx -c Release --no-restore
```

[검증 수치·소스 SHA-256](evidence/2026-10-15-glb-outline/verification.json),
[실제 Kei 적용 전후 이미지](evidence/2026-10-15-glb-outline/comparison.png).
최종 사본의 소스 SHA-256을 실제 작업트리 반영본과 대조한다.

짧은 프레임 시간 표본은 장시간 성능·전체 앱 메모리 통과 근거로 사용하지 않는다.
전체 테스트 스위트·실제 OS 마우스 입력·설치 앱 교체·배포는 수행하지 않았다.
