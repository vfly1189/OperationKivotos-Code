using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

[RequireComponent(typeof(CanvasGroup))]
public class UI_GainExpToast : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _amountText;
    [SerializeField] private CanvasGroup _canvasGroup;
    private int _accumulatedAmount = 0;
    private CancellationTokenSource _hideCts;

    private void Awake()
    {
        _canvasGroup.alpha = 0f; // 처음엔 투명하게 숨김
    }

    public void AddAmount(int amount)
    {
        // 1. 값 누적 및 텍스트 갱신 (N0는 천 단위 콤마 추가)
        _accumulatedAmount += amount;
        _amountText.text = $"+ {_accumulatedAmount:N0}";

        // 2. 즉시 화면에 표시
        _canvasGroup.alpha = 1f;

        // 3. 기존에 돌고 있던 '숨김 타이머'가 있다면 취소하고 리셋 (시간 연장 효과)
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
        // 1. 화면에 유지되는 시간 (2.5초)
        // SuppressCancellationThrow를 쓰면, 도중에 새로운 값을 먹어서 취소되어도 에러 로그가 안 뜹니다.
        bool isCanceled = await UniTask.Delay(2500, cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        // 2. 시간이 지나면 서서히 투명해짐 (0.5초)
        float fadeTime = 0.5f;
        float timer = 0f;

        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeTime);

            isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (isCanceled) return;
        }

        // 3. 완전히 사라지면 누적 값 초기화
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
