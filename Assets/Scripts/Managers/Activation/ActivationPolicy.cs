using System.Collections.Generic;
using UnityEngine;

public abstract class ActivationPolicy
{
    public abstract void Init(ActivationLayout layout);

    // current = 지난 프레임에 켜진 스포너(읽기만). 히스테리시스 정책이 "지금 켜져 있나"를 묻는 데 쓴다.
    public abstract void SelectSpawners(Vector3 playerPos, HashSet<int> current, HashSet<int> result);

    // 씬 Clear 때 1회. 엔진 자원(CullingGroup 등)을 쥔 정책만 덮어쓴다.
    public virtual void Clear() { }

#if UNITY_EDITOR
    // 씬 뷰 확인용 — 이 정책의 활성화 범위(구역 · 칸 창 · 켜기/끄기 원)를 그린다.
    public virtual void DrawGizmos(Vector3 playerPos) { }
#endif
}
