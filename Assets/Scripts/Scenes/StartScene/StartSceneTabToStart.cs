using System.Collections;
using UnityEngine;

public class StartSceneTabToStart : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    private float _fadeSpeed = 0.5f; // 페이드 속도

    [SerializeField]
    private float _minAlpha = 0.5f; // 최소 투명도

    [SerializeField]
    private float _maxAlpha = 1.0f; // 최대 투명도

    private bool _isFading = false;

    void Start()
    {
        // CanvasGroup이 없으면 자동으로 추가
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // 페이드 시작
        _canvasGroup.alpha = _maxAlpha;
        StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        _isFading = true;

        while (_isFading)
        {
            // Fade Out (max → min)
            yield return StartCoroutine(FadeTo(_minAlpha));

            // Fade In (min → max)
            yield return StartCoroutine(FadeTo(_maxAlpha));
        }
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        float startAlpha = _canvasGroup.alpha;
        float elapsed = 0f;
        float duration = Mathf.Abs(targetAlpha - startAlpha) / _fadeSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // SmoothStep으로 부드러운 전환
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);

            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
    }

    private void OnDestroy()
    {
        _isFading = false;
        StopAllCoroutines();
    }
}
