using System.Collections;
using UnityEngine;

public class Barricade : MonoBehaviour, IDamageable
{
    [SerializeField] private float _maxHp = 50f;
    private float _currentHp;

    private Animator _anim;
    private Collider _collider; // 자식들에 있는 콜라이더들

    void Start()
    {
        _currentHp = _maxHp;
        _anim = GetComponent<Animator>();

        // 내 자식에 있는 모든 콜라이더를 찾아둠 (나중에 끄기 위해)
        _collider = GetComponent<Collider>();
    }

    // 총알 스크립트에서 호출할 함수
    // (만약 BaseStat을 상속받았다면 override TakeDamage가 되겠죠?)
    public void TakeDamage(DamageInfo damage)
    {
        if (_currentHp <= 0) return; // 이미 부서졌으면 무시

        _currentHp -= damage.Amount;
        Debug.Log($"바리케이드 체력: {_currentHp}");

        if (_currentHp <= 0)
        {
            StartCoroutine(CoDestroyBarricade());
        }
    }

    private IEnumerator CoDestroyBarricade()
    {
        // 1. 애니메이션 재생
        if (_anim != null) _anim.CrossFade("Destroy", 0.0f);

        // 2. 콜라이더 끄기 (더 이상 안 맞게)
        if (_collider != null) _collider.enabled = false;

        // (선택) NavMeshObstacle 끄기
        var obstacle = GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle != null) obstacle.enabled = false;

        // 3. 2초 대기
        yield return new WaitForSeconds(2.0f);

        // 4. 진짜 삭제
        Destroy(gameObject);
    }
}
