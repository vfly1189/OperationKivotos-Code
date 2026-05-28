using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks; // [추가]
using System.Threading; // [추가]

public class LoadingSceneController : BaseScene
{
    [Header("Random Images")]
    [SerializeField] private Sprite[] _randomSprites;
    [SerializeField] private Image _randomImage;

    [Header("Loading Bar")]
    [SerializeField] private Slider _barFill;

    [Header("Loading Text")]
    [SerializeField] private TextMeshProUGUI _loadingText;
    [SerializeField] private float _dotAnimSpeed = 0.5f;

    [Header("Details (Optional)")]
    [SerializeField] private TextMeshProUGUI _percentText;
    [SerializeField] private TextMeshProUGUI _resourceNameText;

    private float _startValue = 0f;

    // [추가] 점(.) 애니메이션 취소 관리를 위한 전역 토큰 소스
    private CancellationTokenSource _dotAnimCts;

    // [최적화] 가비지 생성 방지를 위해 점 문자열을 미리 캐싱
    private readonly string[] _loadingDotStrings = new string[]
    {
        "Now Loading",
        "Now Loading.",
        "Now Loading..",
        "Now Loading..."
    };

    private void Start()
    {
        SetRandomImage();
        _barFill.value = _startValue != 0 ? _startValue : 0f;

        // [핵심 1] CancellationTokenSource 초기화 및 애니메이션 시작
        _dotAnimCts = new CancellationTokenSource();
        AnimateLoadingDotsAsync(_dotAnimCts.Token).Forget();
    }

    private void SetRandomImage()
    {
        if (_randomSprites == null || _randomSprites.Length == 0) return;
        int randomIndex = Random.Range(0, _randomSprites.Length);
        _randomImage.sprite = _randomSprites[randomIndex];
    }

    public void UpdateProgress(float progress, string fileName = "")
    {
        _barFill.value = Mathf.Clamp01(progress);
        int percent = Mathf.FloorToInt(progress * 100f);

        if (_percentText != null)
        {
            _percentText.SetText("{0}%", percent); // SetText 활용
        }

        if (_resourceNameText != null)
        {
            if (string.IsNullOrEmpty(fileName))
                _resourceNameText.text = "Loading...";
            else
                _resourceNameText.text = $"Loading: {fileName}"; // 파일명은 가변적이므로 보류
        }

        if (_percentText == null && _resourceNameText == null)
        {
            // [핵심 2] 점 찍기 애니메이션 강제 중단
            if (_dotAnimCts != null)
            {
                _dotAnimCts.Cancel();
                _dotAnimCts.Dispose();
                _dotAnimCts = null;
            }

            // [최적화] SetText와 서식 지정자를 사용하여 가비지 감소
            if (string.IsNullOrEmpty(fileName))
                _loadingText.SetText("Loading... ({0}%)", percent);
            else
                _loadingText.text = $"{fileName} ({percent}%)";
        }
    }

    // [핵심 3] 코루틴(IEnumerator)을 UniTaskVoid로 변경
    private async UniTaskVoid AnimateLoadingDotsAsync(CancellationToken token)
    {
        int dotCount = 0;

        while (!token.IsCancellationRequested)
        {
            dotCount = (dotCount + 1) % 4;

            // 캐싱된 문자열을 사용하여 가비지 0 할당
            _loadingText.text = _loadingDotStrings[dotCount];

            // 딜레이 중에 취소 요청이 들어오면 에러 없이 조용히 종료
            bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(_dotAnimSpeed), cancellationToken: token).SuppressCancellationThrow();
            if (isCanceled) return;
        }
    }

    public void SetValue(float value)
    {
        _startValue = value;
    }

    public override void Clear()
    {
        // 씬 전환 시 남아있는 토큰이 있다면 확실하게 파괴
        if (_dotAnimCts != null)
        {
            _dotAnimCts.Cancel();
            _dotAnimCts.Dispose();
            _dotAnimCts = null;
        }
    }

    private void OnDestroy()
    {
        if (_dotAnimCts != null)
        {
            _dotAnimCts.Cancel();
            _dotAnimCts.Dispose();
            _dotAnimCts = null;
        }
    }


}
