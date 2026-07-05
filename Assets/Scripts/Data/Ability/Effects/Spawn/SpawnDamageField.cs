using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

[CreateAssetMenu(menuName = "Kivotos/Effect/SpawnDamageField")]
public class SpawnDamageField : EffectData, IEffect
{
    [SerializeField] private GameObject _fieldPrefab;   // Boss_Skill03_Attack 
    [SerializeField] private float _damageMul = 1f;

    public override IEffect CreateRuntime() => this;

    public UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        var go = Managers.Resource.Instantiate(_fieldPrefab, ctx.TargetPoint, Quaternion.identity);
        go.GetComponent<GenesisAttack>()
          ?.Init(ctx.CasterStat.Attack.Value * _damageMul, ctx.CasterGO);

        // 풀 재사용 대비: 위치를 잡은 '뒤' 명시적으로 재생 (SpawnVFX와 동일)
        // - Play On Awake는 풀 최초 생성 때만 돌아 재사용 시 재생이 안 될 수 있음
        // - Clear로 지난 생애(다른 위치)의 잔여 파티클을 제거 → 엉뚱한 위치에 뿜는 것 방지
        var ps = go.GetComponentInChildren<ParticleSystem>(true);
        if (ps != null)
        {
            ps.Clear(true);
            ps.Play(true);
        }
        return UniTask.CompletedTask;   
    }
}