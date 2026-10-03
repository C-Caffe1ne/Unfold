# Beta v1.0.2 Mac 서명·공증·웹 다운로드 검증

2026-10-01 시작 · 2026-10-02 최종 기록

## 환경과 범위

- 현재 C#·.NET 10·Avalonia 체크아웃의 기존 Beta v1.0.2 배포 후보를 사용했다. 앱 기능을 다시 빌드하거나 버전을 변경하지 않았다.
- Apple Silicon Mac에서 ARM64를 직접 실행하고 Intel 바이너리는 Rosetta로 실행했다. 실제 Intel Mac 검증과 구별한다.
- 사용자가 제공한 Developer ID Application 인증서의 서명 주체는 `DongJin Lee`, Team ID는 `R4TV856AS9`다. 비밀키·비밀번호를 파일에 기록하지 않았다. 공증 인증은 기존 키체인 프로필 `unfold-notary`로 수행했다.
- 기존 작업 폴더의 제품 변경을 보존했다. 이 작업은 Mac 패키지 구조·서명·공증 스크립트, 배포 안내, 검증 보고서와 웹 다운로드 연결에 한정한다.
- 검증 근거는 `artifacts/validation/2026-10-01-macos-notarization/`에 있다. 이 내부 자료는 공개 Release에 올리지 않았다.

## 결과

Apple Silicon·Intel Mac의 앱과 DMG 모두 Developer ID 서명, Apple 공증 승인, 티켓 첨부, Gatekeeper 검사를 통과했다. 공증된 Mac ZIP 두 개와 새 체크섬을 기존 공개 Release에 추가하고 [웹 다운로드](https://unfoldpet.dokhustudio.com/#mac)에 연결했다.

**브라우저에서 받은 앱의 일반적인 첫 실행 UI 확인은 미완료다.** 다운로드의 quarantine을 유지한 앱도 정책 검사는 통과했지만, 마지막 LaunchServices 실행 시도는 90초 대기 후 종료됐다. Mac UI가 잠겨 있었고 자동 잠금 해제도 실패해 확인창을 관찰하거나 조작하지 못했다. 이 시도를 성공으로 보고하지 않는다. 잠금 해제 후 같은 설치 앱의 첫 실행 확인이 필요하다.

## 공증 승인과 티켓

| 대상 | Apple 제출 ID | 결과 |
|---|---|---|
| ARM64 앱 | `a6683bd1-fc79-45e7-8d6b-f888fc7a0fc3` | Accepted·staple·validate 통과 |
| Intel 앱 | `bb4b9151-626b-4c54-a0f5-a874a190b612` | Accepted·staple·validate 통과 |
| ARM64 DMG | `f7b56a3e-d0ce-46f3-8edf-17390b337c94` | Accepted·staple·validate 통과 |
| Intel DMG | `91ccec7e-c3a3-45b6-8e1b-a3332ac8d14d` | Accepted·staple·validate 통과 |

각 제출의 `submit.json`, `result.json`, Apple 검사 로그와 업로드 SHA-256을 별도 시도 폴더에 보존했다. 최종 네 승인 로그에는 공증 이슈가 없다. 초기 실패·중단 후보는 배포에 사용하지 않았다.

## 패키지 수정과 경계면 교차 검증

- 기존 ad-hoc `--deep` 서명이 DLL·JSON·이미지·라이선스에도 일반 서명을 남겼다. 처음에는 Apple 공증이 승인됐지만 Gatekeeper 검사에서 거부됐다.
- Apple의 [TN2206](https://developer.apple.com/library/archive/technotes/tn2206/)에 따라 데이터는 `Contents/Resources`로 배치하고 기존 런타임 경로에는 상대 링크를 둔다. 데이터에 남은 일반 서명은 제거하고 네이티브 코드 19개를 안쪽부터 서명한 뒤 앱을 봉인한다.
- .NET은 엔트리 DLL의 링크를 해석한 폴더를 기본 경로로 사용한다. 이 폴더에도 내장 네이티브 라이브러리·펫 자산·FFmpeg 경로를 연결했다. 중간 후보가 시스템 .NET의 `libhostfxr`로 잘못 이동하던 문제를 수정했다.
- 최종 후보에는 이전 ad-hoc 요구사항과 끊어진 링크가 없다. 두 기종 모두 기존 데이터 파일 233개의 바이트가 동일하며, 바뀐 데이터는 배포 안내 두 파일뿐이다. 각 기종의 네이티브 파일 19개에서 실제 코드·데이터 섹션의 해시가 기존 배포 후보와 같다. 서명 영역은 의도적으로 변경됐다.
- Hardened Runtime을 적용하고 주 실행 파일의 기존 `com.apple.security.cs.allow-jit`만 유지했다. 라이브러리 검증을 끄거나 Gatekeeper를 비활성화하지 않았다. 배포 문제 해결을 위해 quarantine을 지우지도 않았다.
- 앱은 `codesign --verify --deep --strict`, `stapler validate`, `syspolicy_check distribution`, `spctl --assess --type execute` 모두 통과했다. DMG도 서명·티켓 검사와 `spctl --type open --context context:primary-signature`를 통과했다.
- 앱 정책 검사는 macOS 14 이상에서 [Apple의 현재 권장 도구](https://developer.apple.com/forums/thread/130560)인 `syspolicy_check`를 사용하고 이전 OS에는 `spctl`을 사용한다. 이번 최종 후보는 두 도구 모두 통과했다.

## 실행한 검사

| 분류 | 검사와 결과 |
|---|---|
| 자동 검사 | 공증 실패·정책 거부 시 배포 차단, 티켓 첨부 순서, 현대 앱 정책 검사 분기 등 Python 회귀 검사 5개 통과 |
| 자동 검사 | 변경한 셸 파일 `bash -n`, 앱·웹 `git diff --check` 통과 |
| 실제 macOS의 자동 진단 | ARM64 직접 실행·Intel Rosetta 실행 모두 새 `UNFOLD_DATA_DIR`의 smoke 결과 `success: true`, 각 93개 화면 이미지 생성 |
| 실제 macOS의 설치 파일 검사 | DMG를 마운트하고 앱을 검증 폴더로 복사한 뒤 마운트를 해제했다. 양쪽 앱의 버전·기본 펫 자산·서명·티켓·정책 검사 통과 |
| 실제 macOS의 격리 실행 | 별도 설치 사본에 quarantine을 붙인 LaunchServices smoke 진단 `success: true`. 이것은 브라우저에서 받은 최종 파일의 첫 UI 실행 완료와 구별한다 |
| 웹 자동 검사 | Node 테스트 44개, HTML 2개·내부 참조 78개, 배포 dry run, 로컬 HTTP 검사 84개 통과 |
| 공개 배포 검사 | 프로덕션 HTTPS 검사 84개 통과. 공개 HTML SHA-256은 로컬과 동일하며 기존·신규 다운로드 15개가 정확한 Release 자산으로 이동 |
| 공개 파일 검사 | 인증 없이 Mac ZIP 두 개를 전체 다운로드해 크기·SHA-256·CRC·내부 DMG 해시를 대조했다. 내려받은 DMG도 서명·티켓·정책 검사 통과 |
| 브라우저 관찰 | 공개 Mac 버튼 두 개의 파일 저장과 해시 확인, Windows 기존 주소 보존, 콘솔 오류 없음. 390px·1280px 화면에서 가로 넘침 없음 |
| 미완료 | 실제 브라우저 다운로드의 quarantine을 유지한 ZIP→DMG→앱 복사와 앱 정책 검사 통과. 최종 첫 실행 UI는 Mac 잠금으로 확인하지 못함 |

Smoke는 가짜 로컬 계정 서비스와 화면 밖 진단을 포함한다. 실제 Google 로그인·구매·음량 청취·물리적 포인터 조작·새 컴퓨터에서의 첫 실행과 같다고 보지 않는다. 제품 입력이 유지돼 이전 배포의 Release 테스트 526개는 다시 실행하지 않았으며, 그 결과는 이전 배포 보고서의 근거다.

## 공개 다운로드 파일

| 파일 | 크기 | SHA-256 |
|---|---:|---|
| `Unfold-v1.0.2-beta-osx-arm64-notarized-installer.zip` | 74,199,386 bytes | `88dd6ccb2557ae8123a446b3252dae360ab38e7234d6ccecdd7ab9a697a100f1` |
| `Unfold-v1.0.2-beta-osx-x64-notarized-installer.zip` | 78,307,023 bytes | `716e7a29c64a0122a05491bc26b23b84852c5d5b9f554d9aa3191081cf2ccf08` |
| 기존 `Unfold-v1.0.2-beta-win-x64-installer.zip` | 77,477,146 bytes | `d5700c8d0f712245f20bf08f8cd1f755318808c159efca1f00e29413bae869c3` |

새 `Unfold-v1.0.2-beta-notarized-ZIP-SHA256SUMS.txt`는 위 세 파일을 포함한다. 공개 Release 자산은 기존 네 개를 그대로 유지하고 새 세 개만 추가했다. 기존 Mac ZIP과 이전 체크섬의 주소·바이트도 보존했다.

- [공개 Release](https://github.com/C-Caffe1ne/Unfold/releases/tag/v1.0.2-beta)
- 웹 커밋: `58ba62f5aa2c0dd4f70a9ce1f492e5a45c4d0e62`, 기존 Cloudflare `main` 자동 배포 사용.
- Worker·DNS·Custom Domain·메인 스튜디오 사이트 구성은 변경하지 않았다.
- GitHub 자동 Source code 아카이브와 바이너리의 작업 폴더 빌드 입력이 다르다는 기존 안내를 공개 Release 설명에 유지했다. 앱의 기존 제품 변경을 임의로 커밋하거나 태그를 이동하지 않았다.

## 재현 가능한 배포 절차

```sh
export UNFOLD_CODESIGN_IDENTITY="Developer ID Application: 인증서의 이름 (TEAMID)"
export UNFOLD_NOTARY_PROFILE="unfold-notary"
bash Scripts/make-macos-bundle.sh osx-arm64
# ARM 산출물을 먼저 검증·보존한 뒤 Intel을 만든다. 두 빌드는 Unfold.app 경로를 공유한다.
bash Scripts/make-macos-bundle.sh osx-x64
python3 Scripts/package-installers-zip.py --notarized-macos
```

키체인 자격 증명은 별도로 설정한다. 스크립트는 Apple 거부 또는 정책 검사 실패 시 성공으로 표시하지 않고 멈춘다. Mac ZIP 포장 전에는 DMG 서명·티켓·정책 검사를 다시 확인하며 Windows ZIP을 재생성하지 않는다.

## 미검증과 다음 확인

- Mac 잠금을 직접 해제한 뒤 `artifacts/validation/2026-10-01-macos-notarization/browser-installed/Unfold.app`의 첫 열기 확인과 진단 완료를 확인한다. 정상적인 macOS 첫 실행 확인창이 표시될 수 있다.
- 제품을 실행한 이력이 없는 다른 Mac 또는 새 VM에서 브라우저 다운로드→압축 해제→DMG→Applications 복사→첫 실행을 확인한다. 네트워크를 끈 상태의 첫 실행도 별도로 확인해야 하며, 티켓 첨부 성공만으로 오프라인 사용자 경험을 검증했다고 보지 않는다.
- 실제 Intel Mac, macOS 13, Windows 설치·제거·재부팅, OS 보안 저장소의 기존 로그인 복원은 이번 작업에서 검증하지 않았다. Windows 코드 서명은 여전히 미완료다.
- 실제 판매 결제 운영 전환은 이 베타 공증 배포의 범위가 아니다.
