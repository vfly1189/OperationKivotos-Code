# Stat 계층 리팩터 — God 컴포넌트 분해

> **동기**: `CharacterStat`/`MonsterStat`이 "스탯 그릇"이 아니라 캐릭터/몬스터의 도메인 전체를
> 떠안은 God 컴포넌트가 됐다. #2(버프/방깎)가 얹히는 `Stat` 계층을 SRP로 분리한다.
> **선행 조건**: 깨끗한 `StatCollection`(= GAS AttributeSet)이 있어야 Phase 3 버프/방깎이 얹힌다.

---

## 진단 — 책임 과부하

**`CharacterStat` 10가지 책임**: ①스탯 저장·합산(본업) ②데이터 바인딩 ③장비 ④에너지·궁극기(+Update 틱)
⑤스킬 쿨타임(+Update 틱) ⑥피격·죽음·무적 ⑦데미지 출력(크리) ⑧세이브 ⑨음성 ⑩데미지 토스트 UI

**`MonsterStat` 결정적 위반**: `HandleDeath`가 **경제 로직** 수행 — 킬러에게 경험치/크레딧 지급,
드랍 굴리기, 알림 UI 호출. 스탯 클래스가 지갑·드랍·UI를 직접 건드린다.

**공통 뿌리**: `BaseStat`이 "스탯 그릇 + 전투(피격/죽음) + UI(`ToastDamageUI`)"를 한꺼번에 갖는다.

---

## 목표 구조 (composition)

```
[엔티티 GameObject]
 ├─ StatCollection      ← (구 BaseStat 슬림화) 속성 저장 + 합산만. = GAS AttributeSet
 ├─ Health              ← CurrentHp/피격/죽음/무적. StatCollection에서 MaxHp·Defense 읽음
 ├─ DamageOutput        ← BuildOutgoingDamage. 크리는 데이터/플래그로
 ├─(캐릭터) EnergyGauge  ← 에너지·궁 리젠
 ├─(캐릭터) 쿨타임        ← Phase1에서 E/Q 어빌리티화되면 AbilityRunner로 흡수
 └─(몬스터) MonsterReward ← 사망 시 경험치/크레딧/드랍. (경제 로직 이관)
[UI] DamageNumberPresenter ← 데미지 이벤트 구독 (ToastDamageUI 제거)
```

**핵심 이동**: ①스탯↔전투 분리(Health) ②경제/UI를 스탯에서 축출 ③인헤리턴스→컴포지션
(고유 로직이 컴포넌트로 빠지면 `CharacterStat`/`MonsterStat` 서브클래스는 거의 소멸)

---

## 이관 순서 (안전한 증분 — 빅뱅 금지)

| 단계 | 작업 | 상태 |
|---|---|---|
| **1** | **경제 축출** — 몬스터 보상 → `MonsterReward` | ✅ 완료 |
| **2** | **UI 축출** — `ToastDamageUI` → `DamageNumberPresenter`(이벤트 구독) | ✅ 완료 |
| **3** | **Health 추출** — HP/피격/죽음/무적 → `Health` | ✅ **완료 (2026-07-10)** |
| 4 | 에너지/쿨타임 분리 — `EnergyGauge` | ⏸ 보류 |
| 5 | 남은 `CharacterStat`/`MonsterStat` = 초기화 바인딩만 | ⏸ 보류 |

각 단계 후 컴파일·전투 1회 확인 후 다음으로.

**3단계 보류 결정 (2026-07-09) → 재개·완료 (2026-07-10)**: 최초 실측 시 Health 추출은
① blast radius ~15곳(HP바·HUD·Save·무적·Heal/Recover 등) ② 캐릭터/몬스터 죽음 로직 상이 →
여러 시스템 동시 수정(준-빅뱅) 우려로 **보류**했다(#2의 선행조건 아님·오버엔지니어링 경계).
이후 #2가 안정화되어 **재개**했고, 준-빅뱅 우려는 아래 **2개 안전장치**(facade 전환 + 이벤트 구독 분기)로
해소해 6개 서브스텝 증분으로 완료했다. 상세는 [3단계 기록](#3단계-기록--health-추출-완료) 참고.

---

## 1단계 기록 — 몬스터 경제 축출 (완료)

**변경 파일 3개**
- **신규** `MonsterReward.cs`: 사망 시 보상(경험치/크레딧/드랍/알림UI) 지급. `MonsterStat.OnKilledByAttacker` 구독.
  프리팹 수정 없이 `EnsureOn(go, stat)`으로 런타임 부착 + 바인딩(풀 재사용·중복구독 안전).
- **수정** `MonsterStat.cs`: `HandleDeath`의 경제 블록 제거 → `OnKilledByAttacker?.Invoke(shooter)` 이벤트 발행.
  `MonsterStat`은 이제 전투/스탯/죽음 감지만 담당.
- **수정** `BaseMonsterController.Awake`: `MonsterReward.EnsureOn(gameObject, Stat)` 호출 (전 몬스터 공통 초기화 지점).

**설계 근거**
- 경제 로직은 `MonsterStat.HandleDeath` 단일 지점 → 이관 리스크 낮음.
- 부착 지점을 `BaseMonsterController.Awake`로 잡아 **프리팹 편집 없이** 일반/보스 몬스터 전부 커버
  (보스는 Awake 미오버라이드, 일반몹은 `base.Awake()` 호출).
- 사망 이벤트 발행 순서(경제 → CallOnDead → OnMonsterDead)는 기존과 동일하게 유지.

**보상 데이터 위치**: `FinalExpReward`/`FinalCreditReward`/`DropTableID`는 당장은 `MonsterStat`에 잔존
(`MonsterReward`가 이벤트 시점에 읽음). 데이터 이전은 후속 단계에서.

**동반 수정한 기존 버그**: `MonsterStat._isDead`(private)가 풀 재사용 시 리셋되지 않아
2회차 생애에서 `TakeDamage`의 사망 가드가 막혀 `HandleDeath`(→보상) 미호출.
→ `Recover()` 오버라이드로 `_isDead = false` 리셋(OnEnable·SetStat 양쪽 경로가 Recover 호출).
중복 필드 제거(`IsDead` 통일)는 `BossMonsterController`가 `stat.IsDead`를 읽어 부작용 위험이 있어 보류.
(첫 처치는 정상, 리스폰 후 보상 누락으로 재현됨 — 원래 있던 버그였고 경제 축출로 표면화)

---

## 2단계 기록 — 데미지 토스트 UI 축출 (완료)

**변경 파일 6개**
- **신규** `DamageNumberPresenter.cs`: `BaseStat.OnDamageTaken` 구독 → `UI_DamageToast` 표시. 캐릭터·몬스터 공용.
  `EnsureOn(go, stat)`으로 런타임 부착(풀·중복구독 안전).
- **수정** `BaseStat.cs`: `ToastDamageUI` 제거 → `struct DamageTaken` + `event OnDamageTaken` + 발행 헬퍼 `RaiseDamageTaken`.
  BaseStat이 더는 `UI_DamageToast`/`Managers.UI`/`Camera`를 참조하지 않음 (UI 의존 제거).
- **수정** `CharacterStat.cs`·`MonsterStat.cs`: `ToastDamageUI(...)` → `RaiseDamageTaken(...)`.
- **수정** `BaseCharacter.Init`·`BaseMonsterController.Awake`: `DamageNumberPresenter.EnsureOn(...)` 부착.

**설계 근거**
- 이벤트는 선언 클래스에서만 invoke 가능 → 서브클래스(Character/Monster)가 쓸 발행 헬퍼 `RaiseDamageTaken`를 BaseStat에 둠
  (기존 `CallOnHpChanged`/`CallOnDead`와 동일 패턴).
- 부착 지점: 몬스터=`BaseMonsterController.Awake`(1단계와 동일), 캐릭터=`BaseCharacter.Init`.
  Hoshino/Serika 서브클래스 모두 `base.Init()` 호출 확인 → 누락 없음.
- 호출부 렌더 인자·타이밍(최종 데미지·hitPoint·layer·크리) 동일하게 보존.

---

## 3단계 기록 — Health 추출 (완료)

> **2026-07-10 완료.** God 컴포넌트에서 HP/피격/죽음/무적을 **단일 균일 `Health` 컴포넌트**로 완전 분리.
> `BaseStat`은 순수 `StatCollection`(= GAS AttributeSet)이 되었다.

### 최종 구조

```
[엔티티 GameObject]
 ├─ BaseStat (= StatCollection)  ← MaxHp/Attack/Defense + modifier 합산. HealthComp 접근점만 노출
 ├─ Health                       ← CurrentHp/피격/죽음/무적 + IDamageable + TakeDamage. StatCollection에서 MaxHp·Defense 읽음
 ├─(캐릭터) CharacterStat         ← 에너지·궁·쿨타임·크리. HP 복원은 Health에 위임
 └─(몬스터) MonsterStat           ← 보상 캐시 + 사망 재브로드캐스트(OnKilledByAttacker/OnMonsterDead)
[구독자] BaseCharacter(collider off) · MonsterReward(경제) · MonsterSpawner(리스폰) · DamageNumberPresenter(토스트)
```

### 준-빅뱅을 없앤 2개 안전장치

1. **BaseStat facade(전환기 위임)** — Health로 상태를 옮기되 `BaseStat.CurrentHp/IsDead/OnHpChanged` 등을
   `HealthComp`로 위임하는 얇은 멤버로 남겨, 소비 15곳이 계속 컴파일되게 했다. → **동시 수정 소멸.**
   클러스터별로 소비자를 `stat.HealthComp.*`로 옮긴 뒤(Step 5) facade를 삭제(Step 6). facade 삭제 시
   컴파일 에러가 **누락된 소비자를 자동으로 색출**하는 안전망 역할까지 했다.
2. **분기를 구독자로 흡수** — 캐릭터/몬스터 죽음의 차이를 서브클래스가 아니라 **`Health.OnDead`/`OnDied` 구독자**가
   처리. Health는 단일 균일 컴포넌트로 유지(서브클래스 0개). "안 만드는 결정".

### 6개 서브스텝 (각 단계 컴파일 + 전투 1회 검증)

| 스텝 | 작업 | 위험 |
|---|---|---|
| **1** | `Health` 생성(HP 숫자·사망 소유) + BaseStat facade화. 자기부트스트랩 lazy `HealthComp` getter로 부착 타이밍 NRE 차단 | 저 (파일 2개) |
| **2** | 데미지 계산·죽음 → `Health.TakeDamage`로 통합. Character/MonsterStat의 `TakeDamage`/`HandleDeath` 삭제. 분기를 구독자로 이관. `OnDamageTaken`·무적도 Health로. `_isDead`→`Health.IsDead` 통일 | **고 (유일)** |
| **3** | (2에 흡수) 무적 가드가 `Health.TakeDamage`에 필요해 Step 2에서 함께 이관 | — |
| **4** | `IDamageable` seam을 Stat→Health로 **원자적** 이관(이중구현 순간 금지). 투사체/Effect 6경로는 `TryGetComponent<IDamageable>`라 무수정 | 중 (격리) |
| **5** | 소비 15곳을 facade→`stat.HealthComp.*`로 클러스터 이관(UI/파티/세이브). facade는 유지(안전망) | 기계적 |
| **6** | facade 전면 삭제 → BaseStat=순수 StatCollection. CharacterStat 내부 HP대입 재배선. Recover/Heal도 Health로 | 정리 |

### 핵심 설계 결정

- **단일 Health(서브클래스 X)** — 죽음 사이드이펙트가 이미 이벤트로 빠져(경제=`OnKilledByAttacker`, collider=`OnDead`)
  Health 내부에 override 지점이 없다. 무적/풀가드는 무해한 기본값 필드로 균일화. (README §3-1 "값이 다르면 asset, 로직이 다르면 클래스"의 정확한 적용)
- **IDamageable = seam** — 데미지 6경로 중 5경로가 이미 `TryGetComponent<IDamageable>`. Health가 이 인터페이스를 물려받자
  투사체·Effect 코드가 **한 줄도 안 바뀌었다**. typed 호출은 보스 미니언 즉사 1곳뿐.
- **몬스터 death 이벤트 재브로드캐스트** — `MonsterStat`이 `Health.OnDied`를 구독해 자기 고유 이벤트로 되쏨 →
  `MonsterReward`/`MonsterSpawner` 무수정. (오래 미뤄둔 `_isDead` vs `IsDead` 중복 필드도 이때 통일)
- **`BaseStat→StatCollection` 리네임은 스킵** — BaseStat이 GO에 직접 부착되진 않지만 타입 참조 churn이 커
  변별력 대비 비용이 낮아 보류(선택).

### 안전장치 — 롤백 앵커
착수 전 `checkpoint/pre-health-refactor` 태그(커밋 `7bde3e21`)를 찍어, 이상 시 전체 롤백 가능하게 했다.

---

## 관련
- 시각 구조도: 세션 산출물(God 컴포넌트 분해 다이어그램) · 전/후 비교 PDF(`docs/AbilitySystem/HealthExtraction_BeforeAfter.pdf`)
- 후속: [RoleSystemPlan.md](RoleSystemPlan.md) Phase 3 버프/방깎이 `StatCollection` 위에 얹힘
