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
        for (int i = 0; i < _count; i++)          // i �� '���� ����'  �ν��Ͻ� ���� �ƴ�
        {
            var b = Managers.Resource.Instantiate(_bulletPrefab, ctx.Object.position, ctx.Object.rotation);
            b.GetComponent<BulletController>()?.Init(
                new DamageInfo(ctx.CasterStat.Attack.Value, ctx.CasterGO, false), ctx.CasterGO);

            if (_interval > 0f && i < _count - 1)  // ������ �� �ڿ� �� ��ٸ�
            {
                bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(_interval),
                    cancellationToken: token).SuppressCancellationThrow();
                if (canceled) return;
            }
        }
    }
}
