# GIF·GLB 제작·가져오기 안내 구현 검증

2026년 10월 12일 · `4c74880`의 과거 검증 기록. 후속 사용자 요청으로 아래 안내 UI와 설명은 제거했다. [제거 결과](2026-10-12-gif-glb-guidance-removal.md)를 따른다.

## 기준과 범위

- 작업트리: `/Users/hwanghyeonseong/.codex/worktrees/gif-glb-guidance/Unfold`, 브랜치 `codex/gif-glb-import-guidance`.
- 기준 HEAD: `6f0a3ddef99e38f288faaf86ca2fc8793559cac6`, 프로젝트 `1.1.3-beta`. 시작 시 전체 6개 작업트리의 HEAD·브랜치·미커밋 변경·csproj 버전과 커밋 차이를 비교했다.
- 공개 기준: `v1.1.3-beta`, 제품 소스 `b1242ea55e20c0c08823249cf739517ad4557808`. 기준 HEAD와 공개 태그의 제품 source/Tests/Assets diff는 없다. 후속 문서·QA 커밋을 제품 변경과 구분했다.
- 종료 전 원본 `codex/remove-unused-features`는 같은 `6f0a3dd`이고 clean이었다. 기존 사용자 작업트리·데이터를 수정하지 않았다.
- macOS 26.6.2 arm64, .NET SDK 10.0.401/runtime 10.0.12, Avalonia 12.1.2. 테스트와 진단은 매 실행 새 `UNFOLD_DATA_DIR`을 사용했다.
- 기존 GIF/GLB 편집 화면의 안내·저장 조건·가져오기 오류 복구와 관련 문서만 변경했다. Core 파서·스키마·자산·전역 Ui·버전·배포 설정·기존 회귀 테스트는 변경하지 않았다.

## 구현

| 화면·파일 | 변경 |
|---|---|
| `PetImportGuidance.cs` | 기본 접힘 파일 준비 안내, 지원 형식, Aseprite GIF·Blender GLB 제작 순서, 현재 제한, 저장 조건과 안전한 알려진 오류별 원인·해결 방법 |
| `CustomPetWindow.cs` | 이름/파일 선택 아래 안내, 기본 · 필수 표시, 별도 저장 조건 문구, 가져오기 catch에만 오류 mapper 적용 |
| `GlbPetView.cs` | 같은 안내, 기본 대기 필수 반복·미연결 행동 설명, 별도 저장 조건 문구, Read catch에만 오류 mapper 적용 |
| `SmokeDiagnostics.cs` / `GlbDiagnostics.cs` | 실제 기존 UI의 1120×800·640×560 닫힘/펼침/스크롤 캡처 각 6장 |
| 두 신규 테스트 파일 | 오류 whitelist/unknown fallback·초안/저장 gate·키보드 모델·최소 창·가져오기 실패 후 재시도 검증 50개 |
| `pet-packs.md` / `glb-pets.md` | 현재 1.1.3 흐름과 지원 제한, 제작·취소·초안 경계, 기본 ID 보호와 제거된 별도 미리보기 문구 정정 |

안내 열기/닫기는 초안·행동·회전·수정 여부를 바꾸지 않는다. 새 2D는 파일 내보내기 성공 후 초안 초기화 → 펫 저장 확인 순서를 유지한다. 설치 확인 취소는 파일을 남기고 설치하지 않는다. GLB export는 현재 초안을 유지한다. 파싱/검증 실패 전의 기존 GLB 초안은 유지하지만 후보 적용 이후 렌더링 실패까지 rollback을 약속하지 않는다.

현재 parser는 모든 필수 확장을 거부한다. Static이라는 clip 이름만으로 정적 모델을 판정하지 않는다. 미연결 preview의 기본 대기 표시와 실제 펫의 행동 보완은 구별한다. 알려진 오류만 고정된 안전 문구로 변환하고 임의 경로·모델/애니메이션 이름·stderr는 기존 안전 fallback을 따른다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| 시작 전 locked restore·Release build | 성공, 경고 0·오류 0, tracked lockfile 변화 없음 |
| 시작 전 기존 집중 검사 | 60/60 통과, 건너뜀 0 |
| 시작 전 macOS smoke | 성공, 110 PNG |
| 최종 Release build | 성공, 경고 0·오류 0 |
| 최종 집중 검사 | 110/110 통과, 실패·건너뜀 0 |
| 최종 전체 Release 검사 | 840/840 통과, 실패·건너뜀 0 |
| 최종 macOS smoke | success=true, 116 PNG, 640×560 확인, GIF 제작·저장/재열기·기존 기능 회귀 성공 |
| 최종 macOS GLB review | success=true, 33 PNG, 실제 편집 UI 가져오기·설치·디스크 재열기·기본 대기 재생 성공 |
| 최종 macOS 생성 GIF 팩 review | success=true, 4 PNG, idle 반복·click 한 번 후 idle 복귀, 디스크 재열기·설치 선택 성공 |
| Windows x64 자체 포함 publish | 성공, Unfold.exe가 PE32+ GUI x86-64 확인 |
| 독립 source/캡처 검토·diff 검사 | 해당 변경의 미해결 기능 오류·문구 계약 불일치 없음, git diff --check 통과 |

중간 집중 검사에서 파일 배정 후 스크롤바가 사라지며 slot 폭이 24px 증가하는 회귀를 발견했다. 항상 scrollbar 표시 방식은 기존 1400×1600 full-width 계약에 맞지 않아 적용하지 않았다. 기존 파일명·행동 selector·배정 버튼을 같은 40px 행에 두고 Space 8 padding과 2D 본문 간격을 사용해 초기 카드 높이를 줄였다. Auto 스크롤과 두 기존 폭 계약은 그대로 유지했고 기존 테스트를 변경하지 않고 최종 110개와 전체 840개를 통과했다. 임시 측정 테스트·파일 IO는 제거했다.

## 실제 OS와 자동 검사의 경계

- macOS 증거는 네이티브 dispatcher·렌더링·시간 기반 재생·격리 파일 IO 및 RenderTargetBitmap 캡처다. 파일 picker와 버튼은 진단에서 주입했고 `physicalInput=false`다. 실제 macOS 파일 선택 대화상자·물리 키보드/마우스·화면 읽기 프로그램은 이번 작업에서 확인하지 않았다.
- GLB는 직접 만든 삼각형 하나·Idle 0.2초 모델이다. 실제 표시 배율은 1×이며 192/288/384 DIP→같은 픽셀 크기를 확인했다. 포인터 행동이 없는 fixture이므로 `pointerCanvasVerified=false`; 실제 복잡한 모델·Retina 2×·임의 외부 모델 호환성을 이 결과로 증명하지 않는다.
- GIF 팩은 기존 진단 자산의 480×480·18프레임, idle/click 각 4초다. 불투명 배경과 optional 미연결 warning은 fixture의 예상 상태이고 audit errors는 없다.
- MP4 관련 전체 테스트는 기존 PATH의 FFmpeg로 실행됐고 건너뜀은 없다. Windows crosspublish에 미디어 도구 번들·설치 마법사·서명·공개 업데이트 검증은 포함하지 않았다.
- 사용자 답변: 현재 Windows 기기/실행 환경 없음. **Windows 실제 화면·파일 선택·키보드·설치·재생은 미검증**이다. macOS에서 만든 Windows 빌드를 Windows 실행 증거로 분류하지 않는다.
- 배포·원격 push·운영 설정 변경은 수행하지 않았다.

## 실행 근거

검증 원본은 `/tmp/unfold-gif-glb-implementation`에 있다.

- `baseline/baseline.trx`, `focused-complete/focused-complete.trx`, `full/full.trx`
- macOS smoke: `/tmp/unfold-guide-native-final.7UHUyJ/verification/smoke.json`
- GLB: `/tmp/unfold-guide-glb-final.0Z22E7/verification/glb/result.json`
- GIF 팩: `/tmp/unfold-guide-pack-final.hIUDeF/verification/pet-review.json`
- GLB 입력: `guide-sample.glb`, 자체 생성 1,520바이트.
- Windows build: `windows-build/Unfold.exe`.
- 구현/독립 검토 보고서: `helper.md`, `ui.md`, `review.md`.

안내 캡처 12장은 아래 폴더에 보존한다.

`/Users/hwanghyeonseong/.codex/visualizations/2026/10/11/01a12c4a-aab6-77f2-ad0e-8fbe5e551078/gif-glb-guidance-implementation`

| 형식 | 기본 크기 | 최소 크기 |
|---|---|---|
| 2D | `settings-media-guide-1120-800-{closed,expanded,expanded-scrolled}.png` | `settings-media-guide-640-560-{closed,expanded,expanded-scrolled}.png` |
| GLB | `settings-glb-guide-1120-800-{closed,expanded,expanded-scrolled}.png` | `settings-glb-guide-640-560-{closed,expanded,expanded-scrolled}.png` |

재실행 명령은 [검증 안내](../verification.md)의 격리 데이터 원칙을 따른다.

```sh
dotnet build Unfold.slnx -c Release --no-restore
dotnet test Unfold.slnx -c Release --no-build --no-restore
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --smoke-test
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --review-glb <GLB>
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --review-pet-pack <PACK>
dotnet publish src/Unfold.Desktop/Unfold.Desktop.csproj -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o <OUTPUT>
```
