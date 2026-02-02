using UnityEngine;

public class TankBombController : MonoBehaviour
{
    [Header("Child References")]
    [SerializeField] private GameObject _warningDecal; // 여기에 MissilePoint 연결
    [SerializeField] private ParticleSystem _explosionParticle; // 여기에 TankBombExplosion 연결

    private Transform _originalParent; // 원래 부모(Tank)를 기억할 변수


    private void Awake()
    {
        // 시작할 때 내 원래 부모(탱크)를 기억해둠
        _originalParent = transform.parent;
    }

    // 1단계: 위치 잡고 경고 데칼만 켜기
    public void ShowWarning(Vector3 position)
    {
        // 1. 위치 이동
        transform.position = position;

        // [중요] 탱크가 회전해도 따라돌지 않도록 부모 관계를 임시로 끊음
        transform.SetParent(null);

        // 회전값은 월드 기준(0,0,0) 혹은 바닥에 맞게 설정 (데칼이 X축 90도여야 한다면 여기서 보정)
        // transform.rotation = Quaternion.identity; // 필요시 주석 해제

        // 2. 데칼 켜기 / 폭발 끄기
        _warningDecal.SetActive(true);
        _explosionParticle.gameObject.SetActive(false);
    }

    // 2단계: 경고 끄고 폭발 이펙트 재생
    public void Explode()
    {
        _warningDecal.SetActive(false);
        _explosionParticle.gameObject.SetActive(true);
        _explosionParticle.Stop();
        _explosionParticle.Play();
    }

    // 이펙트가 다 끝나고 재사용 대기 상태로 만들 때 호출
    public void ResetState()
    {
        _warningDecal.SetActive(false);
        _explosionParticle.gameObject.SetActive(false);

        // [중요] 다시 탱크의 자식으로 복귀 (따라다니게)
        transform.SetParent(_originalParent);
        transform.localPosition = Vector3.zero; // 위치 초기화 (선택)
    }
}
