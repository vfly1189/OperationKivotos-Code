using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// 한 총구에서 N발을 부채꼴(각도 spread)로 동시에 발사한다. (샷건/산탄)
// SpawnProjectiles(직선 단발/연사)와 '로직'이 다른 payload — 발사 기하가 다르다.
// 캐릭터마다 만들지 않는다: 부채꼴 쓰는 캐릭터는 이 클래스를 공유하고 값(_count/_spreadAngle/_bulletPrefab)만 바꾼다.
[CreateAssetMenu(menuName = "Kivotos/Effect/SpreadProjectile")]
public class SpreadProjectiles : EffectData, IEffect
{
    [SerializeField] private GameObject _bulletPrefab;
    [SerializeField] private int _count = 5;          // 펠릿 수
    [SerializeField] private float _spreadAngle = 20f; // 전체 퍼짐 각(도)

    public override IEffect CreateRuntime() => this;

    public UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        // 시작 각(왼쪽 끝) → 균등 간격. _count==1이면 정면 단발.
        float startAngle = -_spreadAngle * 0.5f;
        float step = _count > 1 ? _spreadAngle / (_count - 1) : 0f;

        // 부채꼴 기준 방향 = 캐스터 몸통(조준). 총구 본 방향/애니 흔들림에 휘둘리지 않도록.
        Quaternion baseRot = ctx.CasterGO.transform.rotation;

        for (int i = 0; i < _count; i++)
        {
            float angle = startAngle + step * i;
            Quaternion rot = baseRot * Quaternion.Euler(0f, angle, 0f);

            var b = Managers.Resource.Instantiate(_bulletPrefab, ctx.Object.position, rot);
            b.GetComponent<IProjectile>()?.Init(
                ctx.CasterStat.BuildOutgoingDamage(ctx.CasterGO), ctx.CasterGO);
        }

        return UniTask.CompletedTask;
    }
}
