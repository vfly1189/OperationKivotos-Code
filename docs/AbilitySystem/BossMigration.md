# 보스 전투 이관 & 데이터 주도 스킬 조합 (AbilitySystem — Part 2)

> **한 줄 요약** — 보스가 **BT + Timeline + Signal + 문자열 스킬 DB**로 얽혀 하드코딩하던 스킬들을, Part 1(몹)에서 만든 **AbilitySystem(AbilityData + Effect 조합)** 위로 이관했다. 스킬 로직/데미지를 씬 프리팹에서 걷어내 **데이터(Effect 조합)** 로 옮기고, 새 스킬은 대부분 **코드 0줄**로 만든다. Part 1 → [README.md](README.md).

| 축 | 이전(보스 전용·하드코딩) | 현재(통합 AbilitySystem) |
|---|---|---|
| 스킬 발동 | Signal → `BossSkillController.CastSkill("이름")` → 문자열 DB → prefab.Cast() | Signal → `CastAbility(AbilityData)` → `AbilityRunner` → `Effect[]` |
| "어떤 스킬" 결정 | BT index + Timeline 문자열, **이중 결정** | Timeline Signal이 AbilityData를 **직접 참조** (문자열 DB 제거) |
| 스킬 정의 | 스킬 = 클래스 1개(`BossSkillBase` 파생 6종) | 스킬 = Effect 블록 **조합(asset)** |
| 데미지 | 씬 프리팹에 하드코딩(`Lightning._damage`, `GenesisAttack=500`) | Effect가 `ctx.CasterStat`를 읽음 (보스 Stat·크리 연결, Attacker=보스) |
| 씬 참조(스폰/번개 지점) | `BossSkillContext`에 필드로 축적(god-context) | 캐스터 컴포넌트 + 인터페이스로 조회 (ctx 비대화 방지) |
| 새 스킬 추가 | 새 클래스 작성 | 대부분 **코드 0줄** (컨테이너 + payload 재사용 + 값) |

---

## 1. 배경 — 보스의 3중 얽힘

보스 전투는 세 도구가 **양방향·다중 타입**으로 대화해 얽혀 있었다.

```
BossMonsterController.BT (RandomNode) → UseSkill(i) → _director가 TimelineAsset[i] 재생
   → Timeline Signal → SignalReceiver → BossSkillController.CastSkill("이름")   ← 문자열 DB
       → prefab의 BossSkillBase.Cast(BossSkillContext)                          ← 스킬=클래스
           → 씬 프리팹이 데미지 하드코딩(Lightning / GenesisAttack)
```

문제점:
1. **이중 디스패치** — "어떤 스킬"이 BT index(어느 타임라인)와 Timeline 문자열(어느 스킬 프리팹)에서 **두 번 따로** 정해짐.
2. **문자열 DB** (`BossSkillController._skillList`) — 오타 런타임 에러, 정의로 점프 불가.
3. **보스 전용 강결합 컨텍스트** (`BossSkillContext`) — 스킬 추가마다 필드가 붙는 god-object (`_spawnPoints`, `_lightningPoints`, `_bossRelicController` …).
4. **데미지가 씬 프리팹에** — 보스 Stat/크리와 단절, Attacker가 VFX 오브젝트.
5. **스킬 = 클래스 1개** — 조합 불가(`BossNormalAttack`, `SummonMonsterSkill`, `GenesisSkill`, `RelicActivation`, `RelicAttack`, `ConvergingLightningSkill`).

---

## 2. 새 구조 — 단방향 3층

핵심은 **Timeline을 버리지 않는 것**이다. Timeline은 보스 연출에 옳은 도구다. 대신 Timeline이 하던 *게임플레이 결정*(문자열로 스킬 지정)을 걷어내고 **연출 + 타이밍 신호**로만 좁힌다.

```
BT (결정)            "무엇을 언제"  — 조건/페이즈/랜덤으로 어떤 패턴을 시작할지
  │  진입점 하나 + IsCasting(busy) 게이트 하나
  ▼
Timeline (연출)      "어떻게 보이나" — 애니/카메라/VFX + '타격 비트' Signal
  │  Signal → CastAbility(AbilityData)   ← 비트마다, AbilityData를 인스펙터에서 직접 배선
  ▼
AbilitySystem (기계) "실제 무슨 일" — AbilityRunner.TryCast → Effect[] (Stat 주도)
```

각 층이 아래 층만 알고 위는 모른다. **접착점 2군데만** 바뀌면 얽힘이 풀린다.

### 2-1. Signal 재배선 — 문자열 → AbilityData 직접 참조
Unity `SignalReceiver`의 반응(`UnityEvent`)은 `Object` 인자를 실을 수 있고 `AbilityData : ScriptableObject : Object`라 **에셋을 드래그로 꽂을 수 있다.**

```yaml
# 이전:  CastSkill,   Mode 5(string), m_StringArgument: "NormalAttack"
# 현재:  CastAbility, Mode 2(object), m_ObjectArgument: {NormalAttack_Ability.asset}
```
→ 문자열 DB(`BossSkillController`)와 보스 전용 컨텍스트(`BossSkillContext`)가 **통째로 제거**된다.

### 2-2. 보스는 캐스터가 된다
```csharp
public void CastAbility(AbilityData ability)   // SignalReceiver가 호출
{
    if (ability == null || _monsterCts == null) return;
    var target = Managers.Party.GetCurrentCharacter();
    if (target == null) return;

    var ctx = new AbilityContext {
        Caster = this, CasterGO = gameObject, CasterStat = Stat,
        Target = target.gameObject,
        TargetPoint = target.transform.position,
    };
    _abilityRunner.TryCast(ability, ctx, _monsterCts.Token).Forget();
}
```

---

## 3. 오늘 확립한 설계 원칙 (핵심)

### 3-1. "그릇만 교체" — 분리는 유지
보스는 이미 *연출(Timeline)/기계(BossSkillController)*로 관심사를 나눠뒀다. 그 분리는 옳았다. 바꿀 건 **기계를 담은 그릇**(문자열 DB + 프리팹 클래스 + 하드코딩 데미지 → `AbilityData` + `Effect`)뿐이다. Timeline의 역할은 손대지 않는다.

### 3-2. payload 재사용 vs 패턴 컨테이너 — "스킬=클래스" 회피
새 스킬마다 Effect를 만들면 다시 "스킬 = 클래스 1개" 안티패턴이다. Effect를 두 종류로 나눈다.
- **payload** ("무엇을") — `SpawnVFX`, `AreaStrike`, `SpawnProjectiles`, `SpawnDamageField`, `SummonMonsters`. **재사용**.
- **패턴/컨테이너** ("어디서/언제") — `RepeatEffect`(시간), `RadialBurstPattern`·`ScatterPattern`(공간), `SelectRandomChoice`·`BranchByChoice`(흐름). 컨테이너는 지점/타이밍만 정하고 **자식 payload에 위임**.

판단 기준(3축): **값이 다름 → asset / 로직이 다름 → 클래스 / 상태가 있음 → new 또는 소유자.**
예) 수렴 vs 확산 번개 = 같은 `RadialBurstPattern`에서 `startRadius`/`endRadius` **값만 스왑**. 새 클래스 아님.

### 3-3. 상태의 소유자
- **지속 장판(DoT tick·진입추적)** = 스폰된 **필드 오브젝트**가 소유 → Effect(`SpawnDamageField`)는 스폰+값주입만, 자신은 무상태.
- **비트를 넘어 유지되는 선택(렐릭 색)** = **캐스터**(`BossRelicController.CurrentChoice`)가 소유 → 두 시그널(선택/포격)이 같은 상태를 공유.

### 3-4. 씬 참조는 캐스터 컴포넌트 + 인터페이스로
`ScriptableObject`(Effect)는 씬 오브젝트를 참조 못 한다. 그래서 씬 결합(부착점·스폰포인트·렐릭 선택)은 **캐스터가 컴포넌트로 소유**하고 Effect는 `ctx.CasterGO`에서 **인터페이스로 조회**한다. `AbilityContext`에 필드를 추가하지 않는다(= `BossSkillContext` god-object 재현 회피).

| 개념 | 컴포넌트 | 인터페이스 |
|---|---|---|
| VFX 부착점 | `VfxAnchorSet` | (id 조회) |
| 소환 스폰포인트 | `SpawnPointSet` | `ISpawnPointProvider` |
| 소환몹 등록 | `BossMonsterController` | `ISummonRegistry` |
| 선택/분기(렐릭) | `BossRelicController` | `IChoiceHandler` |

### 3-5. `AbilityContext`는 보편 정체성만 (비대화 방지)
ctx = **누가(Caster·CasterGO·CasterStat) / 어디(Object·TargetPoint) / 무엇을(Target)** — 시전의 보편 정체성만. config는 asset, 씬참조는 캐스터 컴포넌트, 상태는 소유자로 보낸다. 그래서 코어는 이미 포화, 스킬이 늘어도 필드가 안 붙는다.

### 3-6. 배치 로직 공용화 — `Anchor`
"원점을 어디서 잡나"가 `SpawnVFX`에만 있어, 다른 배치 Effect가 offset을 각자 복제할 위험이 있었다. **`Anchor`(직렬화 타입)** 로 추출해 모든 배치 Effect가 한 필드로 공유한다.
- `Source`: `Muzzle / CasterRoot / TargetPoint / Named / CasterForward` + `localOffset·localEuler·forwardDistance`
- `Resolve(ctx, out pos, out rot)` 한 곳에만 배치 로직 존재. 새 앵커 종류는 여기 한 번만 추가 → 전 Effect 혜택.

---

## 4. 새 Effect 프리미티브 (오늘 추가)

| Effect | 분류 | 역할 | 상태 |
|---|---|---|---|
| `SpawnDamageField` | spawn | 지속 장판 프리팹 스폰 + Stat 데미지 주입 (tick은 필드가 소유) | 무상태 |
| `RadialBurstPattern` | pattern | 방사형 N방향 × 웨이브, 반경 보간(수렴/확산) | 무상태 |
| `ScatterPattern` | pattern | 원/도넛 영역에 N개 산포(면적 균일) | 무상태 |
| `SelectRandomChoice` | flow | `IChoiceHandler`로 N개 중 랜덤 선택 → 캐스터가 적용 | 무상태 |
| `BranchByChoice` | flow | 캐스터의 `CurrentChoice`로 자식 하나 실행 | 무상태 |
| `SummonMonsters` | spawn | `ISpawnPointProvider` 지점마다 소환 + `ISummonRegistry` 등록 | 무상태 |
| `Anchor` (지원 타입) | — | 배치 원점 공용 해결 | — |

payload/컨테이너 전부 `CreateRuntime() => this`(상태 없음, 공유 안전). 상태는 스폰된 오브젝트/캐스터가 소유.

---

## 5. 스킬 = 데이터 조합 (실제 이관 결과)

- **평타(번개 낙하)** — `[ SpawnVFX(Lightning, TargetPoint), AreaStrike(damage) ]`. **신규 코드 0줄** (몹이 쓰던 Effect 재사용). Lightning 프리팹의 `Lightning`(콜라이더+데미지) 컴포넌트 제거 → **이중 데미지 회피**, 풀링 유지.
- **Skill1(제네시스 장판)** — `[ SpawnDamageField(field) ]`. `GenesisAttack`은 필드에 유지(상태 소유), 하드코딩 500 → Stat 주입. 신규 클래스 1(범용).
- **Skill2(수렴 번개)** — `[ RadialBurstPattern{ 4방향, waves, startR→endR } → [Lightning] ]`. 번개 payload 재사용, 수렴은 **값(radius)**. **신규 코드 0줄** (컨테이너만 1회 작성).
- **Skill3(렐릭 분기 포격)** — 2비트: `[ SelectRandomChoice, SpawnVFX(Warning) ]` → (텔레그래프) → `[ BranchByChoice[ 빨강:ScatterPattern(중앙집중), 초록:ScatterPattern(외곽 70발) ] ]`. 색 상태는 `BossRelicController`가 소유, 번개 payload 재사용.
- **Skill4(소환)** — `[ SummonMonsters{ key, count } ]`. Map 스폰포인트를 씬이 `SpawnPointSet`에 주입, Effect는 인터페이스로만 접근.

---

## 6. 문제해결 (실제로 겪은 것)

| 증상 | 원인 | 해결 |
|---|---|---|
| 번개가 데미지 두 번 | 프리팹 `Lightning` 콜라이더(OnTriggerEnter) + `AreaStrike` OverlapSphere 중복 | 프리팹에서 `Lightning` 컴포넌트·콜라이더 제거, 데미지는 `AreaStrike`만 |
| 장판 데미지는 들어오나 파티클 안 보임 | 스폰만 하고 파티클 재생 안 함(옛 호출자 제거됨) + 풀 재사용 시 Play On Awake 미재생 | `SpawnDamageField`가 위치 후 `ps.Clear(true)+Play(true)` |
| 장판 데미지가 항상 500 | `GenesisAttack.ApplyDamage`가 `Amount=500` 하드코딩(serialize된 `_damage` 무시) | `Init(damage, attacker)`로 Stat 주입 |
| Skill3 경고 원과 포격 중심 불일치 | ScatterPattern `_center`가 `TargetPoint`(플레이어) + **월드** offset z=5 | 경고와 동일하게 `CasterForward(dist=5)`로 |
| `ActivateRelic` Effect가 보스 전용됨 | Effect가 `BossRelicController` 직접 참조 | `IChoiceHandler`로 일반화 — 선택은 Effect, 적용은 캐스터 |
| `AbilityContext`가 계속 커질 우려 | 스킬마다 씬참조를 ctx 필드로 넣고 싶어짐 | 보편 정체성만 ctx, 씬참조는 캐스터 컴포넌트 |
| 배치 offset이 Effect마다 복제될 위험 | `SpawnVFX`에만 앵커/offset 존재 | `Anchor` 공용 타입 추출, 4개 기존 에셋 마이그레이션(guid 보존) |
| 소환 스폰포인트가 씬(Map)에 있음 | SO는 씬 참조 불가 | `ISpawnPointProvider`(캐스터) + 씬이 Map 지점 주입 |
| 보스 사망 후 소환 잡몹 잔존 | `HandleDeadState`의 정리 로직이 주석 처리 | 치명타로 정리 복구(null·IsDead 가드) |
| `CastAbility` NRE 가능성 | `GetCurrentCharacter()`/`_monsterCts` null 미검사 | early-return 가드 추가 |

---

## 7. 결과 / 성과

- **보스 5스킬 전부 데이터 조합으로 이관** (평타·Skill2 등 상당수 **신규 코드 0줄** = 컨테이너 + payload 재사용 + 값).
- **전투 발동 진입점 3갈래(보스/플레이어/몹) → 단일 `TryCast`** (보스까지 편입, 플레이어 남음).
- **문자열 스킬 DB(`BossSkillController`) · 보스 전용 컨텍스트(`BossSkillContext`) · 스킬=클래스 6종 제거** (죽은 코드로 전환, 삭제 예정).
- **데미지가 보스 Stat/크리와 연결** (씬 프리팹 하드코딩 제거, Attacker=보스).
- **재사용 Effect 프리미티브 라이브러리 구축** — 이후 스킬/캐스터는 대부분 데이터 조합.
- 스크립트/에셋 **폴더·네이밍 정리** (Effects/Damage·Spawn·Pattern·Flow, Interfaces, Monster/Boss 계층).

---

## 8. 남은 작업 (TODO)

- [x] **플레이어 이관** — 완료. → [CharacterMigration.md](CharacterMigration.md)(Part 3). 12종 데이터 주도화, "누가 쓰든 같은 파이프라인" 성립.
- [ ] **죽은 코드 삭제** — `BossSkillController`/`BossSkillContext`/`BossSkillBase` + 파생 6종, 프리팹에서 컴포넌트 제거, `UseAttack()`/주석 블록.
- [ ] **크리티컬 경로** — `BuildOutgoingDamage(attacker)`(크리 롤) 도입해 Effect가 호출.
- [ ] **모든 보스 어빌리티 `Cooldown=0` 확인** — 한 타임라인이 같은 시그널 반복 시 블록 방지.
- [ ] (선택) `SummonMonsters._monsterKey`를 `string[]`로 확장(몹 종류 혼합).

---

## 관련 파일
- 캐스터/러너: `Assets/Scripts/Ability/{AbilityRunner, IAbilityCaster}.cs`, `Ability/Anchors/{VfxAnchorSet, SpawnPointSet}.cs`
- 데이터/실행: `Assets/Scripts/Data/Ability/{AbilityData, EffectData, Anchor}.cs`
- Effect: `Data/Ability/Effects/{Damage, Spawn, Pattern, Flow}/*.cs`
- 인터페이스: `Data/Ability/Interfaces/{IChoiceHandler, ISpawnPointProvider, ISummonRegistry}.cs`
- 캐스터 구현: `BossMonsterController`, `BossRelicController`
- Part 1(몹 이관): [README.md](README.md)
