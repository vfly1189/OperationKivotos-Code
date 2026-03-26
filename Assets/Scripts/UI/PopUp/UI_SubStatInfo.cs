using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_SubStatInfo : UI_Base
{
    [SerializeField] Image _statIcon;

    [SerializeField] TextMeshProUGUI _statType;
    [SerializeField] TextMeshProUGUI _statValue;
    [SerializeField] TextMeshProUGUI _upgradeCount;

    public override void Init()
    {
        
    }
    public async void SetInfo(StatOption stat)
    {
        // 1. 스탯 텍스트 설정 (예: MaxHP_Percent -> "HP")
        _statType.text = GetStatNameKorean(stat.StatType);

        // 2. 수치 텍스트 설정
        _statValue.text = stat.GetStatString(); // StatOption 클래스에 만들어둔 프로퍼티 활용

        if (stat.UpgradeCount > 0)
            _upgradeCount.text = $"+{stat.UpgradeCount}";
        else
            _upgradeCount.text = "";

        // 3. 아이콘 동적 로드 
        // (이전에 만든 아이콘들의 이름을 "Icon_MaxHP", "Icon_Attack" 등으로 Addressable에 등록해두었다고 가정)
        string iconKey = GetIconNameByStat(stat.StatType);
        //Sprite iconSprite = await Managers.Resource.LoadAsync<Sprite>(iconKey);
        Sprite iconSprite = await Managers.Resource.GetSpriteFromAtlasAsync("StatIconAtlas", iconKey);


        if (this == null || gameObject == null || !gameObject.activeInHierarchy)
            return;

        if (iconSprite != null)
        {
            _statIcon.sprite = iconSprite;
        }
    }

    // 영문 Enum을 한글로 예쁘게 바꿔주는 헬퍼 함수
    private string GetStatNameKorean(EStatType type)
    {
        switch (type)
        {
            case EStatType.MaxHP_Flat:
            case EStatType.MaxHP_Percent: return "최대 체력";
            case EStatType.Attack_Flat:
            case EStatType.Attack_Percent: return "공격력";
            case EStatType.Defense_Flat:
            case EStatType.Defense_Percent: return "방어력";
            case EStatType.MoveSpeed: return "이동 속도";
            case EStatType.CritRate: return "치명타 확률";
            case EStatType.CritDamage: return "치명타 피해";
            case EStatType.EnergyRegen: return "에너지 회복";
            default: return type.ToString();
        }
    }

    private string GetIconNameByStat(EStatType type)
    {
        switch (type)
        {
            case EStatType.MaxHP_Flat:
            case EStatType.MaxHP_Percent: return "Stat_Icon_HP";
            case EStatType.Attack_Flat:
            case EStatType.Attack_Percent: return "Stat_Icon_ATK";
            case EStatType.Defense_Flat:
            case EStatType.Defense_Percent: return "Stat_Icon_DEF";
            case EStatType.MoveSpeed: return "Stat_Icon_SPD";
            case EStatType.CritRate: return "Stat_Icon_CR";
            case EStatType.CritDamage: return "Stat_Icon_CD";
            case EStatType.EnergyRegen: return "Stat_Icon_ERR";
            default: return type.ToString();
        }
    }
}
