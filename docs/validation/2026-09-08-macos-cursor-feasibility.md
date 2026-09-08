# macOS 커서 커스텀 검증 및 직접 배포 방향

검증일: 2026년 09월 08일

## 결론

현재 Mac(macOS 26.6.2, build 25G83, arm64)에서 비공개 API로 `com.apple.coregraphics.ArrowS`의 커서 이미지, 크기, 핫스폿을 변경하고 원본으로 복원하는 등록 데이터 왕복 검증에 성공했다. 화면 표시와 타 앱 클릭 동작까지 검증한 것은 아니다. 제품 기능은 아직 구현하지 않았다.

## 실행 근거

임시 Objective-C 도구를 clang + AppKit으로 빌드했다. 실행 파일은 앱 샌드박스 없이 현재 사용자의 WindowServer 세션에 연결했다. sudo, 시스템 파일 수정, 로그인 항목 설치는 수행하지 않았다.

- 샌드박스 실행: connection=0, 읽기 오류 268435459.
- 샌드박스 밖 실행: 유효한 connection, 기존 Arrow 읽기 성공(28×40, 핫스폿 4,2, 1프레임, 4개 이미지 표현).
- API 존재: CGSMainConnectionID, CGSRegisterCursorWithImages, CGSCopyRegisteredCursorImages, CGSRemoveRegisteredCursor, CoreCursorCopyImages.
- 기존 Arrow: 등록 반환값 0이지만 읽어온 데이터는 원본과 동일. 반환값만으로 성공 판정하면 안 된다.
- 시스템 커서 이름 조회: 0=Arrow, 6=ArrowCtx, 100=ArrowS.
- ArrowS: 자체 생성한 분홍색 32×32 RGBA 화살표, 핫스폿 0,0, 1프레임 등록 후 읽기 결과 변경 확인.

```text
apply=0 changed_from_original=YES size=32x32 hotspot=0,0
immediate_restore=0 exact_readback=YES
restore=0 exact_readback=YES
```

변경 전에 원본 이미지 표현과 메타데이터를 plist로 저장했다. 별도 프로세스가 8초 뒤 복원하도록 먼저 실행한 후 변경했다. 메인 프로세스는 3초 후 원본을 복원했다. 두 복원 모두 PNG 표현과 메타데이터를 포함한 사전 전체의 동등성을 확인했다. 복원 프로세스는 실행됐지만 메인 프로세스 강제 종료 상황은 시험하지 않았다.

검증용 임시 파일: `/private/tmp/unfold-cursor-validation/`의 `probe.m`, `roundtrip.m`, `roundtrip-arrowS.m`, `register.m` 및 실행 파일. 백업: `original.plist`, `original-arrowS.plist`. 등록 결과: `applied.plist`, `applied-arrowS.plist`. 임시 파일은 정리될 수 있으며 제품 코드가 아니다. roundtrip 도구의 종료 코드만으로 적용 성공을 판단하지 말고 변경 여부와 복원 로그를 함께 확인해야 한다.

## 직접 배포 설계 방향

1. 기존 픽셀 에디터와 분리된 CursorEngine을 만든다. API를 동적으로 로드하고 지원 여부를 검사하며, OS에 따라 역할과 식별자를 연결한다. 이번 환경에서는 ArrowS 검증 결과가 있으나 모든 macOS에서 같은 식별자를 사용한다고 가정하지 않는다.
2. apply / restore / status 경계를 둔다. 변경 전 백업, 등록 후 읽기 검증, 실패 시 복원을 기본으로 한다. 기존 제3자 테마가 있다면 기본값으로 덮어쓰지 않고 적용 전 상태로 되돌린다.
3. 직접 배포 타깃에서 엔진을 실행한다. 현재 앱의 샌드박스 entitlement를 일괄 제거하지 않고 배포 설정을 분리한다. Developer ID 서명, Hardened Runtime, 공증 상태에서 다시 검증한다. 이번 unsigned CLI 성공은 서명·공증 성공을 의미하지 않는다.
4. 엔진의 화면 검증을 통과하면 기존 네이티브 PixelEditorModel을 활용해 커서 모드와 핫스폿, 커서 역할, 저장·적용·복원 UI를 연결한다. 캐릭터 패키지와 커서 패키지는 분리한다.
5. 첫 제품 범위는 정적 기본 화살표 하나로 한정한다. 애니메이션·텍스트·링크·리사이즈 커서, 자동 로그인 재적용은 이후 검증 범위다.

## 남은 합격 조건

- Finder, 브라우저, 에디터 사이를 이동해 커서가 실제 표시되고 즉시 덮어써지지 않는지 확인.
- 실제 클릭 좌표가 핫스폿과 맞는지 확인.
- Retina/다중 디스플레이, 커서 확대 접근성 설정, 전체 화면에서 확인.
- 메인 프로세스 비정상 종료, 잠자기/깨우기, 로그인 후 적용/복원 정책 확인.
- 지원 대상으로 선언할 각 macOS 버전과 서명·공증된 빌드에서 재검증.

## 참조 및 코드 사용 제약

- 원본: https://github.com/alexzielenski/Mousecape (검토 commit 184f87b5d8ba52e0529f0ca8a7bd15b25b101358).
- Tahoe 포크: https://github.com/AdamWawrzynkowskiGF/Mousecape-TahoeSupport/tree/PreRelease-v01 — 기본 브랜치는 원본과 동일해서 릴리스 태그의 변경을 확인했다. 시스템 커서 이름에서 Arrow 관련 이름을 탐색하는 대응이 있다.
- 원본 LICENSE에는 상업적 목적 및 금전적 이익을 위한 사용·수정·재배포를 제한하는 조항이 있다. 제품에 소스를 복사하거나 포크를 포함하는 계획은 채택하지 않는다. 코드 사용 권한을 확보하거나 사용 가능한 별도 구현 경로를 정해야 한다. 임시 API 실험이 상업적 권리 문제를 해결하지는 않는다.

## 추가 검증: 2026년 09월 08일

### 판정

등록 데이터 왕복은 통과했으나 화면 교체의 최종 판정은 보류한다. `ArrowS` 등록이 바뀌었다는 사실과 실제 현재 커서가 바뀌었다는 사실을 구분해야 한다. 아래 결과 때문에 기존 결론을 제품 준비 완료로 해석하면 안 된다.

| 항목 | 결과 | 근거 또는 제한 |
| --- | --- | --- |
| Hardened Runtime | 제한적 통과 | ad-hoc 서명, flags=0x10002(adhoc,runtime), Runtime Version=26.5.0인 CLI에서 적용·복원 성공 |
| 45초 등록 유지 | 통과 | 두 실행에서 5초 간격 9회 모두 custom_metadata=YES; 매번 원본 exact_readback=YES |
| 강제 종료 복구 | 통과 | 검증 메인 pid=91762에 SIGKILL, 종료 137; 이후 독립 읽기 비교 independent_post_crash_original_match=YES |
| Finder·Chrome 전환 | 부분 검증 | Finder 메뉴 동작과 Chrome 클릭 수행 중 등록 유지. 캡처에서 커서 모양은 식별하지 못함 |
| 일반 창 클릭 | 입력 좌표 통과 | 임시 AppKit 창 중심 클릭 center_delta=(0.0,0.0), 외부 모니터 1배율 |
| 전체 화면 클릭 | 입력 좌표 부분 통과 | 전체 화면 진입·복귀; center_delta=(-0.9,0.0), 캡처 좌표 변환 반올림 가능성이 있어 시각적 핫스폿 일치 판정은 보류 |
| 실제 현재 커서 | 미확정 | 등록 데이터는 32×32인데 임시 AppKit 창의 NSCursor.currentSystemCursor.image.size는 28×40. API 의미/캐시/실제 표시의 차이를 추가 확인해야 함 |
| 다중 화면 | 환경만 확인 | M2780D 1920×1080, 1배율; Built-in Retina Display 1470×956 논리 좌표, 2배율. 두 화면 간 실제 커서 표시 시험은 미완료 |
| 확대 접근성 설정 | 미실행 | 원래 설정을 변경하지 않음 |
| 잠자기·깨우기, 로그인 | 미실행 | 실제 세션 전환 필요. 로그인 자동 적용 기능 자체는 미구현 |
| Developer ID·공증 | 환경 차단 | security find-identity -v -p codesigning 결과 0 valid identities found. ad-hoc 서명 성공을 공증 성공으로 간주하지 않음 |
| 다른 macOS 버전 | 환경 차단 | 현재 Mac의 26.6.2만 시험 |

### 클릭 로그

```text
click=(350.0,250.0) center_delta=(0.0,0.0) cursor=28.0x40.0 screen=M2780D scale=1.0 fullscreen=0
click=(959.1,540.0) center_delta=(-0.9,0.0) cursor=28.0x40.0 screen=M2780D scale=1.0 fullscreen=1
```

현재 시스템 커서 크기가 원본과 같다는 관찰만으로 변경 실패를 확정하지도 않는다. AppKit 객체가 등록 데이터와 다른 정보를 제공할 가능성이 있으므로 실제 포인터를 포함한 관찰 또는 사용자의 육안 확인과 함께 판정해야 한다. CUA 스크린샷에는 분홍색 포인터가 식별되지 않았으며, UI 클릭 성공은 포인터 이미지 교체의 증거가 아니다. 사용자에게 실제 분홍색 화살표 관찰 여부를 비동기로 질문했으며 이 문서 작성 시 답변은 아직 없다.

### 다음 검증 순서

1. 기본 화살표 위에 놓은 실제 포인터를 사용자가 확인하거나 포인터를 포함하는 검증 가능한 화면 관찰로 확인한다. 현재 API 간 크기 차이가 해소되기 전에는 에디터 적용 버튼을 구현하지 않는다.
2. 외부 1배율/내장 2배율 간 이동과 확대 설정 변경 전후를 검증한다. 각 실행은 원본 커서와 설정 복원으로 끝낸다.
3. 저장되지 않은 작업을 정리한 상태에서 잠자기·깨우기 시험을 사용자와 함께 진행한다. 로그아웃/로그인은 세션이 끊기므로 사용자 주도로 수행하고 재접속 후 상태를 확인한다.
4. Developer ID 인증서와 공증 인증이 준비되면 서명된 직접 배포 빌드에서 동일 시험을 반복한다. 자격 증명 값을 보고서에 기록하지 않는다.

### 라이선스 경계

2026년 09월 08일 원본 LICENSE를 다시 확인했다: https://github.com/alexzielenski/Mousecape/blob/master/LICENSE . 상업적 목적 또는 금전적 이익을 위한 사용을 제한하는 조건이 명시돼 있다. 무료 배포라는 이유만으로 허용된다고 판단하지 않는다.

- Mousecape의 소스·헤더·바이너리·커서 에셋을 Unfold에 추가하지 않았다. Package.swift, Sources, Packaging에서 Mousecape / CGSInternal / MCArrowSynonyms 의존 흔적이 조회되지 않았다.
- 임시 실험은 이전에 확인한 비공개 API 호출 규약에 의존한다. 이미 원본 구현을 읽었으므로 이를 검증된 클린룸 구현 또는 상업적 권리가 해결된 구현이라고 부르지 않는다.
- 제품용 구현에 임시 실험 코드를 그대로 승격하지 않는다. 코드 사용 허가를 확보하거나, 재사용 가능한 구현·문서의 출처와 권리를 정리한 별도 구현 경로를 확정해야 한다.
- 이번 변경은 검증 문서뿐이다. 앱 소스, 샌드박스 설정, 로그인 항목은 변경하지 않았다.

추가 임시 도구: `live.m`(45초 유지), `crash.m`(강제 종료 및 verify), `displays.m`(화면 조회), `Calibration.m` 및 `CursorCheck.app`(클릭 좌표 관측), `clicks.log`. 모두 `/private/tmp/unfold-cursor-validation/`에 있고 제품 패키지에 포함되지 않는다. 실행 도구는 원본 백업 파일을 덮어쓸 수 있으므로 동시 실행하거나 적용 중 새 실행을 시작하지 않는다.

## 후속 검증: 확대 설정과 Retina

사용자 지시에 따라 Developer ID 서명·공증은 후속으로 연기했다.

### 확대 설정 변경: 유지 조건 실패

시스템 설정 > 손쉬운 사용 > 디스플레이에서 포인터 크기 원래 값 `1`, 흔들어 찾기 `on`, 외곽 검정/채우기 흰색을 확인했다. 커스텀 등록 후 크기 슬라이더를 Increment로 `1.3`으로 변경했다.

```text
apply=0 changed_from_original=YES size=32x32 hotspot=0,0
tick=5 custom_metadata=YES
tick=10 custom_metadata=NO
tick=15 custom_metadata=NO
...
tick=45 custom_metadata=NO
immediate_restore=0 exact_readback=YES
restore=0 exact_readback=YES
```

크기 슬라이더는 Decrement로 원래 `1`로 복원했고 UI 값으로 확인했다. 크기만 되돌려도 커스텀 등록은 돌아오지 않았다. 이후 새 실행으로 등록하면 45초 유지 및 복원이 다시 성공했다. 이 결과는 확대 설정 변경으로 등록이 무효화되고 재등록이 필요하다는 관찰을 뒷받침한다. 확대된 상태에서의 실제 커서 이미지 품질은 통과로 판정하지 않는다. 색상과 흔들어 찾기 설정은 변경하지 않았다.

### Retina 입력: 좌표 확인, 시각적 커서 검증은 미완료

각 화면에 검증 창을 만드는 임시 `DualDisplay.m` / `DualCursorCheck.app`를 사용했다. Retina 창에서 중심 클릭 결과:

```text
click=(350.0,250.0) center_delta=(0.0,0.0) cursor=28.0x40.0 screen=Built-in Retina Display scale=2.0 fullscreen=0
```

등록 상태는 45초간 5초 간격으로 모두 YES였고 원본 복원도 성공했다. CUA 캡처에서 분홍색 커서를 확인하지 못했다. Retina 캡처에는 밝은 포인터 모양이 보이지만 캡처/원격 제어의 포인터 표현일 가능성을 구분하지 못하므로 실제 시스템 커서라고 단정하지 않는다. AppKit의 현재 커서 조회는 계속 28×40이다. 등록 성공을 실제 표시 성공으로 올려 판정하지 않는다.

Retina 창을 닫아 외부 화면 창으로 전환된 상태까지 CUA로 확인했다. 뒤이은 외부 창 클릭은 CUA가 `noWindowsAvailable`을 반환해 추가 좌표 증거를 얻지 못했다. 외부 화면 좌표 증거는 이전 일반 창/전체 화면 시험 결과이며, 이번 전환 전체를 합격 처리하지 않는다.

### 다음 범위

1. 확대 설정 변경 이후 재적용 및 기존 테마 복원 정책을 적용 엔진의 필수 요구로 추가한다. 현재는 재실행으로 등록 복구만 확인했다.
2. 실제 분홍색 포인터 표시 여부는 사용자 관찰 또는 실제 포인터를 포함한다고 확인된 관찰 방법으로 해결해야 한다.
3. 잠자기/깨우기는 사용자가 깨우기를 수행할 수 있는 상태에서 시험한다. 로그아웃은 현재 작업을 종료하므로 이번 자동 시험에 포함하지 않았다.
4. 에디터 연결과 상시 로그인 적용은 아직 진행하지 않았다. Mousecape 코드를 프로젝트에 추가하지 않았고 모든 임시 실행 도구는 제품 밖에 유지했다.

## 확대 설정 변경 후 자동 재등록 시험

임시 `recovery.m`에서 1초마다 적용 직후의 전체 사전(PNG 표현과 메타데이터)을 현재 등록과 비교했다. 읽기 실패 시 중단하고, 등록이 달라지면 최대 4회만 재등록하며, 재등록 후 전체 일치를 확인했다. 45초 후 원본을 복원하고 별도 55초 복원 프로세스도 유지했다. 제품 코드가 아닌 가설 검증용이다.

시스템 설정에서 `1 → 1.3 → 1`로 변경한 결과:

```text
RECOVERY second=7 attempt=1 code=0 exact_applied_match=YES
RECOVERY second=28 attempt=2 code=0 exact_applied_match=YES
monitor_done retries=2 failed=NO
immediate_restore=0 exact_readback=YES
restore=0 exact_readback=YES
```

자동 재등록 없는 이전 시험에서는 크기를 원래 값으로 되돌려도 등록이 사라진 상태였다. 이번에는 두 변경 모두 등록 복구가 확인됐다. 따라서 설정 변경 후 재등록으로 등록 계층을 복구할 수 있다는 가설은 지지된다. 화면 표시 성공, 확대 배율에 따른 시각적 품질, 다른 테마 관리자와의 공존을 입증한 것은 아니다. 제품에서는 단순한 무조건 폴링/덮어쓰기를 채택하지 말고 사용자의 테마 변경 의사와 OS 초기화 이벤트를 구분해야 한다.

잠자기 시험 전 검토에서 반복 횟수 기반 재적용 루프는 잠자기 동안 일시 정지했다가 원본 복원 이후 재적용할 가능성이 있음을 확인했다. `recovery-deadline.m`에 실제 시간 기준 마감과 재등록 직전 마감 확인을 추가했고 빌드만 확인했다. 이 수정본의 실제 잠자기/깨우기 안전성은 아직 검증하지 않았다. 사용자가 직접 Mac을 깨울 수 있는지 비동기 질문을 보냈으며 응답 전에는 잠자기를 실행하지 않는다. Developer ID 검증은 사용자 요청으로 후속으로 연기된 상태다.

## 프로세스 정지 후 재개 시험

사용자는 잠자기 시험도 나중에 진행하도록 지정했다. 실제 잠자기·로그아웃·Developer ID 검증은 모두 후속으로 남긴다.

대신 검증 프로세스만 SIGSTOP으로 10초 정지하고 SIGCONT로 재개했다. Mac 전체는 계속 실행된다. 시험용 적용 마감은 5초, 별도 원본 복원은 8초다.

첫 실행은 실패했다. 마감 시각이 최초 적용 로그 뒤에서 계산돼, 로그 직후 정지되면 재개된 뒤에 새 마감이 설정됐다. 그 결과 이미 원본 복원이 끝났는데 `RECOVERY ... attempt=1`이 실행됐다. 자동 시험의 `retries=0` 단언이 실패해 이를 탐지했다. 최종 원본 복원은 성공했다.

마감 시각 계산을 최초 등록 전에 옮긴 뒤 같은 시험을 다시 실행했다.

```text
apply=0 changed_from_original=YES size=32x32 hotspot=0,0
TEST_PROCESS_STOPPED
restore=0 exact_readback=YES
DEADLINE reached; no further reapply
monitor_done retries=0 failed=NO
immediate_restore=0 exact_readback=YES
PAUSE_DEADLINE_PASS
```

재실행 종료 코드는 0이다. 이 결과는 시험한 정지 위치에서 복원 후 재적용을 차단했다는 근거다. 실제 잠자기, 시스템 시각 변경, 임의의 명령 사이에서 정지되는 모든 경쟁 조건까지 검증한 것은 아니다. 제품 엔진에서는 단일 적용 소유자, 취소/복원 상태 전이와 마지막 마감 확인부터 실제 등록까지의 경쟁 조건을 별도로 설계해야 한다.

임시 파일: `pause-deadline.m`, `pause-test.py`, 수정한 `recovery-deadline.m`. 제품 코드나 Mousecape 의존성은 추가하지 않았다. 커서 원본 복원과 포인터 크기 1 복원은 확인됐다. 실제 분홍색 커서 표시 검증은 여전히 미완료다.

## Apple 공식 문서 및 설치 SDK 대조

CoreGraphics, AppKit, ScreenCaptureKit, ServiceManagement 문서와 현재 Xcode SDK 헤더를 대조했다. 이번 단계는 문서/헤더 검증이며 새 화면 캡처나 커서 변경은 실행하지 않았다.

### 기존 관측 방법의 수정

현재 SDK의 `AppKit.framework/Headers/NSCursor.h` 155–156행은 `currentSystemCursor`를 deprecated 항목에 두고, 향후 macOS에서 항상 nil을 반환할 예정이라고 설명한다. 권장 대안은 ScreenCaptureKit의 SCStreamConfiguration.showsCursor이며 앱 자체 커서 조회에는 currentCursor를 안내한다.

따라서 앞선 28×40 값은 보조 관측으로만 유지한다. deprecated라는 사실 자체가 현재 값이 틀렸다는 증거는 아니지만, 이 값 하나를 실제 렌더링 판정의 기준으로 삼아서는 안 된다. 기존의 등록 성공 및 복원 결과는 유지하고, 실제 표시 성공은 여전히 미확정으로 둔다.

### 프레임워크 역할

- CoreGraphics / Quartz Display Services: 공개 커서 API는 표시/숨김, 위치 이동, 마우스와 커서 위치 연결 등을 제공한다. 살펴본 공식 API 목록과 공개 헤더에서는 시스템 역할별 이미지 테마 교체 API를 찾지 못했다. 기존 실험의 CGSRegisterCursorWithImages, CGSCopyRegisteredCursorImages, CGSRemoveRegisteredCursor는 확인한 CoreGraphics/AppKit 공개 헤더에 선언돼 있지 않다.
- AppKit / NSCursor: init(image:hotSpot:)로 앱이 사용할 커서 이미지를 만들 수 있다. 기존 에디터의 앱 내부 미리보기에는 적합하지만 시스템 테마 변경 기능과 동등하지 않다.
- ScreenCaptureKit: SCStreamConfiguration.showsCursor는 커서를 스트림에 포함한다. SCScreenshotManager의 필터+설정 캡처는 SDK 기준 macOS 14 이상이다. 별도 SCScreenshotConfiguration은 macOS 26 이상이며 showsCursor를 제공한다. 지원 OS에 맞춰 API를 선택해야 한다.
- NSWorkspace: willSleep/didWake 알림은 세션 수명주기 처리에 활용할 수 있다. 실제 잠자기 시험은 사용자 요청으로 연기한다.
- ServiceManagement: SMAppService는 향후 로그인 실행 관리의 후보이며, 커서 이미지 교체 API의 대체물이 아니다. 등록/설치는 이번에 수행하지 않았다.

### 화면 검증 절차 변경

1. 빈 검증 창을 띄우고 그 영역만 대상으로 한다.
2. 같은 정적 장면에서 ScreenCaptureKit의 커서 포함/미포함 결과를 비교한다. 실제 포인터 위치가 영역 안에 있는지와 시각적으로 프레임 사이 이동이 없는지 확인한다.
3. 대조군으로 AppKit의 앱 내부 커스텀 커서를 먼저 캡처해 분홍색 이미지가 캡처 경로에서 확인되는지 검증한다.
4. 앱 내부 커서를 해제한 다음 시스템 적용 엔진으로 같은 그림을 적용하고 동일한 비교를 수행한다. 두 방식이 섞이면 전역 적용의 거짓 양성이 생기므로 분리한다.
5. 외부 1배율과 Retina 2배율 각각에서 이미지 색상/형태, 논리 좌표 대비 픽셀 배율, 클릭 핫스폿을 확인한다.
6. 확대 변경 이후 자동 재등록과 실제 화면 재표시를 함께 검증한다. 등록 데이터 복구만으로 화면 복구를 판정하지 않는다.

ScreenCaptureKit 캡처는 아직 실행하지 않았다. 캡처 실행 시 화면 기록 접근 승인이 필요할 수 있으며, 이는 검증 도구의 요구 조건으로 구분한다. 이 접근 권한이 커서 적용 엔진 자체에 반드시 필요하다고 추론하지 않는다.

공식 문서:

- https://developer.apple.com/documentation/coregraphics
- https://developer.apple.com/documentation/coregraphics/quartz-display-services
- https://developer.apple.com/library/archive/documentation/GraphicsImaging/Conceptual/QuartzDisplayServicesConceptual/Articles/MouseCursor.html
- https://developer.apple.com/documentation/appkit/nscursor/currentsystem
- https://developer.apple.com/documentation/appkit/nscursor/init(image:hotspot:)
- https://developer.apple.com/documentation/screencapturekit/scstreamconfiguration/showscursor
- https://developer.apple.com/documentation/screencapturekit/scscreenshotmanager
- https://developer.apple.com/documentation/servicemanagement/smappservice

Apple 공개 API 문서를 바탕으로 캡처/미리보기 검증을 구성하는 것과 Mousecape 소스의 이용 권리는 별개다. Mousecape 의존성은 여전히 추가하지 않았다. 직접 배포 및 비공개 적용 엔진의 호환성 과제는 그대로 남는다.

## ScreenCaptureKit 검증 앱 준비

`/private/tmp/unfold-cursor-validation/ScreenCursorCheck.app`와 `ScreenCursorCheck.m`을 만들었다. 이 도구는 공개 AppKit/ScreenCaptureKit API만 사용한다. 빌드와 ad-hoc Hardened Runtime 서명 검증이 성공했다. Developer ID 서명 검증과는 구분한다.

- 앱 내부 분홍색 커서 켜기/끄기: NSCursor 생성 및 검증 뷰의 cursor rect만 변경.
- 캡처: SCContentFilter(display:includingWindows:)에 해당 검증 창 하나만 포함하며 sourceRect를 해당 창 영역으로 제한. 오디오 및 클릭 원 표시는 끔.
- showsCursor=false/true 두 PNG와 배율·앱 커서 모드 메타데이터를 임시 폴더의 실행별 UUID 디렉터리에 저장.
- 화면 기록 권한 요청 버튼과 캡처 버튼을 분리. 권한이 없으면 캡처를 시작하지 않음.

초기 CGPreflightScreenCaptureAccess 결과는 false였다. UI에서도 권한 없이 캡처 버튼을 눌렀을 때 캡처하지 않고 권한 필요 문구가 표시됐다. 사용자가 화면 기록 권한 요청·허용을 승인했으며, 시스템 설정에서 해당 검증 앱을 추가하는 단계로 진행했다. 추가 시 macOS가 Touch ID/시스템 암호 인증을 요구했다. 이 시점의 실제 캡처는 아직 미실행이며 시스템 인증은 사용자에게 직접 요청했다. 다른 앱의 화면 기록 권한은 변경하지 않았다.
