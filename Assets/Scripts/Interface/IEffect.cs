using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public interface IEffect
{
    UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token);
}
