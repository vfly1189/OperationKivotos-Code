using System.Collections;
using UnityEngine;

public class BulletController : MonoBehaviour
{
    [SerializeField] private float _speed = 20f;
    [SerializeField] private float _lifeTime = 1f;

    private GameObject _shooter;

    private float _damage;
    private Coroutine _lifeTimeCoroutine; // 실행 중인 코루틴 저장용

    // 풀에서 꺼낼 때마다 호출됨 (NonomiCharacter 등에서 호출)


    public void Init(float damage, GameObject shooter)
    {
        _damage = damage;
        _shooter = shooter;
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
        // 0. 예외 처리: 주인이 없으면(이미 죽거나 파괴됨) 아무것도 안 함
        if (_shooter == null) return;

        // 1. 벽 충돌 처리 (가장 흔하므로 먼저 체크하거나 따로 뺌)
        if ((LayerMask.GetMask("Wall") & (1 << other.gameObject.layer)) != 0)
        {
            Managers.Resource.Destroy(gameObject);
            return;
        }
        else if ((LayerMask.GetMask("Barricade") & (1 << other.gameObject.layer)) != 0)
        {
            // 부모에 스크립트가 있을 수 있으니 GetComponentInParent 권장 
            // (Rigidbody 덕분에 GetComponent로도 찾아질 수 있지만 안전하게)
            Barricade barricade = other.GetComponentInParent<Barricade>();

            if (barricade != null)
            {
                barricade.TakeDamage(_damage);
                Managers.Resource.Destroy(gameObject); // 총알 삭제
                return;
            }

            // 만약 파괴 불가능한 그냥 벽이라면 그냥 삭제
            Managers.Resource.Destroy(gameObject);
        }
        // 2. 피아 식별 (아군 오사 방지)
        // "나를 쏜 놈과 맞은 놈의 태그가 같으면(같은 팀이면) 무시"
        if (other.CompareTag(_shooter.tag)) return;


        // 3. 적군 피격 처리 (이제 남은 건 적군뿐)
        // 맞은 놈이 데미지를 받을 수 있는 놈인지 확인 (인터페이스나 BaseStat 활용)
        BaseStat targetStat = other.GetComponent<BaseStat>();

        if (targetStat != null)
        {
            // Player -> Monster 공격이든, Monster -> Player 공격이든 
            // TakeDamage는 다형성으로 알아서 잘 동작함
            targetStat.TakeDamage(_damage, _shooter);

            // 이펙트 생성 등...
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
