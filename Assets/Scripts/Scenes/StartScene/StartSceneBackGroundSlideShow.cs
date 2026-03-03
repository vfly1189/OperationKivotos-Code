using NUnit.Framework;
using NUnit.Framework.Constraints;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StartSceneBackGroundSlideShow : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField]  private float _displayDuration = 5.0f; // 각 이미지 표시 시간

    [Header("Pan Settings")]
    [SerializeField] private float _panSpeed = 20f; // 이동 속도
    [SerializeField] private Vector2 _panDirection = new Vector2(1f, 0.5f); // 이동 방향
    [SerializeField] private float _panRange = 50f; // 이동 거리


    private int _currentSpriteIndex = 0; // 현재 표시 중인 스프라이트 인덱스
    private int _currentLayerIndex = 0; // 현재 활성 레이어 인덱스 (0~6)
    //private int _nextLayerIndex = 1; // 다음에 사용할 레이어 인덱스

    private RectTransform[] _rectTransforms;
    private Vector2[] _initialPositions;

    [SerializeField] private GameObject[] _backGroundImages;

    void Start()
    {
        int childCount = _backGroundImages.Length;
        // RectTransform 배열 초기화
        _rectTransforms = new RectTransform[childCount];
        _initialPositions = new Vector2[childCount];

        for (int i = 0; i < childCount; i++)
        {
            _rectTransforms[i] = _backGroundImages[i].GetComponent<RectTransform>();
            _initialPositions[i] = _rectTransforms[i].anchoredPosition;

            // 모든 레이어 초기화 (투명하게)
            _backGroundImages[i].GetComponent<CanvasGroup>().alpha = 0f;
        }

        //첫번째 이미지만 켜줌
        _backGroundImages[0].GetComponent<CanvasGroup>().alpha = 1.0f;



        // 슬라이드쇼 시작
        StartCoroutine(SlideshowRoutine());
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
            _backGroundImages[_currentLayerIndex].GetComponent<CanvasGroup>().alpha = 0f;

            // 다음 인덱스 계산
            _currentSpriteIndex = (_currentSpriteIndex + 1) % _backGroundImages.Length;
            _currentLayerIndex = (_currentLayerIndex + 1) % _backGroundImages.Length;

            // 새 이미지 설정 및 표시
            _rectTransforms[_currentLayerIndex].anchoredPosition = _initialPositions[_currentLayerIndex];
            _backGroundImages[_currentLayerIndex].GetComponent<CanvasGroup>().alpha = 1f;
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
