using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_EquipmentDecomposePanel : UI_Base, IItemSlotHandler
{
    [Header("보유 장비 목록")]
    [SerializeField] private Transform _havingRelicScrollView;

    [Header("분해목록")]
    [SerializeField] private Transform _decomposeRelicView;

    [Header("분해 결과 그룹")]
    [SerializeField] private TextMeshProUGUI _upgradeBookSmallCountText;
    [SerializeField] private TextMeshProUGUI _upgradeBookMediumCountText;
    [SerializeField] private TextMeshProUGUI _upgradeBookLargeCountText;

    [Header("분해하기 버튼")]
    [SerializeField] private Button _decomposeButton;

    private EquipmentDecomposeService _decomposeService;

    private readonly List<UI_ItemSlot> _itemSlots = new List<UI_ItemSlot>();
    private readonly List<UI_ConsumeMaterialSlot> _consumeMaterialSlots = new();

    private readonly Dictionary<InventorySlot, UI_ItemSlot> _slotMap = new();

    private bool _isSpawning = false; // 중복 실행 방지 플래그

    public override void Init()
    {
        InitAsync().Forget();
    }

    private async UniTask InitAsync()
    {
        _decomposeService = new EquipmentDecomposeService();
        _decomposeService.OnMaterialSlotChanged += OnMaterialSlotChanged;
        _decomposeService.OnDecomposeExecuted += OnDecomposeExecuted;
        _decomposeService.OnMaterialsCleared += OnMaterialClear;
        ResetResultText();

        _decomposeButton.onClick.AddListener(() => _decomposeService.ExecuteDecompose());

        ////  슬롯들 Init에서 미리 생성
        //await UniTask.WhenAll(
        //    SetHavingRelicScrollView(),
        //    SetConsumeMaterialSlots()
        //);

        await SetConsumeMaterialSlots();
    }

    private void ResetResultText()
    {
        _decomposeButton.interactable = false;

        _upgradeBookSmallCountText.text = "x0";
        _upgradeBookMediumCountText.text = "x0";
        _upgradeBookLargeCountText.text = "x0";
    }

    private void OnDecomposeExecuted()
    {
        // 아이템 정보 갱신
        SetHavingRelicScrollView().Forget();
    }

    private async UniTask SetHavingRelicScrollView()
    {
        if (_isSpawning) return; // 이미 생성 중이면 무시
        _isSpawning = true;

        GameLog.Log("SetHavingRelicScrollView() 호출됨");

        foreach (UI_ItemSlot slot in _itemSlots)
            Managers.Resource.Destroy(slot.gameObject);

        _itemSlots.Clear(); // ← 추가
        _slotMap.Clear();

        InventorySlot[] relics = Managers.Inventory.Inventory[ItemCategory.Equipment];

        foreach (InventorySlot relic in relics)
        {
            if (relic.IsEmpty) continue;
            UI_ItemSlot slot = await Managers.UI.MakeSubItemAsync<UI_ItemSlot>("UI_ItemSlot", _havingRelicScrollView);
            slot.gameObject.SetActive(true);
            slot.SetSubTextFormatter(ItemSlotSubText.UpgradeLevel);
            slot.SetSubTextStyle(SlotSubTextStyle.UpgradeLevel);
            slot.SetInfo(relic, ItemCategory.Equipment, -1);
            slot.SetHandler(this);
            _slotMap[relic] = slot;
            _itemSlots.Add(slot);
        }

        _isSpawning = false; // 생성 완료
    }

    private async UniTask SetConsumeMaterialSlots()
    {
        for (int i = 0; i < EquipmentDecomposeService.MAX_DECOMPOSE_COUNT; i++)
        {
            int index = i;
            var slot = await Managers.UI.MakeSubItemAsync<UI_ConsumeMaterialSlot>(
                "UI_ConsumeMaterialSlot", _decomposeRelicView);

            slot.SetCallback(
                onClick: null,
                onCancel: () =>
                {
                    _decomposeService.RemoveMaterial(index); //  Service에 위임
                }
            );

            _consumeMaterialSlots.Add(slot);
        }
    }

    public void OnSlotClicked(UI_ItemSlot slot)
    {
        // 서비스에서 분해 목록에 등록
        _decomposeService.SelectEquipment(slot.CurrentSlotData);
    }

    public void OnSlotPointerEnter(UI_ItemSlot slot, Vector2 screenPos)
    {
        if (slot.CurrentSlotData != null && !slot.CurrentSlotData.IsEmpty)
        {
            // 툴팁 활성화 및 정보 셋팅
            //Managers.UI.ShowItemTooltip(slot.CurrentSlotData, screenPos);
            UI_ItemInfo.ShowTooltip(slot.CurrentSlotData, screenPos);
        }
    }

    public void OnSlotPointerExit(UI_ItemSlot slot)
    {
        //Managers.UI.HideItemTooltip();
        UI_ItemInfo.HideTooltip();
    }


    private void OnMaterialSlotChanged(int index, MaterialEntry slot)
    {
        if (slot == null || slot.IsEmpty)
        {
            _consumeMaterialSlots[index].ClearSlot();
        }
        else
        {
            _consumeMaterialSlots[index].SetSlot(slot);
        }
        RefreshResult();
        RefreshCancelButtons();
        RefreshDecomposeButton();
    }

    private void OnMaterialClear()
    {
        foreach (var slot in _consumeMaterialSlots)
            slot.ClearSlot();

        RefreshResult();
        RefreshCancelButtons();
        RefreshDecomposeButton();
    }

    private void RefreshDecomposeButton()
    {
        bool hasAny = _decomposeService.MaterialSlots.Any(e => !e.IsEmpty);
        _decomposeButton.interactable = hasAny;
    }

    private void RefreshCancelButtons()
    {
        foreach (var (inventorySlot, uiSlot) in _slotMap)
        {
            int idx = _decomposeService.FindMaterialSlotIndex(inventorySlot);
            if (idx >= 0)
            {
                int capturedIdx = idx;
                //  등록된 슬롯 → 취소버튼 ON
                uiSlot.SetCancelActive(true, () => _decomposeService.RemoveMaterial(capturedIdx));
            }
            else
            {
                //  등록 해제된 슬롯 → 취소버튼 OFF
                uiSlot.SetCancelActive(false);
            }
        }
    }

    private void RefreshResult()
    {
        IReadOnlyList<int> result = _decomposeService.GetResult();
        _upgradeBookSmallCountText.text = $"x{result[0]:N0}";
        _upgradeBookMediumCountText.text = $"x{result[1]:N0}";
        _upgradeBookLargeCountText.text = $"x{result[2]:N0}";
    }

    public override void Refresh()
    {
        SetHavingRelicScrollView().Forget();
        _decomposeService?.ClearMaterials();
    }

    private void OnDestroy()
    {
        _decomposeService.OnMaterialSlotChanged -= OnMaterialSlotChanged;
        _decomposeService.OnDecomposeExecuted -= OnDecomposeExecuted;
        _decomposeService.OnMaterialsCleared -= OnMaterialClear;
    }


}
