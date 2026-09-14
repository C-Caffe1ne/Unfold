# Unfold vs 유사 서비스 비교 분석

2026-09-14 기준 · Unfold 저장소(`docs/business-model.md`, `docs/product-direction.md`, `docs/mvp.md`, `docs/development-plan.md`, `docs/pet-resources.md`, `docs/pet-packs.md`)와 각 서비스 공식 페이지·GitHub·SteamDB를 조사해 정리했다. 경쟁사 매출·판매량은 대부분 비공개이며, 아래 수치는 공개된 리뷰 수·SteamDB·서드파티 추정 사이트를 근거로 한 **참고용 추정치**다. Unfold 문서가 이미 지켜온 원칙대로, 확인되지 않은 값은 "추정"으로 표시했다.

---

## 1. Unfold 요약 (비교의 기준점)

| 항목 | 내용 |
|---|---|
| 제품 | Windows 트레이 / macOS 메뉴바에서 동작하는 스트레칭 리마인더 + 데스크톱 펫(고양이 Mochi) |
| 핵심 루프 | 활동 감지 타이머 → 휴식 초대 창 → 루틴 선택·시작 → Mochi가 기지개 → 완료 확인 → 로컬 기록 |
| 기술 스택 | C#/.NET 10, Avalonia UI, SkiaSharp, 로컬 JSON/CSV 저장. Windows/macOS 크로스플랫폼. Swift/Xcode·Piskel 웹 런타임은 폐기 |
| 리소스 | 1인/소수 개발 체제로 추정되는 자체 프로젝트. 기본 캐릭터 Mochi 1종(권리 미확인), 설치 후보 캐릭터 "보리" 1종(원본·프롬프트·SHA-256 보존, 상업적 권리 검토 대기) |
| 배포 포맷 | `.unfoldpet` ZIP 팩(로컬 파일 설치·버전 갱신·재설치). 중앙 카탈로그·계정 기반 구매 복원·DRM 없음 |
| BM 가설 | Free(기본 루틴+펫 1종) + Plus 개인화 일회성 결제($14.99~$19.99 가설) + 캐릭터팩 건별 판매($4.99~$7.99 가설) |
| 마케팅 계획 | 실사용 영상, 개발·디자인 커뮤니티, 소개 페이지. 유료 광고는 설치·7일 사용 지표 확보 전까지 보류 |
| 현재 상태 | 결제·구매 권한 확인 미구현. Free/Plus 권한 구분도 아직 개발 빌드에는 미적용. 전 항목이 검증 전 가설 |

---

## 2. 비교 대상 서비스 개요

| 서비스 | 카테고리 | 플랫폼 |
|---|---|---|
| [Stretchly](https://github.com/hovancik/stretchly) | 순수 휴식 리마인더 (오픈소스) | Win/Mac/Linux |
| [BreakTimer](https://breaktimer.app/) | 순수 휴식 리마인더 (오픈소스) | Win/Mac/Linux |
| [Stretch](https://stretchapp.in/) | 순수 스트레칭 리마인더 (오픈소스) | Win/Mac |
| [Time Out](https://dejal.com/timeout/) | 휴식 리마인더 + 후원형 결제 | Mac 전용 |
| [Pausitive](https://pausitive.app/pricing) | 휴식 리마인더 + 웰니스 대시보드 | Mac 전용 |
| [Viraam](https://www.viraam.app/) | 휴식 리마인더 + 웰니스/생산성 통합 | Mac 전용 |
| [MicroJoyz](https://microjoyz.com/overview) | 데스크톱 펫 + AI 동반자 | Mac 전용(Win 검토 중) |
| [Desktop Mate](https://store.steampowered.com/app/3301060/Desktop_Mate/) (+[DLC](https://store.steampowered.com/dlc/3301060/Desktop_Mate/)) | 데스크톱 펫 + 라이선스 캐릭터 판매 | Windows(Steam) |

앞의 세 개(Stretchly/BreakTimer/Stretch)는 Unfold의 "무료 핵심 경험" 기준점, 뒤의 세 개(Time Out/Pausitive/Viraam)는 "유료 전환 방식" 참고, 마지막 두 개(MicroJoyz/Desktop Mate)는 Unfold와 가장 유사한 "펫+캐릭터 판매" 카테고리다.

---

## 3. BM(수익 모델) 구조 비교

| 서비스 | 기본 제공 | 유료 상품 | 가격 | 결제 방식 |
|---|---|---|---|---|
| **Unfold** | Free: Mochi, 기본 루틴+내 루틴 1개, 로컬 기록 | Plus(개인화), 캐릭터팩 | Plus $14.99~19.99(가설) · 팩 $4.99~7.99(가설) | 일회성(가설), 미구현 |
| Stretchly | 전 기능 무료 | 없음 (후원만) | - | GitHub Sponsors/Patreon/암호화폐/PayPal 후원 |
| BreakTimer | 전 기능 무료 | 없음 | - | 없음 (GPLv3) |
| Stretch | 전 기능 무료 | 없음 | - | 없음, 서버·계정 자체가 없음(MIT) |
| Time Out | 기본 앱 무료 | 후원(콘텐츠 차등 없음) | 3개월 $4.99 · 6개월 $9.99 · 12개월 $19.99 · 평생 $59.99 | 인앱 구매, 자동갱신 아님 |
| Pausitive | 3일 체험 | 건강 점수·히트맵·가이드 운동 전체 잠금 | 1기기 $25 · 2기기 $38(일회성) | Polar.sh 결제, "평생 사용" |
| Viraam | 14일 체험 | 6종 리마인더+웰니스 넛지 전체 | 월 $2.99 · 연 $19.99 · 평생 $49.99 | 구독+평생 라이선스 혼합 |
| MicroJoyz | 3일 체험 | AI 대화 등 MicroJoyz+ | 기본 $9.99(일회성) + Plus 월 $7.69 | 일회성과 구독 이중 구조 |
| Desktop Mate | 본체 무료(Steam) | 캐릭터별 DLC | DLC 1종 $14.99(라이선스 캐릭터는 2,200엔) | Steam 결제, 카탈로그 236종 확장형 |

**시사점.** 무료 리마인더 3종(Stretchly/BreakTimer/Stretch)이 이미 "정확한 타이머+미루기"만으로는 무료 대안이 충분하다는 걸 보여준다. Unfold 자체 문서의 결론과 일치한다. 유료 전환은 크게 두 갈래다: (1) 웰니스 데이터/분석 기능으로 과금(Pausitive, Viraam), (2) 캐릭터 콘텐츠 확장으로 과금(MicroJoyz, Desktop Mate). Unfold의 Plus는 (1)에 가깝고 캐릭터팩은 (2)에 가까운 **두 갈래를 동시에 가설로 두고 있다.**

---

## 4. 마케팅 방식 비교

| 서비스 | 주요 채널 | 핵심 메시지 | 특징 |
|---|---|---|---|
| **Unfold(계획)** | 실사용 영상, 개발/디자인 커뮤니티, 소개 페이지 | "Mochi와 잠깐 쉬고, 내 리듬으로 돌아오세요" | 유료 광고 보류, 채널별 유입 링크로 실제 재실행률 비교 예정 |
| Stretchly | GitHub(스타 6.5k, 포크 555), Weblate 번역 커뮤니티, Discord | 오픈소스 신뢰·투명성 | 패키지 매니저(Homebrew/Chocolatey/winget/Flathub/Snap) 배포로 발견성 확보 |
| BreakTimer | GitHub 저장소, 공식 웹사이트 | "Smart break reminders for healthier focus" | GitHub 스타/다운로드 배지로 사회적 증거 표시 |
| Stretch | 미니멀 웹사이트, Homebrew, GitHub | "설정 후 잊혀지는 경험", 프라이버시 우선(무계정·무원격서버) | 개발자 커뮤니티向 기술적 신뢰 메시징 |
| Time Out | Mac App Store, Setapp 구독 번들 포함, 언론 리뷰(Macworld·Lifehacker) | 전문가 추천 인용 | 오래된 앱 특유의 리뷰·커뮤니티 축적, Setapp 편입으로 발견성 확보 |
| Pausitive | 공식 랜딩 페이지 카피 중심 | "하루 10시간 화면, 당신 눈은 그렇게 만들어지지 않았다" | 공포소구형 문제 제기 + "평생 결제" 대비 |
| Viraam | 공식 사이트, 타겟 직군 명시 | "Gentle Pauses for Well-being™" | 개발자·디자이너·작가 등 니치 타겟 명시, 산스크리트어 네이밍으로 브랜드 차별화 |
| MicroJoyz | 데모 영상 6개, 감정적 카피 | "데스크톱에 기쁨을 불러일으키는" | 기능 시연 영상 다수로 진입장벽 낮춤, AI 대화를 차별화 축으로 강조 |
| Desktop Mate | Steam 페이지, 인플루언서/미디어 리뷰(GIGAZINE 등), **IP 협업(산리오·Vocaloid·동방Project)** | 캐릭터 팬덤 자체가 마케팅 | 라이선스 IP 콜라보로 기존 팬덤을 그대로 유입시키는 것이 핵심 성장 동력 |

**시사점.** Unfold와 같은 "1인/소규모 오리지널 캐릭터" 프로젝트는 Desktop Mate식 라이선스 IP 마케팅을 쓸 수 없다. 대신 Stretchly/Stretch처럼 개발자 커뮤니티(GitHub, 기술 커뮤니티) 기반 신뢰 구축과, Viraam처럼 니치 타겟 직군을 명시한 메시징이 Unfold 규모에 더 현실적인 참고선이다.

---

## 5. 예상 수익 비교 (참고용 추정치)

공개된 매출 데이터가 있는 서비스는 거의 없다. 아래는 리뷰 수·SteamDB·서드파티 추정 사이트로 유추한 **범위 추정**이며, 실제 매출을 증명하지 않는다.

| 서비스 | 근거 | 추정 |
|---|---|---|
| Desktop Mate 본체 | 무료(Steam), 리뷰 약 9,272개(긍정 67%), 동시 플레이어 약 2,700명대 | 리뷰:구매자 비율(업계 통상 1:30~50)로 역산하면 누적 이용자 수십만 명 규모로 **추정**. 본체 매출은 0(무료)이며 수익은 전적으로 DLC 의존 |
| Desktop Mate DLC | 캐릭터당 $14.99(라이선스 IP는 2,200엔), **총 236개 카탈로그** | 카탈로그 규모 자체가 반복 구매를 유도하는 구조. 개별 DLC 판매량은 비공개라 매출 총액은 **미확인**하지만, "장기 다품종 소액과금"이 캐릭터 판매형 BM의 실질 수익원임을 시사 |
| 소규모 유료 데스크톱 펫 사례(games-stats.com "Desktop Pet", $3.99, Windows 전용) | 리뷰 19개, 8/10 평점, 팔로워 184명 | 추정 매출 **약 $1,500** — 마케팅·IP 없이 출시만 했을 때의 하한선 참고 사례 |
| Time Out | 20년 이상 운영, Setapp 편입, App Store 순위 미공개 | 매출 미확인. 다만 후원형 모델의 장기 생존 사례로 참고 가능 |
| Pausitive | $25 일회성, 3일 체험 | 매출 미확인. 일회성 결제 + 명확한 기능 락(건강 대시보드)의 소규모 SaaS 사례 |
| Viraam | 구독+평생 혼합, 14일 체험 | 매출 미확인 |
| MicroJoyz | 일회성 $9.99 + 구독 $7.69/월 | 매출 미확인. 이중 과금 구조 자체가 참고할 실험 |

**Unfold에 주는 시사점.** 캐릭터 판매형 BM에서 실제 유의미한 매출은 "캐릭터 하나"가 아니라 "카탈로그 크기"에서 나올 가능성이 높다(Desktop Mate 236종 vs 소규모 사례 1종의 매출 격차). Unfold는 현재 판매 가능한 오리지널 캐릭터가 사실상 0종(Mochi는 권리 미확인, 보리는 상업 검토 대기)이므로, 캐릭터팩 매출 가설을 검증하려면 카탈로그 확장 계획이 선행돼야 한다. 반대로 Plus(개인화) 매출은 카탈로그 크기와 무관해 상대적으로 검증 부담이 낮다.

---

## 6. 기술 스택 비교

| 서비스 | 언어/프레임워크 | 플랫폼 | 배포 형태 | 라이선스 |
|---|---|---|---|---|
| **Unfold** | C#/.NET 10, Avalonia, SkiaSharp | Windows/macOS | 포터블 zip(Win), 앱 번들(Mac, ad hoc 서명) | 비공개(추정) |
| Stretchly | Electron(Node.js/JS) | Win/Mac/Linux | Homebrew/Chocolatey/winget/MS Store/Flathub/Snap/apt | BSD-2-Clause(오픈소스) |
| BreakTimer | 크로스플랫폼 데스크톱(프레임워크 비공개, Electron 계열 추정) | Win/Mac/Linux | GitHub 직접 배포(exe/dmg/snap/deb/rpm/AppImage) | GPLv3(오픈소스) |
| Stretch | Electron 30 | macOS 12+/Windows 10-11 | Homebrew, GitHub DMG/EXE | MIT(오픈소스) |
| Time Out | macOS 네이티브(구체 언어 비공개, HTML/CSS/JS 테마 + AppleScript/Automator 연동) | macOS 전용(현재 macOS 26+ 요구) | 웹 직접 배포, Mac App Store, Setapp | 비공개(상용) |
| Pausitive | 비공개("네이티브 앱 성능" 표기) | macOS 전용 | 웹 직접 배포 | 비공개(상용) |
| Viraam | **Swift 네이티브**, 앱 크기 ~12MB | macOS 14+ (Apple Silicon/Intel) | 웹 직접 배포 | 비공개(상용) |
| MicroJoyz | 비공개 | macOS 전용(Windows 검토 중) | 웹 직접 배포 | 비공개(상용) |
| Desktop Mate | 비공개(Steam 게임, Unity 계열로 추정) | Windows(Steam) | Steam | 비공개(상용) |

**시사점.** Unfold의 C#/Avalonia 조합은 무료 오픈소스 그룹(Electron 기반)과도, 유료 네이티브 그룹(Swift/macOS 전용)과도 다른 제3의 길이다. Electron 대비 설치 용량·메모리 이점이 있을 수 있으나 검증되지 않았고, Swift 네이티브 그룹(Viraam 12MB) 대비로는 무거울 가능성이 있다. Windows/macOS 동시 지원이라는 점에서는 Electron 그룹과 같은 포지션이지만, macOS 전용 유료 그룹(Time Out/Pausitive/Viraam/MicroJoyz)이 시사하듯 "macOS 단일 플랫폼 + 확실한 유료 기능"으로 좁혀 가는 접근도 참고할 만하다.

---

## 7. 리소스(팀·제작 자산) 비교

| 서비스 | 팀 규모(추정) | 캐릭터/콘텐츠 자산 | 비고 |
|---|---|---|---|
| **Unfold** | 1인/소규모(추정) | 자체 캐릭터 1종(Mochi, 권리 미확인) + 후보 1종(보리, 상업 검토 대기) | 384×384 원본, 5종 반응 애니메이션(idle/attention/stretch/celebrate/click) 제작 기준 문서화됨. `.unfoldpet` 로컬 팩 파이프라인과 자동 검사 스크립트(`validate-character-assets.sh`) 보유 |
| Stretchly | 오픈소스 기여자 100명+ (비상근) | 콘텐츠 자산 없음(순수 기능 앱) | 번역까지 커뮤니티 분산 |
| BreakTimer | 소규모/개인(추정) | 없음 | - |
| Stretch | 개인(추정, 저장소 소유자 1인) | 없음 | "no accounts, no telemetry"로 서버 리소스 자체가 없음 |
| Time Out | 1인 개발사(Dejal, 20년+ 운영) | 없음 | 장기 유지보수가 핵심 자산 |
| Pausitive | 소규모(추정) | 건강 데이터 대시보드·히트맵 로직 | 콘텐츠보다 분석 기능이 핵심 자산 |
| Viraam | 소규모/개인(추정, Swift 네이티브 1인 개발 특징) | 없음(리마인더+넛지 로직) | 경량 네이티브 앱 자체가 자산 |
| MicroJoyz | 소규모(추정) | AI 대화 백엔드, 아케이드 게임, 캐릭터 애니메이션 | AI API 연동 비용이 지속 발생하는 구조로 추정 |
| Desktop Mate | 중소 스튜디오(추정) + **산리오/Vocaloid 등 라이선서** | **캐릭터 236종**(자체 제작 + 라이선스) | 라이선스 계약·로열티 비용이 원가 구조에 포함될 것으로 추정. 캐릭터 수 자체가 스튜디오급 제작 리소스를 필요로 함 |

**시사점.** Unfold는 현재 1인/소규모 리소스로 캐릭터 콘텐츠형 BM(Desktop Mate식)을 목표로 하기엔 제작 캐패시티 격차가 크다. 반면 분석/웰니스형 BM(Pausitive, Viraam)은 콘텐츠 제작 부담이 적고 코드 자산만으로 확장 가능해 현재 팀 규모에 더 맞는 방향일 수 있다. Unfold 문서가 이미 Plus(개인화)를 "주 수익 가설", 캐릭터팩을 "보조 수익 가설"로 둔 것은 이 리소스 격차와 일치한다.

---

## 8. 종합 비교 요약

| 축 | Unfold의 현재 위치 | 근접 경쟁군 |
|---|---|---|
| BM | 웰니스 과금 + 캐릭터 과금 이중 가설, 둘 다 미구현 | Pausitive/Viraam(웰니스 단일) vs Desktop Mate/MicroJoyz(캐릭터·AI 단일) — 대부분 **단일 축**으로 좁혀 검증 중 |
| 마케팅 | 커뮤니티·영상 중심, 저비용, 유료광고 보류 | 무료 그룹은 오픈소스 커뮤니티, 유료 그룹은 전문가 리뷰/IP 콜라보 — Unfold는 아직 어느 쪽 자산도 없음 |
| 예상 수익 | 검증 전(0) | 캐릭터형은 카탈로그 크기에 강하게 비례(Desktop Mate 236종 vs 소규모 1종 사례의 격차), 웰니스형은 매출 비공개지만 장기 생존 사례 존재 |
| 기술스택 | C#/Avalonia(제3의 길) | Electron(무료 그룹) vs Swift 네이티브(유료 macOS 그룹)로 양극화 |
| 리소스 | 1인/소규모, 캐릭터 자산 사실상 0종 | 캐릭터형 경쟁자(Desktop Mate)는 스튜디오+라이선서 규모, 웰니스형 경쟁자는 Unfold와 비슷한 소규모 |

**결론.** Unfold의 기존 문서가 내린 판단 — "무료 핵심 경험으로 시작하고 Plus(개인화)를 주 가설, 캐릭터팩을 보조 가설로 둔다" — 은 이번 조사 결과와도 부합한다. 다만 캐릭터팩 가설을 실제로 키우려면 Desktop Mate 사례가 보여주듯 카탈로그 규모가 매출의 핵심 변수이므로, 현재 리소스로는 장기 과제로 남겨두고 Plus 검증에 우선순위를 두는 편이 근거가 더 명확하다. 마케팅은 라이선스 IP나 언론 리뷰 자산이 없는 만큼, Stretch/Stretchly처럼 개발자 커뮤니티 신뢰 축적과 Viraam처럼 니치 타겟 명시가 규모에 맞는 접근이다.

---

## 9. 플랫폼 의존성 비교 (독립 실행형 vs 호스트 의존형)

Unfold는 OS 트레이/메뉴바에 상주하는 독립 실행 파일이다. 반면 비교 대상 중 일부는 다른 플랫폼(브라우저·Steam 클라이언트) 위에서만 동작한다. 이 축을 별도로 확인했다.

| 서비스 | 실행 방식 | 호스트 의존성 | 확인 상태 |
|---|---|---|---|
| **Unfold** | OS 네이티브 트레이/메뉴바 앱 | 없음(독립) | FACT |
| Stretchly / BreakTimer / Stretch | Electron 독립 실행 파일 | 없음(런타임 내장, 별도 Chrome 불필요) | FACT |
| Time Out / Pausitive / Viraam / MicroJoyz | macOS 네이티브 앱 | 없음(독립) | FACT |
| **Shimeji(현재 주력, shimejis.xyz)** | **크롬 확장 프로그램** | **크롬 실행 중 + 브라우저 창/탭 내부로 표시 범위 한정** | FACT — 공식 FAQ가 "Shimeji Browser Extension"만 다루며 모바일 미지원이라고 명시 |
| Shimeji-ee(2002년 원조 버전) | Java 데스크톱 앱 | 독립 실행(단, Java 런타임 설치 필요) | FACT |
| **Desktop Mate** | **Steam 게임/소프트웨어** | **Steam 클라이언트 상시 실행 필요.** Steam 친구 목록에 "Playing Desktop Mate"로 노출되며, 이를 끄고 싶다는 사용자 불만과 "Steam 없이 실행하는 법" 커뮤니티 가이드가 존재 | FACT — [Steam 커뮤니티 포럼](https://steamcommunity.com/app/3301060/discussions/1/594012019028401725/) |
| Custom Desktop Pet / Screen Pet Engine | Steam 배급 | Steam 클라이언트 의존 가능성 높음 | 추정(Steam 유통 방식에서 유추, 개별 미확인) |

### 왜 이게 단순한 "유통 경로" 차이가 아닌가

브라우저 확장 프로그램은 브라우저 창 내부에서만 렌더링할 수 있는 샌드박스 제약을 가진다. 즉 IDE, Figma 데스크톱 앱, 터미널, 오피스 프로그램 등 **브라우저 밖에서 작업할 때는 펫이 화면에 나타나지 않는다.** 이는 배포 채널의 문제가 아니라 아키텍처상 구조적 한계다. Unfold는 OS 창으로 떠 있어 어떤 앱을 쓰든 화면에 머문다 — 이는 Unfold의 핵심 가치 제안("작업 중 화면에 머무는 동료")과 직접 연결된다.

Desktop Mate 계열은 Steam 클라이언트 상시 실행, 로그인 상태 노출, Steam 30% 플랫폼 수수료(가격 정책에 영향)라는 부담을 진다. 또한 다수의 회사·기관 PC는 정책상 게임 클라이언트(Steam)를 차단하는 경우가 흔해, 업무 환경 배포에 불리하다.

### 이것이 실제 경쟁력 요소인가 — 범위를 좁혀서 판단

- **이 우위는 "펫+캐릭터 판매" 그룹(Desktop Mate 등)과 Shimeji의 현재 주력 제품(크롬 확장)에 대해서만 성립한다.** 순수 리마인더 3종과 웰니스 3종은 애초에 Unfold와 마찬가지로 독립 실행형이라 이 축에서는 우열이 없다.
- **"독립 실행" 자체는 Unfold만의 고유 특성이 아니다.** MicroJoyz(macOS 네이티브)도, 2002년 원조 Shimeji-ee(Java 앱)도 이미 독립 실행형이다. Unfold의 실질적 차별화는 **"독립 실행 + Windows/macOS 크로스플랫폼"의 조합**이다(MicroJoyz는 macOS 전용).
- **다만 Unfold의 타겟 고객(오래 일하는 개발자·디자이너, 종종 회사 PC 사용)에게는 이 축이 실제로 의미 있다.** 게임 클라이언트가 사내 정책상 제한되는 업무 환경이 흔하고, "Playing Desktop Mate" 같은 소셜 상태 노출은 업무용 도구로서 부자연스럽다. Unfold는 이런 마찰이 구조적으로 없다.

**판단.** "펫+캐릭터 판매" 서브카테고리 안에서는 진짜 차별화 요소다. 다만 시장 전체(순수 리마인더·웰니스 그룹)에서는 이미 표준이라 우위가 아니며, Desktop Mate의 근본 강점(§5의 카탈로그 236종·라이선스 IP)을 상쇄할 만큼 크지도 않다. "Steam도 Chrome도 필요 없이, 어떤 앱을 쓰든 화면에 머무는 동료"는 마케팅 메시지로 쓸 수 있는 근거 있는 차별점이지만, 범위를 정확히 좁혀서(펫 카테고리 한정) 사용해야 과장이 되지 않는다.

---

## Sources

- [Stretchly GitHub](https://github.com/hovancik/stretchly)
- [Stretchly 다운로드](https://hovancik.net/stretchly/downloads/)
- [BreakTimer](https://breaktimer.app/)
- [Stretch](https://stretchapp.in/)
- [Time Out](https://dejal.com/timeout/)
- [Pausitive 가격](https://pausitive.app/pricing)
- [Viraam](https://www.viraam.app/)
- [MicroJoyz](https://microjoyz.com/overview)
- [Desktop Mate (Steam)](https://store.steampowered.com/app/3301060/Desktop_Mate/)
- [Desktop Mate DLC 목록](https://store.steampowered.com/dlc/3301060/Desktop_Mate/)
- [Desktop Mate · SteamDB](https://steamdb.info/app/3301060/)
- [Desktop Mate 리뷰 · Steambase](https://steambase.io/games/desktop-mate/reviews)
- [Desktop Pet 매출 추정 · games-stats.com](https://games-stats.com/steam/game/desktop-pet/)
- [Desktop Mate 산리오 콜라보 기사 · GIGAZINE](https://gigazine.net/gsc_news/en/20260413-desktop-mate-sanrio/)

- [Shimeji Browser Extension FAQ](https://shimejis.xyz/faq)
- [Shimeji-ee 다운로드 정보](https://shimeji-ee.en.download.it/)
- [Desktop Mate Steam 상시 실행 관련 포럼](https://steamcommunity.com/app/3301060/discussions/1/594012019028401725/)
