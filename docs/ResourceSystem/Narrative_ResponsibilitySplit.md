# 리소스 시스템 — 해제 책임의 이관 (포트폴리오 서사)

> Unity Addressables 위에 **scope 기반 RAII 수명 관리**를 얹어, "에셋마다 수동 해제"를
> "수명 경계에서 일괄 자동 회수"로 바꾼 리팩터.
> 상세 여정·실측치: [Journey_Full.md](./Journey_Full.md) · [Portfolio_Summary.md](./Portfolio_Summary.md)
> 도식 PDF: `ResourceLifetime_Narrative.pdf`

---

## 0. 한 문장

> Addressables는 "load ↔ release 짝맞춤"을 **호출자에게 위임**한다. 나는 그 짝맞춤을
> **Scope 수명에 바인딩**해 호출자에서 걷어냈고, 짝맞출 지점을 *O(load 호출 수) → O(수명 경계 수)* 로
> 압축했다. **수동 규칙을 구조적 불변식으로 바꾼 것이다.**

---

## 1. 출발점 — 하드코딩 일괄 로딩

이전 프로젝트(유니티 이전 + 유니티 초반)에서는 리소스를 **시작 시 일괄 로딩**하거나 경로·이름을
**하드코딩해서 그때그때** 가져왔다. 리소스가 적을 땐 단순해서 유리하지만, 규모가 커지면
**모든 리소스를 메모리에 상주시키는 것 자체가 성립하지 않는다.** "필요할 때 올리고 안 쓰면 내린다"는
수명 관리가 없으면 대규모에서 무너진다.

## 2. 1차 개선 — ResourceManager + 2버킷(Global/Scene)

`ResourceManager`를 통한 요청/Instantiate 구조를 잡고, 수명을 **Global(게임 전체) / Scene(씬 단위)**
두 종류로 나눴다. 여기서 실제 문제가 드러났다.

> **수명의 종류가 2개로 고정**되어, 애매한 것이 전부 "안전한 쪽 = Global"로 도피해 영구 상주했다.

대표적으로 **팝업 UI** — "팝업이 열려 있는 동안만"이라는 수명을 담을 그릇이 없어 Scene(씬 내내) 또는
Global(게임 내내)로 샜다.

## 3. 설계 질문 — 해제를 자동화할 수 없나?

Unity 공식 문서(Addressables Memory Management)의 기본 규칙:

> *"mirror every call to a load function with a call to a release function."*

즉 **호출↔해제 수동 짝맞춤이 정배**다. (Addressables도 내부 refCount는 갖지만, 그 increment/decrement
**짝맞춤 책임을 호출자에게 위임**한다. 이 수동 짝맞춤이 곧 누수의 근원이다.)

여기서 던진 질문: **호출은 사람이 하되, 해제는 한곳에서 자동으로 관리하면 되지 않을까?**

## 4. 해결 구조 — Manager ↔ Scope ↔ Registry (scope 기반 RAII)

```
호출자 ──(key + 수명)──▶ ResourceManager ──▶ ResourceScope ──▶ ResourceRegistry ──▶ Addressables
        요청/Instantiate만      파사드          수명별 소유 장부      refCount·핸들 단일 진실
```

| 층 | 역할 | 자료구조 |
|---|---|---|
| **ResourceManager** | 외부 유일 진입점. 요청/Instantiate | (파사드) |
| **ResourceScope** | 수명 하나당 스코프 하나. 소유 key 기록. Dispose 시 일괄 반납 | `HashSet<ResourceKey>` |
| **ResourceRegistry** | 살아있는 핸들의 단일 진실. refCount 0에서만 실제 Release | `Dictionary<key, Entry{Handle, refCount}>` |

**설계 3원칙**
1. 핸들 소유는 **레지스트리 한 곳** — 스코프는 티켓만 쥔다. "누가 Release하나" 문제 소멸.
2. 스코프 내 중복 로드 = refCount 1 (`HashSet.Add` 반환값). refCount = "소유한 서로 다른 스코프 수".
3. **취소 ≠ 해제** — 토큰은 기다림 중단만. 수명은 스코프 소유 → use-after-release 구조적 불가.

**수명 계층 (2개 → N개 enum 확장):**
`Global`(영구) · `Scene`(씬) · `Party`(파티 유지, 씬 전환 생존) · `Popup`(스택 있는 동안).

## 5. 급소 — "그럼 Dispose는 누가 부르나?"

이 구조의 급소는 스스로 던진 질문이다. **"해제를 자동화했다지만 Scope.Dispose()는 어디선가 호출돼야 하지 않나?"**

맞다. 그리고 그게 요점이다. **해제 책임이 사라진 게 아니라, 호출자(수백 곳)에서 도메인 경계(몇 곳)로 이관됐다.**

| | 짝맞춤 위치 | 개수 |
|---|---|---|
| raw Addressables | 모든 load 호출 지점 | **O(load 호출 수)** |
| 이 구조 | 수명 경계 (씬 전환·팝업 스택 0·파티 해체) | **O(수명 경계 수)** |

### 반자동화의 정확한 의미

"반자동"은 자동/수동이 반씩 섞인 게 아니라 **책임의 층이 나뉜** 것이다.

| 책임 | 누가 | 자동/수동 |
|---|---|---|
| "이 수명이 언제 끝났나" 판단 | 도메인 로직 (스택 카운터, 씬 전환 훅) | **수동** — 경계를 코드로 선언 |
| "그 안의 에셋들을 실제로 놓기" | Scope.Dispose → Registry refCount 정산 | **자동** — 사람 관여 없음 |

→ **경계는 수동 선언, 회수는 자동 실행.**

## 6. 사례 — Popup 수명 (생성/해제/안전망)

Popup 스코프의 수명을 **"팝업 스택의 0↔1 전이"** 에 묶었다.

- **생성** — 스택이 비어 있다 첫 팝업이 열리는 순간 `CreateScope(Popup)` — `UIManager.cs:134`
- **해제(주 경로)** — 마지막 팝업이 닫혀 스택이 다시 빔 → `DisposeScope(Popup)` — `UIManager.cs:202`

중첩 팝업 여러 개는 스코프 하나를 공유하고, 스택이 완전히 빌 때 딱 한 번 회수된다.

**안전망(2차 경로)** — 팝업을 열어둔 채 씬을 떠나면? 씬 전환 시퀀스가 `UI.Clear()` →
`CloseAllPopupUI()`로 스택을 강제로 비우며 마지막에 `DisposeScope(Popup)`에 도달 — `SceneManagerEx.cs:91`.

> **Popup 스코프는 두 경계에서 회수된다: (1) 유저가 정상적으로 닫을 때, (2) 안 닫고 씬을 떠날 때.
> 어느 쪽으로 나가도 새지 않는다.**

## 7. 검증 — "좋아졌다"가 아니라 숫자로

- **씬 스코프 누수 계측** — 씬 회전 직후 `AssertSceneHandlesCleared`로 이전 씬 핸들 전부 반납 검사
  (`SceneManagerEx.cs:88`) → 누수 0.
- **재로드 churn** — refCount 공유로 왕복 시 재로드 flat.
- **파생물 수명** — 아틀라스 클론 스프라이트까지 핸들 해제 시 `OnReleased` 훅으로 함께 파기 →
  Addressables가 관리 못 하는 파생물 누수도 차단.
- **취소 정산** — 로드 중 취소/실패 시 `RollbackLoad`로 방금 더한 ref만 되돌려 유령 참조 방지.
- **메모리 실측** — 상세 수치는 [Portfolio_Summary.md](./Portfolio_Summary.md) §2 참조.
  ⚠️ 에디터 측정이므로 **절대값 주장 금지, 증감·비율만** 인용.

## 8. 절제 — 안 한 선택

- **UI 루트 DDOL 대공사 → 기각.** 계측상 누수 0이 근거. 문제 없는데 갈아엎지 않았다.
- **풀 티켓은 일부러 Scope 밖.** 풀 원본 핸들을 스코프로 잡으면 씬 스코프 Dispose 때 함께 날아가
  위험 창이 생긴다. 유일하게 수동 1:1 짝(`AcquirePoolRef`/`ReleasePoolRef`)으로 격리 —
  자동화의 예외를 스스로 인지하고 좁게 가뒀다.

---

## 9. 포트폴리오 포지셔닝 (객관적 판단)

- **배치:** 유니티 헤드라인 **#1 = AbilitySystem(게임플레이)**, **#2 = 리소스 시스템(엔지니어링 성숙도)**.
  이 서열은 "게임 만드는 사람 + 코드베이스 책임질 사람" 순서라 옳다.
- **역할:** 이건 대표작이 아니라 **성숙도 뒷받침 챕터**다. 채용자가 원하는 "기존 코드를 안 망가뜨릴 사람"의 증거.
- **명명 주의:** "반자동화"는 구두 직관용. 문서엔 **scope-based RAII / scoped ownership**라고 정식 용어를
  붙인다 — RAII를 "발명"한 것처럼 프레이밍하면 감점, "Addressables 위에 RAII 수명 관리를 얹었다"면 깊이의 증거.
- **한 줄로 축약 금지:** "load/release 짝맞춤"이라는 관찰 자체는 새 정보가 아니다(Addressables 써본 사람은 다 앎).
  가치는 관찰이 아니라 **실행 + 계측 + 엣지 처리 + 절제**에 있다. 한 줄은 TL;DR 입구로만.
- **레벨:** 강한 주니어 ~ 미들 초입 신호. 과대포장(시니어 아키텍처) 금지.

### 방어 질문 3종

| 질문 | 답 |
|---|---|
| **"Addressables가 이미 refCount인데 왜 또?"** | Addressables refCount는 **핸들 단위 수동 짝맞춤** — 호출부가 Release를 '기억'해야 하고 그 기억의 실패가 누수였다. 스코프는 **기억을 구조(수명 단위 자동 정산)로 대체**한 것. |
| **"본인이 만든 문제(2버킷)를 본인이 고친 것 아닌가?"** | 맞다, 숨기지 않는다. 2버킷은 초기 설계였고 **계측으로 한계 발견 → 개인 주의력이 아니라 구조로 재발 차단**. 레거시 개선은 실무의 일상. |
| **"1인 프로젝트에 오버엔지니어링 아닌가?"** | 측정치가 필요성을 증명. 반대로 DDOL 대공사·프레임 예산 스트리밍은 **측정 근거로 제외** = 필요한 것만 했다는 증거. |

---

## 부록 — 핵심 파일

- `Assets/Scripts/Managers/Core/Resource/ResourceManager.cs` — 파사드 + 스코프 회전 + 아틀라스/계측
- `Assets/Scripts/Managers/Core/Resource/ResourceScope.cs` — 수명 티켓(HashSet) + Dispose 일괄 반납
- `Assets/Scripts/Managers/Core/Resource/ResourceRegistry.cs` — Entry{Handle, refCount} + RollbackLoad
- `Assets/Scripts/Managers/Core/UIManager.cs` — Popup 스코프 0↔1 경계
- `Assets/Scripts/Managers/Core/SceneManagerEx.cs` — 씬 전환 회전 + 누수 assert + 안전망
