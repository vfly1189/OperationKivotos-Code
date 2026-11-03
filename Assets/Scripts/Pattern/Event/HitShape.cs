using System;
using System.Collections.Generic;
using UnityEngine;

// "누구에게" — 그 점, 그 순간에 물리 엔진에 한 번 묻는다. 공격 쪽엔 콜라이더 없음.
// 무상태: 패킷과 중복 제거 집합은 부르는 쪽(Event)이 넘긴다 → 같은 Event가 겹쳐 Fire돼도 안전.
[Serializable]
public class HitShape
{
    [SerializeField] private float _radius = 1f;
    [SerializeField] private LayerMask _mask;            // 맞을 쪽 레이어 (Player)

    // Apply는 동기 → 겹쳐 불려도 한 호출 안에서만 쓰므로 static 공유가 안전
    private static readonly Collider[] s_buffer = new Collider[16];
    private static readonly HashSet<IDamageable> s_local = new();

    // alreadyHit : 여러 점에 걸친 중복 제거(Strike = Fire 단위). null이면 이번 호출 안에서만(Field 틱).
    // 반환 : 이번에 맞힌 수
    public int Apply(in DamageInfo packet, Vector3 center, HashSet<IDamageable> alreadyHit = null)
    {
        int n = Physics.OverlapSphereNonAlloc(center, _radius, s_buffer, _mask,
                                              QueryTriggerInteraction.Collide);
        if (n == s_buffer.Length)
            Debug.LogWarning($"[HitShape] 버퍼 가득 참({n}) — 일부 대상 누락 가능");

        // 한 대상이 콜라이더를 여러 개 가질 수 있으므로 호출 안에서도 항상 중복 제거
        HashSet<IDamageable> seen = (alreadyHit != null) ? alreadyHit : s_local;
        if (alreadyHit == null) s_local.Clear();

        int hits = 0;
        for (int i = 0; i < n; i++)
        {
            Collider col = s_buffer[i];
            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null || !seen.Add(target)) continue;

            DamageInfo copy = packet;                    // 구조체 → 대입이 곧 복사
            copy.HitPoint = col.ClosestPoint(center);    // 판정 쪽은 HitPoint만 채운다
            target.TakeDamage(copy);
            hits++;
        }
        s_buffer.AsSpan(0, n).Clear();                   // 콜라이더 참조를 남기지 않음
        return hits;
    }

#if UNITY_EDITOR
    public void DrawGizmo(Vector3 center)
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(center, _radius);
    }
#endif
}