using Cysharp.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UI_Info : UI_PopUp
{
    //HP, ATK, DEF, SPD, CR, CD, ERR
    [SerializeField] TextMeshProUGUI[] _staTexts;

    //무기정보
    [SerializeField] Image _weaponImage;
    [SerializeField] TextMeshProUGUI _weaponLevel;
    [SerializeField] TextMeshProUGUI _weaponName;


    //캐싱용
    private BaseCharacter _curCharacter;

    public override void Init()
    {
        base.Init();


        // 초기 화면 그리기
        RefreshUI();
    }

    private void SetStat()
    {
        //HP, ATK, DEF, SPD, CR, CD, ERR
        BaseCharacter currentCharacter = Managers.Party.GetCurrentCharacter();
        _curCharacter = currentCharacter;

        _staTexts[0].text = currentCharacter.Stat.MaxHp.Value.ToString("N0");
        _staTexts[1].text = currentCharacter.Stat.Attack.Value.ToString("N0");
        _staTexts[2].text = currentCharacter.Stat.Defense.Value.ToString("N0");
        _staTexts[3].text = currentCharacter.Stat.MoveSpeed.Value.ToString("N0");

        _staTexts[4].text = $"{currentCharacter.Stat.CritRate.Value * 100}%";
        _staTexts[5].text = $"{currentCharacter.Stat.CritDamage.Value * 100}%";
        _staTexts[6].text = $"{currentCharacter.Stat.EnergyRecharge.Value * 100}%";
    }

    private async UniTask SetWeaponInfo()
    {
        WeaponDataSO weaponData = _curCharacter.Stat.WeaponData;

        if(weaponData != null)
        {
            Sprite icon = await Managers.Resource.LoadAsync<Sprite>(weaponData.weaponIcon);
            _weaponImage.sprite = icon;
        }

        _weaponLevel.text = $"+{_curCharacter.Stat.WeaponLevel}";
        _weaponName.text = weaponData.weaponName;
    }

    private void RefreshUI()
    {

        //캐싱 해놔서 순서 중요
        SetStat();
        SetWeaponInfo().Forget();
    }


}
