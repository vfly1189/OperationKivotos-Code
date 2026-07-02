using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 원샷 VFX(총구 화염, 피격 이펙트 등)를 일정 시간 뒤 자동으로 반환/파괴한다.
// Managers.Resource.Instantiate로 스폰한 이펙트에 붙이면:
//   - Poolable 이면 Managers.Resource.Destroy 경유로 풀에 Push 되어 재사용되고,
//   - 아니면 Object.Destroy 되어 사라진다.
// => "스폰만 하고 회수 안 함"으로 인한 누수 / 풀 무한증식을 원천 차단한다.
//
// 풀 재사용에 안전하도록 OnEnable마다 타이머를 재무장하고, OnDisable(풀 반환)에서 취소한다.
[DisallowMultipleComponent]
public class AutoReturnToPool : MonoBehaviour
{
    [Tooltip("활성화 후 이 시간(초)이 지나면 자동 반환한다.")]
    [SerializeField] private float _lifeTime = 2f;

    [Tooltip("루프가 아닌 ParticleSystem이 있으면 그 지속시간으로 _lifeTime을 자동 계산한다.")]
    [SerializeField] private bool _useParticleDuration = true;

    private CancellationTokenSource _cts;

    private void Awake()
    {
        if (!_useParticleDuration) return;

        ParticleSystem ps = GetComponent<ParticleSystem>();
        if (ps == null) ps = GetComponentInChildren<ParticleSystem>();

        // 루프 이펙트는 스스로 끝나지 않으므로 자동 계산 대상에서 제외(직렬화된 _lifeTime 사용)
        if (ps != null && !ps.main.loop)
        {
            var main = ps.main;
            _lifeTime = main.duration + main.startLifetime.constantMax;
        }
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        ReturnAfterDelayAsync(_cts.Token).Forget();
    }

    private void OnDisable()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    private async UniTaskVoid ReturnAfterDelayAsync(CancellationToken token)
    {
        bool isCanceled = await UniTask.Delay(
            TimeSpan.FromSeconds(_lifeTime), cancellationToken: token).SuppressCancellationThrow();

        if (isCanceled) return; // 이미 반환/파괴되었거나 풀로 돌아감

        Managers.Resource.Destroy(gameObject); // Poolable이면 풀 Push, 아니면 Destroy
    }
}
