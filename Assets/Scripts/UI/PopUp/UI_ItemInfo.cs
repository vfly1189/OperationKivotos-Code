using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_ItemInfo : UI_Base
{
    // 1. 자기 자신을 저장할 스태틱 변수
    private static UI_ItemInfo _instance;
    private static bool _isLoading = false;

    // 진행 중인 Show 요청 추적. async 로드 도중 마우스가 슬롯을 벗어나거나
    // 다른 슬롯으로 옮겨가면 이 값이 바뀌어, 뒤늦게 완료된 Show가 자기 차례를 폐기한다.
    private static InventorySlot _pendingSlot;

    // [측정용] 세션 첫 표시 1회만 계측한다. (Before/After 비교)
    private static bool _firstShowMeasured = false;


    [SerializeField] Image _itemIcon;

    [SerializeField] TextMeshProUGUI _itemName;
    [SerializeField] TextMeshProUGUI _itemDescription;
    [SerializeField] TextMeshProUGUI _itemUpgradeLevel;

    [SerializeField] TextMeshProUGUI _itemGrade;
    [SerializeField] TextMeshProUGUI _itemTier;

    [SerializeField] Transform _mainStatParent;
    [SerializeField] Transform _subStatParent;

    private const int MAX_STAT_COUNT = 6;
    private List<UI_MainStatInfo> _mainStatSlots = new();
    private List<UI_SubStatInfo> _subStatSlots = new();

    private bool _statSlotsReady = false;
    private bool _buildingStats = false;

    // UI_Base.Start()가 부르지만 여기서 스탯 슬롯을 만들지 않는다.
    // (비활성 오브젝트는 Start가 안 돌고, SetInfo가 Init보다 먼저 실행되는 레이스가 있었음)
    // 생성은 GetInstanceAsync가 await하는 EnsureStatSlotsAsync로 이동.
    public override void Init() { }

    //  스탯 슬롯(Main 6 + Sub 6)을 최초 1회 생성. 중복 진입은 완료까지 대기.
    private async UniTask EnsureStatSlotsAsync()
    {
        if (_statSlotsReady) return;
        if (_buildingStats) { await UniTask.WaitUntil(() => _statSlotsReady); return; }

        _buildingStats = true;
        for (int i = 0; i < MAX_STAT_COUNT; i++)
        {
            var main = await Managers.UI.MakeSubItemAsync<UI_MainStatInfo>("UI_MainStatInfo", _mainStatParent);
            main.gameObject.SetActive(false);
            _mainStatSlots.Add(main);

            var sub = await Managers.UI.MakeSubItemAsync<UI_SubStatInfo>("UI_SubStatInfo", _subStatParent);
            sub.gameObject.SetActive(false);
            _subStatSlots.Add(sub);
        }
        _buildingStats = false;
        _statSlotsReady = true;
    }

    public void SetInfo(InventorySlot slotData)
    {
        if (slotData.IsEquipment) SetEquipmentInfo(slotData);
        else SetMaterialInfo(slotData);
    }

    public void SetEquipmentInfo(InventorySlot inventorySlot)
    {
        SetIcon(inventorySlot.itemID, ItemCategory.Equipment).Forget();
        SetItemName(inventorySlot.itemID, ItemCategory.Equipment);
        SetDescription(inventorySlot.itemID, ItemCategory.Equipment);
        SetUpgradeLevel(inventorySlot.EquipInstance.UpgradeLevel);
        SetGrade(inventorySlot.itemID, ItemCategory.Equipment);
        SetTier(inventorySlot.itemID, ItemCategory.Equipment);

        //  Destroy/생성 없이 SetActive만
        SetMainStat(inventorySlot.EquipInstance);
        SetSubStat(inventorySlot.EquipInstance);
    }

    public void SetMaterialInfo(InventorySlot inventorySlot)
    {
        SetIcon(inventorySlot.itemID, ItemCategory.Material).Forget();
        SetItemName(inventorySlot.itemID, ItemCategory.Material);
        SetDescription(inventorySlot.itemID, ItemCategory.Material);
        SetGrade(inventorySlot.itemID, ItemCategory.Material);
        SetTier(inventorySlot.itemID, ItemCategory.Material);
        _itemUpgradeLevel.text = "";

        //  재료 아이템은 스탯 없으므로 전부 숨기기
        _mainStatSlots.ForEach(s => s.gameObject.SetActive(false));
        _subStatSlots.ForEach(s => s.gameObject.SetActive(false));
    }

    //  완전 동기 - Destroy/생성 없음
    private void SetMainStat(EquipmentInstance instance)
    {
        var stats = instance.MainStats;
        for (int i = 0; i < _mainStatSlots.Count; i++)
        {
            bool active = i < stats.Count;
            _mainStatSlots[i].gameObject.SetActive(active);
            if (active) _mainStatSlots[i].SetInfo(stats[i]);
        }
    }

    private void SetSubStat(EquipmentInstance instance)
    {
        var stats = instance.SubStats;
        for (int i = 0; i < _subStatSlots.Count; i++)
        {
            bool active = i < stats.Count;
            _subStatSlots[i].gameObject.SetActive(active);
            if (active) _subStatSlots[i].SetInfo(stats[i]);
        }
    }

    public async UniTask SetIcon(int itemID, ItemCategory category)
    {
        BaseItemData data = Managers.Data.GetItemData(itemID, category);
        if (data == null) return;

        // 아이콘 아틀라스는 아이템 타입별로 다르다. (UI_ItemSlot.SetItemIcon과 동일 규칙)
        // 이전엔 "EquipmentIconAtlas"를 하드코딩해 재료/소비 아이템 아이콘이 안 떴음.
        string atlasKey = data switch
        {
            EquipmentData => "EquipmentIconAtlas",
            ConsumableData => "ConsumablesAtlas",
            MaterialData => "MaterialIconAtlas",
            _ => "CommonAtlas"
        };

        Sprite icon = await Managers.Resource.GetSpriteFromAtlasAsync(atlasKey, data.IconKey);

        if (this == null || !gameObject.activeInHierarchy) return;
        _itemIcon.sprite = icon;
    }

    public void SetItemName(int itemID, ItemCategory category)
        => _itemName.text = Managers.Data.GetItemData(itemID, category).Name;

    public void SetDescription(int itemID, ItemCategory category)
        => _itemDescription.text = Managers.Data.GetItemData(itemID, category).Description;

    public void SetUpgradeLevel(int upgradeLevel)
        => _itemUpgradeLevel.text = "+" + upgradeLevel;

    public void SetGrade(int itemID, ItemCategory category)
    {
        ItemGrade grade = Managers.Data.GetItemData(itemID, category).Grade;
        _itemGrade.text = grade.ToString();
        _itemGrade.color = ColorDict.GetGradeColor(grade);
    }

    public void SetTier(int itemID, ItemCategory category)
    {
        BaseItemData data = Managers.Data.GetItemData(itemID, category);
        int tier = data is EquipmentData eq ? eq.Tier
                 : data is MaterialData mat ? mat.Tier : 0;
        _itemTier.text = $"Tier {tier}";
    }


    //  인벤토리/강화 패널이 열릴 때 미리 호출해 첫 호버의 콜드 스타트(오브젝트 13개 생성)를 없앤다.
    //  PreloadEnabled=false로 두면 Before(콜드 스타트) 상태를 재현할 수 있다. (측정용 스위치)
    public static bool PreloadEnabled = true;
    public static void Preload()
    {
        if (!PreloadEnabled) return;
        GetInstanceAsync().Forget();
    }

    // 2. 인스턴스를 가져오거나 생성하는 헬퍼 함수 (스탯 슬롯 준비까지 보장)
    private static async UniTask<UI_ItemInfo> GetInstanceAsync()
    {
        if (_instance != null)
        {
            await _instance.EnsureStatSlotsAsync();
            return _instance;
        }
        if (_isLoading) // 누군가 이미 로딩 중이라면 끝날 때까지 대기
        {
            await UniTask.WaitUntil(() => _instance != null);
            await _instance.EnsureStatSlotsAsync();
            return _instance;
        }

        _isLoading = true;
        // UIManager는 생성만 돕습니다.
        _instance = await Managers.UI.MakeSubItemAsync<UI_ItemInfo>("UI_ItemInfo", Managers.UI.CanvasSystem.transform);
        _instance.gameObject.SetActive(false);
        _isLoading = false;

        await _instance.EnsureStatSlotsAsync(); // SetInfo 전에 슬롯이 반드시 준비되도록
        return _instance;
    }

    // 3. 외부에서 접근하는 정적(Static) 메서드
    public static async void ShowTooltip(InventorySlot slot, Vector2 screenPos)
    {
        if (slot == null || slot.IsEmpty) return;

        // 이 요청을 최신 요청으로 등록. async 로드가 끝난 뒤 요청이 바뀌었으면 폐기.
        _pendingSlot = slot;

        // [측정] 세션 첫 호버만 계측: 진입 → 실제 표시까지 걸린 시간/GC.
        //  After(Preload=true): 이미 만들어져 있어 ~0ms.  Before(Preload=false): 여기서 13개 생성.
        bool measure = !_firstShowMeasured;
        long startBytes = measure ? UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() : 0;
        var sw = measure ? System.Diagnostics.Stopwatch.StartNew() : null;

        var tooltip = await GetInstanceAsync();

        // await 도중 HideTooltip(마우스 이탈) 또는 다른 슬롯 진입 → 이 Show는 취소
        if (_pendingSlot != slot) return;

        tooltip.gameObject.SetActive(true);
        tooltip.SetInfo(slot);

        // 위치 조정 로직
        RectTransform tooltipRect = tooltip.GetComponent<RectTransform>();
        RectTransform canvasRect = Managers.UI.CanvasSystem.GetComponent<RectTransform>();

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out Vector2 localPoint))
        {
            localPoint += new Vector2(350f, 0);
            tooltipRect.localPosition = localPoint;
        }

        if (measure)
        {
            sw.Stop();
            _firstShowMeasured = true;
            long kb = (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() - startBytes) / 1024;
            GameLog.Log($"[TooltipProfiler] 첫 호버→표시: {sw.Elapsed.TotalMilliseconds:F1} ms | GC {kb} KB | Preload={PreloadEnabled}");
        }
    }

    public static void HideTooltip()
    {
        _pendingSlot = null; // 진행 중인 Show 요청 취소
        if (_instance != null && _instance.gameObject.activeSelf)
            _instance.gameObject.SetActive(false);
    }

    public static void RefreshItemTooltip()
    {
        // 툴팁이 켜져있지 않다면 무시
        if (_instance == null || !_instance.gameObject.activeSelf) return;

        // 방금 아이템이 교체/소모되어 빈 슬롯이 되었을 수 있으므로 
        // 일단 무조건 툴팁을 끕니다.
        HideTooltip();

        // 끄고 난 뒤, 유니티의 EventSystem을 이용해 현재 마우스(포인터) 아래에 
        // 어떤 UI가 있는지 검사하여 다시 OnPointerEnter 이벤트를 발생시킵니다.
        // (마우스가 여전히 아이템 슬롯 위에 있다면 툴팁이 즉시 다시 켜짐)

        // 3. New Input System의 마우스 연결 상태를 체크합니다.
        if (Mouse.current == null) return;

        // 4. 최신 Input System의 마우스 좌표를 가져옵니다. (Vector2 반환)
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // 5. 마우스 위치를 기반으로 UI Raycast를 쏩니다.
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = mousePos // <- 여기서 Input.mousePosition 대신 최신 좌표를 넣습니다.
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        if (results.Count > 0)
        {
            // 마우스 아래에 있는 첫 번째 UI 오브젝트를 가져옴
            GameObject hoveredObject = results[0].gameObject;

            // 그 오브젝트(또는 부모)에 UI_ItemSlot 컴포넌트가 있다면 Enter 이벤트를 수동 호출
            UI_ItemSlot slot = hoveredObject.GetComponentInParent<UI_ItemSlot>();
            if (slot != null)
            {
                slot.OnPointerEnter(pointerData);
            }

            // [추가] UI_EquipSlot 위에서 더블클릭으로 해제했을 때를 대비해 EquipSlot도 체크
            UI_EquipSlot equipSlot = hoveredObject.GetComponentInParent<UI_EquipSlot>();
            if (equipSlot != null)
            {
                equipSlot.OnPointerEnter(pointerData);
            }
        }
    }
}
