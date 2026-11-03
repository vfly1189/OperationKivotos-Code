using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// 점마다 몬스터 소환 — 위치는 보통 SocketsPlacement(Spawn_1~N), 간격은 PointEmitter.
// 등록(ISummonRegistry)은 선택 — 시전자에 있으면 등록, 없으면 소환만.
public class SummonEvent : PatternEvent
{
    [SerializeField] private PointEmitter _emit;
    [SerializeField] private string _monsterKey;

    protected override UniTask FireAsync(PatternContext ctx, CancellationToken token)
    {
        ctx.owner.TryGetComponent<ISummonRegistry>(out var registry);
        return _emit.Run(ctx, p => Summon(p, registry, token), token);
    }

    private async UniTask Summon(Pose p, ISummonRegistry registry, CancellationToken token)
    {
        GameObject mon = await MonsterFactory.CreateMonsterByAddressableKeyAsync(
            _monsterKey, Managers.Context.CurrentDungeonID, p.position, p.rotation, token);
        if (mon == null) return;

        // 로드 도중 시전자가 죽었거나 회수됨 — 주인 없는 소환수를 남기지 않는다
        if (token.IsCancellationRequested) { Managers.Resource.Destroy(mon); return; }

        registry?.RegisterSummoned(mon);
    }
}
