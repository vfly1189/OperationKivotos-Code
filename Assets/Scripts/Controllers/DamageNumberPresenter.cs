using Cysharp.Threading.Tasks;
using UnityEngine;

// 데미지 표시(토스트 숫자) 책임을 Stat에서 분리한 컴포넌트.
// BaseStat.OnDamageTaken을 구독해 UI_DamageToast를 띄운다. 캐릭터·몬스터 공용. (SRP 분리 2단계)
// 프리팹 수정 없이 컨트롤러가 런타임에 부착·바인딩한다.
public class DamageNumberPresenter : MonoBehaviour
{
    private BaseStat _stat;

    // 컨트롤러가 호출: 컴포넌트 보장 + 스탯 바인딩 (풀 재사용·중복구독 안전)
    public static DamageNumberPresenter EnsureOn(GameObject go, BaseStat stat)
    {
        if (go == null || stat == null) return null;

        var presenter = go.GetComponent<DamageNumberPresenter>();
        if (presenter == null) presenter = go.AddComponent<DamageNumberPresenter>();
        presenter.Bind(stat);
        return presenter;
    }

    private void Bind(BaseStat stat)
    {
        if (_stat == stat) return;
        if (_stat != null) _stat.OnDamageTaken -= HandleDamageTaken;
        _stat = stat;
        _stat.OnDamageTaken += HandleDamageTaken;
    }

    private void OnDestroy()
    {
        if (_stat != null) _stat.OnDamageTaken -= HandleDamageTaken;
    }

    private void HandleDamageTaken(DamageTaken info) => ShowToastAsync(info).Forget();

    private async UniTask ShowToastAsync(DamageTaken info)
    {
        UI_DamageToast toast = await Managers.UI.MakeSubItemAsync<UI_DamageToast>(
            "UI_DamageToast", Managers.UI.CanvasSystem.transform);
        if (toast == null) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        toast.gameObject.SetActive(true);
        toast.transform.position = mainCam.WorldToScreenPoint(info.HitPoint);
        toast.SetupDamageText((int)info.Amount, info.IsCritical, info.AttackerLayer);
    }
}
