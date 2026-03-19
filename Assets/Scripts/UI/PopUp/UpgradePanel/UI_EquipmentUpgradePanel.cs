using System;
using System.Collections.Generic;
using UnityEngine;

public enum EquipmentTabType
{
    Weapon = 0,
    Relic = 1,
    Decompose = 2,
    // 나중에 Accessory = 2, Material = 3 등 탭이 추가되면 여기에만 적어주면 됩니다.
}

public class UI_EquipmentUpgradePanel : UI_PopUp
{
    [SerializeField] private UI_EquipmentTabPanel _tabPanel;
    [SerializeField] private UI_WeaponUpgradePanel _weaponUpgradePanel;
    [SerializeField] private UI_RelicUpgradePanel _relicUpgradePanel;
    [SerializeField] private UI_EquipmentDecomposePanel _equipmentDecomposePanel;

    private Dictionary<EquipmentTabType, UI_Base> _panels = new Dictionary<EquipmentTabType, UI_Base>();

    public override void Init()
    {
        base.Init();

        //_tabPanel.Init();
        //_weaponUpgradePanel.Init();
        //_relicUpgradePanel.Init();

        //딕셔너리에 패널 매핑 (새 탭이 생기면 여기만 추가하면 됨)
        _panels.Clear();
        _panels.Add(EquipmentTabType.Weapon, _weaponUpgradePanel);
        _panels.Add(EquipmentTabType.Relic, _relicUpgradePanel);
        _panels.Add(EquipmentTabType.Decompose, _equipmentDecomposePanel);

        _tabPanel.OnTabClicked -= HandleTabChange; 
        _tabPanel.OnTabClicked += HandleTabChange;
    }

    // 탭이 변경될 때 호출되는 함수
    private void HandleTabChange(EquipmentTabType selectedTab)
    {
        
        foreach (var kvp in _panels)
        {
            bool isActive = (kvp.Key == selectedTab);
            kvp.Value.gameObject.SetActive(isActive);

            if (isActive)
                kvp.Value.Refresh();
            // 만약 켜지는 패널에 최신 데이터를 갱신해줘야 한다면 여기서 처리 가능
        }
    }

 
    private void OnDestroy()
    {
        if (_tabPanel != null)
        {
            _tabPanel.OnTabClicked -= HandleTabChange;
        }
    }
}
