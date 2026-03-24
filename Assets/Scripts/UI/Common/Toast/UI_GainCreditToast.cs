using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

[RequireComponent(typeof(CanvasGroup))]
public class UI_GainCreditToast : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _amountText;
    [SerializeField] private CanvasGroup _canvasGroup;

    private int _accumulatedAmount = 0;
    private CancellationTokenSource _hideCts;

    private void Awake()
    {
        _canvasGroup.alpha = 0f;
    }

    public void AddAmount(int amount)
    {
        _accumulatedAmount += amount;
        _amountText.text = $"+ {_accumulatedAmount:N0}";

        _canvasGroup.alpha = 1f;

        if (_hideCts != null)
        {
            _hideCts.Cancel();
            _hideCts.Dispose();
        }
        _hideCts = new CancellationTokenSource();

        WaitAndHideAsync(_hideCts.Token).Forget();
    }

    private async UniTaskVoid WaitAndHideAsync(CancellationToken token)
    {
        bool isCanceled = await UniTask.Delay(2500, cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        float fadeTime = 0.5f;
        float timer = 0f;

        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeTime);

            isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (isCanceled) return;
        }

        _canvasGroup.alpha = 0f;
        _accumulatedAmount = 0;
    }

    private void OnDestroy()
    {
        if (_hideCts != null)
        {
            _hideCts.Cancel();
            _hideCts.Dispose();
        }
    }
}
