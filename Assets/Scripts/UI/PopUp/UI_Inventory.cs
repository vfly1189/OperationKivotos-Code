using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_Inventory : UI_PopUp
{
    [SerializeField] private Transform _contentParent; // ScrollView의 Content
    [SerializeField] private Button[] _tabButtons; // 0:장비, 1:소비, 2:재료

    private ItemCategory _currentCategory = ItemCategory.Equipment;
    private List<UI_ItemSlot> _activeSlots = new List<UI_ItemSlot>();

    public override void Init()
    {
        base.Init();

        // 1. 탭 버튼 이벤트 연결
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            int index = i;
            _tabButtons[i].onClick.AddListener(() => OnClickTab((ItemCategory)index));
        }

        // 2. InventoryManager의 갱신 이벤트 구독
        Managers.Inventory.OnInventoryUpdated -= RefreshUI;
        Managers.Inventory.OnInventoryUpdated += RefreshUI;

        // 초기 화면 그리기
        RefreshUI(_currentCategory);
    }

    private void OnClickTab(ItemCategory category)
    {
        if (_currentCategory == category) return;

        _currentCategory = category;
        RefreshUI(_currentCategory);
    }

    private async void RefreshUI(ItemCategory category)
    {
        // 현재 보고 있는 탭이 아니면 무시 (백그라운드에서 인벤토리 아이템을 먹었을 때 대비)
        if (_currentCategory != category) return;

        // 1. 인벤토리 데이터 가져오기
        var invenList = Managers.Inventory.Inventory[category];

        if (invenList == null) return;
        // 2. 슬롯 갯수 맞추기 (오브젝트 풀링/재사용 개념)
        // 부족하면 새로 만들고, 남으면 꺼둡니다. 매번 파괴(Destroy)하지 않습니다.

        // 부족한 만큼 생성
        while (_activeSlots.Count < invenList.Count)
        {
            // UIManager의 SubItem 생성 기능 활용 (Addressable Key 사용)
            UI_ItemSlot slot = await Managers.UI.MakeSubItemAsync<UI_ItemSlot>("UI_ItemSlot", _contentParent);
            _activeSlots.Add(slot);
        }

        // 3. 데이터 매핑 및 활성화/비활성화 처리
        for (int i = 0; i < _activeSlots.Count; i++)
        {
            if (i < invenList.Count)
            {
                _activeSlots[i].gameObject.SetActive(true);
                _activeSlots[i].SetInfo(invenList[i], category); // 데이터 주입
            }
            else
            {
                _activeSlots[i].gameObject.SetActive(false); // 남는 슬롯 숨기기
            }
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제 필수
        if (Managers.Inventory != null)
        {
            Managers.Inventory.OnInventoryUpdated -= RefreshUI;
        }
    }
}
