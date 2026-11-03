# 전투 행동 설계 — CombatAbility

> 포트폴리오 파트 제목: **전투 행동 설계 : 언제, 무엇을, 어떻게 처리하는가?**
> 한 줄: 같은 레퍼런스(GAS)를 두 번 봤다 — 처음엔 이름을, 두 번째엔 구조를.

## 서사 (2026-10-24 확정 — A안, 실제 경위)

| 단계 | 내용 | 결과 |
|---|---|---|
| ① 하드코딩 | 보스 스킬마다 전용 스크립트(BossSkillBase 파생 6종) | 새 보스 = 처음부터 |
| ② 첫 시도 | GAS의 **이름**(태그 · 게이팅)만 가져오고 층 구분 없이 Effect 조립 | 짧은 공격엔 동작, 보스엔 선택 자리 없음 · 4단 중첩 |
| ③ 둘째 시도 | 내 기준(재사용 · 새 보스 코드 0줄)으로 재설계 — Pattern · Action · Event · Placement | 보스는 해결, 보스 전용 → 공격 시스템이 둘 |
| ④ GAS를 다시 | 이번엔 **구조**(층 경계) — 선택은 대상별, 흐름 · 결과 · 연출은 공용 | 진행 중 |
| 증명 (예정) | 플레이어 · 몹 · 보스가 같은 HitEffect 공유 + 두 번째 보스(텍스처 반전) 코드 0줄 | |


## 현재 상태

- **④ 1단계 구현 중** (2026-10-27) — `Assets/Scripts/SkillSystem/`
  - 구조: BMC(언제 · BT) → MPS(무엇을 · 슬롯 원본, Awake에 Grant) → SkillSystem(할 수 있나 · 실행 · 신호 창구) → Pattern → Action(막음) → Task(태그 + Event 목록) → Event(반응 · 데미지는 각자)
  - 결정: Task는 Action 범위(구독/해제 없음) · 끝남은 `PatternEnded` 이벤트(SS가 BT 용어를 모름) · Context는 매번 바뀌는 것만 · Selector는 상속 없이 하나(기믹 리스트는 선택)
  - 아직 안 돎 — **다음 = [03](03_SkillSystem_Structure.pdf) 6절 P0 다섯 개**(TryPick · 신호 입구 → SendTag · PatternTask `[Serializable]` · 대상 null 체크 · 취소 토큰)
- 증명 항목은 전부 예정 — 문서의 "예상" 표는 구현 후 실제 값으로 교체

## 문서

| 파일 | 내용 |
|---|---|
| [01_GAS_Basics.pdf](01_GAS_Basics.pdf) | GAS 입문 — 보스 방사형 번개로 용어 10개 · 12단계 상호작용 · ②③ 대응표 (5쪽) |
| [02_GAS_Comparison.pdf](02_GAS_Comparison.pdf) | ③과 GAS 층별 비교 + 개선안 A~F (5쪽) — ④ 이전에 쓴 것이라 "③ 개선" 관점, 내용은 ④ 1~3단계와 같음 |
| [03_SkillSystem_Structure.pdf](03_SkillSystem_Structure.pdf) | ④ 구현 중 점검 (10-27) — BMC · MPS · SS · Pattern 구조도 · 실행 순서 · 평가 · 구현 목록 P0~P2 · GAS 층별 비교 (8쪽) |
| [04_Placement_Explained.pdf](04_Placement_Explained.pdf) | Placement 설명 (10-30, 설계안) — 기준점 + 모양 · Pose/방향 · 출처 3가지 · PatternContext · 타겟을 언제 읽나 · 경고→타격 고정(memo) · GAS Targeting 대응 (5쪽) |
| [05_Pattern_Event_Structure.pdf](05_Pattern_Event_Structure.pdf) | Pattern Event 구조 (10-31, 구현 · 컴파일 확인 · 플레이 미확인) — 전체 흐름 · Event 5개(상속 1단 + PointEmitter 조합) · 간격/묶음 · 기준점 4 × 모양 4 · 패턴 4개 조립 · 패턴 3 실행 순서 · 바뀐 파일 · 남은 일 (6쪽) |
| [06_Final_Structure.pdf](06_Final_Structure.pdf) · [.md](06_Final_Structure.md) | **최종 구조 (10-31, 커밋 b7660195 · 플레이 확인)** — 층 지도 · 한 패턴의 일생 · 의존 방향 · Context · Event/Emitter · Placement · 프리팹 배선 · 실제 패턴 6개 · 취소 경로 · 계약 8 · 확장 표 · GAS 대응 · 남은 일 (9쪽) |
| [07_Damage_Pipeline.pdf](07_Damage_Pipeline.pdf) | 데미지 파이프라인 (11-04) — 읽기 · 판정 · 계산 · 보이기 분리 · 세 방법 비교 · 패킹 시점 · 콜라이더 vs 쿼리 · 정할 것 3개 (4쪽) |
| [08_SkillSystem_Overview.pdf](08_SkillSystem_Overview.pdf) · [.md](08_SkillSystem_Overview.md) | **전체 구조 (11-04, 커밋 8c071058 · fb55dfaf · 13b82158 · 플레이 확인)** — 보스 · 일반몹 공용 · 층 지도 · 한 패턴의 일생 · 같은 SS 다른 호출자 · 데미지 · 신호 입구 둘 · 이관 기록(RL .cs 0) · 끊기 · AbilitySystem · SkillSystem · GAS 대조 · 남은 일 (8쪽) |

HTML 원본은 같은 이름으로 옆에 있다. PDF는 Chrome headless로 인쇄 (절대경로 필수).

## 이전 기록 (다른 폴더)

- `docs/Boss/` — ③ 둘째 시도의 설계 · 비교
- `docs/AbilitySystem/` — ② 첫 시도(AbilityData · Effect · 태그 · Phase 1 게이팅)
