using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// 프리팹 안에 이미 있는 오브젝트 켜기 / 끄기 — 위치가 필요 없다.
// _choiceKey가 있으면 memo의 그 값 번째 하나만 _active로, 나머지는 반대로 (예: 유물 빨강 · 초록 중 하나)
public class ActivationEvent : PatternEvent
{
    [SerializeField] private GameObject[] _targets;
    [SerializeField] private bool _active = true;

    [Tooltip("비우면 전부. 적으면 ChooseEvent가 memo에 적은 값으로 하나만 고른다")]
    [SerializeField] private string _choiceKey;

    protected override UniTask FireAsync(PatternContext ctx, CancellationToken token)
    {
        if (string.IsNullOrEmpty(_choiceKey))
        {
            foreach (var t in _targets)
                if (t != null) t.SetActive(_active);
        }
        else if (ctx.memo != null && ctx.memo.TryGetInt(_choiceKey, out int pick))
        {
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] != null)
                {
                    _targets[i].SetActive(i == pick ? _active : !_active);
                    
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ActivationEvent] {name}: memo에 '{_choiceKey}' 없음 — 같은 Task 앞쪽에 ChooseEvent가 있는지 확인");
        }

        return UniTask.CompletedTask;
    }
}
