using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField]
    Define.CameraMode _mode = Define.CameraMode.QuarterView;

    [SerializeField]
    Vector3 _delta = new Vector3(0.0f, 4.0f, -5.0f);

    [SerializeField]
    public GameObject _player = null;

    void Start()
    {
        //Managers.Party.OnActiveCharacterChanged += SetTarget;
    }

    private void Awake()
    {
        // [수정] 중복 구독 방지
        if (Managers.Party != null)
        {
            Managers.Party.OnActiveCharacterChanged -= SetTarget;
            Managers.Party.OnActiveCharacterChanged += SetTarget;
        }
    }

    void LateUpdate()
    {
        if (_mode == Define.CameraMode.QuarterView && _player != null)
        {
            //RaycastHit hit;

            ////[카메라 - 벽 - 캐릭터] 이렇게 있는경우
            ////카메라를 [벽 - 카메라 - 캐릭터] 구조가 되게 이동
            //if (Physics.Raycast(_player.transform.position, _delta, out hit, _delta.magnitude, LayerMask.GetMask("Wall")))
            //{
            //    float dist = (hit.point - _player.transform.position).magnitude * 0.8f;
            //    transform.position = _player.transform.position + _delta.normalized * dist;
            //}
            //else
            //{
            //    //Update() -> LateUpdate() 덜덜거리는게 없어짐
            //    //캐릭터 이동먼저 하고 -> 카메라 이동
            //    transform.position = _player.transform.position + _delta;
            //    transform.LookAt(_player.transform);
            //}
            transform.position = _player.transform.position + _delta;
            transform.LookAt(_player.transform);
        }
    }

    public void SetQuaterView(Vector3 delta)
    {
        _mode = Define.CameraMode.QuarterView; ;
        _delta = delta;
    }

    public void SetTarget(GameObject target)
    {
        Debug.Log($"카메라 타겟 설정 : {target.name}");
        _player = target;
    }

    // [추가] 파괴 시 이벤트 해제
    private void OnDestroy()
    {
        if (Managers.Party != null)
        {
            Managers.Party.OnActiveCharacterChanged -= SetTarget;
        }
    }

}
