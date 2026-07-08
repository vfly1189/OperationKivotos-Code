using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

[CreateAssetMenu(menuName = "Kivotos/Effect/SpawnProjectile")]
public class SpawnProjectiles : EffectData, IEffect
{
    public GameObject _bulletPrefab;

    [SerializeField] public int _count = 1;
    [SerializeField] public float _interval = 0.1f;

    public override IEffect CreateRuntime() => this;
    public async UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        for (int i = 0; i < _count; i++)        
        {
            // 위치는 총구, 방향은 '캐스터 몸통'(=조준 방향). 총구(_firePoint)는 무기 본에 붙어
            // 몸통과 다른 각으로 돌아가 있거나 발사 애니로 흔들리므로 회전 기준으로 쓰면 안 된다.
            var b = Managers.Resource.Instantiate(_bulletPrefab, ctx.Object.position, ctx.CasterGO.transform.rotation);
            b.GetComponent<IProjectile>()?.Init(
                ctx.CasterStat.BuildOutgoingDamage(ctx.CasterGO), ctx.CasterGO);

            if (_interval > 0f && i < _count - 1)  
            {
                bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(_interval),
                    cancellationToken: token).SuppressCancellationThrow();
                if (canceled) return;
            }
        }
    }
}
