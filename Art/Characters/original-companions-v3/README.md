# 강아지·고슴도치·펭귄 펫 팩

2026-09-22. [생성 가이드](../../../docs/pet-generation-guide.md)의 보리·Mochi 그림체를
참조해 만든 세 팩이다. 새 빌드의 홈 펫 선택 목록에 함께 나타난다.

| 이름 | ID | 내용 버전 | 배포 파일 |
|---|---|---|---|
| 강아지 | `puppy-dog` | 0.1.0 | `artifacts/pet-packs/companions-v3/puppy-dog-0.1.0.unfoldpet` |
| 고슴도치 | `hedgehog` | 0.1.0 | `artifacts/pet-packs/companions-v3/hedgehog-0.1.0.unfoldpet` |
| 펭귄 | `penguin` | 0.1.0 | `artifacts/pet-packs/companions-v3/penguin-0.1.0.unfoldpet` |

## 생성과 정렬

- `image_gen` 내장 도구로 각 동물을 따로 생성했다. 정확한 생성·수정 프롬프트는
  [generation.md](generation.md)에 있다. Aseprite 레이어 원본은 만들지 않았다.
- 최초 4×6 시트와 격자 보정 시트는 일부 셀을 침범하여 사용하지 않았다.
  각 후보는 `source/candidates/`에 보존했다.
- 최종 생성물은 4×4 자세 배치의 **1254×1254 RGBA PNG**다.
  `source/<id>-atlas.png`는 해당 생성 출력의 바이트를 그대로 보존한다.
- 사용자가 후속 진행을 승인한 뒤 `build-assets.py`로 자세를 잘라 정렬했다.
  실행 시트는 **1024×1024, 4열×4행, 셀당 256×256**이다.
- 이 원본들에서 확인한 빈 구간으로 16개 자세를 분리한다. alpha ≥26의 몸 경계에
  원본 안티앨리어싱 여백 2px을 붙여 자르고, 수평 중앙·발 기준선 230px에 배치한다.
  확대·축소, 다시 그리기, 색 변경, alpha 곱셈 없이 잘라낸 RGBA 픽셀을 복사한다.
- 원본과 실행 시트의 해시는 서로 다르다. 각각 `<id>-resource.json`에 기록하며,
  16개 원본 좌표와 목적지 좌표도 남긴다. 모든 셀의 가장 바깥 픽셀은 완전 투명이다.
  이 절차는 바이트 그대로 실행 시트를 쓰는 기존 보리·Mochi 제작 절차와 구별한다.

## 포즈와 동작

| 인덱스 | 자세 |
|---|---|
| 0–3 | 기본, 깜빡임, 왼쪽 보기, 오른쪽 보기 |
| 4–7 | 졸기, 잠자기, 하품, 주목 |
| 8–11 | 오른쪽으로 스트레칭, 기쁨, 놀람, 삐짐 |
| 12–15 | 오른쪽 제자리 걷기 네 자세 |

세 팩 모두 `smooth`, `unfold-original-v1`과 10개 행동 키를 사용한다.
`build-manifests.py`에 타임라인을 보존했다. 클릭은 놀람 → 좌우 보기이며,
스퀴시·바운스, 랜덤 대기 행동, 세 번째 미루기 반응, 스트레칭 후 화면 이동은
기존 앱 런타임을 이용한다. 일반 커스텀 제작기의 다섯 슬롯은 변경하지 않았다.

잠자기와 스트레칭은 각각 한 자세를 유지하는 방식이다. 수면 호흡의 별도 중간 프레임이나
스트레칭의 연속 중간 프레임을 새로 그린 것은 아니다. 걷기는 네 자세로 반복한다.

## 재생성

Python 3와 Pillow가 필요하다. 저장소 루트에서 실행한다.

```sh
python3 Art/Characters/original-companions-v3/build-assets.py
dotnet build Unfold.slnx -c Release --no-restore
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --validate-characters
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll \
  --pack-character Assets/Characters/puppy-dog 0.1.0 /tmp/puppy-dog-0.1.0.unfoldpet
```

첫 명령은 그림을 생성하지 않으며 이 세 ID만 갱신한다. 나머지 팩은 마지막 명령의 ID를
`hedgehog`, `penguin`으로 바꾼다. 팩 출력 경로는 새 파일이어야 한다.
기본 팩으로 포함된 현재 앱에서는 별도 재설치 없이 새 빌드로 재시작한 뒤 홈에서 선택한다.

[검증 보고서](../../../docs/validation/2026-09-22-companion-trio.md)에 검사 범위와 결과를 기록한다.
