# 필드 엔티티 관리 (Field Entity)

> 시작: 2026-09-28 · 대주제 3 — "넓은 필드에서 몬스터·NPC를 어떻게 관리하는가"
> 이 폴더는 **새 출발점**이다. 이전 기록 [MonsterSimulation/](../MonsterSimulation/README.md)은 그대로 두고, 필요한 사실만 여기로 가져온다.

---

## 이 트랙이 답하려는 질문

**넓은 필드에서 누구를 살려두고, 얼마나 자주 돌릴 것인가.**

한 단어("필드 몬스터 관리")로 부르지만 장면마다 비용을 만드는 변수가 다르다.

| 장면 | 예 | 핵심 변수 |
|---|---|---|
| 연속된 일반 필드 | 로스트아크 일반 필드 | **넓이** — 어디를 켤까 |
| 좁은 아레나 대량 | 카오스 던전 | **밀도** — 켜진 걸 얼마나 싸게 돌릴까 |
| 오픈월드 · 반오픈월드 | 오픈월드, 스타레일식 맵 단위 월드 | **스트리밍** — 무엇을 메모리에 둘까 |

그래서 방식을 **층**으로 나눠 본다: ① 틱(품질) · ② 존재(GameObject) · ③ 로드(에셋) · ④ 레코드(데이터).
층마다 반경이 다르고, 방식들은 대부분 "어느 층의 반경을 무엇으로 정하느냐"의 차이다.

## 출발점

현재 코드: **Sector(박스 볼륨) 단위로 나눠, 현재 캐릭터가 안에 서 있는 Sector의 스포너를 켠다** (겹침 구간은 둘 다, `1872a610`).
([Sector.cs](../../Assets/Scripts/Managers/Core/Field/Sector.cs) ·
[SectorActivation.cs](../../Assets/Scripts/Managers/Activation/SectorActivation.cs) — 옛 SectorManager 판정을 ① 정책으로 옮긴 것 ·
[MonsterSpawner.cs](../../Assets/Scripts/Managers/Core/Field/MonsterSpawner.cs))

규모: Sector 5(30m × 91m 띠, 가운데 통로로만 이어짐) · 스포너 23 · 몬스터 363 (`BackStreetMap.prefab`), 가장 큰 Sector 119마리.
화면에 보이는 땅은 앞 6m · 뒤 3m · 좌우 최대 9m (`Game.unity` 카메라 기준 — 예전 "≈ 25m"는 틀린 가정).

문제 정의 (리쉬를 항상 적용한다는 전제) — 자세한 근거는 **[Sector_Problems.md](Sector_Problems.md)**:
1. **팝인/팝아웃** → 인접 섹터 필요 → 인접 표 작성·검수
2. **저작** — 새 맵마다 섹터 분할 + 스포너 소속 배치 + 인접 표
3. **유지 비용** — 안 보이는 곳도 섹터 모양·크기만큼 켜짐 (화면 밖 87%)
4. **순간 비용** — 켜지고 꺼질 때 크기만큼 한꺼번에 (S4 119마리)
5. **리쉬 제약** — 리쉬 거리가 섹터 폭보다 길 수 없음

1·3·4·5가 **섹터 크기 하나에 묶여** 있어 튜닝으로 풀리지 않는다 → 판정 방식(거리)을 바꾸는 비교의 근거.
경계 소실(따라온 몹이 사라짐)의 원인은 판정 단위가 아니라 **회수 규칙**이라, 해결은 정책과 무관한 리쉬다.

## 문서

| 문서 | 내용 |
|---|---|
| **[Approaches.pdf](Approaches.pdf)** ([HTML 원본](Approaches.html)) | 방식 지도 — 도식 9장. 세 장면 · 층 구조 · Sector 문제 · A 청크 · B 거리 · C 청크+거리 · 움직이는 엔티티 · D~H 기타 방식 · 비교표 · 장면별 조합 · 제안 |
| [01_Approaches.md](01_Approaches.md) | 위 PDF의 텍스트 요약 (검색·diff용) |
| **[02_Plan.md](02_Plan.md)** ([PDF](Plan.pdf) · [HTML](Plan.html)) | 실행 계획서 (Phase 1~3 완료분의 기록. **Phase 4~8은 03_Comparison_Plan으로 대체**). 확정 결정 · 몬스터 코드 현재 상태 · Phase 0~8 · 완료 조건 · 리스크 |
| [Phase1_Result.md](Phase1_Result.md) | Phase 1 결과 — 생명주기 최종 계약(입구 팩토리 / 출구 `Resource.Destroy`) · ResourceManager 로드/생성 분리 · HP바 풀링 · 해소 항목 |
| [Spawner_Authoring.pdf](Spawner_Authoring.pdf) ([HTML](Spawner_Authoring.html)) | 스포너 저작 방식 비교 → 방법 1(손 배치 + ID 표) 채택 |
| [Spawner_Structure.pdf](Spawner_Structure.pdf) ([HTML](Spawner_Structure.html)) | 스포너 구조 — 네 층 분리 · 슬롯 상태 기계 · 불변식 I1~I5 |
| [Spawner_ReadyAt_Gen.pdf](Spawner_ReadyAt_Gen.pdf) ([HTML](Spawner_ReadyAt_Gen.html)) | ReadyAt(알람 시각) · Gen(주문 번호) 개념 그림 |
| **[Phase3_Result.pdf](Phase3_Result.pdf)** ([HTML](Phase3_Result.html) · [MD](Phase3_Result.md)) | **Phase 3 결과 — SpawnerManager · Sector 연결 · 작업 중 만난 문제와 해결(파티 준비 순서 · 로딩 커버 BaseScene 버그) · 검증 · 남은 위험** |
| **[Sector_Problems.pdf](Sector_Problems.pdf)** ([HTML](Sector_Problems.html) · [MD](Sector_Problems.md)) | **Sector 문제 정의 — 측정 전 검증(Sector 판정 버그 수정) · 인접 섹터 검토 · 리쉬 전제 · 최종 문제 5개 · 섹터 크기 결합 · 비교 분석의 틀** |
| **[03_Comparison_Plan.md](03_Comparison_Plan.md)** ([PDF](Comparison_Plan.pdf) · [HTML](Comparison_Plan.html)) | **비교 계획서 — 현재 작업 순서의 기준 문서(02_Plan Phase 4~8 대체, Phase 0부터 다시 셈).** 서사(문제 → 진단 → 해결) · 후보 7개(⑦ CullingGroup 포함) · 판정 방법(탈락 → 우열) · 측정 항목 · 예측 박제 · Phase 0~6 |
| [Spotlight_Chunks.pdf](Spotlight_Chunks.pdf) ([HTML](Spotlight_Chunks.html)) | 스포트라이트 청크(플레이어 중심 청크 창) 분석 — 6개 방식 같은 자리 비교 · 실제 맵 시뮬레이션 · 교전 유지 규칙 · 거리 O(N) 오해 정정 |
| **[Predictions.pdf](Predictions.pdf)** ([HTML](Predictions.html)) | **측정 전 예측(0-6 박제)** — 원본 · 변형(섹터 균형) 두 배치에서 6개 방식 시뮬레이션 · 예측 문장 P1~P9 |
| [CullingGroup_Comparison.pdf](CullingGroup_Comparison.pdf) ([HTML](CullingGroup_Comparison.html)) | 예측 보강 — Unity CullingGroup을 ⑦로 추가. 거리 밴드는 ⑤와 결과 동일(차이 = 계산 위치 · 1프레임 지연 · 카메라 의존), 가시성은 탈락, 제자리는 틱 층 · P10~P14 · 켜기 반경 20m가 0.5m 짧다는 부수 발견 |
| [Chunk_Origin.pdf](Chunk_Origin.pdf) ([HTML](Chunk_Origin.html)) | 예측 보강 — 청크 원점 (0, 0) vs (−90, −45)로 ③ · ④ 재계산 + 원점 1m 전수(③ 400 · ④ 100). ⑤와의 결론은 원점 무관, ③ · ④ 수치는 크게 흔들림 → 측정 원점 (−90, −45) 고정 |
| **[Harness_Result.pdf](Harness_Result.pdf)** ([HTML](Harness_Result.html)) | **측정 기록(0-4 · 0-5)** — 하네스 3회 재현(이벤트 수 동일 · p50 2.6%) · 경로 ⓐ 7개 정책 실측 = 예측 ±1 · ① 기준선(119마리 스폰 66.8ms · 경계 왕복 680마리 · 화면 안 등장 9) · 스폰 1마리 +0.53ms · 풀 꺼내기 63% |
| [Worklog_2026-10-09.md](Worklog_2026-10-09.md) | 2026-10-09 작업 기록 — SectorManager 정리 · 교전 유지 · 계측 · 하네스 · 측정 결과 · **다음에 할 일** |
| **[Shadow_Result.pdf](Shadow_Result.pdf)** ([HTML](Shadow_Result.html) · [MD](Shadow_Result.md)) | **그림자 측정 결과(2-5)** — 실제 1 + 그림자 6으로 7개 정책을 한 플레이에서 비교 · 원본 · 변형 두 맵. 동등성 불일치 0 · 히스테리시스 위반 0 · 순위 예측과 같음 · 순간 비용 격차는 예측보다 큼(③ 68 vs ⑤ 17) · 떨림은 ③④ 칸 경계에서만 · P1~P14 대조 |
| [tools/](tools/README.md) | 분석 스크립트 — 맵 프리팹 → JSON(`parse_map.py`) · 그림자 로그 분석(`analyze_shadow.py`) |

## 현재 상태

- [x] 방식 지도 작성 (2026-09-28)
- [x] 범위·방향 확정 (2026-09-28) — 일반 필드만 · 메인 = Sector vs B(거리) · 확장 = B vs C(청크+거리)
- [x] 실행 계획서 작성 (2026-09-28)
- [ ] **Phase 0** — `03_Predictions.md` 예측 박제 (Before 영상은 Phase 4에서 하네스로, Phase 5 전 마감)
- [x] Phase 1 — 몬스터 생명주기 완성 (2026-09-29, 플레이 확인) → [Phase1_Result.md](Phase1_Result.md)
- [ ] ~~Phase 2 — 풀 용량 · 관측~~ — 생략 (2026-10-02 결정, 측정 때 감안)
- [x] Phase 3 — 스포너 재설계 · Sector 연결 (2026-10-02, 플레이 확인) → [Phase3_Result.md](Phase3_Result.md)
- [x] 측정 전 검증 · Sector 판정 버그 수정(`1872a610`) · Sector 문제 정의 (2026-10-02~03) → [Sector_Problems.md](Sector_Problems.md)
- [ ] 문제 정의 결과를 계획서에 반영 (거리 반경 재설정 · 리쉬 공통 전제 · 청크 활성 중간 단계 여부) — [Sector_Problems.md](Sector_Problems.md) 8절
- [x] 스포트라이트 청크 분석 · 비교 서사 확정 (2026-10-06) → [Spotlight_Chunks.pdf](Spotlight_Chunks.pdf)
- [x] 비교 계획서 작성 (2026-10-06) → [03_Comparison_Plan.md](03_Comparison_Plan.md) — 이후 작업은 이 문서의 Phase 0~6
- [x] 정책 이음새 `ActivationManager` + ① Sector · ② 인접 · 변형 맵 (2026-10-07, `f6cd9d64`)
- [x] ③ · ④ 스포트라이트 정책 `SpotlightActivation` (2026-10-07, 플레이 확인) — 측정 원점 (−90, −45) 고정 → [Chunk_Origin.pdf](Chunk_Origin.pdf)
- [x] ⑤ 거리 · ⑥ 스포트라이트 + 거리 · ⑦ CullingGroup 정책 + 활성화 범위 기즈모 (2026-10-08, `101bb363`, 플레이 확인)
- [x] 정책 선택 설정(GameScene 인스펙터) · 켜고 끈 로그 · 그림자 측정 (2026-10-08, `026bbfee`)
- [x] **2-5 동등성 검증 통과** — 원본 · 변형 두 맵 그림자 측정 (2026-10-08) → [Shadow_Result.md](Shadow_Result.md)
- [x] SectorManager 정리 (2026-10-09) — 판정은 ① `SectorActivation`이 이미 대체 → `SectorManager` 삭제, `Sector`는 영역 + 스포너 id만 남겨 `Field/`로 이동, BenchmarkTester F1/F2(전체 켜기 · 복귀)는 `ActivationManager.SetForceAll`로
- [x] 0-2 교전 유지 규칙 (2026-10-09, 플레이 확인) — 정책이 스포너를 꺼도 교전 중인 몹은 유예 목록에 두고 매 틱 확인, 교전이 끝나면 회수 · F3 = 리쉬 + 교전 유지 함께 끔
- [x] 0-4 계측 (2026-10-09, 플레이 확인) — `FieldMetrics`: 마커 9개를 프레임마다 CSV로 · 스폰/회수/유예 순간 화면 판정 · 풀 확장 · 분석 `tools/analyze_field.py`
- [x] 0-5 하네스 (2026-10-09, ① 개발 빌드 3회 — 이벤트 수 재현 · 시간 편차 p50 2.6% · p99 6.5%) — `HarnessRunner` F10 / `-harness` · 경로 v2(ⓐ 예측 경로 · ⓑ 끌기 2 · ⓒ 왕복 8) · `tools/run_harness.ps1`
- [x] 풀 꺼내기 비용 원인 · 수정 (2026-10-09, `8900c307`) — 원인은 SetParent가 아니라 `CurrentScene`의 매 호출 `FindAnyObjectByType`(꺼내기의 94%) → 씬당 1회 캐시, 스폰 1회 506 → 188μs
- [x] 정책 7개 × 3회 측정 (2026-10-09 ~ 10, 무인 하네스 `58f32b83`) → **[Policy_Result.pdf](Policy_Result.pdf)** ([HTML](Policy_Result.html)) — dt p50 = 1.307ms + 7.57μs × 살아 있는 몹, ⑤⑥⑦ 동률
- [x] 계측 기록 목록 묶음 저장 (2026-10-10) — 계측 자체 최대 4.7 → 0.16ms
- [x] **결정 (2026-10-10) — ⑤ Distance 채택** · Phase 4 · 반경 스윕 · 프레임 스폰 예산 생략 → [03_Comparison_Plan.md](03_Comparison_Plan.md) 8절
- [ ] **다음** — ⑤를 기본 정책으로 · 교체 조건 주석(5-2) → Sector 볼륨의 운명(5-3) → After 측정 · 예측 대조 · 영상(5-4 · 5-5) → 결과 문서 · 포트폴리오. 곁가지: Before 영상
- [x] 결정 (2026-10-07) — 켜기 반경 **20m 유지** · **⑦ CullingGroup 거리 밴드를 후보에 추가**(⑦′ 가시성은 시뮬레이션으로 탈락) → [03_Comparison_Plan.md](03_Comparison_Plan.md) 갱신

## 이전 기록에서 가져온 것 / 버린 것

**가져온 것** (사실·제약)
- 규모·화면 크기 수치 (MonsterSimulation/Comparison/01_Methods.md)
- 선행 조건: 몬스터 생명주기 계약 미배선, 풀 프리워밍 부족(풀 30 vs 최대 119) — 어떤 방식이든 먼저 성해야 함
  (MonsterSimulation/Audit/Lifecycle.md, Comparison/07_Prerequisites.md)
- 서버는 고려하지 않음(완전 싱글). 서버가 있으면 존재 층은 서버로, 품질 층만 클라에 남는다는 정리

**새로 잡은 것**
- 질문을 "몬스터 최적화 5가지 비교"에서 **"세 장면 × 네 층"** 으로 넓힘 (카오스 던전형·오픈월드형 포함)
- 청크 격자를 존재 판정뿐 아니라 **로드(리소스 시스템)·광역 판정(어빌리티)** 에 재사용하는 고리로 봄
- 몬스터 "레코드"와 "오브젝트" 분리(가상화)를 독립 항목으로 세움
