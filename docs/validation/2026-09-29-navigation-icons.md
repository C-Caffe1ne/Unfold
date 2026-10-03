# 내비게이션 명칭·Figma 아이콘 적용

## 범위

사용자가 지정한 Figma 파일 `blkEPmylT7Brsr0js13vHH`의 여섯 노드만 사용했다. 다른 파일의 원본 컴포넌트, 기본 라이브러리 변형은 가져오지 않았다.

| 위에서부터 | 아이콘 | 노드 | 표시 크기 |
|---|---|---|---|
| 홈 | 집 | `26:32` | 24×24 |
| 펫 추가 | 발바닥 | `21:5` | 24×24 |
| 기록 | 막대그래프 | `24:22` | 24×24 |
| 설정 | 톱니바퀴 | `26:38` | 24×24 |
| 테마 | 붓 | `23:15` | 24×24 |
| 종료 | 전원 | `35:60` | 20×20 |

기존 상단 네 탭과 하단 테마·종료 배치를 유지한다. 툴팁과 버튼 이름도 표의 명칭으로 통일했다. 내부 컨트롤 ID와 명령은 유지해 탭 전환·테마 선택·종료 확인 흐름을 보존한다.

## 구현

- `Assets/Icons/Navigation/`: SVG 원본 6개와 출처·크기·SHA-256 기록. 종료는 현재 페이지 인스턴스의 굵기 3 외곽선이다.
- `NavigationIcon.cs`: SVG를 벡터로 읽고 투명도를 마스크로 사용한다. 테마·선택 상태의 Foreground를 그대로 상속한다. 고정 색상으로 원본을 재작성하지 않는다.
- 마스크에 정수 크기의 여백을 두고 원본 영역만 표시한다. 종료 SVG의 부동소수점 외곽선 경계가 마스크 중간 텍스처를 한 픽셀 크게 만드는 현상을 회귀 검사로 확인하고 수정했다.
- `SettingsWindow.Layout.cs`, `SettingsWindow.Themes.cs`: 아이콘·명칭 연결, 46×46 클릭 영역과 24×24 내부 공간 확보.
- `Svg.Controls.Avalonia` 12.0.0.17 및 잠금 파일 추가. 기존 Avalonia 12.1.2와 SkiaSharp 3.119.4는 유지한다. [렌더러 공식 문서](https://github.com/wieslawsoltes/Svg.Skia).
- `NavigationIconTests.cs`: 원본 SVG와 마스크 출력의 알파·색상·크기·중앙 정렬 검사. 6종 × 4테마 × 선택 2상태 × 해상도 2종 = 96개 조합.
- `SettingsDashboardTests.cs`: 여섯 버튼의 명칭 순서 및 기존 탭 전환 검사.

## 검증

집중 검사 중 기존 탭·테마·종료 검사 22개가 통과했다. 종료 아이콘 보정 후 SVG 비교 검사도 통과했다.

- `dotnet test Unfold.slnx -c Release --no-restore -m:1 -nr:false`: **412 통과, 0 실패, 2 건너뜀**, 종료 코드 0. MP4 변환 도구가 없는 환경의 기존 두 검사만 건너뛰었다.
- Release 데스크톱·테스트 빌드 성공.
- SVG 6개가 비어 있지 않고 저장한 SHA-256과 일치함을 확인.
- `git diff --check` 통과.
- macOS Native 렌더링 17장: 4테마 × 4탭, 최소 창 1장. 기본 1120×800·최소 640×560에서 아이콘 표시, 선택 색상, 배치 확인. 저장 위치: `artifacts/verification/navigation-icons/captures/`.
- 최종 캡처 진단이 읽은 `Unfold.dll`과 제품 Release 출력의 SHA-256 일치: `cc512a2b2144e9d5160d3c9373287b20ba68ecec4eecc93bf52d3d50e2a6e232`.

## 검증 경계

macOS 진단은 새 `UNFOLD_DATA_DIR`에서 실제 Avalonia Native 렌더러로 창을 구성하고 RenderTargetBitmap을 저장한다. 물리 마우스·키보드 입력과 Windows 실기 확인을 대신하지 않는다. 로그인·구매·펫 행동 변경은 이 작업에 포함하지 않았다.
