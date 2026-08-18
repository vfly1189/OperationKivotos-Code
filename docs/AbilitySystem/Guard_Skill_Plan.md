# 가드 스킬 설계 계획 — 태그 기반 방어 + 에너지 회복

> 목표: **딜러 E 스킬 = 가드.** 발동 후 일정 시간 안에 (보스 등의) 공격을 받으면
> **데미지 0 + 에너지 회복.** 궁(Q)이 에너지 풀충전으로 발동되므로, "잘 막으면 궁이 빨리 찬다"는 루프를 만든다.
>
> 대전제: **새 시스템을 만들지 않는다.** 이미 있는 태그 시스템 / `StatusRunner` / `Health` 단일 피격 경로 /
> `CharacterStat.AddEnergy`를 조립한다. 새로 짜는 건 **방어측 한 조각(가드 판정)**뿐.

---

## 0. 현황 — 이미 있는 것 (재사용)

가드에 필요한 조각의 **대부분이 이미 존재**한다. 실측 근거:

| 조각 | 위치 | 상태 |
|---|---|---|
| **데미지 단일 관문** | `Health.TakeDamage(DamageInfo)` — 보스 공격(GenesisAttack/Lightning)·투사체·DoT가 전부 이 경로 | ✅ 있음. `IsInvincible`/`IsDead` early-out 자리 = 가드 개입 지점 |
| **지속시간 태그** | `StatusRunner.ApplyTimedTag(container, tag, duration)` — 시간 뒤 자동 해제 | ✅ 있음 |
| **태그 저장/판독** | `GameplayTagOwner.Owned`(엔티티별), `GameplayTagContainer.Has(tag)` (카운트형) | ✅ 있음 |
| **가드 태그 애셋** | `State.Guarding.asset` | ✅ **이미 생성됨** |
| **가드 적용 이펙트** | `GuardApplyStatusTag.asset` (`ApplyStatusTag`, scope=Self) — E가 캐스터에 가드 태그 부착 | ✅ **이미 생성됨** |
| **에너지 회복** | `CharacterStat.AddEnergy(float)` (공개). Q는 `CurrentEnergy >= MaxEnergy`일 때 발동 | ✅ 있음 |
| **역할 기반 E** | `RoleDataSO.eAbility` → `UseSkillE()` → `UseAbility(Ex)` → 데이터 주도 발동 | ✅ 있음 |

`ApplyStatusTag.cs` 주석에 **가드 유즈케이스가 이미 명시**되어 있다:
```
가드 = [ ApplyStatusTag(Self, State.Guarding), (선택) ApplyStatModifier(Self, Defense, +x) ]
```

### 결론: 공격측(E가 가드 태그를 단다)은 사실상 준비 완료. **빠진 것은 방어측 하나.**

> 지금 `Health.TakeDamage`는 가드 태그를 **읽지 않는다.** 그래서 가드 중에도 데미지가 그대로 들어간다.
> 이번 작업 = **"Health가 피격 순간 가드 태그를 확인하고, 있으면 데미지 0 + 에너지 회복 + 피드백"**을 추가하는 것.

---

## 1. 목표 동작

```
E 누름 (딜러)
  → GuardApplyStatusTag 이펙트: 캐스터에 State.Guarding 태그를 duration 초 부착 (StatusRunner가 만료 관리)
  → 이 duration 동안이 "가드 창(window)"

가드 창 안에 피격 (보스 공격/투사체/DoT → Health.TakeDamage)
  → Health가 "나 State.Guarding 보유?" 확인
  → 보유 시:  데미지 0  +  AddEnergy(가드보상)  +  가드 성공 피드백(VFX/SFX/"GUARD")
  → 미보유:   기존 데미지 처리 그대로
```

핵심: **가드는 상태(태그)로 표현**되고, **판정은 데미지 단일 관문(Health)에서** 이뤄진다. 새 흐름을 안 만든다.

---

## 2. 설계 — 방어측 한 조각

### 2.1 신규: `GuardReceiver` (캐릭터에 붙는 MonoBehaviour)

`Health`를 오염시키지 않기 위해(몬스터도 `Health`를 씀) 가드 로직은 **선택적 컴포넌트**로 분리한다.

```csharp
// 신규: Assets/Scripts/Ability/Guard/GuardReceiver.cs
public class GuardReceiver : MonoBehaviour
{
    [SerializeField] private GameplayTagSO _guardingTag;   // State.Guarding
    [SerializeField] private float _energyOnGuard = 20f;   // 가드 성공 시 에너지 회복량(튜닝)
    [SerializeField] private bool  _consumeOnHit = false;  // true=패링(1회 소모), false=창 유지(다중 가드)

    private GameplayTagContainer _tags;   // GameplayTagOwner.Owned
    private CharacterStat _stat;

    public event System.Action<DamageInfo> OnGuardSuccess;   // 피드백 구독점

    private void Awake()
    {
        _tags = GameplayTagOwner.EnsureOn(gameObject).Owned;
        _stat = GetComponent<CharacterStat>();
    }

    // 데미지 적용 직전에 Health가 호출. true 반환 시 Health는 데미지를 무효화한다.
    public bool TryGuard(in DamageInfo info)
    {
        if (_guardingTag == null || _tags == null || !_tags.Has(_guardingTag))
            return false;

        _stat?.AddEnergy(_energyOnGuard);      // 에너지 회복 (궁 게이지 충전)
        OnGuardSuccess?.Invoke(info);          // 피드백(VFX/SFX/"GUARD")

        if (_consumeOnHit) _tags.Remove(_guardingTag);   // 패링형이면 1회 소모
        return true;                           // 데미지 0
    }
}
```

### 2.2 변경: `Health.TakeDamage`에 가드 훅 1줄

```csharp
public void TakeDamage(DamageInfo damageInfo)
{
    if (IsInvincible || IsDead) return;

    // ── 추가: 가드 판정 (컴포넌트 없으면 null → 몬스터 등은 영향 없음) ──
    if (_guard != null && _guard.TryGuard(damageInfo))
        return;   // 데미지 무효 (에너지·피드백은 GuardReceiver가 처리)

    float finalDamage = Mathf.Max(damageInfo.Amount - DefenseValue, 1);
    // ... 이하 기존 그대로
}
```
- `_guard`는 `Awake`/`EnsureOn`에서 `GetComponent<GuardReceiver>()`로 캐시(선택적, 없으면 null).
- **몬스터·비딜러**엔 `GuardReceiver`가 없으니 `null` → 완전 무영향.

### 2.3 왜 이 구조인가

- **`Health`는 범용 유지**: 가드는 별도 컴포넌트, `Health`엔 훅 1줄. 몬스터 피격 경로 불변.
- **데이터 주도**: 가드 발동은 E 어빌리티 데이터(`GuardApplyStatusTag`)일 뿐. 지속시간·태그가 데이터.
- **기존 심(seam) 재사용**: 지속=`StatusRunner`, 저장/판독=태그 컨테이너, 에너지=`AddEnergy`, 발동=역할 기반 E.
- **단일 관문 판정**: 보스 신규 패턴이 `Health.TakeDamage`만 타면 **자동으로 가드 대상**이 된다.

---

## 3. 단계별 계획

### Phase A — 코어: 데미지 0 + 에너지 (리스크 낮음)
- `GuardReceiver.cs` 신규.
- `Health`에 `_guard` 캐시 + `TryGuard` 훅 1줄.
- 딜러 프리팹에 `GuardReceiver` 부착, `_guardingTag = State.Guarding` 배선.
- **완료 기준**: 가드 창 안에 보스 공격을 맞으면 HP 안 깎이고 에너지가 오른다. 창 밖엔 정상 피격.

### Phase B — 피드백 (리스크 낮음)
- `GuardReceiver.OnGuardSuccess` 구독 → 가드 이펙트(VFX/SFX), 데미지 숫자 대신 **"GUARD"/"막음" 표시**.
- 주의: 가드 시 `Health.OnDamageTaken`(데미지 토스트)은 **발화되지 않아야** 함(무효 경로에서 return하므로 자연히 안 뜸) → 대신 가드 전용 표시.
- **완료 기준**: 가드 성공이 시각/청각으로 명확히 구분된다.

### Phase C — 튜닝 & 역할 배선 (리스크 낮음)
- `duration`(가드 창), `_energyOnGuard`, E `Cooldown`을 실전 튜닝.
- 딜러 역할(`RoleDataSO`)의 `eAbility`가 `GuardApplyStatusTag` 기반 어빌리티인지 확인/배선.
- **완료 기준**: E→가드→피격→에너지→궁(Q) 루프가 체감된다.

### (선택·후속) 심화 — 필요할 때만
- **퍼펙트 가드/패링**: 가드 발동 직후 짧은 창(예: 0.2s)에 맞으면 보너스(에너지 2배·반격·시간정지). `_consumeOnHit`+별도 태그로 확장.
- **방향 가드**: 정면 각도 안의 공격만 가드(공격자 위치 vs 캐릭터 forward). `DamageInfo.Attacker`로 판정 가능.
- **가드 관통 공격**: 특정 보스 패턴은 가드 무시(태그 `Damage.Unblockable`을 공격측이 달고, `TryGuard`가 확인).

---

## 4. 신규/변경 파일 요약

```
신규:
  Assets/Scripts/Ability/Guard/GuardReceiver.cs      (가드 판정·에너지·피드백 이벤트)

변경:
  Health.cs        → _guard 캐시 + TakeDamage에 TryGuard 훅 1줄
  (딜러 프리팹)     → GuardReceiver 부착 + State.Guarding 배선
  (RoleDataSO/E)   → 딜러 E = GuardApplyStatusTag 어빌리티 확인

이미 존재(재사용, 신규 아님):
  State.Guarding.asset, GuardApplyStatusTag.asset, ApplyStatusTag.cs,
  StatusRunner.cs, GameplayTagOwner/Container, CharacterStat.AddEnergy
```

---

## 5. 리스크 & 확인 포인트

- **태그 스코프 = Self**: `GuardApplyStatusTag`의 `scope`가 반드시 **Self**여야 한다(캐스터 본인 컨테이너에 부착 → 본인 Health가 읽음). Party 스코프면 개별 Health가 못 읽음.
- **`IsInvincible`와의 순서**: 무적이면 조용히 무시(에너지·피드백 없음), 가드면 무효+보상. 둘은 별개 — 무적 체크가 먼저라 무적 중엔 가드 보상도 없음(의도대로면 OK).
- **패링형 소모 + StatusRunner 이중 해제**: `_consumeOnHit`가 태그를 지워도 `StatusRunner`가 만료 때 또 `Remove` 호출 → 카운트형이라 **안전(no-op)**. 단 다중 스택(E 연타로 카운트≥2) 시 1회 소모는 1스택만 제거함(엣지) — 스택 방지하려면 재부착 시 갱신 정책 고려.
- **DoT/다중 히트**: `GenesisAttack`의 도트는 틱마다 `TakeDamage` → 창 유지형이면 틱마다 가드(전부 무효), 패링형이면 첫 틱만. 의도에 맞게 `_consumeOnHit` 선택.
- **풀링 안전**: `GameplayTagOwner.OnDisable`/`StatusRunner.OnDisable`이 태그를 비움 → 스왑·풀 반환 시 가드 잔류 없음. ✅
- **가드 중 이동/공격 가능?**: 가드는 태그라 상태(Idle/Attack)와 독립 유지. "가드 켜고 평타"도 가능 — 원치 않으면 가드 태그를 게이트(BlockedTags)나 상태 연동으로 제한.

---

## 6. 착수 전 결정할 것 (내일)

| # | 결정 | 옵션 | 메모 |
|---|------|------|------|
| D1 | **가드 소모 모델** | 창 유지(다중 가드) / 패링(1회 소모) | "일정시간 내 피격 무효" 문구는 **창 유지**에 가까움. 패링은 심화(D2)로. |
| D2 | 퍼펙트 가드 보너스 넣나 | 예(짧은 창 반격/에너지2배) / 아니오 | Phase C 이후 선택 |
| D3 | 방향 가드 | 전방향 / 정면만 | v1은 전방향 권장(간단) |
| D4 | 튜닝값 | `duration`, `_energyOnGuard`, E `Cooldown` | 실전 조정 |
| D5 | 가드 중 다른 행동 제한 | 자유 / 이동만 / 완전 고정 | 태그 게이트로 조절 가능 |

**권장 v1**: 창 유지형(D1) · 퍼펙트 가드 없음(D2) · 전방향(D3) · 가드 중 행동 자유(D5). 최소 구현으로 "E→막기→에너지→궁" 루프부터 완성하고, 심화는 후속.

---

## 7. 포트폴리오 서술 포인트

- **데이터 주도 + 단일 관문**: 가드를 "새 시스템"이 아니라 **기존 태그/StatusRunner/단일 피격 경로의 조립**으로 구현. 새 코드는 `GuardReceiver` 하나 + `Health` 훅 1줄.
- **확장성의 실제 증거**: `Health.TakeDamage`가 단일 관문이라, 보스가 신규 패턴을 추가해도 그 데미지가 이 경로만 타면 **가드가 공짜로 적용**된다 — "확장 위해 추상화"가 아니라 **실제로 확장이 싼** 구조.
- **시스템 간 유기적 연결**: 입력(E) → AbilitySystem(태그 부착) → 피격(태그 판독) → 스탯(에너지) → 궁(Q) 게이트. 개별 기능이 아니라 **루프**로 물린다.
