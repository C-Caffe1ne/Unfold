# Revenue-First Solo Product Validation OS
## AI와 함께 제품 아이디어를 검증하고, 폐기하고, 실제 매출까지 연결하기 위한 공통 운영 문서

> Version: 1.0  
> 대상: 1~3인 소규모 팀 / 1인 개발자 / 바이브 코더 / 인디 개발자  
> 적용 범위: Web App, Mobile App, macOS/Windows Desktop App, Chrome Extension, SaaS, Utility, AI Tool  
> 목적: **“만들 수 있는 제품”이 아니라 “실제로 돈을 벌 가능성이 있는 제품”을 빠르게 선별한다.**

---

# 0. 이 문서의 역할

이 문서는 아이디어를 칭찬하기 위한 문서가 아니다.

AI와 팀원이 다음 일을 동일한 순서와 기준으로 수행하도록 만드는 **의사결정 운영체계**다.

```text
Observation
↓
Problem
↓
Root Cause
↓
Existing Behavior
↓
Market Forensics
↓
Alternatives
↓
Positioning
↓
Critical Assumptions
↓
Cheapest Test
↓
Willingness To Pay
↓
Economics
↓
Distribution
↓
Build / Pivot / Kill
```

핵심 질문은 이것이다.

> **“이 아이디어가 좋아 보이는가?”가 아니라,  
> “이 제품이 돈을 벌기 위해 참이어야 하는 조건은 무엇이며, 그 조건을 뒷받침하는 실제 증거가 있는가?”**

---

# 1. 기본 원칙

## 1.1 아이디어를 증명하려 하지 않는다

AI와 팀은 아이디어를 지지하는 증거만 찾지 않는다.

반드시 동시에 찾는다.

- 성공 사례
- 실패 사례
- 낮은 사용량의 제품
- 서비스 종료 사례
- 무료 대체재
- 오픈소스 대체재
- 사용자가 아무것도 하지 않는 경우
- 같은 문제를 더 싸게 해결하는 방법

목표는 **확증편향을 줄이는 것**이다.

---

## 1.2 개발은 검증 수단이지 검증 그 자체가 아니다

다음은 증거가 아니다.

- 기능을 구현할 수 있다.
- 앱이 예쁘다.
- AI가 좋은 아이디어라고 평가한다.
- 주변 사람이 좋아 보인다고 말한다.
- Product Hunt에서 upvote를 받았다.
- SNS에서 조회수가 높다.

더 강한 증거는 다음이다.

```text
실제 반복 결제
↑
실제 결제
↑
Checkout 시작
↑
반복 사용
↑
현재 돈이나 시간을 쓰는 workaround
↑
제품 설치 / 가입 / 저장
↑
검색 행동
↑
Bookmark / Like / Upvote
↑
의견
↑
창업자의 직감
```

가능하면 더 아래 단계의 의견을 더 위 단계의 행동으로 바꾼다.

---

## 1.3 인터뷰를 못 해도 검증은 가능하다

사용자 인터뷰가 어렵다면 **시장이 남긴 흔적을 조사한다.**

조사 대상:

- Reddit
- Hacker News
- App Store / Google Play 리뷰
- Chrome Web Store 리뷰
- Mac App Store 리뷰
- Product Hunt
- GitHub
- YouTube 댓글
- Discord/Forum의 공개 게시물
- 경쟁사 pricing page
- 경쟁사 changelog
- 경쟁사 종료 공지
- Founder retrospective
- Indie Hackers
- 검색 결과
- “X alternative”
- “X vs Y”
- “why did X shut down”
- “0 revenue”
- “no users”
- “failed app”
- “I built X but…”

이를 **Market Forensics — 시장 포렌식**이라고 부른다.

과거 창업자들이 이미 실행한 실험을 최대한 재사용한다.

---

# 2. 팀의 현실 조건부터 정의한다

아이디어 분석 전에 프로젝트 조건을 먼저 작성한다.

```yaml
team:
  people:
  available_hours_per_week:
  primary_skills:
  weak_skills:

financial:
  monthly_revenue_goal:
  maximum_initial_budget:
  maximum_monthly_operating_cost:
  minimum_acceptable_margin:

time:
  desired_first_sale_date:
  maximum_validation_period:
  maximum_mvp_build_period:

constraints:
  can_do_interviews:
  can_run_paid_ads:
  can_build_backend:
  can_support_ios:
  can_support_android:
  can_support_windows:
  can_support_macos:
  legal_or_platform_constraints:
```

이 조건은 제품 판단에 반드시 반영한다.

예:

> 시장은 크지만 첫 매출까지 18개월이 걸리는 제품

은 자본이 없는 팀에게 좋은 사업이 아닐 수 있다.

---

# 3. 월 목표 매출에서 역산한다

시장규모보다 먼저 **필요한 고객 수**를 계산한다.

## One-time Product

```text
필요 월 신규 구매자
=
월 목표 순수익
÷
건당 순수익
```

## Subscription Product

```text
필요 유료 사용자 수
=
월 목표 순수익
÷
사용자당 월 순수익
```

여기서 **구매전환율을 임의로 가정하지 않는다.**

실제 판매 데이터가 생긴 이후에만:

```text
필요 유입량
=
필요 구매자 수
÷
실제 관측 구매율
```

을 계산한다.

---

# 4. 제품 검증의 14 Gates

---

# GATE 0 — OBSERVATION
## 우리가 실제로 무엇을 이상하게 느꼈는가?

솔루션을 제거한다.

### 나쁜 표현

> macOS 메뉴바 앱을 만들자.

### 좋은 표현

> 작업 중 여러 번 반복해서 확인해야 하는 정보 때문에 브라우저와 앱을 계속 왕복한다.

다음 형식으로 적는다.

```yaml
observation:
  situation:
  trigger:
  observed_friction:
  current_behavior:
  consequence:
```

### AI의 역할

AI는 아이디어 설명에서:

- Solution
- Problem
- Assumption
- Observation

을 분리한다.

솔루션으로 문제를 역추론하지 않는다.

---

# GATE 1 — PROBLEM EXISTENCE
## 이 문제가 실제로 존재하는가?

질문:

> 나에게만 발생하는 문제인가?

인터뷰가 없다면 독립적인 외부 증거를 찾는다.

### 찾아야 할 것

- 같은 문제를 설명하는 글
- 같은 질문
- workaround
- 기존 앱
- 무료 스크립트
- GitHub repository
- 유료 대체재
- 반복되는 불만 리뷰

### 좋은 증거

여러 독립된 출처에서 동일한 문제가 **다른 표현으로 반복**된다.

### 위험 신호

- 창업자 자신 외에는 문제를 찾기 어렵다.
- 문제를 검색하는 언어 자체가 없다.
- 현재 아무도 시간이나 돈을 쓰지 않는다.
- 문제를 설명하면 이해하지만 스스로 문제라고 말하지 않는다.

### 출력

```yaml
problem_evidence:
  supporting:
  contradicting:
  current_workarounds:
  confidence:
```

confidence는 임의 점수가 아니라 다음처럼 쓴다.

- Weak
- Mixed
- Moderate
- Strong

그리고 반드시 이유를 적는다.

---

# GATE 2 — JOB / PAIN
## 사용자는 실제로 무엇을 하려고 하는가?

Jobs To Be Done 방식으로 본다.

```yaml
job:
  situation:
  trigger:
  desired_progress:
  functional_job:
  emotional_job:
  social_job:
```

문제의 강도는 다음 관점에서 본다.

### Frequency

얼마나 반복되는가?

### Severity

발생했을 때 얼마나 불편한가?

### Urgency

즉시 해결해야 하는가?

### Existing Spend

현재 돈이나 시간을 얼마나 쓰는가?

### Consequence

해결하지 않으면 무엇을 잃는가?

숫자를 억지로 매기지 않는다.

대신 실제 사례와 행동을 적는다.

---

# GATE 3 — ROOT CAUSE
## 보이는 문제가 진짜 원인인가?

First Principles + 5 Whys를 사용한다.

예:

```text
사용자가 앱을 자주 잊어버린다.
↓ 왜?
알림이 부족하다.
↓ 왜?
알림의 문제가 아니라 작업 상태를 기억하기 어렵다.
↓ 왜?
작업 context가 여러 앱에 분산되어 있다.
```

다음 질문을 반드시 한다.

1. 지금 해결하려는 것은 원인인가 증상인가?
2. 이 문제를 구성하는 최소 요소는 무엇인가?
3. 우리가 당연하다고 생각한 전제는 무엇인가?
4. 플랫폼이나 AI가 발전하면 이 문제가 사라지는가?
5. 사용자가 행동을 바꾸면 제품이 없어도 해결되는가?
6. 더 단순한 해결책이 존재하는가?

### 출력

```yaml
root_cause:
  surface_problem:
  underlying_causes:
  assumptions:
  existential_risks:
  simpler_solution:
```

---

# GATE 4 — CURRENT BEHAVIOR
## 사용자는 지금 어떻게 해결하고 있는가?

경쟁자는 회사만이 아니다.

항상 다음을 포함한다.

```text
Direct competitor
Adjacent competitor
Manual workaround
General-purpose tool
Spreadsheet / Notes
AI / ChatGPT / Claude
Search
Friend / Expert
Do nothing
```

중요한 질문:

> **우리 제품이 없다면 사용자는 무엇을 할 것인가?**

현재 행동이 강할수록 문제의 실재성도 강하다.

---

# GATE 5 — MARKET FORENSICS
## 이미 우리 대신 실험한 사람들을 조사한다

성공 제품만 조사하지 않는다.

최소 다음 종류를 찾는다.

```text
1. 명확한 성공
2. 적당히 살아남은 제품
3. 설치/사용량이 낮은 제품
4. 공개 실패
5. 종료된 제품
6. 무료 제품
7. 오픈소스 제품
8. 기존 제품의 대체재
```

---

# 5.1 Case Card

각 사례는 다음 형식으로 저장한다.

```yaml
case:
  product:
  platform:
  founder:
  launch_date:
  current_status:

problem:
target_user:
core_job:

product:
  initial_features:
  current_features:
  special_characteristic:

business:
  initial_price:
  current_price:
  business_model:
  disclosed_revenue:
  disclosed_users:

distribution:
  initial_channels:
  successful_channels:
  failed_channels:

history:
  important_changes:
  pivot:
  price_changes:

failure_or_success:
  founder_stated_reason:
  observable_facts:
  our_inference:

evidence:
  sources:
```

---

# 5.2 사실과 추론을 분리한다

반드시 세 가지 태그를 사용한다.

### FACT

공식 페이지, Store, 공개 숫자 등 직접 확인 가능.

### SELF-REPORTED

창업자가 직접 주장한 내용.

### INFERENCE

자료를 바탕으로 우리가 추론한 내용.

AI는 **INFERENCE를 FACT처럼 쓰면 안 된다.**

---

# 5.3 실패 원인 Taxonomy

실패 사례를 다음 기준으로 분류한다.

```text
P1 No real problem
P2 Weak pain
P3 Wrong target
P4 Existing solution good enough
P5 Bad positioning
P6 Too broad
P7 Poor onboarding
P8 Weak retention
P9 No willingness to pay
P10 Wrong pricing model
P11 No distribution
P12 Wrong acquisition channel
P13 Platform mismatch
P14 High operating cost
P15 Technical maintenance burden
P16 Trust/privacy problem
P17 Competition commoditized the feature
P18 Founder stopped too early
P19 Founder overbuilt before launch
P20 Platform owner copied/bundled feature
P21 Regulation/legal issue
P22 Timing problem
P23 Unknown / insufficient evidence
```

확정할 수 없으면 반드시 `P23 Unknown`을 사용한다.

---

# GATE 6 — MARKET STRUCTURE
## 이 시장은 1인 개발자에게 좋은 시장인가?

Porter Five Forces를 단순화해서 본다.

### Existing Rivalry

경쟁 강도.

### New Entrants

얼마나 쉽게 복제되는가?

### Substitutes

다른 방식으로 해결 가능한가?

### Buyer Power

사용자가 쉽게 갈아탈 수 있는가?

### Supplier / Platform Power

Apple, Google, Microsoft, Chrome, OpenAI 등에 얼마나 의존하는가?

추가 질문:

> 대기업에게 너무 작지만 우리에게는 충분히 큰 시장인가?

이것은 오히려 장점일 수 있다.

---

# GATE 7 — DIFFERENTIATION
## 기존보다 왜 나은가?

Blue Ocean ERRC를 사용한다.

### Eliminate

업계가 당연하게 제공하지만 제거할 것.

### Reduce

과도하게 제공되는 것.

### Raise

고객에게 중요하지만 부족한 것.

### Create

새롭게 제공할 것.

```yaml
errc:
  eliminate:
  reduce:
  raise:
  create:
```

단순히 다르기만 해서는 안 된다.

항상 묻는다.

> **이 차이가 고객에게 실제로 중요한가?**

---

# GATE 8 — POSITIONING
## 누구에게 무엇으로 인식될 것인가?

April Dunford식으로 순서대로 정리한다.

1. Competitive Alternatives
2. Unique Capabilities
3. Value
4. Best-fit Customer
5. Market Category

### Positioning 문장

```text
For [specific user]
who [specific situation/problem],

[product] is a [category]

that [main value],

unlike [main alternative],

it [important differentiated capability].
```

### 금지

- “모두를 위한”
- “혁신적인”
- “AI 기반”
- “올인원”
- 기능 숫자만 강조
- 경쟁자보다 기능이 많다는 것만 강조

---

# GATE 9 — ASSUMPTION MAP
## 성공하기 위해 무엇이 참이어야 하는가?

제품을 만들기 전에 핵심 가설을 작성한다.

종류:

### Desirability

사람이 원하는가?

### Viability

돈이 되는가?

### Feasibility

만들고 유지할 수 있는가?

### Distribution

고객에게 도달할 수 있는가?

### Survivability

경쟁과 플랫폼 변화 속에서 살아남는가?

예:

```yaml
assumption:
  id: H1
  statement:
  category:
  why_critical:
  current_evidence:
  contradicting_evidence:
  cheapest_test:
```

우선순위는:

> **중요한데 증거가 가장 약한 가설**

이다.

점수를 억지로 계산할 필요는 없다.

---

# GATE 10 — CHEAPEST TEST
## 가장 적은 비용으로 가설을 죽이는 방법은?

코드를 바로 쓰지 않는다.

가능한 실험:

### Research Test

시장 흔적 조사.

### Smoke Test

Landing page + CTA.

### Demo Test

Figma / 영상 / prototype.

### Concierge Test

사람이 수동으로 대신 수행.

### Wizard of Oz

자동처럼 보이지만 내부는 수동.

### Preorder / Early Access

실제 결제.

### Thin Product

핵심 Job만 수행하는 최소 앱.

### Feature Gate

유료 기능을 실제로 잠그고 upgrade 행동 관찰.

---

# 10.1 실험 선택 원칙

다음 순서로 생각한다.

```text
Research로 죽일 수 있는가?
↓
Demo로 죽일 수 있는가?
↓
Landing으로 죽일 수 있는가?
↓
결제로 죽일 수 있는가?
↓
그제야 코드가 필요한가?
```

---

# GATE 11 — WILLINGNESS TO PAY
## 말이 아니라 돈으로 검증한다

가능하면 실제 가격을 보여준다.

### 증거

약한 순서:

```text
"살 것 같아요"
↓
가격 설문
↓
Checkout click
↓
Checkout start
↓
Card entry
↓
Purchase
↓
Repeat payment
```

---

# 11.1 가격은 시장과 원가를 동시에 본다

조사:

- Direct competitor
- Adjacent tool
- Customer's current workaround cost
- Platform norm
- Support cost
- Server cost
- AI inference cost

---

# 11.2 Lifetime이 적합한 경우

다음에 가까울수록 One-time / Lifetime이 자연스럽다.

- Local-first
- 서버 비용 거의 없음
- 지속 콘텐츠 공급 없음
- 단일 Utility
- 유지비가 낮음
- 고객이 subscription을 기대하지 않음

---

# 11.3 Subscription이 적합한 경우

다음에 가까울수록 Subscription이 자연스럽다.

- Cloud Sync
- Hosting
- AI inference
- 지속 업데이트되는 데이터
- Collaboration
- Team workspace
- Monitoring
- 계속되는 서비스 비용
- 사용자가 반복적으로 얻는 명확한 지속 가치

---

# 11.4 Hybrid

예:

```text
Core App
→ One-time

Cloud / AI
→ Subscription
```

가능하다.

---

# GATE 12 — UNIT ECONOMICS
## 돈이 남는가?

최소한 다음을 계산한다.

```yaml
economics:
  price:
  payment_fee:
  platform_fee:
  tax_estimate:
  variable_cost_per_user:
  support_cost:
  refund_rate_observed:
  net_revenue_per_sale:
```

구독이라면:

```yaml
subscription:
  monthly_price:
  variable_monthly_cost:
  observed_churn:
  observed_retention:
```

**관측하지 않은 churn/CAC/Conversion은 임의 숫자로 채우지 않는다.**

`UNKNOWN`이라고 쓴다.

---

# GATE 13 — DISTRIBUTION
## 제품을 만들기 전에 첫 고객이 어디서 오는지 설명할 수 있는가?

다음 문장을 완성한다.

> 우리의 첫 고객은 __________ 에서 __________ 를 찾다가 우리를 발견한다.

---

# 13.1 Distribution Channel 유형

### Search

- Google
- App Store Search
- Chrome Web Store Search
- Mac App Store Search

### Community

- Reddit
- Hacker News
- Discord
- Facebook Groups
- Forums

### Content

- YouTube
- Shorts
- X
- Threads
- Blog
- Tutorials

### Launch

- Product Hunt
- Hacker News Show HN
- BetaList

### Referral

- watermark
- share link
- exported artifact
- collaboration invitation

### Partnership

- creators
- newsletters
- affiliate
- complementary tools

### Paid

- Google Search Ads
- Meta
- Reddit Ads
- sponsorship

Paid는 가능하면 **organic purchase가 확인된 뒤** 사용한다.

---

# 13.2 Platform마다 기대가 다르다

## Chrome Extension

사용자가 기대하는 것:

- 바로 작동
- 현재 페이지에서 동작
- 낮은 friction
- 좁은 목적
- 적은 permission

---

## Mobile App

사용자가 기대하는 것:

- 명확한 반복 use case
- onboarding이 짧음
- notification을 남용하지 않음
- 모바일에서 하는 이유가 명확함

검토:

- iOS/Android distribution
- App Store review
- subscription expectation
- acquisition cost
- mobile-only advantage

---

## macOS / Windows Desktop App

사용자가 기대하는 것:

- OS integration
- keyboard shortcut
- menu bar / tray
- native workflow
- local files
- background utility

검토:

- code signing
- notarization
- auto-update
- Windows/macOS 별 maintenance
- direct sales vs Store

---

## Web SaaS

사용자가 기대하는 것:

- 어디서나 접근
- collaboration
- cloud persistence
- onboarding
- reliable service

검토:

- hosting
- auth
- database
- security
- support
- recurring infra cost

---

# 13.3 Channel ↔ Product Fit

반드시 확인한다.

예:

```text
Desktop utility
+
Mobile-heavy ad traffic
=
Mismatch
```

좋은 Traffic이 아니라 **구매할 수 있는 Traffic**이 필요하다.

---

# GATE 14 — DEFENSIBILITY & EXPANSION
## 성공하면 무엇이 쌓이는가?

초기 제품은 복제 가능해도 된다.

중요한 것은 사용하면서 방어력이 생기는가다.

가능한 자산:

### Switching Cost

사용자가 축적한:

- history
- data
- library
- settings
- workflows

### Brand

특정 문제의 대표 제품.

### Distribution

SEO / community / creator network.

### Data

합법적이고 동의 기반으로 축적된 고유 데이터.

### Process

남들이 쉽게 재현하기 어려운 운영 노하우.

### Network

사용자가 많아질수록 가치 증가.

### Integration

사용자 workflow에 깊게 들어감.

---

# 14.1 Expansion

확장은 네 종류로 나눈다.

```text
Same User + More Jobs
New User + Same Job
New Platform
New Product
```

예:

```text
Mac utility
→ Windows

Chrome Extension
→ Edge / Firefox

Solo user
→ Team

Local
→ Cloud
```

하지만 **Core가 돈을 벌기 전에는 확장을 정당화하지 않는다.**

---

# 15. Kill / Continue / Pivot 판단

임의의 성공률이나 전환율을 기준으로 하지 않는다.

## KILL / HOLD 신호

- 동일 문제의 독립적인 외부 증거가 거의 없다.
- 사용자들이 workaround를 사용하지 않는다.
- 기존 해결책이 충분히 좋고 싸다.
- 차별점이 고객에게 중요하다는 증거가 없다.
- 무료 대체재로 충분하고 유료 사례도 찾기 어렵다.
- 관심은 있지만 구매 행동이 반복적으로 나타나지 않는다.
- 유입 경로를 찾을 수 없다.
- 플랫폼 수수료/운영비/지원비가 경제성을 파괴한다.
- 1인 팀의 유지 능력을 초과한다.
- 플랫폼 사업자가 기본 기능으로 흡수할 가능성이 매우 높고 대응 경로가 없다.

---

## CONTINUE 신호

- 동일 문제의 반복적인 외부 증거.
- 현재 workaround 존재.
- 실제 사용.
- 반복 사용.
- 사용자가 핵심 feature를 스스로 반복 사용.
- Checkout.
- 실제 구매.
- 재구매/갱신.
- 추천.
- 검색이나 콘텐츠에서 지속 organic acquisition.

---

## PIVOT 신호

사용자가 우리가 예상한 기능이 아니라 다른 기능을 핵심으로 사용한다.

예:

```text
우리는 저장 앱이라 생각
↓
사용자는 Export만 사용
↓
실제 Job = Sharing
```

제품 정의를 사용자 행동에 맞춘다.

---

# 16. Revenue-First 제품 개발 순서

일반적인 순서:

```text
Idea
↓
기획
↓
디자인
↓
개발
↓
출시
↓
마케팅
↓
수익화
```

를 사용하지 않는다.

권장:

```text
Observation
↓
Market Forensics
↓
Problem Evidence
↓
Alternative Research
↓
Offer
↓
Positioning
↓
Demo
↓
Price
↓
Distribution Test
↓
Thin Product
↓
Payment
↓
Improve
```

**수익화를 마지막 단계로 미루지 않는다.**

---

# 17. 팀이 아이디어를 제출할 때 사용하는 Idea Intake

```yaml
idea:
  name:
  one_sentence:

observation:
  what_happened:
  when:
  how_often_observed:

problem:
  user:
  situation:
  pain:

proposed_solution:

why_now:

platform:
  - web
  - ios
  - android
  - macos
  - windows
  - browser_extension

business:
  expected_model:
  possible_price:
  revenue_goal:

constraints:
  max_build_time:
  max_budget:
```

---

# 18. AI가 자동으로 수행해야 하는 절차

AI에게 이 문서를 시스템/프로젝트 규칙으로 제공한 뒤 새로운 아이디어가 들어오면 다음을 수행한다.

---

## STEP 1 — Separate

사용자의 설명을 다음으로 분리한다.

```text
Observation
Problem
Solution
Assumption
Claim
Unknown
```

---

## STEP 2 — Do not jump to competitors

먼저 문제 자체를 분석한다.

질문:

- 누가?
- 언제?
- 무엇을 하려다?
- 어디서 막히나?
- 지금 무엇을 하나?
- 해결하지 않으면?

---

## STEP 3 — Root Cause

First Principles로 전제를 분해한다.

---

## STEP 4 — Market Forensics

반드시 Web Search를 사용해:

### Problem Evidence

찾는다.

### Existing Solutions

찾는다.

### Success Cases

찾는다.

### Failure Cases

찾는다.

### Dead / Abandoned Products

찾는다.

### Free / Open Source Alternatives

찾는다.

---

## STEP 5 — Anti-survivorship Search

성공 사례 5개를 찾았다면 실패/저사용/중단 사례도 적극적으로 찾는다.

검색 예:

```text
"[category] failed"
"[category] no users"
"[category] zero revenue"
"[category] shut down"
"[category] abandoned"
"I built [category] and..."
"[platform] app failed"
```

---

## STEP 6 — Evidence Table

```markdown
| Claim | Evidence | Type | Source | Confidence |
|---|---|---|---|---|
```

Type:

```text
FACT
SELF-REPORTED
INFERENCE
```

---

## STEP 7 — Critical Assumptions

성공하기 위해 반드시 참이어야 할 명제를 작성한다.

예:

```text
H1. 사용자는 이 문제를 반복적으로 겪는다.
H2. 현재 해결법이 충분하지 않다.
H3. 우리의 방식이 더 빠르다.
H4. 이 차이에 돈을 낸다.
H5. 사용자를 획득할 채널이 있다.
```

---

## STEP 8 — Rank Without Fake Precision

임의로:

```text
H1 = 87%
```

같은 숫자를 만들지 않는다.

대신:

```text
Critical + Weak Evidence
Critical + Moderate Evidence
Non-critical + Weak Evidence
```

형태로 분류한다.

---

## STEP 9 — Cheapest Test

각 Critical + Weak Evidence 가설에 대해:

> 가장 싼 검증 방법은 무엇인가?

를 제안한다.

---

## STEP 10 — Revenue Math

팀의 월 목표 수익에서 역산한다.

모르는 값은 `UNKNOWN`.

추측하지 않는다.

---

## STEP 11 — Decision

최종적으로 다음 중 하나만 선택한다.

```text
RESEARCH MORE
TEST
BUILD THIN MVP
CONTINUE
REPOSITION
PIVOT
HOLD
KILL
```

그리고 이유를 Evidence와 연결한다.

---

# 19. AI가 절대로 하면 안 되는 것

## 19.1 아이디어에 자동 동의

금지:

> 좋은 아이디어입니다.

근거 없이는 사용하지 않는다.

---

## 19.2 근거 없는 시장 규모

근거 없이:

> 시장은 수십억 달러입니다.

라고 쓰지 않는다.

---

## 19.3 임의의 성공 기준

근거 없이:

> 전환율 5% 이하면 실패.

라고 정하지 않는다.

---

## 19.4 경쟁사 기능표만 만들기

경쟁 분석에는 반드시 포함한다.

- 가격
- 고객
- 초기 제품
- 출시 방식
- 유입
- 실패
- pivot
- 현재 상태

---

## 19.5 성공 사례만 조사

금지.

---

## 19.6 Product Hunt 반응 = PMF

금지.

---

## 19.7 SNS 조회수 = WTP

금지.

---

## 19.8 기능 추가를 문제 해결로 착각

Traffic이나 Payment 문제가 있으면:

> 기능 하나 더 만들자.

가 기본 답변이 되어서는 안 된다.

먼저:

```text
Problem?
Target?
Position?
Offer?
Price?
Trust?
Distribution?
```

을 확인한다.

---

# 20. Product Decision Document Template

각 아이디어마다 아래 파일 하나를 만든다.

```markdown
# [Product] Decision Report

## 1. Observation

## 2. Problem

## 3. Job To Be Done

## 4. Root Cause

## 5. Existing Behavior

## 6. Current Alternatives

## 7. Market Forensics

### Success Cases

### Failure Cases

### Dead Products

### Free/Open Source

## 8. Why Products Succeeded

## 9. Why Products Failed

## 10. Market Structure

## 11. Differentiation

## 12. Positioning

## 13. Critical Assumptions

## 14. Evidence Table

## 15. Cheapest Experiments

## 16. Pricing Evidence

## 17. Revenue Model

## 18. Distribution

## 19. Platform Risk

## 20. Maintenance Cost

## 21. Defensibility

## 22. Expansion

## 23. Unknowns

## 24. Decision

BUILD / TEST / PIVOT / KILL

## 25. Next Action
```

---

# 21. Weekly Revenue Review

이미 출시한 제품은 매주 다음을 검토한다.

```yaml
revenue:
  gross:
  net:
  new_buyers:
  renewals:
  refunds:

acquisition:
  source:
  visits:
  installs:
  purchases:

usage:
  active_users:
  repeated_core_action:
  retention_observed:

product:
  most_used_feature:
  least_used_feature:
  unexpected_behavior:

support:
  common_question:
  common_bug:
  refund_reason:

decision:
  continue:
  improve:
  remove:
  test:
```

핵심은 vanity metrics보다:

```text
Money
Core Behavior
Return
Acquisition Source
```

다.

---

# 22. 플랫폼별 추가 체크리스트

---

## Web App / SaaS

```text
[ ] Auth가 정말 필요한가?
[ ] Backend가 정말 필요한가?
[ ] Cloud 저장이 실제 Job의 일부인가?
[ ] Hosting 비용은?
[ ] AI API 비용은?
[ ] Support burden은?
[ ] Subscription 명분이 있는가?
[ ] SEO로 유입 가능한가?
```

---

## Chrome / Browser Extension

```text
[ ] Single purpose가 명확한가?
[ ] 필요한 permission만 쓰는가?
[ ] 현재 페이지에서 바로 가치가 발생하는가?
[ ] 설치 후 1분 내 첫 가치가 나오는가?
[ ] Store 밖 distribution이 있는가?
[ ] Chrome 정책 변경 위험은?
[ ] Edge/Firefox 확장이 쉬운가?
```

---

## Mobile App

```text
[ ] 모바일이어야 하는 이유가 있는가?
[ ] 반복 use case가 있는가?
[ ] Notification이 실제 가치인가?
[ ] App Store 검색 수요가 있는가?
[ ] Apple/Google 수수료 고려했는가?
[ ] Review rejection risk는?
[ ] ASO 외 acquisition은?
[ ] Free app 대체재가 강한가?
```

---

## macOS App

```text
[ ] macOS에 특화된 Job인가?
[ ] Menu bar / Shortcut / Files / Clipboard / Window 관리가 핵심인가?
[ ] Direct distribution과 Mac App Store 중 무엇인가?
[ ] Code signing / notarization 고려했는가?
[ ] Auto-update는?
[ ] Lifetime 가격이 자연스러운가?
[ ] Setapp과 같은 distribution을 고려할 가치가 있는가?
```

---

## Windows Desktop App

```text
[ ] Windows-specific pain인가?
[ ] Tray / Files / Explorer integration이 필요한가?
[ ] Installer / update 전략은?
[ ] Microsoft Store가 필요한가?
[ ] 기업 보안 소프트웨어와 충돌 가능성은?
[ ] Support matrix가 복잡해지는가?
```

---

# 23. 작은 팀의 Scope Rule

새 기능은 다음 중 하나를 증명해야 한다.

```text
1. Acquisition 개선
2. Activation 개선
3. Core Job 개선
4. Retention 개선
5. Revenue 개선
6. Operating Cost 감소
```

아무것도 해당하지 않으면 보류한다.

---

# 24. Feature Request 판단

```yaml
feature:
  name:
  requested_by:
  problem:
  which_metric_or_job:
  evidence:
  implementation_cost:
  maintenance_cost:
  cheaper_alternative:
  decision:
```

단순히:

> 있으면 좋아 보인다.

는 개발 이유가 아니다.

---

# 25. 수익이 급할수록 좋은 제품의 특성

1~3인 팀이 빠르게 돈을 벌어야 한다면 일반적으로 다음 특성이 유리하다.

```text
좁은 Job
+
이미 존재하는 수요
+
낮은 구축 비용
+
낮은 운영 비용
+
눈으로 설명 가능한 가치
+
짧은 onboarding
+
명확한 가격
+
기존 유료 시장
+
검색 가능한 문제
```

반대로 위험:

```text
새로운 시장 교육 필요
+
긴 network effect 구축
+
고비용 AI
+
대규모 콘텐츠 DB
+
복잡한 양면시장
+
기업영업 필수
+
긴 무료 사용 후 가치 발생
```

이것은 절대 규칙이 아니라 **현재 자원 조건에서의 우선순위**다.

---

# 26. “짜치는 시장”을 판단하는 방법

감정적으로 판단하지 않는다.

질문:

> 대기업에게는 너무 작지만 우리 목표 수익에는 충분한가?

예:

```text
가격 × 필요한 고객 수
```

를 계산한다.

작은 시장에서도 필요한 고객 수가 현실적이면 충분하다.

우리는 반드시 Unicorn을 만들 필요가 없다.

**작고 유지비가 낮은 Utility도 성공적인 사업이다.**

---

# 27. 대기업 진입 위험

질문:

```text
이 기능이 OS/Browser/LLM의 자연스러운 기본 기능이 될 수 있는가?
```

YES라면 추가 질문:

```text
기본 기능이 되어도 우리가 살아남을 이유는?
```

가능한 답:

- 더 깊은 workflow
- history
- user data
- specialized vertical
- cross-platform
- integrations
- community
- superior UX
- proprietary classification/process

없다면 장기 risk가 높다.

---

# 28. 모방 가능성

질문:

> 경쟁자가 우리의 홈페이지를 보고 일주일 안에 기능을 복제했다고 가정한다.

그 후에도 무엇이 남는가?

```text
Users
Data
Workflow
Brand
SEO
Community
Integrations
History
Trust
Process
```

없어도 초기에는 괜찮다.

그러나 시간이 지나도 아무것도 쌓이지 않는다면 commodity risk가 크다.

---

# 29. AI 시대 추가 규칙

AI 때문에 개발 비용이 낮아졌다.

따라서 다음은 더 이상 강한 장점이 아니다.

```text
"우리가 이걸 만들 수 있다."
```

경쟁자도 만들 수 있다.

희소해지는 것은:

```text
좋은 문제 선택
↓
좋은 Taste
↓
좋은 Positioning
↓
빠른 검증
↓
Distribution
↓
사용자 이해
↓
제품 판단
```

이다.

AI는 구현 속도를 높이기 위한 도구이면서 동시에 **잘못된 아이디어를 너무 오래 만들게 하는 위험**도 있다.

따라서:

> Coding speed보다 Kill speed가 중요하다.

---

# 30. 최종 Master Flow

```text
IDEA
│
▼
OBSERVATION
“무엇이 이상했나?”
│
▼
PROBLEM
“다른 사람도 겪나?”
│
▼
JOB
“무엇을 하려 하나?”
│
▼
ROOT CAUSE
“진짜 원인은?”
│
▼
CURRENT BEHAVIOR
“지금은 어떻게 하나?”
│
▼
MARKET FORENSICS
“누가 먼저 시도했고 어떻게 됐나?”
│
├── SUCCESS
├── FAILURE
├── DEAD
├── FREE
└── OPEN SOURCE
│
▼
ALTERNATIVES
“우리 없이 무엇을 쓰나?”
│
▼
DIFFERENTIATION
“무엇을 제거/개선/창조하나?”
│
▼
POSITIONING
“누구에게 무엇으로 보일까?”
│
▼
ASSUMPTIONS
“무엇이 참이어야 하나?”
│
▼
CHEAPEST TEST
“코드 없이 죽일 수 있나?”
│
▼
WTP
“실제로 돈 내나?”
│
▼
ECONOMICS
“목표 수익이 현실적인가?”
│
▼
DISTRIBUTION
“첫 고객은 어디서 오나?”
│
▼
DEFENSIBILITY
“성공하면 무엇이 쌓이나?”
│
▼
PLATFORM RISK
“Apple/Google/Microsoft/Chrome/AI가 죽일 수 있나?”
│
▼
DECISION
│
├── BUILD
├── CONTINUE
├── REPOSITION
├── PIVOT
├── HOLD
└── KILL
```

---

# 31. AI에게 바로 사용할 Master Prompt

아래 지시를 새로운 제품 아이디어와 함께 사용한다.

```text
너는 우리 소규모 제품팀의 Revenue-First Product Analyst다.

목표는 아이디어를 칭찬하거나 완성된 사업계획서를 만드는 것이 아니다.
우리 팀이 제한된 시간과 돈으로 실제 수익을 만들 가능성을 높이는 것이다.

반드시 첨부된 Revenue-First Solo Product Validation OS를 따른다.

규칙:

1. 사용자가 제시한 아이디어에서 Observation, Problem, Solution, Assumption을 먼저 분리한다.

2. 솔루션을 정당화하기 전에 문제가 실제로 존재하는지 조사한다.

3. 성공 사례뿐 아니라 반드시 실패, 저사용, 종료, 무료, 오픈소스 사례를 조사한다.

4. 실패 이유는 FACT / SELF-REPORTED / INFERENCE로 구분한다.

5. 인터뷰가 없더라도 Reddit, Store review, GitHub, Product Hunt, founder retrospective, 검색 행동, 경쟁사 pricing 등 시장 흔적을 증거로 사용한다.

6. 임의의 시장규모, 성공확률, 전환율, retention, CAC를 만들어내지 않는다.
확인할 수 없으면 UNKNOWN이라고 쓴다.

7. 현재 시장의 해결방식에는 직접 경쟁사뿐 아니라 manual workaround, AI, spreadsheet, general-purpose tools, do nothing을 포함한다.

8. 성공하기 위해 반드시 참이어야 하는 Critical Assumptions를 작성한다.

9. 가장 중요하면서 증거가 약한 가설부터 가장 싼 실험을 설계한다.

10. 코드 구현은 최후의 검증 수단으로 취급한다.
Research → Demo → Landing → Checkout → Thin MVP 순으로 더 싼 검증이 가능한지 먼저 확인한다.

11. Willingness To Pay는 의견보다 실제 결제를 우선한다.

12. 사용자의 월 목표 수익을 기준으로 필요한 판매량을 역산한다.
관측되지 않은 conversion rate를 임의로 넣지 않는다.

13. Platform별 리스크를 분석한다.
Web / iOS / Android / macOS / Windows / Browser Extension 각각의 distribution, fee, policy, maintenance 특성을 고려한다.

14. 기능 추가가 Acquisition, Activation, Core Job, Retention, Revenue, Cost 중 무엇을 개선하는지 설명하지 못하면 개발을 추천하지 않는다.

15. 경쟁자가 기능을 쉽게 복제할 수 있다고 가정하고, 시간이 지나면서 어떤 방어력이 쌓일 수 있는지 분석한다.

16. 마지막에는 반드시 다음 중 하나를 선택한다.

RESEARCH MORE
TEST
BUILD THIN MVP
CONTINUE
REPOSITION
PIVOT
HOLD
KILL

17. 결론은 근거와 연결해야 하며, 아이디어에 과도하게 낙관적이거나 비관적이어서는 안 된다.

최종 출력:

A. Executive Decision
B. Observation / Problem / Job
C. Root Cause
D. Market Evidence
E. Existing Alternatives
F. Success Case Analysis
G. Failure / Dead Product Analysis
H. Positioning
I. Critical Assumptions
J. Evidence Gaps
K. Cheapest Experiments
L. Pricing / WTP Evidence
M. Revenue Math
N. Distribution
O. Platform / Maintenance Risk
P. Defensibility
Q. Build / Pivot / Kill Decision
R. Next 3 Actions
```

---

# 32. 이 문서의 최종 원칙

좋은 제품팀은 아이디어를 많이 만드는 팀이 아니다.

**틀린 아이디어에 오래 머물지 않는 팀**이다.

우리의 목표는:

```text
Idea
↓
Evidence
↓
Behavior
↓
Usage
↓
Payment
↓
Repeatable Revenue
```

로 최대한 빨리 이동하는 것이다.

AI는 이 과정에서:

- 리서처
- 분석가
- 경쟁사 조사원
- 가설 생성기
- 반론자
- 실험 설계자
- 문서화 도구

역할을 수행한다.

하지만 최종 판단은 항상:

> **실제 시장 행동과 실제 돈**

을 우선한다.

---

# End
