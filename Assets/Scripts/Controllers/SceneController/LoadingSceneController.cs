using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingSceneController : BaseScene
{
    [Header("Random Images")]
    [SerializeField] 
    private Sprite[] _randomSprites; // 10개 이미지 배열
    [SerializeField] 
    private Image _randomImage;

    [Header("Loading Bar")]
    [SerializeField] 
    private Image _barFill;

    [Header("Loading Text")]
    [SerializeField] 
    private TextMeshProUGUI _loadingText;
    [SerializeField] 
    private float _dotAnimSpeed = 0.5f;

    private void Start()
    {
        // 랜덤 이미지 설정
        SetRandomImage();

        // 로딩바 초기화
        _barFill.fillAmount = 0f;

        // 점 애니메이션 시작
        StartCoroutine(AnimateLoadingDots());
    }

    private void SetRandomImage()
    {
        if (_randomSprites.Length == 0)
        {
            Debug.LogError("Random sprites not assigned!");
            return;
        }

        int randomIndex = Random.Range(0, _randomSprites.Length);
        _randomImage.sprite = _randomSprites[randomIndex];
    }

    // 외부에서 진행률 업데이트
    public void UpdateProgress(float progress)
    {
        _barFill.fillAmount = Mathf.Clamp01(progress);
    }

    private IEnumerator AnimateLoadingDots()
    {
        string baseText = "Now Loading";
        int dotCount = 0;

        while (true)
        {
            dotCount = (dotCount + 1) % 4; // 0, 1, 2, 3 반복
            _loadingText.text = baseText + new string('.', dotCount);
            yield return new WaitForSeconds(_dotAnimSpeed);
        }
    }

    public override void Clear()
    {
        
    }
}
