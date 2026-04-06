using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PartyDeathHandler
{
    private readonly PartyRegistry _registry;
    private readonly PartySwapController _swapController;

    private bool _isHandlingDeath = false;
    private CancellationTokenSource _deathCts;

    public event Action OnPartyWiped;

    public PartyDeathHandler(PartyRegistry registry, PartySwapController swapController)
    {
        this._registry = registry;
        this._swapController = swapController;
    }

    public async UniTaskVoid HandleCharacterDeathAsync(BaseCharacter deadChar)
    {
        // 현재 캐릭터가 아닌 대기 멤버 사망 시 무시
        if (deadChar != _registry.GetCurrent()) return;
        if (_isHandlingDeath) return;

        _isHandlingDeath = true;
        CancelDeathTasks();
        _deathCts = new CancellationTokenSource();

        // 사망 연출 대기 (2초)
        bool isCanceled = await UniTask
            .Delay(TimeSpan.FromSeconds(2.0f), cancellationToken: _deathCts.Token)
            .SuppressCancellationThrow();

        if (isCanceled)
        {
            _isHandlingDeath = false;
            return;
        }

        int nextAlive = _registry.FindNextAliveIndex();
        if (nextAlive != -1)
            _swapController.TrySwap(nextAlive, isForce: true);
        else
            OnPartyWiped?.Invoke();

        _isHandlingDeath = false;
    }

    public void CancelDeathTasks()
    {
        if (_deathCts == null) return;
        _deathCts.Cancel();
        _deathCts.Dispose();
        _deathCts = null;
        _isHandlingDeath = false;
    }
}