using System;
using System.Collections;
using UnityEngine;

public class PartyDeathHandler
{
    private PartyRegistry _registry;
    private PartySwapController _swapController;
    private MonoBehaviour _coroutineRunner;

    private bool _isHandlingDeath = false;

    public event Action OnPartyWiped; // 전멸 이벤트

    // 생성자 - 의존성 주입 (MonoBehaviour 필요!)
    public PartyDeathHandler(
        PartyRegistry registry,
        PartySwapController swapController,
        MonoBehaviour coroutineRunner)
    {
        _registry = registry;
        _swapController = swapController;
        _coroutineRunner = coroutineRunner;
    }

    public void HandleCharacterDeath(BaseCharacter deadChar)
    {
        if (deadChar != _registry.GetCurrent()) return;
        if (_isHandlingDeath) return;

        _coroutineRunner.StartCoroutine(CoAutoSwap());
    }

    private IEnumerator CoAutoSwap()
    {
        _isHandlingDeath = true;
        yield return new WaitForSeconds(2.0f);

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
}

