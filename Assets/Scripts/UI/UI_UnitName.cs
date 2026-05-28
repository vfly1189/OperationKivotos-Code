using UnityEngine;

public class UI_UnitName : UI_Base
{
    private Camera _mainCamera;
    private Transform _target;
    private RectTransform _rectTransform;

    private string _name;

    public override void Init()
    {
        // 1. 컴포넌트 캐싱
        _rectTransform = GetComponent<RectTransform>();
        _mainCamera = Camera.main;
    }

    
    public void SetTarget(Transform target, string name)
    {
        _target = target;
        _name = name;
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            Managers.Resource.Destroy(gameObject);
            return;
        }

        // 카메라가 없으면 찾기 시도
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        Vector3 screenPos = _mainCamera.WorldToScreenPoint(_target.position);

        // [보강] Z값 체크 (카메라 뒤쪽)
        if (screenPos.z <= 0)
        {
            // 그냥 캔버스 밖으로 날려버림
            screenPos = new Vector3(-1000, -1000, 0);
        }
        else
        {
            // [보강] Z값을 0으로 맞춰야 UI 캔버스 평면에 딱 붙음 (Overlay가 아닌 경우 중요)
            screenPos.z = 0;
        }

        _rectTransform.position = screenPos;
    }

}
