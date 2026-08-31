# Operation Kivotos — 3D 쿼터뷰 액션 RPG

> ### 저작권 및 저장소 안내
>
> 본 저장소는 **소스 코드와 설계 문서만 포함**합니다.
> 캐릭터 · 배경 · 음원 등 모든 아트 에셋의 저작권은 **NEXON Games / Blue Archive**에 있으며,
> 본 저장소에는 포함되어 있지 않습니다.
>
> 비상업 개인 학습 목적의 프로젝트이며, 배포 및 상업적 이용 계획이 없습니다.
> 에셋이 제외되어 있어 Unity에서 바로 실행되지 않습니다 — 플레이 화면은 아래 **시연 영상**을 참고해 주세요.

---

## 1. 프로젝트 개요

| 항목 | 내용 |
| --- | --- |
| **장르** | 3D 쿼터뷰 액션 RPG (4인 파티 태그) |
| **엔진** | Unity 6.3 LTS |
| **개발 기간** | 2026.01 ~ 2026.04 (1인 개발) |
| **주요 패키지** | Addressables, UniTask, NPOI, Newtonsoft.Json |

**코어 루프**
4명의 캐릭터로 파티를 구성해 메인 맵(Hub)에 진입 → 이동 포탈을 통해 **일반 던전(몬스터 웨이브 클리어)** 과 **보스 던전**으로 이동 → 클리어 후 메인 맵으로 복귀해 NPC 상호작용으로 장비를 강화하는, **전투와 성장이 반복되는 구조**입니다.

- **시연 영상:** [YouTube — 전체 플레이 데모](https://youtu.be/yPBj7T_f7ds)
- **개발 일지:** [Velog 시리즈](https://velog.io/@vfly1189/series/Unity-블루아카이브-창작-프로젝트)

---

## 2. 주요 구현 기능

기능별 상세 구현 과정과 트러블슈팅 기록은 하이퍼링크(Velog)에서 확인하실 수 있습니다.

### 데이터 및 아키텍처 설계
- **매니저 시스템 설계** — 싱글톤 기반 15종 매니저의 책임 분리 및 초기화 데드락 방지 구조 구축 · [개발 일지](https://velog.io/@vfly1189/Blue-Archive-%EA%B8%B0%EB%B0%98-%EC%B0%BD%EC%9E%91-%ED%94%84%EB%A1%9C%EC%A0%9D%ED%8A%B8-%EA%B0%9C%EB%B0%9C-%EA%B8%B0%EB%A1%9D-2)
- **Addressables 비동기 로딩** — Task에서 UniTask로 마이그레이션해 비동기 로딩의 동기화 안정성 및 가독성 확보 · [개발 일지](https://velog.io/@vfly1189/Blue-Archive-%EA%B8%B0%EB%B0%98-%EC%B0%BD%EC%9E%91-%ED%94%84%EB%A1%9C%EC%A0%9D%ED%8A%B8-%EA%B0%9C%EB%B0%9C-%EA%B8%B0%EB%A1%9D-3)
- **리소스 수명 스코프** — Global / Scene 2버킷 구조를 수명 스코프 4종 + 레지스트리 refCount 자동 정산 구조로 재설계
- **Data-Driven 파이프라인** — NPOI와 ScriptableObject를 활용한 Excel-to-SO 빌드타임 베이킹

### 전투 및 AI 시스템
- **데이터 주도 AbilitySystem** — 캐스터마다 흩어진 발동을 `AbilityRunner` 단일 게이트로 통합, 스킬을 `AbilityData(SO) + Effect` 조합으로 구성
- **몬스터 AI (Behavior Tree)** — 다형성과 행동 트리를 결합한 확장성 높은 몬스터 제어 로직 · [개발 일지](https://velog.io/@vfly1189/Unity-%EC%B0%BD%EC%9E%91-5.-%ED%96%89%EB%8F%99-%ED%8A%B8%EB%A6%ACBehavior-Tree%EC%99%80-%EB%8B%A4%ED%98%95%EC%84%B1%EC%9D%84-%ED%99%9C%EC%9A%A9%ED%95%9C-%EB%AA%AC%EC%8A%A4%ED%84%B0-AI-%EC%84%A4%EA%B3%84-1%ED%8E%B8)
- **보스 패턴 제어** — 행동 트리와 Unity Timeline을 융합한 보스 스킬 연출 동기화 · [개발 일지](https://velog.io/@vfly1189/Unity-%EC%B0%BD%EC%9E%91-6.-%ED%96%89%EB%8F%99-%ED%8A%B8%EB%A6%ACBehavior-Tree%EC%99%80-%EB%8B%A4%ED%98%95%EC%84%B1%EC%9D%84-%ED%99%9C%EC%9A%A9%ED%95%9C-%EB%AA%AC%EC%8A%A4%ED%84%B0-AI-%EC%84%A4%EA%B3%84-2%ED%8E%B8)
- **Combat System** — 무기(연사 · 샷건 · 런처) 다형성 기반 공용 데미지 파이프라인 및 타격 로직

### 인게임 코어 및 최적화
- **Sector 기반 존 로딩** — 단일 필드를 Sector로 공간 분할해 플레이어가 위치한 Sector만 활성화 (Script 연산 ▼57% · Triangles ▼67%)
- **파밍 및 인벤토리 아키텍처** — 장비 스탯(Base · % · Flat) 구조화 및 인벤토리 데이터 파이프라인 · [개발 일지](https://velog.io/@vfly1189/Blue-Archive-%EA%B8%B0%EB%B0%98-%EC%B0%BD%EC%9E%91-%ED%94%84%EB%A1%9C%EC%A0%9D%ED%8A%B8-%EA%B0%9C%EB%B0%9C-%EA%B8%B0%EB%A1%9D-4)
- **UI 아키텍처** — EventSystem 기반 드래그 앤 드롭, 팝업 스택 렌더링 오더 관리
- **메모리 최적화** — 오브젝트 풀링 고도화로 총알 및 이펙트 생성 시 GC 호출 방지

---

## 3. 저장소 구조

```text
📦 Assets
 ┣ 📂 Scripts
 ┃ ┣ 📂 Data           # ScriptableObject 정의 및 데이터 모델
 ┃ ┣ 📂 UI             # 팝업 스택, 인벤토리/장비 슬롯, 상호작용 UI
 ┃ ┣ 📂 Managers       # 전역 시스템 (Resource, Data, UI, Pool, Save 등 15종)
 ┃ ┣ 📂 Controllers    # 플레이어/몬스터 제어, Behavior Tree
 ┃ ┣ 📂 Scenes         # 씬 진입/전환 로직
 ┃ ┣ 📂 Ability        # AbilityRunner, Effect 블록
 ┃ ┣ 📂 Gameplay       # 전투 · 스탯 · 상태이상
 ┃ ┣ 📂 Pool, Factory  # 오브젝트 풀링 / 생성
 ┃ ┗ 📂 Interface      # 공용 계약 (IDamageable, IAbilityCaster 등)
 ┃
 ┣ 📂 Editor           # Excel 임포터, 아틀라스 감사 도구, 리소스 디버그 윈도우
 ┗ 📂 ExcelData        # 기획 데이터 원본 (Character/Item/Monster/Dungeon/Drop)

📦 docs                # 설계 문서 및 실측 결과
📦 ProjectSettings     # Unity 프로젝트 설정
```

---

## 4. 설계 문서

구현 배경과 의사결정 과정은 `docs/`에 정리되어 있습니다.

- **리소스 시스템** — [`docs/ResourceSystem/Phase4_Result.md`](docs/ResourceSystem/Phase4_Result.md) · 수명 스코프 도입과 refCount 정산 결과
- **어빌리티 시스템** — [`docs/AbilitySystem/STATUS.md`](docs/AbilitySystem/STATUS.md) · 현재 상태 및 검증 기준 문서
- **비동기 파이프라인** — [`docs/AsyncResourcePipeline/README.md`](docs/AsyncResourcePipeline/README.md)
- **기획 문서** — [`docs/GDD.md`](docs/GDD.md)

---

## 5. 기본 조작법

| 동작 | 키 |
| --- | --- |
| 이동 | `W` `A` `S` `D` |
| 일반 공격 | 마우스 좌클릭 |
| 스킬 | `Q` `E` |
| 상호작용 | `F` |
| 인벤토리 | `I` |
| 정보 창 | `T` |

---

## 6. 빌드 및 의존성

본 저장소에는 아트 에셋과 외부 라이브러리 바이너리가 포함되어 있지 않아 그대로 빌드되지 않습니다.
Excel 파싱에 사용하는 **NPOI**는 라이선스 문제를 피하기 위해 DLL을 제외했습니다.
