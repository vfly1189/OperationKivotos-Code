using System.Collections;
using UnityEngine;

public class BulletController : MonoBehaviour
{
    [SerializeField] private float _speed = 20f;
    [SerializeField] private float _lifeTime = 1f;

    private float _damage;
    private Coroutine _lifeTimeCoroutine; // 실행 중인 코루틴 저장용

    // 풀에서 꺼낼 때마다 호출됨 (NonomiCharacter 등에서 호출)


    public void Init(float damage)
    {
        _damage = damage;

        // 기존에 돌던 코루틴이 있다면 멈춤 (재사용 시 안전장치)
        if (_lifeTimeCoroutine != null) StopCoroutine(_lifeTimeCoroutine);

        // 수명 카운트 시작
        _lifeTimeCoroutine = StartCoroutine(CoLifeTimeTimer());
    }

    // [중요] 풀링되어 비활성화될 때 코루틴도 확실히 꺼줌
    private void OnDisable()
    {
        if (_lifeTimeCoroutine != null) StopCoroutine(_lifeTimeCoroutine);
    }

    void Update()
    {
        // 앞으로 전진
        transform.Translate(Vector3.forward * _speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Monster"))
        {
            // 데미지 처리
            // Monster monster = other.GetComponent<Monster>();
            // if(monster != null) monster.TakeDamage(_damage);

            Debug.Log("몬스터 피격!!!");

            // ★ 수정: 매니저를 통해 반납 (Destroy 대체)
            Managers.Resource.Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        // 1. 트레일 초기화
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail != null) trail.Clear();

        
    }

        // 일정 시간(_lifeTime) 지나면 자동 반납
    IEnumerator CoLifeTimeTimer()
    {
        yield return new WaitForSeconds(_lifeTime);
        Managers.Resource.Destroy(gameObject);
    }
}
