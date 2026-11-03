using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// 0 ~ count-1 중 하나를 골라 memo에 적는다 — 위치가 필요 없다.
// 읽는 쪽: ActivationEvent(_choiceKey) · PatternTask 조건(_ifKey). 고르기와 분기를 떼어 두면 Event 안에 if가 없다.
public class ChooseEvent : PatternEvent
{
    [SerializeField] private string _key = "choice";
    [SerializeField] private int _count = 2;

    protected override UniTask FireAsync(PatternContext ctx, CancellationToken token)
    {
        ctx.memo?.SetInt(_key, Random.Range(0, _count));
        return UniTask.CompletedTask;
    }
}
