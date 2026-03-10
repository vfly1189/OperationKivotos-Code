using Cysharp.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UI_Info : UI_PopUp
{
    //HP, ATK, DEF, SPD, CR, CD, ERR
    [SerializeField] TextMeshProUGUI[] _statTexts;

    //무기정보
    [SerializeField] Image _weaponImage;
    [SerializeField] TextMeshProUGUI _weaponLevel;
    [SerializeField] TextMeshProUGUI _weaponName;

    [SerializeField] UI_EquipSlot[] _equipSlots;


    //캐싱용
    private BaseCharacter _curCharacter;

    public override void Init()
    {
        base.Init();


        // 초기 화면 그리기
        RefreshUI();

        Managers.Equipment.OnEquipmentChanged -= RefreshEquipSlot;
        Managers.Equipment.OnEquipmentChanged += RefreshEquipSlot;
    }

    private void SetStat()
    {
        //HP, ATK, DEF, SPD, CR, CD, ERR
        BaseCharacter currentCharacter = Managers.Party.GetCurrentCharacter();
        _curCharacter = currentCharacter;

        _statTexts[0].text = currentCharacter.Stat.MaxHp.Value.ToString("N0");
        _statTexts[1].text = currentCharacter.Stat.Attack.Value.ToString("N0");
        _statTexts[2].text = currentCharacter.Stat.Defense.Value.ToString("N0");
        _statTexts[3].text = currentCharacter.Stat.MoveSpeed.Value.ToString("N0");

        _statTexts[4].text = $"{currentCharacter.Stat.CritRate.Value * 100}%";
        _statTexts[5].text = $"{currentCharacter.Stat.CritDamage.Value * 100}%";
        _statTexts[6].text = $"{currentCharacter.Stat.EnergyRecharge.Value * 100}%";
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

    private void SetEquipment()
    {
        for(int i=0; i<_equipSlots.Length; i++)
        {
            InventorySlot slot = Managers.Equipment._equippedItem[(EquipType)i];

            _equipSlots[i].SetInfo(slot);
            _equipSlots[i].SetType((EquipType)i);
        }
    }

    private void RefreshEquipSlot(EquipType equipType, InventorySlot newSlot, InventorySlot oldSlot)
    {
        //if(newSlot == null && oldSlot != null)
        //{
        //    _equipSlots[(int)equipType].SetInfo(null);
        //}
        //else if(newSlot != null && oldSlot == null)
        //{
        //    _equipSlots[(int)equipType].SetInfo(newSlot);
        //}

        //SetStat();


        // 장착이든, 해제든, 교체든 
        // 그냥 최종적으로 착용하게 된 장비(newSlot)의 상태로 UI 슬롯을 덮어씌우면 끝입니다. (null이면 알아서 빈 아이콘 처리됨)
        _equipSlots[(int)equipType].SetInfo(newSlot);

        // 장비가 바뀌었으니 스탯 UI도 갱신
        SetStat();
    }

    private void RefreshUI()
    {

        //캐싱 해놔서 순서 중요
        SetStat();
        SetWeaponInfo().Forget();
        SetEquipment();
    }

    private void OnDestroy()
    {
        Managers.Equipment.OnEquipmentChanged -= RefreshEquipSlot;
    }

}
