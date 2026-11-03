# 02. 최적화 기법 정리 + 업계 사례

## 0. 먼저 — 축이 두 개다

> **축 1 — 몇 마리를 돌릴 것인가** (섹터/거리 컬링)
> **축 2 — 한 마리를 얼마나 싸게 돌릴 것인가**

둘은 곱셈이다. 100마리 × 0.1ms = 10ms 를, 축2만 손봐서 100마리 × 0.03ms = 3ms로 만들 수 있다. **구조를 하나도 안 바꾸고.**
기존 Sector 논의는 축1만 다뤘고 축2는 통째로 비어 있었다.

그리고 축2 안에서 다시 갈린다:

| | **설정 최적화** | **구조적 최적화** |
|---|---|---|
| 하는 일 | 마리당 비용의 상수를 낮춤 | **정책을 넣을 제어점을 만듦** |
| 예 | Animator 컬링 모드, NavMesh 회피 품질 | 매니저 일괄 처리, 틱 그룹, 계단식 판정 |
| 한계 | 한 번 하면 끝 | 위에 계속 얹을 수 있음 |
| 면접에서 | "설정을 찾아봤다" | **"구조를 설계했다"** |

---

## 1. 몬스터 한 마리가 프레임마다 쓰는 비용

| 항목 | 왜 비싼가 | 줄이는 법 | 난이도 | 효과 |
|---|---|---|---|---|
| **NavMeshAgent** | 경로 계산 + **서로 밀어내는 회피 계산**(마리 수의 제곱) | 회피 품질 하향, 멀면 컴포넌트 자체 끄기, 경로 재계산 스태거 | 쉬움 | ★★★ |
| **Animator** | 뼈대 전부를 매 프레임 계산 | Culling Mode → `Based on Renderers` + SkinnedMeshRenderer의 `Update When Offscreen` 끄기 | **체크박스 2개** | ★★★ |
| **물리 콜라이더** | 겹침 검사 | 레이어 충돌 매트릭스에서 불필요 조합 해제 | 아주 쉬움 | ★★ |
| **AI (BT)** | 트리 순회 + 조건 검사 | 거리·상태별 틱 주기 하향 | 중간 | ★★ |
| **Update() 호출 자체** | C#↔네이티브 왕복 | 매니저가 모아서 호출 | 중간 | — (**우리 규모엔 무의미**) |

### 근거 수치 (검색)
- **NavMeshAgent**: 개발자 보고 기준 **30마리만 넘어도 체감 부하**, **150마리면 회피 품질 최저에서도 프레임 드랍**. 목표(활성 60마리)가 정확히 이 구간.
- **Update 오버헤드**: Unity 문서 기준 **수백~수천 개**일 때 유의미. 수십 개 규모에서는 효과 없음 → **하지 않는다** (복잡도만 증가).

### 근본 기법은 셋뿐
| | 방법 | 절감 | 위험 |
|---|---|---|---|
| 1 | **아예 없앤다** (디스폰) | 100% | 게임플레이 파괴 |
| 2 | **있지만 끈다** (컴포넌트 비활성) | ~90% | 켤 때 상태 복구 필요 |
| 3 | **있지만 덜 돌린다** (틱 하향) | 50~80% | 반응 지연 |

거리 밴드는 **"언제 어떤 기법을 쓸지 정하는 규칙"** 일 뿐이고, 실제 절감은 위 표에서 나온다.

---

## 2. 구조적 기법 8가지

외우기: **모은다(①) / 나눈다(②③) / 안 한다(④⑤⑥⑧) / 붙여놓는다(⑦)**

### ① 소유권 뒤집기 — N개가 각자 → 1개가 일괄 ★전제조건★
지금은 몬스터 60마리가 **60개의 독립 Update**를 돈다. 이 구조에서는
"이번 프레임엔 12마리만", "멀면 5프레임에 한 번", "예산 초과 시 다음 프레임" — **전부 불가능하다.** 아무도 전체를 보고 있지 않기 때문.

> **최적화의 첫 단계는 빠르게 만드는 게 아니라 통제권을 회수하는 것.**

**주의**: 이것만으로는 성능이 거의 안 변한다. 성과가 아니라 **수단**이다.

### ② 시간 축으로 나누기 (Time-slicing)
60마리를 한 프레임에 다 처리하지 않고 프레임당 15마리씩 4프레임에 분산. 총 일은 같고 **프레임당 최댓값이 1/4**.
같은 프레임에 몰리지 않도록 **위상 어긋내기(stagger)** 를 함께 쓴다.

> 근거: 여러 NavMeshAgent가 같은 프레임에 경로를 재계산하면 터진다는 것이 알려진 문제. Unity ECS 길찾기도 **프레임당 최대 쿼리 수(기본 256)** 제한을 둔다.

### ③ 틱 그룹 (주기별 버킷)
```
60Hz : 전투 중
10Hz : 근처, 비전투
 2Hz : 멀리
```
몬스터를 버킷 사이로 옮기는 것만으로 부하가 조절된다. "거리 밴드별 틱 차등"이 **이 자료구조로 구현**된다.

### ④ 계단식 판정 (브로드페이즈 → 내로우페이즈)
**컬링(대상 제거)이 아니다.** 하나의 판정을 싼 단계부터 나누는 것.
```
1단계 제곱거리        (아주 쌈) → 90% 탈락
2단계 시야각 내적      (쌈)     → 또 절반 탈락
3단계 레이캐스트 시야   (비쌈)   → 남은 소수만
```
거꾸로 하면 재앙 — 어차피 탈락할 대상에게 가장 비싼 계산을 먼저 하게 된다.

### ⑤ 증분 갱신 (Incremental)
입력이 유의미하게 안 변했으면 결과를 재사용. **이미 코드에 있음** — `SetDestination`을 목표가 0.5m 이상 움직였을 때만 호출.

### ⑥ 푸시 vs 풀 (이벤트 기반)
매 프레임 "바뀌었나?" 묻지 말고 바뀌는 쪽이 알려주게 한다. **이미 코드에 있음** — 타겟 갱신이 `OnPlayerCharacterChanged` 이벤트.
Unity `CullingGroup`의 `onStateChanged`도 같은 원리.

### ⑦ 데이터를 붙여 놓기 (캐시 지향 / SoA)
MonoBehaviour 400개 순회 = 메모리 400번 튐. 매니저가 위치·상태를 **연속 배열**로 소유하면 캐시 라인 몇 개.
DOD·ECS의 핵심이고 Job/Burst는 그 위에 얹는다.
**지금 규모엔 과함.** 다만 ①을 배열 기반으로 짜두면 나중에 그대로 확장된다.

### ⑧ 대표값 시뮬레이션 (Proxy / Aggregate)
멀리 있는 건 실제 개체 없이 숫자로만 시뮬. **이미 코드에 있음** — 스포너가 몬스터 없이 리스폰 타이머만 소유.

---

## 3. 업계 사례 (검색 결과)

### 3.1 Unity `CullingGroup` API — 엔진이 이 목적으로 만든 물건

스크립트 전용 API. 바운딩 스피어 배열을 넘기면 엔진이 컬링 파이프라인에서 함께 계산한다.

- `SetBoundingSpheres()` — 배열을 **복사하지 않고 참조**한다 (⚠ 수명·인덱스 주의)
- `SetBoundingDistances(new[]{25,35,50})` — 거리를 **밴드 정수**로 양자화. 우리가 설계한 4단계가 이 API의 기본 기능
- `SetDistanceReferencePoint()` — 기준점 (카메라가 아니라 **플레이어**로 두는 게 맞다)
- `onStateChanged` — **밴드/가시성이 바뀌는 순간에만** 콜백. 폴링이 아예 없음
- 거리와 **동시에 "화면에 보이는가"**(프러스텀 + 정적 오클루전)를 준다

Unity 매뉴얼이 명시한 사용 사례:
> 적 스폰 포인트가 카메라에 안 보이는지 추적해 **팝인 없이 스폰** / 가까우면 완전한 애니메이션·AI, 멀면 저품질 / 군중 시뮬레이션

**제약**: 구(sphere)만 지원, 카메라 1개 기준, 동적 오클루더 미반영, 비동기 계산.
→ 쿼터뷰 싱글플레이라 세 제약 모두 문제가 되지 않는다. (단, 연출 카메라 전환은 주의 — [04_Risks](04_Risks.md))

### 3.2 Unreal `Significance Manager` — 점수 기반 시뮬 품질 배분

오브젝트마다 "중요도" 점수를 계산해 **먼 AI의 틱 주기를 낮추거나 컴포넌트를 끄는** 프레임워크.
스코어링 모드: Distance / **Distance and Render** / Distance and Angle / Weighted Distance and Render / Screen Space Ratio 등.

> **업계 표준은 "거리만"이 아니라 "거리 + 화면 비중/각도"** 라는 근거. Unity CullingGroup이 거리 밴드와 가시성을 함께 주는 것과 같은 방향.

**Unity에는 이 계층이 없다** → 직접 설계하면 기여도가 크게 보인다.

### 3.3 MMO 스포너 아키텍처

- 서버 시작 시 **몬스터를 만들지 않고 스포너만 만든다.** 스포너에게 "월드 로드됨" / "네 몬스터가 다 죽었다"를 통보 → 현재 `MonsterSpawner`와 같은 방향
- 그룹 스포너가 여러 마리를 추적 → 이미 동일
- **리스폰 타이머는 고정값이 아니라 "최소시간 + 변동폭"**, 스폰 위치도 고정점이 아니라 **범위 내 랜덤 + 랜덤 방향** → **지금 없음. 추가 가치 있음**
- **리쉬(leash)** — 플레이어가 너무 멀어지면 몬스터가 원래 자리로 복귀. MMO 표준 기법이며 ②'의 정체

### 3.4 GDC — "Cities at Scale: Simulating Street Life on a Budget"
매 프레임 시뮬 대신 **짧은 미래 구간을 미리 스케줄**하고, 그 원칙을 스폰·LOD·길찾기 전반에 적용.

### 3.5 오픈월드 타일 비활성화
플레이어 거리로 타일을 끄고, "최대 거리" 파라미터로 제어 — 대규모 맵 처리의 정석.

---

## 4. 출처

- [Unity Manual — Introduction to the CullingGroup API](https://docs.unity3d.com/Manual/CullingGroupAPI.html)
- [Unity Scripting API — CullingGroup.SetBoundingDistances](https://docs.unity3d.com/ScriptReference/CullingGroup.SetBoundingDistances.html)
- [Significance Manager in Unreal Engine](https://dev.epicgames.com/documentation/en-us/unreal-engine/significance-manager-in-unreal-engine)
- [Unreal Engine Game Optimization on a Budget — Tom Looman](https://tomlooman.com/unreal-engine-optimization-talk/)
- [Unity Manual — Animator Performance and Optimization](https://docs.unity3d.com/540/Documentation/Manual/MecanimPeformanceandOptimization.html)
- [Unity Scripting API — AnimatorCullingMode](https://docs.unity3d.com/ScriptReference/AnimatorCullingMode.html)
- [NavMeshAgent 성능 논의 (30개 이상 부하 / 150개 프레임 드랍)](https://discussions.unity.com/t/navmesh-agent-make-big-performance-impact/248175)
- [NavMeshAgent 회피 품질 5단계](https://www.toolify.ai/ai-news/mastering-navmeshagent-avoidance-in-unity-5-proven-tips-for-optimal-results-2359601)
- [Unity Manual — Using a custom update manager](https://docs.unity3d.com/6000.5/Documentation/Manual/events-per-frame-optimization.html)
- [Unity Blog — 10000 Update() calls](https://unity.com/blog/engine-platform/10000-update-calls)
- [Unity Manual — Layer collision matrix](https://docs.unity3d.com/6000.2/Documentation/Manual/physics-optimization-cpu-collision-layers.html)
- [Spawn & Control MOBs with an MMO server — GameDev.net](https://gamedev.net/forums/topic/656350-spawn-38-control-mobs-with-a-mmo-server/)
- [Preferred Spawn System — MMORPG.com](https://forums.mmorpg.com/discussion/391074/preffered-spawn-system)
- [A Guide To Handling Huge Open Worlds In Unity — GameDev Academy](https://gamedevacademy.org/how-to-handle-huge-worlds-in-unity-part-1-deactivating-distant-regions-to-improve-performance/)
- [GDC Vault — Cities at Scale: Simulating Street Life on a Budget](https://www.gdcvault.com/play/1027576/AI-Summit-Cities-at-Scale)
