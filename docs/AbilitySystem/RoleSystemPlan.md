# 역할 시스템 구현 계획 (Implementation Plan)

> 대상: [RoleSystemDesign.md](RoleSystemDesign.md)의 역할/E/전투 루프를 코드로 구현.
> 원칙: 새 시스템 최소화 — 기존 자산(Stat 합산·AbilitySystem·파티 인프라) 위에 얹는다.

---

## 실사 요약 — 현재 코드 상태

| 항목 | 상태 | 근거 |
|---|---|---|
| **Attribute 합산** | ✅ **완성** | `Stat.cs`: base + Flat/PercentAdd + Source 추적 + dirty 캐싱. 무기가 이미 사용 |
| 좌클릭 평타 | ✅ 데이터 주도 | `BaseCharacter.TryUseAbility(0)` → `AbilityRunner.TryCast` |
| Effect 조합 | ✅ 완성 | `AbilityData.BuildRuntimeEffects` / `EffectData.CreateRuntime` |
| 파티 인프라 | ✅ 존재 | `PartyManager` / `PartyCharacterActivator` / `PartySwapController` |
| **E / Q** | ❌ **레거시** | state machine + timeline 경유(`TryUseSkillE/Q` → `E_Skill` 상태). 어빌리티 아님 |
| **GameplayTag** | ❌ 없음 | 태그 시스템 부재 |
| **Role 개념** | ❌ 없음 | `SchoolDataSO`는 `characters[]`만, role 필드 없음 |
| 인코딩 | ⚠️ | `AbilityRunner.cs` 주석 깨짐(`��`) — 리팩터 김에 정리 |

**핵심 시사점**: 포트폴리오 최강 카드(Attribute 합산)가 이미 있음.
이번 작업 = "새 시스템 구축"이 아니라 **E를 어빌리티로 이관 + Tag 추가 + 기존 Stat에 버프/방깎 얹기**.

---

## 구현 현황 (2026-07-10)

- ✅ **Phase 0** — `ERole`/`RoleDataSO`/`CharacterDataSO.role`
- ✅ **Phase 1** — E-cast 배선(Model 1): `BaseCharacter._roleData`/`_eAbilityOverride` → `_eAbility = override ?? 역할기본`, `UseSkillE`가 `TryCast(_eAbility)`, `TryUseAbility(int)→TryCast(AbilityData)` 추출, Hoshino 빈 셸, E 쿨타임 미적용 버그 수정
- ✅ **상태 토대** — `StatusRunner`(지속 modifier 자동해제, 파티스코프=영속 컨테이너 소유) + `ApplyStatModifier`(stateless Effect: EStatType·값·StatModType·duration·`ETargetScope`) + `ETargetScope`
  - 버프(Party 공격력↑)·가드(Self 방어력↑) 동작 확인
- ✅ **표식(브레이커)** — `BulletController._onHitEffects`로 명중 시 `ApplyStatModifier(scope=Target)` 실행 → 적 방어력↓ (딜러 자동 이득)
- ✅ **부수 개선** — UI_Info 스탯 실시간 갱신(`Stat.OnChanged`)+캐릭터 교체 시 갱신 / 쿨타임 오프필드 진행(`Managers.Update`→`PartyManager.TickCooldowns`)
- ⏳ **남음** — Q 궁극기 편입(Q_Skill 애니 이벤트→`OnQSkillEvent`→`TryCast(_qAbility)`), 태그(State.Guarding/Marked), Hoshino .cs 삭제, StatusSystem 깊이(스택/DoT/tick)

**Stat 리팩터** ([StatRefactor.md](StatRefactor.md)): 1(경제)·2(UI) 완료, 3+(Health) 보류(#2 비차단).

---

## Phase 0 — Role 토대 (데이터)
- **신규**: `ERole` enum(Dealer/SubDealer/Buffer/Breaker), `RoleDataSO`(역할 → E `AbilityData` + 역할 태그)
- **수정**: `CharacterDataSO`에 `ERole role` 추가
- **산출물**: 4개 RoleData 에셋, 12캐릭에 role 지정
- **왜**: "캐릭터 = 개별데이터 + 역할데이터" 조립 구조의 뼈대

## Phase 1 — E를 AbilitySystem으로 이관 + 역할 부여 ★서사 핵심
- **수정**: `BaseCharacter.UseSkillE()` → 레거시 상태전환 대신 역할 E `AbilityData`를 `TryCast`
- 캐릭터 Init/활성화 시 `role → RoleData.eAbility` 해석·보유
- **삭제**: Hoshino E 서브클래스 → "딜러류 가드 = 공용 데이터"로 흡수
  (`.cs` 삭제 전 프리팹 `m_Script`를 BaseCharacter로 재지정 — 함정)
- **결정 필요**: E 쿨타임을 `AbilityRunner`(이미 쿨다운 보유)로 통합 vs `CharacterStat.ESkillCoolTime`(UI 구동) 유지 → UI 배선 확인 후
- **왜**: 전투 캐릭터 100% 데이터화 = "발동경로 3→1"에 이어 마지막 특수 케이스 제거 (서사 완성)

## Phase 2 — GameplayTag (Lite)
- **신규**: `GameplayTag`(계층 문자열 or SO), `TagContainer`(HashSet + 계층 매칭), `IAbilityCaster`에 노출
- **신규 Effect**: `AddTagEffect` / `RemoveTagEffect`
- **적용**: 가드 → `State.Guarding`(기존 `IsInvincible` 연동), 표식 → 대상 `State.Marked`
- **왜**: 태그 실사용 2곳 확보 = 시스템 정당화

## Phase 3 — 버프/방깎 Effect (기존 Stat 재사용)
- **신규 Effect**: `ApplyStatModifierEffect` — 대상 `Stat.AddModifier(new StatModifier(..., source: 효과 인스턴스))`
- **버퍼 E** = 파티 대상(`PartyRegistry` 쿼리) 공격력 PercentAdd 버프
- **브레이커** = **on-hit** 대상 방어력 음수 modifier + `Marked` 태그
  (표식은 발동형이 아니라 명중 시 부여 → 투사체/데미지 경로에 훅 필요)
- **왜**: Attribute 양방향 합산 데모 — 새 아키텍처 0, 기존 `Stat.cs` 활용

## Phase 4 — StatusSystem (지속/만료) = 깊이 정점
- **신규**: 타임드 효과 러너(캐스터별) — 만료 시 `RemoveAllModifiersFromSource(effect)`로 버프/방깎 자동 해제 + 태그 제거. 스택 정책(수·리프레시)
- **왜**: duration/periodic/stack = "표준 구현" 약점을 깊이로 메움

## Phase 5 — 선택 확장
- Q 궁극기 어빌리티 편입(Timeline Signal → CastAbility)
- 저스트가드 카운터(타이밍 판정 + `State.Broken` 부여)

---

## 의존 순서

```
Phase 0 (Role 데이터)
   └─ Phase 1 (E 이관·부여) ── Hoshino 삭제
        └─ Phase 2 (Tag) ── 가드/표식 상태
             └─ Phase 3 (버프/방깎) ── Attribute 데모
                  └─ Phase 4 (StatusSystem) ── 지속·스택
```

## 착수 전 확인할 리스크 3

1. **E 쿨타임/애니메이션 배선** — `E_Skill` 상태·`ESkillCoolTime`·UI 얽힘 (Phase 1)
2. **표식 on-hit 훅** — 표식은 명중 시 부여 → 투사체/데미지 경로 진입점 (Phase 3)
3. **버프 타겟팅** — `PartyRegistry`가 파티원 쿼리 제공 여부 (Phase 3)

**추천 착수점**: Phase 0 → 1. 여기까지만으로 "역할 부여 + Hoshino 삭제 + 100% 데이터화" 서사 완성.

---

## 선행 작업 — Stat 리팩터 (부분 완료·나머지 보류)
[StatRefactor.md](StatRefactor.md): 1단계(경제 축출)·2단계(UI 축출) 완료. 3단계+(Health 추출)는
**보류** — blast radius 크고 **#2 선행조건 아님**(버프/방깎은 현재 `Stat.AddModifier`로 동작).
→ Stat 리팩터를 더 밀지 않고 이 문서의 Phase 0부터 #2 착수.
