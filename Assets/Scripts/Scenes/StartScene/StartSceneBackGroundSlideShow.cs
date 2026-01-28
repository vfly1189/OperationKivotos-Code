using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StartSceneBackGroundSlideShow : MonoBehaviour
{
    [Header("Background Images")]
    [SerializeField] private Sprite[] _backgroundSprites; // 배경 이미지 배열 ( Sprite )

    [Header("UI Elements")]
    [SerializeField] private Image[] _backgourndImages;
    [SerializeField] private CanvasGroup[] _backgroundCanvasGroups;

    [Header("Timing")]
    [SerializeField]  private float _displayDuration = 5.0f; // 각 이미지 표시 시간

    [Header("Pan Settings")]
    [SerializeField] private float _panSpeed = 20f; // 이동 속도
    [SerializeField] private Vector2 _panDirection = new Vector2(1f, 0.5f); // 이동 방향
    [SerializeField] private float _panRange = 50f; // 이동 거리

    [Header("Button")]
    [SerializeField] private Button _sceneChangeButton;

    private int _currentSpriteIndex = 0; // 현재 표시 중인 스프라이트 인덱스
    private int _currentLayerIndex = 0; // 현재 활성 레이어 인덱스 (0~6)
    //private int _nextLayerIndex = 1; // 다음에 사용할 레이어 인덱스

    private RectTransform[] _rectTransforms;
    private Vector2[] _initialPositions;

    void Start()
    {
        if (_backgroundSprites.Length == 0)
        {
            Debug.LogError("배경 이미지가 없습니다!");
            return;
        }

        if (_backgourndImages.Length != _backgroundCanvasGroups.Length)
        {
            Debug.LogError("Image와 CanvasGroup 배열 크기가 다릅니다!");
            return;
        }

        // RectTransform 배열 초기화
        _rectTransforms = new RectTransform[_backgourndImages.Length];
        _initialPositions = new Vector2[_backgourndImages.Length];

        for (int i = 0; i < _backgourndImages.Length; i++)
        {
            _rectTransforms[i] = _backgourndImages[i].GetComponent<RectTransform>();
            _initialPositions[i] = _rectTransforms[i].anchoredPosition;

            // 모든 레이어 초기화 (투명하게)
            _backgroundCanvasGroups[i].alpha = 0f;
        }

        // 첫 번째 이미지 설정
        _backgourndImages[_currentLayerIndex].sprite = _backgroundSprites[_currentSpriteIndex];
        _backgroundCanvasGroups[_currentLayerIndex].alpha = 1f;

        _sceneChangeButton.onClick.AddListener(OnClick);


        // 슬라이드쇼 시작
        StartCoroutine(SlideshowRoutine());
    }
    
    void OnClick()
    {
        Managers.SceneEx.LoadScene(Define.Scene.Select);
        Managers.Sound.StopBgm();
    }

    void Update()
    {
        // 현재 활성 레이어에 패닝 효과 적용
        ApplyPanEffect(_currentLayerIndex);
    }

    private IEnumerator SlideshowRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_displayDuration);

            // 이전 레이어 숨기기
            _backgroundCanvasGroups[_currentLayerIndex].alpha = 0f;

            // 다음 인덱스 계산
            _currentSpriteIndex = (_currentSpriteIndex + 1) % _backgroundSprites.Length;
            _currentLayerIndex = (_currentLayerIndex + 1) % _backgourndImages.Length;

            // 새 이미지 설정 및 표시
            _backgourndImages[_currentLayerIndex].sprite = _backgroundSprites[_currentSpriteIndex];
            _rectTransforms[_currentLayerIndex].anchoredPosition = _initialPositions[_currentLayerIndex];
            _backgroundCanvasGroups[_currentLayerIndex].alpha = 1f;
        }
    }
    private void ApplyPanEffect(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= _rectTransforms.Length)
            return;

        float time = Time.time * _panSpeed * 0.01f;

        // Sin/Cos로 부드러운 움직임
        Vector2 offset = new Vector2(
            Mathf.Sin(time) * _panRange * _panDirection.x,
            Mathf.Cos(time * 0.7f) * _panRange * _panDirection.y
        );

        _rectTransforms[layerIndex].anchoredPosition = _initialPositions[layerIndex] + offset;
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
