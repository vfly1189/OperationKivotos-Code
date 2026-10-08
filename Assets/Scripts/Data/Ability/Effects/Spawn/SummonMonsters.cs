using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

// 캐스터가 제공하는 스폰포인트들에 몬스터를 소환한다.
// 씬 결합(스폰포인트·등록)은 전부 인터페이스로 가려 이 Effect 는 보스/맵을 전혀 모른다.
//   - 어디에  : ISpawnPointProvider (캐스터 컴포넌트, 씬이 주입)
//   - 등록    : ISummonRegistry (선택 — 있으면 등록, 없으면 소환만)
// 상태 없음 → CreateRuntime() => this.
[CreateAssetMenu(menuName = "Kivotos/Effect/SummonMonsters")]
public class SummonMonsters : EffectData, IEffect
{
    [SerializeField] private string _monsterKey = "Droid_Helmet_RL";
    [SerializeField] private int _maxCount = 0;     // 0 = 스폰포인트 수만큼
    [SerializeField] private float _interval = 0f;  // 소환 간 간격(연출용)

    public override IEffect CreateRuntime() => this;

    public async UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        if (!ctx.CasterGO.TryGetComponent<ISpawnPointProvider>(out var provider)) return;
        var points = provider.GetSpawnPoints();
        if (points == null || points.Count == 0) return;

        ctx.CasterGO.TryGetComponent<ISummonRegistry>(out var registry);  // 선택

        int n = _maxCount > 0 ? Mathf.Min(_maxCount, points.Count) : points.Count;
        for (int i = 0; i < n; i++)
        {
            if (token.IsCancellationRequested) return;
            if (points[i] == null) continue;

            GameObject monster = await MonsterFactory.CreateMonsterByAddressableKeyAsync(
                _monsterKey, Managers.Context.CurrentDungeonID, points[i].position, points[i].rotation, token);

            if (monster != null) registry?.RegisterSummoned(monster);

            if (_interval > 0f && i < n - 1)
                if (await UniTask.Delay(TimeSpan.FromSeconds(_interval),
                    cancellationToken: token).SuppressCancellationThrow()) return;
        }
    }
}
