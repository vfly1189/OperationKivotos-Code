using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingSceneController : BaseScene
{
    [Header("Random Images")]
    [SerializeField] private Sprite[] _randomSprites; // 10개 이미지 배열
    [SerializeField]private Image _randomImage;

    [Header("Loading Bar")]
    [SerializeField] private Slider _barFill;

    [Header("Loading Text")]
    [SerializeField] private TextMeshProUGUI _loadingText;
    [SerializeField] private float _dotAnimSpeed = 0.5f;

    // [추가] 퍼센트와 파일명을 띄울 UI 요소를 추가하세요. 
    // (인스펙터에서 안 넣으면 _loadingText 하나에 다 출력하도록 안전장치 해둠)
    [Header("Details (Optional)")]
    [SerializeField] private TextMeshProUGUI _percentText;     // 0% ~ 100% 표시용
    [SerializeField] private TextMeshProUGUI _resourceNameText;// 어떤 파일 로딩중인지 표시용

    private Coroutine _dotCoroutine;
    private float _startValue = 0f;

    //public float SliderValue
    //{
    //    get { return _barFill.fillAmount; }
    //    set { _barFill.fillAmount = value; }
    //}

    //private void Start()
    //{
    //    // 랜덤 이미지 설정
    //    SetRandomImage();

    //    // 로딩바 초기화
    //    _barFill.fillAmount = 0f;

    //    // 점 애니메이션 시작
    //    StartCoroutine(AnimateLoadingDots());
    //}

    //private void SetRandomImage()
    //{
    //    if (_randomSprites.Length == 0)
    //    {
    //        Debug.LogError("Random sprites not assigned!");
    //        return;
    //    }

    //    int randomIndex = Random.Range(0, _randomSprites.Length);
    //    _randomImage.sprite = _randomSprites[randomIndex];
    //}

    //// 외부에서 진행률 업데이트
    //public void UpdateProgress(float progress)
    //{
    //    _barFill.fillAmount = Mathf.Clamp01(progress);
    //}

    //private IEnumerator AnimateLoadingDots()
    //{
    //    string baseText = "Now Loading";
    //    int dotCount = 0;

    //    while (true)
    //    {
    //        dotCount = (dotCount + 1) % 4; // 0, 1, 2, 3 반복
    //        _loadingText.text = baseText + new string('.', dotCount);
    //        yield return new WaitForSeconds(_dotAnimSpeed);
    //    }
    //}

    //public override void Clear()
    //{

    //}

    private void Start()
    {
        SetRandomImage();

        if (_startValue != 0)
            _barFill.value = _startValue;
        else
            _barFill.value = 0f;

        _dotCoroutine = StartCoroutine(AnimateLoadingDots());
    }

    private void SetRandomImage()
    {
        if (_randomSprites == null || _randomSprites.Length == 0) return;
        int randomIndex = Random.Range(0, _randomSprites.Length);
        _randomImage.sprite = _randomSprites[randomIndex];
    }

    // [수정] 외부(ResourceManager)에서 진행률과 파일명을 받아 업데이트
    public void UpdateProgress(float progress, string fileName = "")
    {
        // 1. 게이지바 채우기
        _barFill.value = Mathf.Clamp01(progress);

        // 2. 0~100% 수치화
        int percent = Mathf.FloorToInt(progress * 100f);

        // UI 텍스트 업데이트
        if (_percentText != null)
        {
            _percentText.text = $"{percent}%";
        }
        if (_resourceNameText != null)
        {
            _resourceNameText.text = string.IsNullOrEmpty(fileName) ? "Loading..." : $"Loading: {fileName}";
        }

        // 만약 퍼센트/파일명 전용 TextMeshPro가 안 달려있으면 기존 _loadingText에 합쳐서 출력
        if (_percentText == null && _resourceNameText == null)
        {
            if (_dotCoroutine != null)
            {
                StopCoroutine(_dotCoroutine); // 기존 점 찍기 중단
                _dotCoroutine = null;
            }
            _loadingText.text = $"{fileName} ({percent}%)";
        }
    }

    private IEnumerator AnimateLoadingDots()
    {
        string baseText = "Now Loading";
        int dotCount = 0;
        while (true)
        {
            dotCount = (dotCount + 1) % 4;
            _loadingText.text = baseText + new string('.', dotCount);
            yield return new WaitForSeconds(_dotAnimSpeed);
        }
    }

    //각 씬들에서 먼저 보여질 커버의 value를 그냥 1로 설정해놓고 로딩이 안된 것처럼 보이는 fake 화면용
    public void SetValue(float value)
    {
        _startValue = value;
    }

    public override void Clear() { }
}
