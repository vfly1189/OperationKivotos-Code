# 플레이어 재설계 Phase 1 — 게이팅 단일화 (완료)

> **한 줄 요약** — 세 곳(`CharacterCombat` 평타레이트 · `CharacterStat` Q/E 쿨 · `AbilityRunner` 쿨)에 흩어져 있고
> 두 벌로 평행 존재하던 쿨 상태를, **`AbilityRunner` 한 권위**로 모았다. 쿨은 절대시각(IClock)으로만 존재 →
> `PartyManager.TickCooldowns` 삭제. 코스트(궁게이지)만 Stat에 남기고 러너가 위임 소비. UI(쿨 채움·궁 준비)는 러너를 폴링.
>
> 계획서: [Phase1_Plan.pdf](Phase1_Plan.pdf) · 선행: [Phase0_Result.md](Phase0_Result.md) · 청사진: [Player_Refactor_Blueprint.pdf](Player_Refactor_Blueprint.pdf)

---

## 무엇을 했나

### 1. AbilityRunner — admission / fire 분리 + 읽기 API

`TryCast` 한 덩이(게이트+쿨+이펙트)를 셋으로 쪼갰다. **몹/보스 호출부는 `TryCast`(=Commit+Fire)라 무변경.**

- `bool Commit(a, ctx)` — 쿨·태그 게이트 통과 시 **쿨만 시작**(이펙트 X). 입력 시점 상태 전이 결정용.
- `UniTask Fire(a, ctx, token)` — 캐스트타임 + 이펙트 + `GrantsTags` try/finally (무게이트).
- `UniTask TryCast(a, ctx, token)` — `Commit` 성공 시 `Fire`. (몹/보스/E 즉발용)
- **읽기 API**: `CooldownRemaining(a)` / `CooldownDuration(a)`. 쿨 상태는 `_cd: Dictionary<AbilityData,(float end,float dur)>`로 저장 — CDR로 실제 쿨이 base와 달라져도 채움비율이 정확.

### 2. 쿨 값의 출처 = CharacterDataSO (러너는 모름)

쿨 "값"은 `CharacterDataSO.QSkillCoolTime/ESkillCoolTime`에 그대로 두고, **캐스터가 `ctx.CoolDown`으로 건넨다**. 러너는
`ctx.CoolDown >= 0 ? ctx.CoolDown : a.Cooldown`만 본다 → 러너가 CharacterDataSO에 결합되지 않음. 몹은 안 건네므로 `a.Cooldown` 폴백.

- `BaseCharacter.SlotCooldown(slot)` = 슬롯 → DataSO 쿨값 매핑.
- 쿨 **상태**는 러너 인스턴스(캐스터별)가 소유 → 같은 E 에셋을 공유해도 캐릭터 간 쿨 안 섞임.

### 3. BaseCharacter — 입력=Commit, 비트=Fire

- `UseSkillQ` — 슬롯 가드 + **궁게이지 만충 검사** → `Commit`(쿨 입력 시점 시작) → `Stat.ConsumeUltimateGauge()` → `ChangeState(QSkillCutScene)`.
  이펙트는 이후 **Q_Skill 애니 `AbilityBeat` → `UseAbility(Q_Skill,beat)` → `Fire`**. (컷신→Q_Skill 흐름 유지, OnSkillEnter/OnCutsceneEnded 무변경)
- `UseSkillE` — `Commit` → `ChangeState(E_Skill)` → `Fire`(즉발). (UseAbility 안 거침 → 쿨 시작 보장)
- `UseAbility` — **Fire 전용**(비트 경로). 쿨/코스트 안 건드림.
- 읽기 파사드 — `SlotCooldownRemaining/Duration(slot)`, `IsUltimateReady`(에너지 만충 && Q 쿨 0).
- 평타 — 쿨 없이 **StateMachine으로 게이팅**(Attack 상태 재진입 차단 = 애니 길이가 발사 간격). `CharacterCombat` 소멸.

### 4. UI — 러너 폴링으로 이관

쿨이 절대시각(무tick)이라 "쿨 끝남" 이벤트가 없어졌다. → UI가 폴링.

- **쿨 채움** — `GameSceneCanvas.Update` → `ActiveCharacterHUD.UpdateCooldowns(BaseCharacter)` → 파사드 조회. (`SkillIconUI`는 `currentCool>0`일 때만 나눗셈 → 미발동 스킬 0 나눗셈 없음)
- **궁 준비 글로우** — 활성 HUD는 `UpdateCooldowns` 폴링에 `SetUltimateReady(bc.IsUltimateReady)` 추가. 벤치 멤버(`PartySlotUI`)는 `Update()`에서 `IsUltimateReady` 폴링 → 절대시각 쿨이 끝나는 순간 점등. (에너지 바·HP는 이벤트 유지)

### 5. 죽은 코드 정리

- **삭제 파일**: `CharacterCombat.cs`(+meta) — 평타레이트·Q/E 포워딩 전부 흡수됨.
- `CharacterStat`: `QSkillCoolTime`/`ESkillCoolTime`(Stat), `CurrentQSkillCoolTime`/`CurrentESkillCoolTime`, `TickCooldowns`, `TryUseSkillQ/E`, `CheckUltimateReadyState`, `_isUltimateReady`, `OnUltimateStateChanged` 제거. (`ConsumeUltimateGauge`·`AddEnergy`·에너지 regen·`OnEnergyChanged` 유지)
- `PartyManager.TickCooldowns` + `Managers.Update`의 호출 제거.
- `BaseCharacter`: `_combat`/`_attackRate` 필드·생성 제거. `AbilityRunner`: 미사용 `using` 제거.

---

## 검증 (2026-08-19 갱신 — 완료)

- **소스 레벨** — 죽은 심볼 전수 grep 후 잔여 참조 0 확인(`Stat.GetData().QSkillCoolTime`은 CharacterDataSO 값이라 의도적 유지). 프리팹·씬의 `CharacterCombat` 참조도 0.
- ✅ **에디터 컴파일 + 플레이 실측 완료 (2026-08-19)** — 이 구조가 적용된 상태로 정상 플레이 확인. 브랜치 `ability-phase1-gating`은 `main`에 머지됨(`b4b1295a`).
- **Q 이펙트는 여전히 미발사** — 어떤 `Exs`(Q) 클립에도 `AbilityBeat` 이벤트가 없고 `Slot:1` 배선은 Hoshino 하나(값은 평타 asset 플레이스홀더). **쿨·궁게이지·글로우는 이벤트 없이 정상 동작**하므로 게이팅 검증에는 영향 없음 → 연출 배선은 가치가 낮다고 판단해 **의도적 보류**.

## 남은 것 (트리거 조건부 — 순번 소화 대상 아님)

- **E 발동을 AbilityBeat로** — 지금 E는 즉발이고 즉발로 충분. **투사체/지연 발동 E가 실제로 생길 때** `SlotForState`에 `E_Skill→E_Skill` + 비트로 승격(= Phase 2).
- **CDR(쿨감)** — 쿨감 스탯을 실제로 도입할 때. 러너 `_cd`가 이미 dur를 저장하므로 `ctx.CoolDown = base × CDR` 한 줄.

> 현재 상태 요약은 **[STATUS.md](STATUS.md)** 가 기준.
