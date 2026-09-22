# 클릭 중 펫 잔상 수정

2026-09-22 · `release/mvp` · macOS · C# / Avalonia 12.1.2.

## 원인 확인

이전 `AnimationView.RenderTransform` 변경만으로는 해결되지 않았다. 초기의 비활성 창 검사와
간격을 둔 정지 캡처는 짧은 잔상을 놓쳤다. 창을 활성화하고 AppKit 누름·해제 이벤트를 반복한
네이티브 화면 녹화에서 사용자의 첨부 사진과 같은 반투명 중첩을 재현했다.

비교 결과 문제를 **macOS Metal 표시 경로**로 좁혔다. 동일한 펫·클립·포즈와 입력 조건에서
Metal은 이전 실루엣을 잠깐 표시했으며, OpenGL/Software 경로에서는 같은 중첩이 발견되지
않았다. 원본 이미지 중첩, 창 그림자와 `CALayer.contents` 암묵적 애니메이션은 확인되지 않았다.
GPU 드라이버나 Avalonia/Skia 내부의 어느 지점이 결함인지는 확정하지 않았다.

부분 영역 클리핑, 루트 중간 레이어, 고정된 그리기 범위도 비교했다. 일부 간격 추출 캡처가
깨끗하더라도 전체 프레임에는 잔상이 남는 경우가 있어, 이 실험 변경들은 최종 코드에서 제외했다.

## 최종 변경

`Program.ConfigureRendering`에서 macOS의 렌더링 우선순위를 `OpenGl`, `Software`로 지정한다.
OpenGL 초기화가 불가능한 Mac은 소프트웨어 렌더링으로 대체한다. Windows의 기존 렌더러 설정은
유지한다. 원본 리소스, 스퀴시·바운스, 놀란 뒤 두리번거리는 반응과 포인터 처리도 유지한다.

이번 수정 파일은 `src/Unfold.Desktop/Program.cs`와 이 검증 문서다. 앞선 작업의
`AnimationView.cs`, 렌더링 검사 및 다른 미커밋 변경은 보존했다.

옵션의 공식 계약은
[AvaloniaNativePlatformOptions](https://docs.avaloniaui.net/api/avalonia/avalonianativeplatformoptions)를 참고했다.

## 검증

- 전체 Release 테스트: **272 통과, 실패·건너뜀 0**. 빌드 포함.
- 실제 앱 `--smoke-test`: **success: true**, 자동 UI 캡처 **91장**. 새 임시 프로필 사용.
- 활성화된 실제 macOS 펫 창에 AppKit 이벤트를 주입해 보리와 Mochi 각각 **18회 연속 클릭**.
  수정 경로에서 보리 **479프레임**, Mochi **481프레임**을 추출했다. 전체 프레임의 저채도 픽셀
  집계로 중첩 후보를 추린 뒤 후보·시간별 화면을 시각 검토했으며 기존과 같은 잔상은 발견되지 않았다.
- 보리의 재현 프레임은 저채도 픽셀이 약 2,800개였고, OpenGL 비교 녹화의 최대값은 165개였다.
  이 집계는 밝은 배경의 해당 리소스에 대한 진단 보조 수치이며, 일반적인 이미지 품질 판정기가 아니다.
- macOS 진단은 출하 앱과 같은 `Program.ConfigureRendering`을 사용해도 정상 표시되었다.
  별도 실행에서 Software fallback을 제외한 OpenGL 단독 초기화·재생도 성공해 현재 Mac의 GPU 경로를 확인했다.
- `git diff --check` 통과.

## 재현·회귀 확인

진단 코드와 증거는 `artifacts/probes/pet-ghost/`,
`artifacts/verification/2026-09-22-pet-ghost/`에 보관했다.

- 수정 전: `clicks-activated.mov`, `video-active-007.bmp`.
- 수정 후: `opengl-bori-rabbit.mov`, `configured-default-cat.mov`.
- 화면 비교: `comparison.png`.
- 전체 프레임 집계: `opengl-bori-gray-counts.json`, `configured-mochi-gray-counts.json`.

회귀 확인 시에는 새 `UNFOLD_DATA_DIR`의 펫 창을 활성화하고 누름 60ms·해제 후 260ms 간격으로
반복 클릭한다. 반드시 녹화의 전체 프레임을 추출한다. 12fps 등의 간격 추출만으로는 이 문제의
해결 여부를 판정할 수 없다. 클릭 후 기본 모습으로 돌아오는 것과 드래그 동작도 확인한다.

## 한계

현재 Mac의 네이티브 렌더링은 확인했지만 입력은 주입한 이벤트다. 물리적 마우스·트랙패드,
다른 Mac/GPU·고DPI 화면, Windows 실기와 장시간 CPU·전력 영향은 미검증이다. macOS 설정은
앱 전체 렌더링에 적용되므로 다른 Mac에서의 배포 확인에 이 항목을 포함해야 한다.
실행 중인 앱에는 시작 설정이 소급 적용되지 않으므로 수정 빌드로 다시 실행해야 한다.
