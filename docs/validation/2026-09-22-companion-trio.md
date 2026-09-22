# 강아지·고슴도치·펭귄 생성·적용 검증

2026-09-22 · `release/mvp` 작업 트리 · macOS arm64 · C#/.NET 10/Avalonia.

## 결과와 변경 범위

- `puppy-dog`(강아지), `hedgehog`(고슴도치), `penguin`(펭귄)을 기본 펫에 추가했다.
  각 내용 버전은 0.1.0이다. 새 빌드로 재시작하면 홈에서 선택한다.
- 보리·Mochi의 선화·색감·단순한 동물 표현을 참조해 내장 `image_gen`으로 생성했다.
  [제작 원장](../../Art/Characters/original-companions-v3/README.md)에 프롬프트와 원본을 보존한다.
- 첫 4×6 생성물과 격자 보정본에는 셀 침범이 있어 사용하지 않았다. 최종 4×4 원본도
  1254px 크기여서 균등 분할 대신 사용자 승인 후 코드로 분리·정렬했다.
- 16개 자세를 256px 셀에 배치해 1024px 정사각 시트로 만들었다. 48개 잘라낸 영역의
  RGBA 픽셀이 생성 원본과 같고, 실행 셀의 네 변은 모두 alpha 0임을 확인했다.
  원본과 실행 파일의 별도 SHA-256·분리 좌표는 각 리소스 JSON에 기록한다.
- 10개 행동 프로필을 연결했다. 기존 앱의 랜덤 행동, 클릭 모션, 미루기 반응과
  휴식 이동을 재사용한다. 사용자 설정·설치 폴더와 기존 두 펫은 이번 추가 작업에서 바꾸지 않았다.
- 진단은 하드코딩한 두 ID 대신 `HasOriginalBehavior`를 만족하는 로드된 펫을 순회한다.
  자산 목록·팩 왕복·렌더링 회귀 검사에 새 세 ID를 추가했다.

## 자동 검사

| 검사 | 결과 |
|---|---|
| `build-assets.py` | 세 원본 보존, 48개 자세 정렬, 세 시트·manifest·원장 생성 |
| 집중 `dotnet test` | 종료 0, 23 통과, 실패·건너뜀 0; 빌드 포함 |
| 전체 Release 테스트 | 종료 0, 278 통과, 실패·건너뜀 0 |
| `--validate-characters` | 종료 0, 다섯 팩 오류·경고 0 |
| 새 팩의 디코딩 픽셀 예산 | 각 18.5 MiB, 모든 클립 10초 이내, 빈 프레임 0 |
| 원본 픽셀 비교 | 잘라낸 48개 RGBA 영역 전부 일치, 시트 가장자리 투명 |
| 팩 생성·설치 왕복 | 새 세 ID의 10개 행동·프로필·걷기 픽셀 보존 |
| 최종 배포 파일 | ZIP CRC, 내부 SHA-256, 현재 실행 자산 바이트 일치 |
| `git diff --check` | 통과 |

실행한 테스트 명령:

```sh
dotnet test Unfold.slnx -c Release --no-restore \
  --filter 'FullyQualifiedName~OriginalCompanionTests|FullyQualifiedName~CharacterAssetAuditTests|FullyQualifiedName~AnimationRenderingTests'
dotnet test Unfold.slnx -c Release --no-build --no-restore
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --validate-characters
```

## macOS 네이티브 자동 재생

새 격리 프로필 `/private/tmp/Unfold-trio-review-1790074923987`에서
`--review-original-pets`를 실행했다. 종료 0이며 다섯 팩의 PNG 115장을 생성했다.

- 실제 dispatcher 시간으로 각 10개 클립을 재생하고 일회성 행동의 idle 복귀를 확인했다.
- 눌림·해제 포즈, 세 번째 미루기의 삐짐, 스트레칭 종료 후 walk,
  완료·숨김 시 이동 중단을 모두 확인했다.
- 새 세 펫의 정렬된 16개 자세와 네이티브 기본·클릭·스트레칭 캡처를 시각 확인했다.
- 이 진단은 화면 밖 창과 코드 호출을 이용한다. 실제 마우스 입력이나 바탕화면 이동을
  확인한 것으로 해석하지 않는다. 네이티브 합성기의 연속 클릭 잔상도 이번 정지 캡처로 판단하지 않는다.

원본 결과: [자산 검사](2026-09-22-trio-assets.json),
[재생 진단](2026-09-22-trio-playback.json), [배포 파일 검증](2026-09-22-trio-packages.json).
캡처: `artifacts/verification/2026-09-22-companion-trio/`.

## 산출물과 한계

| 파일 | 크기 |
|---|---:|
| `artifacts/pet-packs/companions-v3/puppy-dog-0.1.0.unfoldpet` | 819,136 bytes |
| `artifacts/pet-packs/companions-v3/hedgehog-0.1.0.unfoldpet` | 861,890 bytes |
| `artifacts/pet-packs/companions-v3/penguin-0.1.0.unfoldpet` | 723,170 bytes |

현재 빌드는 이 ID들을 기본 팩으로 보호하므로 별도 설치 없이 홈에서 선택한다.
잠자기·스트레칭은 한 자세를 유지하는 클립이고 걷기는 네 자세다. 별도 호흡 프레임이나
스트레칭 중간 자세를 추가한 것은 아니다.

실제 Windows 실행, macOS 물리 포인터·다중 모니터 이동·연속 클릭 녹화·장시간 사용,
사용자의 최종 아트 검수는 미검증이다. 이번 작업에서 설치용 앱 배포물을 다시 만들지는 않았다.
