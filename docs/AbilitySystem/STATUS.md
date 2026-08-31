# AbilitySystem — 현재 상태 (단일 기준 문서)

> **최종 갱신: 2026-08-19** · 이 폴더의 다른 문서보다 **이 문서가 우선**한다.
> 다른 문서들은 각자 작성 시점의 스냅샷이며, 그 시점 이후 상태가 바뀐 부분은 여기에 기록된다.

---

## 한 줄

**Ability 트랙은 종료 상태다.** Phase 1(게이팅 단일화)이 `main`에 머지됐고, 이 구조가 적용된 상태로 **플레이 정상 동작을 확인했다**. 남은 항목은 전부 *순번 소화 대상이 아니라 트리거 조건부*다.

## 사실 확인 (2026-08-19 기준)

| 항목 | 상태 |
|---|---|
| 브랜치 | `main` = `origin/main` = `ability-phase1-gating` = **`b4b1295a`** (머지 완료) |
| 컴파일 | 정상 |
| 플레이 검증 | **완료** — 이 구조 적용 상태로 정상 플레이 확인 |
| 삭제 심볼 잔여 참조 | 0 (`CharacterCombat`·`TickCooldowns`·`TryUseSkillQ/E`·`CurrentQ/ESkillCoolTime`·`CheckUltimateReadyState`·`OnUltimateStateChanged`·`_attackRate`·`_isUltimateReady`) |
| 프리팹·씬의 `CharacterCombat` 참조 | 0 |
| UI 폴링 배선 | 완료 — `GameSceneCanvas.Update` → `ActiveCharacterHUD.UpdateCooldowns` → 파사드, 벤치는 `PartySlotUI` 자체 폴링 |

> ⚠️ 이전 로드맵 문서(2026-08-10)의 "브랜치 미머지·미검증"과
> [Phase1_Result.md](Phase1_Result.md)의 "⚠ 실측 필요"는 **작성 시점 기준이며 현재는 해소됐다.**

## 실물 인벤토리

- **Effect 부품(`IEffect` 구현)**: 15개
- **어빌리티 asset**: 54개 = `AbilityData` 19 + Effect asset 35 (별도로 `RoleDataSO` 4 · `GameplayTagSO` 4)
- **캐스터 3진영이 같은 러너를 공유**: 플레이어 / 일반몹(AR·RL·Tank) / 보스
- **보스 이관 효과**: `BossSkillBase` 파생 **6종 328줄** 소멸 (기반 `BossSkillBase` 19 + 디스패처 `BossSkillController` 88 포함하면 **8파일 436줄**)
- **조합 실증**: 몹 라이플 5연사(`RifleRepeatEffect` `_count:5,_interval:0.1`) ↔ 히나 8연사(`Hina_Rapid` `_count:8,_interval:0.05`) — 코드 차이 0줄

## 의도적으로 안 한 것 (미완이 아니라 판단)

1. **Q 이펙트 배선** — 어떤 `Exs`(Q) 애니 클립에도 `AbilityBeat` 이벤트가 없고, `Slot:1`이 배선된 건 Hoshino 하나뿐이며 그 값도 평타 asset(`Hoshino_Attack_Ability`)이 꽂힌 플레이스홀더다. **쿨·궁게이지·글로우는 이벤트 없이 정상 동작**하므로 구조 검증에는 영향 없음. 연출 배선은 포트폴리오 가치가 낮다고 판단해 보류.
2. **E(역할) 전 캐릭터 배선** — `_roleData`가 배정된 건 아비도스 4명(Shiroko=Breaker, Nonomi=Buffer, Hoshino=MainDealer, Serika=SubDealer). 나머지 8명은 미배정 = E 없음. 역할 시스템 동작 증명에는 4명으로 충분.
3. **GC/alloc 측정 — 축이 안 맞아 철회.** 이 시스템의 명제는 "발동 3곳→1곳 · 스킬=조합 · 새 스킬 코드 0줄"로 **변경 비용**에 관한 것이라, 캐스트당 힙 할당은 그 명제를 검증하지 않는다. 게다가 [`AbilityData.BuildRuntimeEffects()`](../../Assets/Scripts/Data/Ability/AbilityData.cs)가 **캐스트마다 `new List<IEffect>`를 판다** — [Aug_Plan.md](Aug_Plan.md)의 "캐스트당 할당 ≈ 0" 가설은 성립하지 않는다. 측정하면 포폴 숫자를 위해 실제 병목도 아닌 곳을 캐싱 최적화하게 되므로 하지 않는다. **런타임 비용 질문은 설계 근거(SO 불변·공유, 스냅샷 배달)로 답하고, 리스트 할당은 "알고 있으나 병목이 아니라 미최적화"로 답한다.**

## 재개 트리거 (전부 조건부 — 순번 아님)

| 항목 | 트리거 |
|---|---|
| **Phase 2** — E를 `AbilityBeat`로 승격 | 투사체/지연 발동이 필요한 E가 실제로 생길 때. 지금 E는 즉발이고 즉발로 충분 |
| **CDR(쿨감)** | 쿨감 스탯을 실제로 도입할 때. 러너 `_cd`가 이미 `dur`를 저장하므로 `ctx.CoolDown = base × CDR` 한 줄 |
| **`IAbilityCaster` 마커 → 역할별 소계약** | 실행 중 캐스터 질의가 필요한 pull-Effect(유도탄·실시간 락온·소환수 관리)가 등장할 때. 뚱뚱한 인터페이스 하나가 아니라 `IAimProvider` 등으로 쪼갠다 |
| **StatusSystem 깊이**(스택 정책 3종 등) | 이 시스템을 대표작으로 더 밀기로 결정할 때만 |

## 문서 읽는 순서

1. **이 문서** — 현재 상태
2. **서사·판단 근거** (특히 `IAbilityCaster` = 의도된 마커라는 결정 A). *로드맵 항목 0·1은 완료됨*
3. [Phase1_Result.md](Phase1_Result.md) · [Phase0_Result.md](Phase0_Result.md) — 구현 상세
4. [README.md](README.md) · [BossMigration.md](BossMigration.md) · [CharacterMigration.md](CharacterMigration.md) — 이관 기록
5. [Aug_Plan.md](Aug_Plan.md) — **보류**(위 3번 사유)
