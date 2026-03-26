using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

using TMPro;

using UnityEngine;
using UnityEngine.UI;
using static WeaponUpgradeService;

public class UI_RelicUpgradePanel : UI_Base, IItemSlotHandler
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

    private EquipmentUpgradeService _upgradeService;
    private List<UI_ItemSlot> _itemSlots = new List<UI_ItemSlot>();

    private List<UI_ConsumeMaterialSlot> _consumeMaterialSlots = new();
    private List<UI_UpgradePanelMainStatInfo> _mainStatSlots = new();
    private List<UI_UpgradePanelSubStatInfo> _subStatSlots = new();


    private const int MAX_STAT_COUNT = 6;
    private CancellationTokenSource _itemInfoCts;
    private CancellationTokenSource _linkedCts;

    public override void Init()
    {
        InitAsync().Forget();
    }

    private async UniTask InitAsync()
    {
        //  Service 생성 및 이벤트 구독
        _upgradeService = new EquipmentUpgradeService();
        _upgradeService.OnEquipmentSelected += OnEquipmentSelected;
        _upgradeService.OnMaterialSlotChanged += OnMaterialSlotChanged;
        _upgradeService.OnExpPreviewChanged += OnExpPreviewChanged;

        // InitAsync() 내 Service 이벤트 구독 부분에 추가
        _upgradeService.OnUpgradeExecuted += OnUpgradeExecuted;

        // 버튼 이벤트 등록
        _upgradeButton.onClick.AddListener(() => _upgradeService.ExecuteUpgrade());

        ResetItemInfo();

        //  슬롯들 Init에서 미리 생성
        await UniTask.WhenAll(
            SetHavingRelicScrollView(),
            SetConsumeMaterialSlots(),
            PreloadStatSlots()
        );
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

    private async UniTask SetHavingRelicScrollView()
    {
        foreach (UI_ItemSlot slot in _itemSlots)
            Managers.Resource.Destroy(slot.gameObject);

        _itemSlots.Clear(); // ← 추가

        InventorySlot[] relics = Managers.Inventory.Inventory[ItemCategory.Equipment];
        foreach (InventorySlot relic in relics)
        {
            if (relic.IsEmpty) continue;
            UI_ItemSlot slot = await Managers.UI.MakeSubItemAsync<UI_ItemSlot>("UI_ItemSlot", _havingRelicScollView);
            slot.gameObject.SetActive(true);
            slot.SetSubTextFormatter(ItemSlotSubText.UpgradeLevel);
            slot.SetSubTextStyle(SlotSubTextStyle.UpgradeLevel);
            slot.SetInfo(relic, ItemCategory.Equipment, -1);
            slot.SetHandler(this);
            _itemSlots.Add(slot);
        }
    }

    private async UniTask SetConsumeMaterialSlots()
    {
        for (int i = 0; i < EquipmentUpgradeService.MAX_MATERIAL_SLOTS; i++)
        {
            int index = i;
            var slot = await Managers.UI.MakeSubItemAsync<UI_ConsumeMaterialSlot>(
                "UI_ConsumeMaterialSlot", _consumeMaterialParent);

            slot.SetCallback(
                onClick: () =>
                {
                    if (_upgradeService.SelectedEquipment == null) return;
                    OpenMaterialSelectPopup();
                },
                onCancel: () => _upgradeService.RemoveMaterial(index) //  Service에 위임
            );

            _consumeMaterialSlots.Add(slot);
        }
    }

    private async UniTask PreloadStatSlots()
    {
        for (int i = 0; i < MAX_STAT_COUNT; i++)
        {
            var main = await Managers.UI.MakeSubItemAsync<UI_UpgradePanelMainStatInfo>(
                "UI_UpgradePanelMainStatInfo", _mainStatParent);
            main.gameObject.SetActive(false);
            _mainStatSlots.Add(main);

            var sub = await Managers.UI.MakeSubItemAsync<UI_UpgradePanelSubStatInfo>(
                "UI_UpgradePanelSubStatInfo", _subStatParent);
            sub.gameObject.SetActive(false);
            _subStatSlots.Add(sub);
        }
    }

    //재료슬롯 콜백 함수
    private async void OpenMaterialSelectPopup()
    {
        Debug.Log("재료 슬롯 눌림");
        var popup = await Managers.UI.ShowPopupUIAsync<UI_MaterialSelectPopup>("UI_MaterialSelectPopup");
        //  Service를 통째로 주입 → 팝업도 같은 Service를 바라봄
        popup.SetService(_upgradeService);
    }

    // =========================================================
    // Service 이벤트 수신 → UI 갱신
    // =========================================================
    private void OnEquipmentSelected(InventorySlot slot)
    {
        RefreshItemInfo(slot).Forget();
        _upgradeButton.interactable = _upgradeService.CanUpgrade();
    }

    // OnMaterialSlotChanged 시그니처 변경
    private void OnMaterialSlotChanged(int index, MaterialEntry entry)
    {
        if (entry == null || entry.IsEmpty)
            _consumeMaterialSlots[index].ClearSlot();
        else
            _consumeMaterialSlots[index].SetSlot(entry);

        _upgradeButton.interactable = _upgradeService.CanUpgrade();
    }
    // 수신 후 UI 갱신
    private void OnExpPreviewChanged(ExpPreviewResult result)
    {
        _itemUpgradeLevel.text = $"+{result.SimulatedLevel}"; // 공통으로 빼기

        int itemID = _upgradeService.SelectedEquipment.itemID;
        ItemGrade grade = Managers.Data.GetItemData(itemID, ItemCategory.Equipment).Grade;
        int maxLevel = Managers.Data.GetData<ItemGrade, GradeConfig>(grade).MaxLevel;

        if (result.SimulatedLevel < maxLevel)
        {
            _itemExpText.text = $"{result.SimulatedExp} / {result.SimulatedRequireExp}";
            _itemExpBar.value = (float)result.SimulatedExp / result.SimulatedRequireExp;
        }
        else
        {
            _itemExpText.text = "MAX LEVEL";
            _itemExpBar.value = 0;
        }

        int requireCredit = _upgradeService.CalcCredit(_upgradeService.SelectedEquipment.EquipInstance.UpgradeLevel, result.SimulatedLevel);

        _consumeCreditNum.text = requireCredit.ToString("N0");

        if (!Managers.Wallet.CanConsumeCurreny(CurrencyType.Credit, requireCredit))
            _consumeCreditNum.color = Color.red;     
        else
            _consumeCreditNum.color = Color.white;
    }

    // =========================================================
    // 장비 정보 갱신 (Service 이벤트로만 호출됨)
    // =========================================================
    private async UniTask RefreshItemInfo(InventorySlot slotData)
    {
        _itemInfoCts?.Cancel();
        _itemInfoCts?.Dispose();
        _linkedCts?.Dispose();
        _itemInfoCts = new CancellationTokenSource();
        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _itemInfoCts.Token, this.GetCancellationTokenOnDestroy());
        var token = _linkedCts.Token;

        EquipmentData data = Managers.Data.GetData<int, EquipmentData>(slotData.itemID);

        //_itemIcon.sprite = await Managers.Resource.LoadAsync<Sprite>(data.IconKey, isGlobal: true)
        //    .AttachExternalCancellation(token);

        _itemIcon.sprite = await Managers.Resource.GetSpriteFromAtlasAsync("EquipmentIconAtlas", data.IconKey)
            .AttachExternalCancellation(token);

        _itemIcon.gameObject.SetActive(true);
        _itemName.text = data.Name;
        _itemType.text = data.EquipPart;
        _itemTier.text = $"Tier {data.Tier}";
        _itemUpgradeLevel.text = $"+{slotData.EquipInstance.UpgradeLevel}";

        ItemGrade grade = Managers.Data.GetItemData(slotData.itemID, ItemCategory.Equipment).Grade;
        int maxLevel = Managers.Data.GetData<ItemGrade, GradeConfig>(grade).MaxLevel;

        if (slotData.EquipInstance.UpgradeLevel == maxLevel)
        {
            _itemExpText.text = "MAX LEVEL";
            _itemExpBar.value = 0;
        }
        else
        {
            _itemExpText.text = $"{slotData.EquipInstance.CurrentExp} / {slotData.EquipInstance.NextLevelRequireExp}";
            _itemExpBar.value = (float)slotData.EquipInstance.CurrentExp / slotData.EquipInstance.NextLevelRequireExp;
        }

        _consumeCreditNum.text = "";
        

        // SetActive 토글만 (Destroy/생성 없음)
        RefreshMainStat(slotData.EquipInstance);
        RefreshSubStat(slotData.EquipInstance);
    }

    private void RefreshMainStat(EquipmentInstance instance)
    {
        var stats = instance.MainStats;
        for (int i = 0; i < _mainStatSlots.Count; i++)
        {
            bool active = i < stats.Count;
            _mainStatSlots[i].gameObject.SetActive(active);
            if (active) _mainStatSlots[i].SetInfo(stats[i]);
        }
    }

    private void RefreshSubStat(EquipmentInstance instance)
    {
        var stats = instance.SubStats;
        for (int i = 0; i < _subStatSlots.Count; i++)
        {
            bool active = i < stats.Count;
            _subStatSlots[i].gameObject.SetActive(active);
            if (active) _subStatSlots[i].SetInfo(stats[i]);
        }
    }

    private void OnUpgradeExecuted(InventorySlot slot, UpgradeResult result)
    {
        // 아이템 정보 갱신
        RefreshItemInfo(slot).Forget();

        //여기서 비동기로 해버리면 레벨 갱신이랑 타이밍싸움이걸림.
        SetHavingRelicScrollView().Forget();


        //// 스크롤뷰 슬롯도 레벨 표시 갱신 (해당 슬롯 찾아서)
        //var uiSlot = _itemSlots.Find(s => s.CurrentSlotData == slot);
        //uiSlot?.SetInfo(slot, ItemCategory.Equipment, -1);

        //_upgradeButton.interactable = _upgradeService.CanUpgrade();
    }


    //  인터페이스 구현은 void, 내부에서 async 메서드에 위임
    public void OnSlotDoubleClicked(UI_ItemSlot slot)
    {
        _upgradeService.SelectEquipment(slot.CurrentSlotData);
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
        // Managers.UI.HideItemTooltip();
        UI_ItemInfo.HideTooltip();
    }

    // Refresh()용 - Destroy 없이 표시만 초기화
    private void ResetEquipmentDisplay()
    {
        _itemIcon.gameObject.SetActive(false);
        _itemUpgradeLevel.text = "";
        _itemName.text = "선택된 장비 없음";
        _itemType.text = "";
        _itemTier.text = "";
        _itemExpBar.value = 0f;
        _itemExpText.text = "0 / 0";
        _consumeCreditNum.text = "";
        _upgradeButton.interactable = false;

        // 스탯 슬롯은 Destroy 없이 SetActive(false)만
        foreach (var slot in _mainStatSlots) slot.gameObject.SetActive(false);
        foreach (var slot in _subStatSlots) slot.gameObject.SetActive(false);
    }

    public override void Refresh()
    {
        // 선택된 장비가 있으면 그 정보 갱신
        // 재료 목록 갱신
        SetHavingRelicScrollView().Forget();
        ResetEquipmentDisplay();
        _upgradeService?.DeselectEquipment(); // ← 서비스 상태도 초기화
    }

    private void OnDestroy()
    {
        _itemInfoCts?.Cancel();
        _itemInfoCts?.Dispose();
        _linkedCts?.Dispose();

        // 이벤트 구독 해제
        if (_upgradeService != null)
        {
            _upgradeService.OnEquipmentSelected -= OnEquipmentSelected;
            _upgradeService.OnMaterialSlotChanged -= OnMaterialSlotChanged;
            _upgradeService.OnExpPreviewChanged -= OnExpPreviewChanged;
            _upgradeService.OnUpgradeExecuted -= OnUpgradeExecuted;
        }

        foreach (var slot in _itemSlots)
        {
            slot.SetHandler(null);
            slot.CancelButtonOff();
            Managers.Resource.Destroy(slot.gameObject);
        }
    }

    
}
