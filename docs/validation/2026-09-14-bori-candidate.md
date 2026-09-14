# 보리 아트 후보 제작·재생 검증

2026-09-14 · `release/mvp`, 기준 커밋 `d9ee699`에 이번 변경을 적용한 작업 트리.
macOS arm64, .NET 10.0.12 런타임의 자체 포함 Release 게시 앱을 사용했다.
보리 0.1.0은 로컬 설치 가능한 아트 후보이며 판매 승인이 완료된 팩이 아니다.

## 산출물과 제작 기록

[보리 제작 안내](../../Art/Characters/bori-rabbit/README.md)에 디자인, 반응, 파일 위치와
팩 생성 명령을 기록했다. 내장 `image_gen`으로 외부 참조 이미지 없이 크림색 토끼 원본을
생성하고, [최종 프롬프트](../../Art/Characters/bori-rabbit/generation.md)와 결과 PNG를 보존했다.
시간 배치와 프레임 인덱스만 manifest에서 구성했고 원본 이미지 픽셀은 수정하지 않았다.

| 파일 | SHA-256 |
|---|---|
| 원본 PNG 및 실행 `spritesheet.png` | `5558e3b0dff14addf559c083d5e0247cb32e08c5a2741e156cab23eb8a759623` |
| 실행 `character.json` | `149619d545467fcafb4344b42d982d1f80f7e94676443e3c95391b3463c5731c` |
| 이번에 생성한 `Bori-0.1.0.unfoldpet` | `7c865178eef8f10c8599a87331d85335db471d1b149f6303bc9ceeed0a779a03` |

팩의 로컬 사본은 `artifacts/pet-packs/Bori-0.1.0.unfoldpet`에 있다.
ZIP에는 `pack.json`, `character.json`, `spritesheet.png` 3개만 포함한다.
원본·프롬프트·제작 원장을 함께 배포하지 않으며, 보리를 기본 번들에 추가하지 않았다.
`Assets/Characters/default-cat/`과 기존 Mochi 원장은 변경하지 않았다.

## 자동 검사와 게시

| 확인 | 결과 |
|---|---|
| Release 전체 테스트 | 119 통과, 실패·건너뜀 0. 보리 자산·원본 보존·설치 검사 5개 추가 |
| 런타임 리소스 감사 | 오류 0, 경고 0. 반응 5종 모두 투명 픽셀을 포함하고 빈 프레임 없음 |
| 원본과 실행 PNG | 전체 바이트와 원장 SHA-256 일치 |
| 전체 클립 디코딩 픽셀 | 19,136,512 bytes, 18.25 MiB. 제작 목표 64 MiB 이내 |
| macOS arm64 게시 | locked restore, 자체 포함·ReadyToRun Release publish 성공 |
| 게시 앱 번들 | 기존 Mochi 파일 3개만 포함. 보리 원본/실행 후보 제외 |
| 진단 인자 누락 | 종료 코드 2 |
| 비어 있지 않은 진단 프로필 | 종료 코드 2. 기존 sentinel 파일 내용·디렉터리 항목 유지 |

전체 테스트 후 진단의 즉시 반응 상태 확인과 캡처 창 높이를 조정했다. 최종 코드는
다시 게시했고 아래 네이티브 진단을 통과했다. 전체 테스트 결과를 최종 진단 변경 후
반복 실행한 것으로 기록하지 않는다.

## macOS 네이티브 재생

새 프로필에 다음 명령을 실행했다. 실행 파일은 이번 변경을 게시한 결과다:

```sh
UNFOLD_DATA_DIR=/private/tmp/unfold-bori.fgGaps/review \
  /private/tmp/unfold-bori.fgGaps/publish/Unfold --review-pet-pack \
  /Users/hwanghyeonseong/Documents/GitHub/Unfold/artifacts/pet-packs/Bori-0.1.0.unfoldpet
```

종료 코드 0, [원본 결과 JSON](2026-09-14-bori-review.json)의 `success: true`.
설치 창의 미리보기가 라이브러리를 변경하지 않는지, Install 후 보리 선택·Pause 유지,
디스크에서 다시 읽기, 숨긴 펫에 반응을 요청해도 숨김 상태가 유지되는지 확인했다.

| 반응 | 설정 길이 | 관측 구간 | 밝은/어두운 미리보기 완료 이벤트 |
|---|---:|---:|---|
| idle | 4.000초 | 4.124초 | 각각 0회, 반복 유지 |
| attention | 1.125초 | 1.253초 | 각각 1회 |
| celebrate | 1.125초 | 1.250초 | 각각 1회 |
| click | 1.000초 | 1.122초 | 각각 1회 |
| stretch | 2.500초 | 2.626초 | 각각 1회 |

실제 경과 시간을 기다리며 반응 시작과 펫의 idle 루프 복귀를 검사했다. 관측 구간에는
캡처와 dispatcher 처리, 의도한 100ms 여유가 포함된다. 정확한 FPS나 지연 성능의
측정값은 아니다. 생성한 PNG 7장 중 idle·반응 4종·설치 전 미리보기의 정적 화면을 확인했다.
192px 밝고 어두운 배경에서 토끼 주변이 투명하며, 사각형 배경이나 가시 영역 잘림은
관찰하지 못했다. [기지개 캡처](../../Art/Characters/bori-rabbit/quality/stretch-preview.png)

원본 JSON·PNG 7장·테스트/게시 로그·인자 검증 결과는 로컬
`artifacts/verification/bori-2026-09-14/`에 보관했다. `artifacts/`는 Git에서 제외되므로
핵심 JSON, 기지개 캡처와 픽셀 측정은 문서·제작 폴더에도 보존했다.

## 남은 검수

- 이 진단은 off-screen 네이티브 창과 코드로 실행한 버튼 이벤트를 사용했다.
  실제 OS 파일 선택 창·마우스·키보드 입력을 확인한 것은 아니다.
- 원본은 1024×1536 RGBA, 256px 셀 24개다. 우선 제작 기준인 384px와 고DPI 최종 검수는
  남아 있다. 단순 확대를 새 고해상도 원본으로 기록하지 않는다.
- alpha 범위는 0–254이고 일부 셀 경계에 alpha 1이 있다. 모든 가장자리 픽셀이 완전
  투명한 것은 아니다. alpha 26 이상 가시 영역의 발 위치는 y=241–242로 1px 차이이며,
  프롬프트의 y=224·24px 여백 목표와 다르다. 상세 [픽셀 측정](../../Art/Characters/bori-rabbit/quality/atlas-metrics.json).
- 시작/끝의 정확한 idle 프레임 일치는 자동 확인했으나, 프레임 사이 선·귀·표정 변화와
  연속 재생의 자연스러움은 사람의 아트 검수가 남아 있다.
- 제작 원본은 단일 PNG이며 다층 `.aseprite`가 아니다. 상업적 권리·판매 조건 확인,
  실제 구매 의사, Windows 실기와 macOS 장시간 사용·실제 알림은 미검증이다.

이 상태는 개발 계획 3차의 아트 후보 전달이다. 3차 전체와 유료 출시의 완료로 판단하지 않는다.
