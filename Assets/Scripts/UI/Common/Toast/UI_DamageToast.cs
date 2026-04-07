using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;

public class UI_DamageToast : UI_Base
{
    [SerializeField] private TextMeshProUGUI _damageText;

    [Header("Material Presets")]
    [SerializeField] private Material normalDamageMaterial;   // 인스펙터에서 MainFont_Bold_DamageNormal 할당
    [SerializeField] private Material criticalDamageMaterial; // 인스펙터에서 MainFont_Bold_DamageCritical 할당
    [SerializeField] private Material monsterDamageMaterial; // 인스펙터에서 MainFont_Bold_DamageCritical 할당

    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Animation Settings")]
    [SerializeField] private float _lifeTime = 1.0f;    // 화면에 떠 있는 총 시간
    [SerializeField] private float _floatSpeed = 50f;   // 위로 떠오르는 속도 (화면 픽셀 기준)

    private CancellationTokenSource _cts;

    public override void Init()
    {
    }

    // 풀링으로 인해 비활성화될 때 진행 중인 타이머/애니메이션 안전하게 종료
    private void OnDisable()
    {
        CancelTimer();
    }

    /// <summary>
    /// 데미지와 치명타 여부를 입력받아 텍스트와 스타일을 업데이트합니다.
    /// </summary>
    public void SetupDamageText(int damageAmount, bool isCritical, int attackerLayer)
    {
        _canvasGroup.alpha = 1f;
        _damageText.text = damageAmount.ToString();
        transform.localScale = Vector3.one * (isCritical ? 1.5f : 1.0f);

        int playerLayer = LayerMask.NameToLayer("Player");
        int monsterLayer = LayerMask.NameToLayer("Monster"); // 혹은 적군 레이어
        int monsterBulletLayer = LayerMask.NameToLayer("MonsterBullet");

        if (attackerLayer == monsterLayer || attackerLayer == monsterBulletLayer)
        {
            // 몬스터가 때린 경우 (플레이어 피격)
            _damageText.fontSharedMaterial = monsterDamageMaterial;
        }
        else if (attackerLayer == playerLayer)
        {
            // 플레이어가 때린 경우
            _damageText.fontSharedMaterial = isCritical ? criticalDamageMaterial : normalDamageMaterial;
        }
        else
        {
            // 기타 (함정, 환경 데미지 등)
            _damageText.fontSharedMaterial = normalDamageMaterial;
        }

        CancelTimer();
        _cts = new CancellationTokenSource();
        AnimateAndDestroyAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid AnimateAndDestroyAsync(CancellationToken token)
    {
        float elapsed = 0f;
        Vector3 startPos = transform.position;

        // _lifeTime 동안 매 프레임 애니메이션 실행
        while (elapsed < _lifeTime)
        {
            // 도중에 객체가 꺼지거나 파괴되면 즉시 중단 (에러 방지)
            if (token.IsCancellationRequested) return;

            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / _lifeTime; // 0.0 ~ 1.0

            // (1) 위로 서서히 떠오르기
            transform.position = startPos + (Vector3.up * _floatSpeed * normalizedTime);

            // (2) 절반(0.5)의 시간이 지난 후부터 서서히 투명해지기 (Fade Out)
            if (normalizedTime > 0.5f)
            {
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, (normalizedTime - 0.5f) * 2f);
            }

            // 다음 프레임까지 대기
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // 시간이 완전히 지나면 매니저를 통해 자신을 파괴 (오브젝트 풀 반환)
        Managers.Resource.Destroy(gameObject);
    }

    private void CancelTimer()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}
