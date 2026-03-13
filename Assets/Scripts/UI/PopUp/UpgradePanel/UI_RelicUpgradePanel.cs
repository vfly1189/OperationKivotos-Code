using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UI_RelicUpgradePanel : UI_Base
{
    [Header("보유 장비 목록")]
    [SerializeField] private Transform _havingRelicScollView;

    [Header("장비 아이콘 그룹")]
    [SerializeField] private Image _itemIcon;
    [SerializeField] private TextMeshProUGUI _itemUpgradeLevel;
    [SerializeField] private Slider _itemExpBar;
    [SerializeField] private TextMeshProUGUI _itemExpText;

    [Header("장비 INFO 그룹")]
    [SerializeField] private TextMeshProUGUI _itemName;
    [SerializeField] private TextMeshProUGUI _itemType;
    [SerializeField] private TextMeshProUGUI _itemTier;
    [SerializeField] Transform _mainStatParent;
    [SerializeField] Transform _subStatParent;

    [Header("소모 재료 그룹")]
    [SerializeField] Transform _consumeMaterialParent;
    [SerializeField] TextMeshProUGUI _consumeCreditNum;

    [Header("장비 강화 버튼")]
    [SerializeField] Button _upgradeButton;

    private List<UI_ItemSlot> _itemSlots = new List<UI_ItemSlot>();
    private const int MAX_CONSUME_MATERIAL_SLOT_COUNT = 8;
    private InventorySlot _selectedSlot = null;

    public override void Init()
    {
        ResetItemInfo();
        SetHavingRelicScollView();
        SetConsumeMaterialSlot();
    }

    //맨 처음 진입할때 아이템 정보들 초기화
    private void ResetItemInfo()
    {
        // 1. 이미지는 안 보이게 끄기
        _itemIcon.gameObject.SetActive(false);
        //_itemIcon.sprite = null; // 굳이 안 해도 SetActive(false)로 가려짐

        // 2. 텍스트 요소들 기본값으로 초기화 또는 비우기
        _itemUpgradeLevel.text = "";
        _itemName.text = "선택된 장비 없음";
        _itemType.text = "";
        _itemTier.text = "";

        // 3. 슬라이더 바 초기화
        _itemExpBar.value = 0f;
        _itemExpText.text = "0 / 0";

        // 4. 스탯 프리팹들도 지우기 (자식 오브젝트들 파괴 또는 풀링 반환)
        foreach (Transform child in _mainStatParent)
        {
            Managers.Resource.Destroy(child.gameObject);
        }
        foreach (Transform child in _subStatParent)
        {
            Managers.Resource.Destroy(child.gameObject);
        }

        // 5. 버튼도 비활성화 (선택된 장비가 없으니 강화 불가)
        _upgradeButton.interactable = false;


        // 6. 소모 크레딧 없음
        _consumeCreditNum.text = "";

        // 7. 소모 재료 삭제
        foreach (Transform child in _consumeMaterialParent)
        {
            Managers.Resource.Destroy(child.gameObject);
        }
    }

    private async void SetHavingRelicScollView()
    {
        InventorySlot[] relics = Managers.Inventory.Inventory[ItemCategory.Equipment];

        if (relics.Length == 0)
            Debug.Log($"장비 없음");


        foreach(InventorySlot relic in relics)
        {
            if (relic.IsEmpty) continue;

            UI_ItemSlot slot = await Managers.UI.MakeSubItemAsync<UI_ItemSlot>("UI_ItemSlot", _havingRelicScollView);
            slot.gameObject.SetActive(true);
            slot.SetInfo(relic, ItemCategory.Equipment, -1);
            slot.SetCallback(SetItemInfo, null);

            _itemSlots.Add(slot);
        }
    }

    private async void SetItemInfo(UI_ItemSlot uiSlot)
    {
        InventorySlot slotData = uiSlot.CurrentSlotData;
        _selectedSlot = slotData;

        EquipmentData equipmentData = Managers.Data.GetData<int, EquipmentData>(slotData.itemID);
       
        _itemIcon.sprite = await Managers.Resource.LoadAsync<Sprite>(equipmentData.IconKey);
        if (this == null || gameObject == null || !gameObject.activeInHierarchy)
            return;
        _itemIcon.gameObject.SetActive(true);

        _itemName.text = equipmentData.Name;
        _itemType.text = equipmentData.EquipPart;
        _itemTier.text = $"Tier " + equipmentData.Tier;

        _itemUpgradeLevel.text = $"+{slotData.EquipInstance.UpgradeLevel}";
        _itemExpText.text = $"{slotData.EquipInstance.CurrentExp} / {slotData.EquipInstance.NextLevelRequireExp}";
        _itemExpBar.value = (float)slotData.EquipInstance.CurrentExp / (float)slotData.EquipInstance.NextLevelRequireExp;


        SetMainStat(slotData.EquipInstance).Forget();
        SetSubStat(slotData.EquipInstance).Forget(); 
    }


    public async UniTask SetMainStat(EquipmentInstance equipmentInstance)
    {
        foreach (Transform child in _mainStatParent)
        {
            Managers.Resource.Destroy(child.gameObject);
        }

        List<StatOption> mainStats = equipmentInstance.MainStats;

        foreach (StatOption statOption in mainStats)
        {
            UI_UpgradePanelMainStatInfo mainStatInfo = await Managers.UI.MakeSubItemAsync<UI_UpgradePanelMainStatInfo>("UI_UpgradePanelMainStatInfo", _mainStatParent);

            if (this == null || gameObject == null || !gameObject.activeInHierarchy)
                return;

            if (mainStatInfo != null)
            {
                mainStatInfo.SetInfo(statOption);
            }
        }
    }

    public async UniTask SetSubStat(EquipmentInstance equipmentInstance)
    {
        foreach (Transform child in _subStatParent)
        {
            Managers.Resource.Destroy(child.gameObject);
        }

        List<StatOption> subStats = equipmentInstance.SubStats;

        foreach (StatOption statOption in subStats)
        {
            UI_UpgradePanelSubStatInfo subStatInfo = await Managers.UI.MakeSubItemAsync<UI_UpgradePanelSubStatInfo>("UI_UpgradePanelSubStatInfo", _subStatParent);

            if (this == null || gameObject == null || !gameObject.activeInHierarchy)
                return;

            if (subStatInfo != null)
            {
                subStatInfo.SetInfo(statOption);
            }
        }
    }

    public async void SetConsumeMaterialSlot()
    {
        for(int i=0; i< MAX_CONSUME_MATERIAL_SLOT_COUNT; i++)
        {
            UI_ConsumeMaterialSlot slot = await Managers.UI.MakeSubItemAsync<UI_ConsumeMaterialSlot>("UI_ConsumeMaterialSlot", _consumeMaterialParent);
            if (this == null || gameObject == null || !gameObject.activeInHierarchy)
                return;

            slot.SetCallback(() =>
            {
                if (_selectedSlot == null)
                {
                    Debug.Log("강화할 장비를 먼저 선택해주세요!");
                    return;
                }

                OpenMaterialSelectPopup();
            });

        }
    }

    private async void OpenMaterialSelectPopup()
    {
        UI_MaterialSelectPopup popup = await Managers.UI.ShowPopupUIAsync<UI_MaterialSelectPopup>("UI_MaterialSelectPopup");
        // 나중에 여기서 popup 쪽에 데이터나 콜백을 넘겨줄 수 있습니다.
        if(_selectedSlot != null) popup.SetSelectedSlot(_selectedSlot);
    }

    private void OnDestroy()
    {
        foreach (UI_ItemSlot slot in _itemSlots)
        {
            slot.SetCallback(null, null);
            Managers.Resource.Destroy(slot.gameObject);
        }  
    }

}
