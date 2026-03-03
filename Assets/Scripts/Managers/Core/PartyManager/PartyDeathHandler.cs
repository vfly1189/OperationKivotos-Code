using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Threading;
using UnityEngine;

public class PartyDeathHandler
{
    private PartyRegistry _registry;
    private PartySwapController _swapController;

    private bool _isHandlingDeath = false;
    private CancellationTokenSource _deathCts; // 진행 중인 사망 태스크 제어용

    public event Action OnPartyWiped;

    // 생성자에서 MonoBehaviour 삭제
    public PartyDeathHandler(PartyRegistry registry, PartySwapController swapController)
    {
        _registry = registry;
        _swapController = swapController;
    }

    // [핵심] 밖에서 Fire-and-forget으로 부를 수 있게 UniTask 반환형 사용
    public async UniTaskVoid HandleCharacterDeathAsync(BaseCharacter deadChar)
    {
        if (deadChar != _registry.GetCurrent()) return;
        if (_isHandlingDeath) return;

        _isHandlingDeath = true;

        CancelDeathTasks(); // 기존 찌꺼기 초기화
        _deathCts = new CancellationTokenSource();

        // 2초 대기 (취소 가능하게)
        bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(2.0f), cancellationToken: _deathCts.Token).SuppressCancellationThrow();

        if (isCanceled)
        {
            _isHandlingDeath = false;
            return;
        }

        int nextAlive = _registry.FindNextAliveIndex();

        if (nextAlive != -1)
        {
            _swapController.TrySwap(nextAlive);
        }
        else
        {
            OnPartyWiped?.Invoke();
        }

        _isHandlingDeath = false;
    }

    // 씬 전환, 시스템 종료 시 호출하여 남은 딜레이 즉시 소멸
    public void CancelDeathTasks()
    {
        if (_deathCts != null)
        {
            _deathCts.Cancel();
            _deathCts.Dispose();
            _deathCts = null;
        }
        _isHandlingDeath = false;
    }
}

