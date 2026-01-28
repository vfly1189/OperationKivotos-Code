using UnityEngine;

public class BulletController : MonoBehaviour
{
    [SerializeField] private float _speed = 20f;
    [SerializeField] private float _lifeTime = 2f;


    private float _damage;

    public void Init(float damage)
    {
        _damage = damage;
        Destroy(gameObject, _lifeTime); // 일정 시간 후 자동 삭제
    }

    void Update()
    {
        // 앞으로 전진
        transform.Translate(Vector3.forward * _speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 적(Monster)과 부딪혔는지 확인
        // 태그나 레이어로 구분 (예: "Monster")
        if (other.CompareTag("Monster"))
        {
            // 데미지 처리 (나중에 몬스터 스크립트 추가 시 연결)
            // other.GetComponent<Monster>().TakeDamage(_damage);

            Destroy(gameObject); // 총알 삭제
        }
    }
}
