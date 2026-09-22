# Mochi·보리 확장 행동 팩

2026-09-22. 현재 번들에 포함하는 고양이 Mochi와 토끼 보리의 제작 원본이다.
두 펫 모두 홈의 펫 선택 메뉴에 나타난다. 기존 보리 0.1.0 후보 폴더는 보존한다.

같은 콘셉트로 다시 제작하려면 [생성·적용 가이드](../../../docs/pet-generation-guide.md)를 먼저 읽는다.

| 항목 | Mochi | 보리 |
|---|---|---|
| ID | `default-cat` | `bori-rabbit` |
| 내용 버전 | 2.0.0 | 0.2.0 |
| 생성 원본 | `source/mochi-atlas.png` | `source/bori-atlas.png` |
| 실행 폴더 | `Assets/Characters/default-cat` | `Assets/Characters/bori-rabbit` |
| 셀 / 시트 | 256×256 / 1024×1536 RGBA | 256×256 / 1024×1536 RGBA |
| 전체 클립 디코딩 픽셀 | 18.75 MiB | 18.5 MiB |

두 팩의 `behaviorProfile`은 `unfold-original-v1`이다. 기존 다섯 반응에 잠자기,
두리번거리기, 하품, 삐지기, 걷기를 더했다. 클릭은 놀람 → 좌우 살피기다.
스퀴시·바운스와 화면 이동은 앱이 처리하므로 시트에 이동 좌표를 구워 넣지 않는다.
앱 버전과 위의 팩 내용 버전은 서로 독립적이다.

## 원본과 제작 결정

- [generation.md](generation.md)에 실제 생성·수정 프롬프트를 보존한다.
- 생성 PNG는 바이트 그대로 실행 자산으로 복사한다. alpha 경계로 수평 중심과 발 기준선을
  맞추는 작업은 `AnimationView`의 렌더링 단계에서만 수행한다.
- 보리 18/19번 셀은 다음 행의 귀 조각, 22/23번 셀은 잘린 귀가 있어 참조하지 않는다.
  반대 방향을 향한 10번 스트레칭 셀도 제외했다. 스트레칭은 9번 자세를 유지하고,
  걷기는 완전한 20/21번 두 자세를 반복한다. 사용하지 않는 셀은 원본 보존을 위해 남긴다.
- 레이어가 있는 Aseprite 원본은 만들지 않았다. PNG 원본, manifest와 타임라인 생성 스크립트를 제공한다.
- 원본 해시는 각각 `default-cat-resource.json`, `bori-rabbit-resource.json`에 있다.
  사용자의 아트 승인과 판매용 최종 검수는 아직 받지 않았다.

## 재생성·패키징

저장소 루트에서 실행한다. 첫 명령은 원본 PNG를 다시 그리지 않고 실행 복사본과
manifest, 제작 원장의 메타데이터만 갱신한다. 팩 출력 파일은 새 경로여야 한다.

```sh
python3 Art/Characters/original-companions-v2/build-manifests.py
dotnet build Unfold.slnx -c Release --no-restore
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll \
  --pack-character Assets/Characters/default-cat 2.0.0 /tmp/Mochi-2.0.0.unfoldpet
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll \
  --pack-character Assets/Characters/bori-rabbit 0.2.0 /tmp/Bori-0.2.0.unfoldpet
```

이번 산출물은 저장소의 `artifacts/pet-packs/`에 있다. 파일과 내부 해시를 다시 검증했다.
현재 앱은 두 ID를 기본 팩으로 보호하므로 별도 설치 없이 홈에서 선택한다.
구버전 앱은 기존 다섯 이벤트만 사용하며, 확장 행동은 이번 런타임 변경이 필요하다.

[구현·검증 기록](../../../docs/validation/2026-09-22-original-companions.md)에
자동 검사와 실제 OS 입력 검증의 한계를 구분했다.
