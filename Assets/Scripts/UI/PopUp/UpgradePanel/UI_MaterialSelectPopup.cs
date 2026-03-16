using Cysharp.Threading.Tasks;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

public enum UpgradeBookID
{
    Small = 30003,
    Medium,
    Large
}


public class UI_MaterialSelectPopup : UI_PopUp, IItemSlotHandler
{
    [Header("등록 & 취소 버튼")]
    [SerializeField] Button _confirmButton;
    [SerializeField] Button _cancelButton;

    [Header("컨텐츠 뷰")]
    [SerializeField] Transform _contents;

    private List<UI_ItemSlot> _uiSlots = new List<UI_ItemSlot>();


    private EquipmentUpgradeService _upgradeService; //  같은 Service 공유

    //  slot 데이터 → UI_ItemSlot 역조회용
    private Dictionary<InventorySlot, UI_ItemSlot> _slotMap = new();
    public override void Init()
    {
        _cancelButton.onClick.AddListener(ClosePopupUI);
    }

    //  Panel에서 Service를 주입받음
    public void SetService(EquipmentUpgradeService service)
    {
        _upgradeService = service;
        _upgradeService.OnMaterialSlotChanged += OnMaterialSlotChanged;
        InitContentView().Forget();
    }

    //  생성 완료 후 현재 등록 상태 즉시 반영
    private async UniTask InitContentView()
    {
        await SetContentView();
        RefreshCancelButtons(); // 팝업 재오픈 시 이미 등록된 것들 취소버튼 ON
    }

    private async UniTask SetContentView()
    {
        foreach (var slot in _upgradeService.GetAvailableMaterials())
        {
            var uiSlot = await Managers.UI.MakeSubItemAsync<UI_ItemSlot>("UI_ItemSlot", _contents);
            uiSlot.gameObject.SetActive(true);
            uiSlot.SetInfo(slot, slot.IsEquipment ? ItemCategory.Equipment : ItemCategory.Material, -1);
            uiSlot.SetHandler(this);
            _slotMap[slot] = uiSlot;
            _uiSlots.Add(uiSlot);
        }
    }

    // Service 이벤트 수신 → 팝업 내 취소버튼 갱신
    private void OnMaterialSlotChanged(int index, MaterialEntry entry)
    {
        RefreshCancelButtons();
    }


    private void RefreshCancelButtons()
    {
        foreach (var (inventorySlot, uiSlot) in _slotMap)
        {
            int idx = _upgradeService.FindMaterialSlotIndex(inventorySlot);
            if (idx >= 0)
            {
                int capturedIdx = idx;
                //  등록된 슬롯 → 취소버튼 ON
                uiSlot.SetCancelActive(true, () => _upgradeService.RemoveMaterial(capturedIdx));
            }
            else
            {
                //  등록 해제된 슬롯 → 취소버튼 OFF
                uiSlot.SetCancelActive(false);
            }
        }
    }


    public void OnSlotClicked(UI_ItemSlot slot)
    {
        _upgradeService.TryAddMaterial(slot.CurrentSlotData);
    }

 

    private void OnDestroy()
    {
        if (_upgradeService != null)
            _upgradeService.OnMaterialSlotChanged -= OnMaterialSlotChanged;

        foreach (var slot in _uiSlots)
        {
            slot.SetHandler(null);
            slot.CancelButtonOff();
            Managers.Resource.Destroy(slot.gameObject);
        }
    }
}
