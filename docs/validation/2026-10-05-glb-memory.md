# GLB 메모리 최적화 검증 — 2026-10-05

Windows에서 GLB 펫 사용 시 RAM이 400MB를 넘는다는 보고에 따라, 최신 `codex/beta-v1.0.4`의 `717a63b` (`1.0.4-beta`)를 기준으로 렌더링 중 반복 할당을 줄였다. **Windows 작업 관리자 수치는 아직 재측정하지 않았다.** 아래 메모리 수치는 macOS에서 측정했다. 변경은 개발 소스에 있으며 공개 설치 파일·업데이트 피드에는 반영하지 않았다.

## 원인과 변경

기존 GLB 경로는 프레임마다 출력 가로·세로의 2배 크기로 색상·깊이 버퍼를 만들고, 각 메시의 변환된 정점·관절 행렬도 다시 할당했다. 큰 배열이 큰 개체 힙에 반복 생성되어 GC 부담과 메모리 사용량 변동을 키웠다.

`GlbModel.Rendering.cs`에서 색상·깊이·정점·관절 임시 버퍼를 모델당 한 묶음으로 재사용한다. 행동 클립과 미리보기의 출력 크기가 달라도 가장 큰 요청에 맞춘 버퍼를 함께 사용한다. 렌더와 축소가 끝날 때까지 같은 잠금을 유지하며, 외부로 반환하는 이미지는 독립된 픽셀 배열을 가진다. 다음 프레임이나 다른 창의 렌더가 이전 이미지를 덮어쓰지 않는다.

출력 해상도, 4개 샘플을 사용하는 가장자리 처리, 몸 방향 고정, 애니메이션 속도, 클릭·드래그 경로는 유지했다. 강제 GC, OS 메모리 축소 API, 화질·프레임 속도 하향은 추가하지 않았다.

버퍼는 모델이 살아 있는 동안 가장 큰 요청 크기를 유지한다. 최대 출력 1024px에서 색상·깊이 임시 버퍼의 합은 모델당 32MiB이며, 정점·관절 버퍼는 별도다. 반환 이미지·텍스처·UI·GC 힙을 포함한 앱 전체 RAM의 상한을 뜻하지 않는다. 모델을 여러 개 편집하거나 최대 크기까지 확대한 경우 보유 메모리는 달라질 수 있다.

## 렌더러 전후 비교

Kazusa.glb, 11,466개 삼각형, `Cafe_Idle`, macOS 26.6.2 / .NET 10.0.12. 크기별 5프레임 예열 뒤 40프레임씩 측정했다. `GC.GetAllocatedBytesForCurrentThread()`의 프레임 렌더 구간만 집계했으며 해시 계산·대기·JSON 작성은 제외했다. 이는 **누적 할당량**이고 앱의 상주 RAM 수치가 아니다.

| 출력 크기 | 변경 전 / 프레임 | 변경 후 / 프레임 | 할당 감소 | 픽셀 SHA-256 |
|---|---:|---:|---:|---|
| 192px | 1.809 MiB | 0.165 MiB | 90.9% | 일치 |
| 384px | 5.609 MiB | 0.587 MiB | 89.5% | 일치 |
| 576px | 11.940 MiB | 1.290 MiB | 89.2% | 일치 |
| 1024px | 36.546 MiB | 4.024 MiB | 89.0% | 일치 |

160개 프레임의 픽셀이 전부 일치했다. 비교 기준은 공개 v1.0.4-beta 패키지의 Core DLL이고, 변경 후는 현재 소스의 Release DLL이다. 측정에 사용한 DLL·소스·모델 해시는 [검증 JSON](2026-10-05-glb-memory/verification.json)에 기록했다. 원본 GLB 파일은 저장소나 배포물에 추가하지 않았다.

[변경 전 원본](2026-10-05-glb-memory/renderer-before.json) · [변경 후 원본](2026-10-05-glb-memory/renderer-after.json)

## 실제 macOS 앱 측정

동일한 네이티브 Avalonia 진단 호스트에서 Core DLL만 교체하고 각각 새 `UNFOLD_DATA_DIR`을 사용했다. 실제 AppRuntime·펫 창·설정 창을 실행했다. 로그인·네트워크 영향은 진단 모드로 제외했다. 각 상태 전환 뒤 4초를 기다리고 1초 간격으로 16번 `Process.WorkingSet64`를 읽었다. 관측된 화면 배율은 1x였다. 강제 GC는 실행하지 않았다.

| 상태 | 평균 상주 메모리, 전 → 후 | 측정 구간 최대, 전 → 후 |
|---|---:|---:|
| 펫 100% (192px) | 106.5 → 112.1 MiB | 134.4 → 190.6 MiB |
| 펫 150% (288px) | 161.5 → 102.8 MiB | 218.4 → 118.2 MiB |
| 150% 펫 + 설정 창 | 207.2 → 122.1 MiB | 240.7 → 151.8 MiB |
| 펫·설정 모두 숨김 | 87.9 → 51.8 MiB | 98.7 → 62.0 MiB |

확대 상태의 평균은 약 36%, 설정 창까지 연 상태는 약 41% 감소했다. 기본 크기 상태의 평균·최댓값은 오히려 증가했다. GC 실행 시점, 초기 로딩, OS 압축·상주 정책의 영향을 받으므로 모든 상태에서 RAM이 일정 비율 감소한다고 해석할 수 없다. 각 조건을 한 번씩 측정한 참고 관측이며 다른 프로세스의 부하는 통제하지 않았다. 장시간 사용·Windows 실기 결과를 대신하지 않는다. 숨김 상태에서는 전후 모두 관측 구간에 렌더링 할당이 사실상 멈췄다.

[변경 전 원본](2026-10-05-glb-memory/native-before.json) · [변경 후 원본](2026-10-05-glb-memory/native-after.json)

## 회귀 검사와 한계

- 새 메모리 검사: 기존 코드에서 4개 실패·1개 통과, 변경 후 5개 모두 통과.
- GLB 집중 검사: 23개 통과. 출력 해상도·투명도·루트 고정·모프, 동시에 다른 크기/자세 렌더, 반환 이미지 보존, 클릭·들기·착지·숨김 흐름을 포함한다.
- `dotnet test Unfold.slnx -c Release --no-restore`: 610개 통과, 실패·건너뜀 0개.
- 실제 macOS 앱: 두 DLL로 네 상태 실행 완료. 포인터 동작은 자동 검사 결과이며 이번 작업에서 물리 마우스 조작을 재검증하지 않았다.
- 관련 없는 Supabase·브랜드 기존 변경 22개는 해시가 일치한다.
- Windows 현재 변경 빌드의 RAM, 사용자 PC의 400MB 재현, 장시간 사용, 설치·배포는 미검증이다. 이전 릴리스의 Windows 통과 기록을 이번 최적화의 통과 근거로 사용하지 않는다.

[변경 전 검사 로그](2026-10-05-glb-memory/tests-before.log) · [집중 검사 로그](2026-10-05-glb-memory/tests-focused.log) · [전체 검사 로그](2026-10-05-glb-memory/tests-full.log)

## Windows/macOS에서 다시 측정

.NET 10 SDK가 있는 개발 체크아웃에서 아래 명령을 실행한다. GLB 파일과 새 보고서 경로를 지정한다. 프로브는 검증용이며 앱 설치·배포에 포함하지 않는다. Native 모드는 새 임시 프로필에 펫을 가져와 약 80초간 상태별로 측정하고 종료한다. 기존 사용자 설정을 읽거나 바꾸지 않는다.

```powershell
# 저장소 루트에서 네이티브 앱 메모리 측정
dotnet build docs/validation/2026-10-05-glb-memory/memory-probe.csproj -c Release -p:ProbeMode=Native
dotnet docs/validation/2026-10-05-glb-memory/bin/Release/net10.0/Unfold.Tests.dll "C:/pets/model.glb" "C:/temp/glb-native-new.json"

# 렌더러만 측정: JSON을 표준 출력으로 기록
dotnet build docs/validation/2026-10-05-glb-memory/memory-probe.csproj -c Release -p:ProbeMode=Renderer
dotnet docs/validation/2026-10-05-glb-memory/bin/Release/net10.0/Unfold.Tests.dll "C:/pets/model.glb" > "C:/temp/glb-renderer-new.json"
```

macOS에서는 두 입력 경로를 해당 머신의 경로로 바꾼다. 전후를 비교할 때 GLB·DPI·창 표시 상태·프로브 코드·.NET 런타임을 같게 유지하고 Core DLL의 해시를 확인한다. Windows에서는 작업 관리자의 작업 집합·개인 작업 집합·커밋 크기를 구분해 기록한다. 이번 macOS 관측은 `WorkingSet64` 기준이다.
