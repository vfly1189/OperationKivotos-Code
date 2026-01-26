using UnityEngine;

public class TestBillBoard : MonoBehaviour
{
    private Transform _mainCamera;

    void Start()
    {
        if (Camera.main != null)
        {
            _mainCamera = Camera.main.transform;
        }
    }

    void LateUpdate()
    {
        if (_mainCamera != null)
        {
            // 1. 카메라를 바라보게 회전
            // (transform.LookAt은 Z축이 대상을 향하므로, UI나 Plane의 경우 뒤집힐 수 있음.
            //  반대 방향을 보게 하거나, forward를 일치시키는 방식을 많이 씀)

            // 방법 A: 정석 (카메라와 평행하게)
            transform.forward = _mainCamera.forward;

            // 방법 B: 나를 쳐다보게 (LookAt)
            // transform.LookAt(transform.position + _mainCamera.rotation * Vector3.back, _mainCamera.rotation * Vector3.up);
        }
    }
}
