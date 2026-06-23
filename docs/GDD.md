# Operation Kivotos — 게임 기획서 (GDD)

> 본 문서는 코드베이스에 **실제 구현된 내용을 기반으로** 역으로 정리한 종합 기획서입니다.
> 향후 개발 방향은 [ROADMAP.md](ROADMAP.md)를 참고하세요.
>
> - 최종 갱신: 2026-06
> - 작성 기준: 1인 개발 포트폴리오 프로젝트 (게임플레이 클라이언트 프로그래머 포지셔닝)

---

## 1. 게임 개요

| 항목 | 내용 |
|---|---|
| **타이틀** | Operation Kivotos |
| **장르** | 3D 쿼터뷰 액션 RPG |
| **개발 엔진** | Unity 6.3 LTS |
| **플랫폼** | PC (Windows) |
| **개발 형태** | 1인 개발 / 포트폴리오 |
| **타깃** | 블루 아카이브 IP 팬, 핵앤슬래시·파밍형 액션 RPG 선호 유저 |
| **플레이 형태** | 싱글 플레이, 4인 파티 편성 |
| **주요 패키지** | Addressables, UniTask, NPOI, Newtonsoft.Json, Timeline |
| **한 줄 콘셉트** | "4명의 학생을 지휘해 던전을 돌고, 무기를 강화하며 더 강한 보스에 도전하는 반복형 성장 액션" |

---

## 2. 게임 비전 & 핵심 재미 (Pillars)

1. **조작하는 손맛** — FSM 기반의 즉각적인 이동/공격/스킬 반응, 무기 타입별(연사·샷건·런처) 차별화된 타격감
2. **파티 운용** — 4인 파티를 실시간으로 전환(스왑)하며 상황에 맞는 캐릭터·스킬을 쓰는 전술성
3. **성장의 보상감** — 던전 → 드롭 → 강화 → 더 어려운 던전으로 이어지는 명확한 코어 루프
4. **연출** — 보스의 복합 패턴(Behavior Tree + Timeline)과 캐릭터별 궁극기 연출

---

## 3. 코어 게임플레이 루프

```
[메인 맵(Hub)]
   │  파티 4인 편성
   ▼
[포탈 진입] ──► 일반 던전 (몬스터 웨이브 클리어)
   │       └─► 보스 던전 (보스 처치)
   ▼
[전투 / 클리어 조건 달성]
   │  EXP · 크레딧 · 드롭 아이템 획득
   ▼
[메인 맵 복귀]
   │  NPC 상호작용 → 무기/장비 강화, 파티 재편성
   ▼
[더 높은 난이도 던전 재도전]  ← 반복
```

- **클리어 조건** (구현됨): 전체 몬스터 처치(`KillAllMonsterCondition`), 보스 처치(`KillBossCondition`)
- **실패 조건**: 파티 전멸(`PartyWipeCondition`)
- 사망자는 마을 귀환 시 **HP 1로 예외 부활**, 생존자는 체력 유지 (`CharacterStat.ReturnToTownState`)

---

## 4. 캐릭터 시스템

### 4.1 플레이어블 캐릭터 (현재 구현 풀)
Serika, Shiroko, Aris, Aru, Asuna, Hina, Hoshino, Iori, Karin, Nonomi, Toki, Ako, Noah 등

### 4.2 스탯 구조 (`CharacterDataSO` / `CharacterStat`)
- **기본 스탯**: HP, 공격력, 방어력 — 레벨당 성장: `Base + Growth × (Lv-1)`
- **고정 스탯**: 치명타 확률, 치명타 피해, 이동 속도, 에너지 회복, 최대 에너지
- **레벨링**: 캐릭터 개별 레벨이 아닌 **파티 레벨**에 종속 (`UpdateBaseStatsByPartyLevel`)
- **데미지 공식**: `최종 데미지 = max(공격력 − 방어력, 1)`, 치명타 시 `공격력 × 치명타피해` 배수 적용

### 4.3 스킬 / 궁극기 (EX 스킬)
- **에너지 차지식 궁극기**: 시간 경과 + 전투 행동으로 에너지 충전 → 가득 차고 Q 쿨타임이 0이면 **Q(궁극기)** 발동 가능
- **E 스킬**: 별도 쿨타임 기반 보조 스킬
- 궁극기 준비 완료 시 UI 글로우 연출 (`OnUltimateStateChanged`)
- 발동 시 `skillTimeline`(PlayableDirector) 재생 → 컷씬 종료 후 상태 전환

> ⚠️ **미정/개선 필요**: 현재 에너지 회복이 `CharacterStat.Update`에 `+10/초`로 하드코딩(`//테스트용`)되어 있어 정식 수치화 필요. → [ROADMAP 6월](ROADMAP.md) 참조

---

## 5. 전투 시스템

- **조작**: WASD 이동 / 좌클릭 일반공격 / Q·E 스킬 / F 상호작용 / I 인벤토리 / T 정보창
- **무기 타입 다형성**: 연사형, 샷건형, 런처형 등 — 공용 데미지 파이프라인 위에 발사/타격 로직만 분기
- **공격 흐름**: FSM(`CharacterStateMachine`) + 애니메이션 이벤트 기반 (`OnAttackEvent` → `PerformAttackAction` → `FireOneBullet`)
- **투사체**: 오브젝트 풀링으로 총알·이펙트 GC 최소화
- **피격 판정**: `IDamageable` 인터페이스 + `DamageInfo`(데미지량, 타격 위치, 공격자, 치명타 여부)

> ⚠️ **현재 한계**: 스킬·공격 로직이 캐릭터별 서브클래스(`SerikaCharacter`, `HoshinoCharacter` 등)에 하드코딩되어 신규 추가 비용이 큼. 타격 VFX는 머즐 이펙트 수준에 머무름. → 데이터 주도 스킬 시스템으로 일반화 예정([ROADMAP](ROADMAP.md))

---

## 6. 몬스터 & 보스

### 6.1 일반 몬스터
- 타입: 근접(Normal), 원거리(Ranged), AR / RL / Tank 계열
- **Behavior Tree 기반 AI** + 다형성으로 타입별 행동 확장

### 6.2 보스
- BT로 페이즈·패턴을 제어하고 **Timeline으로 복합 스킬 연출 동기화**
- 구현된 패턴 예: 메테오 낙하, 제네시스 공격, 집중 번개(Converging Lightning), 유물 소환/공격, 몬스터 소환
- 전용 HP 바 UI (`BossHPBar`)

---

## 7. 성장 & 경제 시스템

### 7.1 무기 강화 (`WeaponDataSO`)
- 무기는 **레벨별 스탯 테이블**(Lv.1~Max, 약 25단계)을 사전 계산해 보유
- 강화 시 공격력·HP·치명타 확률·치명타 피해가 **Flat 가산**으로 캐릭터 스탯에 합산 (`ApplyWeaponStats`)
- 강화 비용: `WeaponEnhanceCostTableSO`

### 7.2 장비 / 스탯 모디파이어 (호요버스식)
- 스탯 연산 구조: **Base → % 증감 → Flat 가산** (`StatModifier` / `StatModType`)
- 장비·무기 출처(source)별로 모디파이어를 붙이고 떼어 갱신

### 7.3 재화 & 드롭
- **크레딧**(주 재화, `WalletManager`), **EXP**, 강화 재료 아이템
- **드롭 테이블**(`DropTableDatabaseSO`)로 던전별 보상 풀 정의, 클리어 시 지급 (`DropManager`)

### 7.4 데이터 파이프라인
- 엑셀(NPOI) → ScriptableObject 파싱 → 캐싱(`DataManager`). 기획자가 엑셀 수치만 바꾸면 인게임 반영

---

## 8. 던전 / 콘텐츠 구조 (`DungeonDatabaseSO`)

- **그룹 → 난이도별 던전** 2단 구조 (`DungeonGroup` → `DungeonData`)
- 던전 타입: `Normal`(웨이브) / `Boss`
- 던전별 파라미터: 권장 레벨, 입장 비용(행동력), 클리어 EXP·크레딧, 드롭 테이블 ID

> ⚠️ **미정**: 입장 비용/행동력은 "기획 추가 대비용" 필드로 예약된 상태. 정식 도입 여부 결정 필요.

---

## 9. UI / UX

- **HUD**: 파티 4슬롯, HP/에너지 바, 궁극기 준비 표시, 보스 HP 바, 데미지 토스트
- **팝업 스택**: 렌더 오더 관리 기반 팝업 시스템(`UIManager`), 인벤토리(I)·정보창(T)
- **드래그 앤 드롭**: EventSystem 기반 인벤토리/장비 슬롯
- 캐릭터 선택 씬, 로딩 씬, 시작 씬(슬라이드쇼 / Tap to Start)

---

## 10. 사운드

- BGM / SFX 분리 관리(`SoundManager`)
- 캐릭터 음성: 편성(Formation In), 전투 진입(Battle In), 승리(Victory) 보이스 — Addressable 비동기 로드

---

## 11. 기획 결정이 필요한 미정 항목

| # | 항목 | 현재 상태 | 결정 필요 |
|---|---|---|---|
| 1 | 에너지/궁극기 수치 | 초당 +10 하드코딩 | 정식 차지 속도·전투 기여량 정의 |
| 2 | 행동력(입장 비용) | 예약 필드만 존재 | 도입 여부 |
| 3 | 캐릭터 차별화 | 코드엔 일부 존재, 문서 미정 | 캐릭터별 고유 역할(탱/딜/힐) 명세 |
| 4 | 밸런스 테이블 | 임시 수치 | 별도 밸런스 기획서 필요 |
| 5 | 엔드 콘텐츠 | 없음 | 반복 루프 장기 목표(최고 난이도, 도전 과제 등) |

---

## 부록: 핵심 폴더 구조

```text
📦 Assets/Scripts
 ┣ 📂 Managers/Core    # 전역 시스템 15종 (Resource, Data, UI, Pool, Save, Sound 등)
 ┣ 📂 Controllers      # 캐릭터/몬스터/투사체 제어
 ┃ ┣ 📂 Character      # BaseCharacter + FSM + Units/ (캐릭터별 서브클래스)
 ┃ ┗ 📂 Monster        # BT 기반 일반/보스 AI
 ┣ 📂 Data             # ScriptableObject 스키마 (Unit/Weapon/Dungeon/Stat/DropTable 등)
 ┣ 📂 Gameplay         # 클리어/실패 조건, 시퀀스 디렉터
 ┣ 📂 UI               # 팝업 스택, 인벤토리/장비 슬롯, 토스트, 이펙트
 ┣ 📂 Scenes           # 씬별 로직 (GameScene/NormalDungeon/BossDungeon/Select 등)
 ┗ 📂 Pool / Factory   # 오브젝트 풀링, 몬스터/장비 팩토리
```
