using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

// 점마다 장판 설치 + 지속시간 뒤 회수. 피해(틱)는 나중에 — 지금은 놓고 치우기만.
public class FieldEvent : PatternEvent
{
    [SerializeField] private PointEmitter _emit;
    [SerializeField] private GameObject _field;

    [Tooltip("초. 0 = 이 Event가 회수하지 않음(프리팹이 스스로 회수)")]
    [SerializeField] private float _duration = 5f;

    [Tooltip("프리팹 자체 자세 보정 — 점의 방향(Placement)과 별개")]
    [SerializeField] private Vector3 _fieldEuler;

    [SerializeField] private DamageSpec _damage;
    [SerializeField] private HitShape _hit;

    [SerializeField] private float _tick = 0.5f;

    protected override UniTask FireAsync(PatternContext ctx, CancellationToken token)
    {
        DamageInfo packet = _damage.Pack(ctx);
        return _emit.Run(ctx, p => Place(p, packet, token), token);
    }

    private UniTask Place(Pose p, DamageInfo packet, CancellationToken token)
    {
        GameObject go = SpawnAt(_field, p.position, p.rotation * Quaternion.Euler(_fieldEuler));
        if (go != null && _duration > 0f) Lifetime(go, p, packet, token).Forget();
        return UniTask.CompletedTask;
    }

    // 장판 하나의 수명 — 여기서 기다리면 다음 점이 막히므로 따로 띄운다. 취소(사망 · 회수)면 바로 회수.
    private async UniTaskVoid Lifetime(GameObject go, Pose p, DamageInfo packet, CancellationToken token)
    {
        float tick = Mathf.Max(_tick, 0.05f);
        float end = Time.time + _duration;
        while (Time.time < end)
        {
            _hit.Apply(packet, p.position, null);   // 틱마다 새로 판정
            float wait = Mathf.Min(tick, end - Time.time);
            if (wait <= 0f || await UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: token)
                                           .SuppressCancellationThrow()) break;
        }
        if (go != null && go.activeSelf) Managers.Resource.Destroy(go);
    }
}
