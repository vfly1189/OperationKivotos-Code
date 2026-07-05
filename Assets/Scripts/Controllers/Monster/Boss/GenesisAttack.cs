using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class GenesisAttack : MonoBehaviour
{
    [Header("데미지 설정")]
    [SerializeField] private float _duration = 5.0f; // 장판 지속 시간

    [Header("DoT 설정")]
    [SerializeField] private bool _isDotDamage = true; // 기본값 true로 설정
    [SerializeField] private float _dotInterval = 0.5f;

    private float _damage;
    private GameObject _attacker;

    // 현재 장판 위에 올라와 있는 타겟들을 관리 (Target -> Coroutine)
    //private Dictionary<IDamageable, Coroutine> _activeTargets = new Dictionary<IDamageable, Coroutine>();

    // Coroutine 대신 CancellationTokenSource 관리
    private Dictionary<IDamageable, CancellationTokenSource> _activeTargets = new Dictionary<IDamageable, CancellationTokenSource>();

    // 클래스 상단에 캐싱용 변수 추가
    private void Start()
    {
        // 일정 시간 후 장판 자체 삭제
        Destroy(gameObject, _duration);
        //Managers.Resource.Destroy(gameObject, _duration);
    }

    private void OnTriggerEnter(Collider other)
    {

        // 1. 데미지 입을 수 있는 녀석인지 확인
        if (other.TryGetComponent<IDamageable>(out IDamageable target))
        {
            // 중복 진입 방지
            if (_activeTargets.ContainsKey(target)) return;

            // 2. 즉시 데미지 1회 (선택사항: 들어오자마자 맞게 할지, 0.5초 뒤에 맞게 할지)
            // 여기서는 "들어오자마자 1대 맞고 + 주기적으로 맞음"으로 구현
            ApplyDamage(target, other.ClosestPoint(transform.position));

            // 3. DoT 모드라면 주기적 데미지 코루틴 시작
            if (_isDotDamage)
            {
                //Coroutine dotRoutine = StartCoroutine(CoDotDamage(target, other));
                //_activeTargets.Add(target, dotRoutine);

                CancellationTokenSource cts = new CancellationTokenSource();
                _activeTargets.Add(target, cts);
                DotDamageAsync(target, other, cts.Token).Forget();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 나갈 때 리스트에서 제거하고 코루틴 정지
        if (other.TryGetComponent<IDamageable>(out IDamageable target))
        {
            if (_activeTargets.TryGetValue(target, out CancellationTokenSource cts))
            {
                //if (routine != null) StopCoroutine(routine);
                //_activeTargets.Remove(target);
                //GameLog.Log($"{other.name} 장판 탈출 -> DoT 종료");

                if (cts != null)
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                _activeTargets.Remove(target);
            }
        }
    }

    private void OnDisable()
    {
        //// 장판이 꺼지거나 파괴될 때 모든 코루틴 정리
        //StopAllCoroutines();
        //_activeTargets.Clear();

        // 장판이 꺼질 때 딕셔너리 순회하며 토큰 캔슬
        foreach (var kvp in _activeTargets)
        {
            if (kvp.Value != null)
            {
                kvp.Value.Cancel();
                kvp.Value.Dispose();
            }
        }
        _activeTargets.Clear();
    }

    public void Init(float damage, GameObject attacker)
    {
        _damage = damage;
        _attacker = attacker;
    }

    private void ApplyDamage(IDamageable target, Vector3 hitPoint)
    {
        var dmg = new DamageInfo(_damage, _attacker, false);   // ★ 하드코딩 500 제거 (버그였음)
        dmg.HitPoint = hitPoint;
        target.TakeDamage(dmg);
    }

    private async UniTaskVoid DotDamageAsync(IDamageable target, Collider collider, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(_dotInterval), cancellationToken: token).SuppressCancellationThrow();
            if (isCanceled) return;

            MonoBehaviour targetMono = target as MonoBehaviour;

            // [핵심 추가] targetMono.gameObject.activeInHierarchy 로 비활성화 여부 검사!
            if (targetMono == null || targetMono.gameObject == null || !targetMono.gameObject.activeInHierarchy)
            {
                // 타겟이 파괴되었거나 SetActive(false)로 꺼졌다면
                // 딕셔너리에서 본인을 안전하게 제거하고 루프를 즉시 탈출합니다.
                if (_activeTargets.ContainsKey(target))
                {
                    _activeTargets.Remove(target);
                }
                return;
            }

            // 혹시 콜라이더 컴포넌트 자체만 꺼진 경우도 방어하고 싶다면 아래 조건도 추가 가능합니다.
            // if (!collider.enabled) return;

            Vector3 hitPoint = collider.ClosestPoint(transform.position);
            ApplyDamage(target, hitPoint);
        }
    }
}
