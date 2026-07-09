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
| 3 | **Health 추출** — HP/피격/죽음/무적 → `Health` | ⏸ **보류 (#2 우선)** |
| 4 | 에너지/쿨타임 분리 — `EnergyGauge` | ⏸ 보류 |
| 5 | 남은 `CharacterStat`/`MonsterStat` = 초기화 바인딩만 | ⏸ 보류 |

각 단계 후 컴파일·전투 1회 확인 후 다음으로.

**3단계 이후 보류 결정 (2026-07-09)**: 착수 전 실측 결과 Health 추출은
① blast radius ~14곳(HP바·HUD·Save·무적·Heal/Recover 등) ② 캐릭터/몬스터 죽음 로직 상이 →
여러 시스템 동시 수정(준-빅뱅). **결정적으로 #2의 선행조건이 아님** — 버프/방깎은
`Stat.AddModifier`(공격력·방어력 modifier)로 현재 아키텍처에서 이미 동작한다.
분해 패턴은 1·2단계로 이미 2회 증명됨. → **#2(역할/버프/방깎) 착수를 우선**하고 Health 추출은
필요 시 후속으로. (오버엔지니어링 경계 · "안 하는 결정"도 시니어 시그널)

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

## 관련
- 시각 구조도: 세션 산출물(God 컴포넌트 분해 다이어그램)
- 후속: [RoleSystemPlan.md](RoleSystemPlan.md) Phase 3 버프/방깎이 `StatCollection` 위에 얹힘
