using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class GenesisAttack : MonoBehaviour
{
    [Header("데미지 설정")]
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _duration = 5.0f; // 장판 지속 시간

    [Header("DoT 설정")]
    [SerializeField] private bool _isDotDamage = true; // 기본값 true로 설정
    [SerializeField] private float _dotInterval = 0.5f;

    // 현재 장판 위에 올라와 있는 타겟들을 관리 (Target -> Coroutine)
    //private Dictionary<IDamageable, Coroutine> _activeTargets = new Dictionary<IDamageable, Coroutine>();

    // Coroutine 대신 CancellationTokenSource 관리
    private Dictionary<IDamageable, CancellationTokenSource> _activeTargets = new Dictionary<IDamageable, CancellationTokenSource>();

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
                //Debug.Log($"{other.name} 장판 탈출 -> DoT 종료");

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

    //// 주기적으로 데미지 주는 코루틴
    //private IEnumerator CoDotDamage(IDamageable target, Collider collider)
    //{
    //    while (true)
    //    {
    //        // 간격 대기
    //        yield return new WaitForSeconds(_dotInterval);

    //        // 타겟이 유효한지(파괴되지 않았는지) 확인 (MonoBehaviour로 캐스팅)
    //        MonoBehaviour targetMono = target as MonoBehaviour;
    //        if (targetMono == null || targetMono.gameObject == null)
    //        {
    //            _activeTargets.Remove(target);
    //            yield break;
    //        }

    //        // 가장 가까운 위치 다시 계산 (이동했을 테니까)
    //        Vector3 hitPoint = collider.ClosestPoint(transform.position);
    //        ApplyDamage(target, hitPoint);
    //    }
    //}

    private async UniTaskVoid DotDamageAsync(IDamageable target, Collider collider, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(_dotInterval), cancellationToken: token).SuppressCancellationThrow();
            if (isCanceled) return;

            MonoBehaviour targetMono = target as MonoBehaviour;
            if (targetMono == null || targetMono.gameObject == null)
            {
                // [최적화] 자신을 파괴할 때 Dictionary에서 스스로 Remove하는 것은 안전하지 않으므로
                // 나갈 때(Exit)나 Disable에서 지워지도록 내버려두고, 루프만 탈출합니다.
                return;
            }

            Vector3 hitPoint = collider.ClosestPoint(transform.position);
            ApplyDamage(target, hitPoint);
        }
    }

    private void ApplyDamage(IDamageable target, Vector3 hitPoint)
    {
        // 인터페이스 호출
        Debug.Log($"장판 데미지: {_damage}");
        target.TakeDamage(new DamageInfo(_damage, this.gameObject, hitPoint));
    }
}
