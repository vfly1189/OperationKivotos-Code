using UnityEngine;
using Cysharp.Threading.Tasks; // [추가]
using System.Threading; // [추가]

public class StartSceneTabToStart : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float _fadeSpeed = 0.5f;
    [SerializeField] private float _minAlpha = 0.5f;
    [SerializeField] private float _maxAlpha = 1.0f;

    private bool _isFading = false;

    void Start()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        _canvasGroup.alpha = _maxAlpha;
        _isFading = true;

        // [핵심] 파괴 시 자동 취소되는 토큰을 넘겨서 무한 반복 실행
        FadeRoutineAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid FadeRoutineAsync(CancellationToken token)
    {
        while (_isFading)
        {
            // Fade Out (max → min)
            await FadeToAsync(_minAlpha, token);

            // 취소되었다면 루프 즉시 탈출
            if (token.IsCancellationRequested) return;

            // Fade In (min → max)
            await FadeToAsync(_maxAlpha, token);
        }
    }

    private async UniTask FadeToAsync(float targetAlpha, CancellationToken token)
    {
        float startAlpha = _canvasGroup.alpha;
        float elapsed = 0f;
        float duration = Mathf.Abs(targetAlpha - startAlpha) / _fadeSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);

            // [안전망] 1프레임 대기하면서 파괴/취소 여부 확인
            bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (isCanceled) return;
        }

        _canvasGroup.alpha = targetAlpha;
    }

    private void OnDestroy()
    {
        _isFading = false;
        // StopAllCoroutines(); <- 이제 필요 없음! 토큰이 알아서 취소해줌
    }
}
