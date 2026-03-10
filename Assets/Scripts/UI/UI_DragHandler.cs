using UnityEngine;
using UnityEngine.EventSystems;

public class UI_DragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Tooltip("실제로 이동될 팝업의 최상단 RectTransform")]
    [SerializeField] private RectTransform _targetTransform;

    private Canvas _canvas;

    private void Start()
    {
        // 팝업이 속한 캔버스를 찾습니다 (해상도 보정을 위해 필요)
        _canvas = GetComponentInParent<Canvas>();

        // 타겟을 지정하지 않았다면, 기본적으로 이 상단바의 최상단 부모(팝업 전체)를 타겟으로 잡습니다.
        if (_targetTransform == null)
        {
            _targetTransform = transform.root.GetComponentInChildren<UI_PopUp>().GetComponent<RectTransform>();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 드래그를 시작할 때 팝업 창을 맨 앞으로(최상단 렌더링) 가져옵니다.
        _targetTransform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_canvas == null) return;

        // 해상도 스케일(scaleFactor)에 맞춰서 마우스 이동량(delta) 보정
        _targetTransform.anchoredPosition += eventData.delta / _canvas.scaleFactor;
    }
}