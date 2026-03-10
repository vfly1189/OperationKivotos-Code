using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_ItemSlot : UI_Base, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
{
    [SerializeField] private Image _defaultBackGround;
    [SerializeField] private Image _itemGradeBackGround;
    [SerializeField] private Image _itemIcon;
    [SerializeField] private TextMeshProUGUI _stackText;

    private int _slotIndex;

    public ItemCategory CurrentCategory => _currentCategory;
    private ItemCategory _currentCategory; // 카테고리도 기억해두면 좋음
    private InventorySlot _currentSlotData;

    public static UI_ItemSlot DraggingSlot = null;

    // 임시로 화면에 띄울 가짜(Ghost) 아이콘
    private static GameObject _dragGhost;
    private static Image _dragGhostImage;

    public override void Init()
    {
        // 클릭 이벤트 등을 바인딩하려면 여기서
    }

    public void SetInfo(InventorySlot slotData, ItemCategory category, int index)
    {
        _currentSlotData = slotData;
        _currentCategory = category;
        _slotIndex = index;

        if (slotData.IsEmpty)
        {
            _itemIcon.gameObject.SetActive(false);
            _itemGradeBackGround.gameObject.SetActive(false);
            _stackText.gameObject.SetActive(false);
            return;
        }

        // 아이템 정보가 있을 때 셋팅
        _itemIcon.gameObject.SetActive(true);
        _itemGradeBackGround.gameObject.SetActive(true);

        BaseItemData itemData = Managers.Data.GetItemData(slotData.itemID, category);
        if (itemData != null)
        {
            SetStackText(slotData.Amount);
            SetItemIcon(itemData.IconKey);
            SetGradeBackGround(itemData.Grade);
        }
    }

    private void SetStackText(int amount)
    {
        if (amount > 1)
        {
            _stackText.gameObject.SetActive(true);
            _stackText.text = "x " + amount.ToString();
        }
        else
        {
            _stackText.gameObject.SetActive(false);
        }
    }

    private async void SetItemIcon(string iconKey)
    {
        if (string.IsNullOrEmpty(iconKey)) return;

        Debug.Log($"Test : {iconKey}");
        // ResourceManager를 통해 비동기로 Sprite 로드
        Sprite sprite = await Managers.Resource.LoadAsync<Sprite>(iconKey);
        if (sprite != null && _itemIcon != null)
        {
            _itemIcon.sprite = sprite;
            _itemIcon.gameObject.SetActive(true);
        }
    }

    private async void SetGradeBackGround(ItemGrade grade)
    {
        // 등급에 맞는 Addressable Key 문자열 조합 (예: "Common_Gray", "Rare_Blue")
        string gradeKey = $"GradeBg_{grade.ToString()}"; // 예시
        Debug.Log($"GradeKey : {gradeKey}");
        Sprite bgSprite = await Managers.Resource.LoadAsync<Sprite>(gradeKey);
        if (bgSprite != null && _itemGradeBackGround != null)
        {
            _itemGradeBackGround.sprite = bgSprite;
        }
    }
    // --- Drag & Drop 구현부 ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 빈칸이면 드래그 불가
        if (_currentSlotData == null || _currentSlotData.IsEmpty) return;

        DraggingSlot = this;

        // 1. 고스트(임시) 아이콘 생성
        if (_dragGhost == null)
        {
            _dragGhost = new GameObject("DragGhost");
            _dragGhostImage = _dragGhost.AddComponent<Image>();
            _dragGhostImage.raycastTarget = false; // 자신이 이벤트를 막지 않게 설정

            // UI 맨 위에 그려지도록 최상단 Canvas를 찾아 자식으로 설정
            Canvas canvas = GetComponentInParent<Canvas>();
            _dragGhost.transform.SetParent(canvas.transform, false);
            _dragGhost.transform.SetAsLastSibling(); // 맨 앞으로 빼기
        }

        // 2. 가짜 아이콘에 현재 내 아이콘 이미지 입히기
        _dragGhost.SetActive(true);
        _dragGhostImage.sprite = _itemIcon.sprite;
        _dragGhostImage.rectTransform.sizeDelta = _itemIcon.rectTransform.sizeDelta;

        // 3. 원본 슬롯 아이콘은 드래그 중임을 알 수 있게 살짝 투명하게 처리
        _itemIcon.color = new Color(1, 1, 1, 0.5f);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (DraggingSlot != this || _dragGhost == null) return;

        // 가짜 아이콘이 마우스를 따라다님
        _dragGhost.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (DraggingSlot != this) return;

        // 가짜 아이콘 숨기기
        if (_dragGhost != null) _dragGhost.SetActive(false);

        // 원본 아이콘 투명도 원상복구
        _itemIcon.color = new Color(1, 1, 1, 1f);

        DraggingSlot = null;
    }

    public void OnDrop(PointerEventData eventData)
    {
        // 내 위에 무언가 떨어졌을 때
        if (DraggingSlot != null && DraggingSlot != this)
        {
            // 같은 카테고리(탭) 탭 안에서만 작동하도록 방어
            if (this._currentCategory == DraggingSlot._currentCategory)
            {
                Managers.Inventory.SwapItems(_currentCategory, DraggingSlot._slotIndex, this._slotIndex);
            }
        }
    }

    // 더블 클릭 시 장착 로직
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2)
        {
            // 장비 탭이고 빈 슬롯이 아닐 때만 장착 시도
            if (_currentCategory == ItemCategory.Equipment && _currentSlotData != null && !_currentSlotData.IsEmpty)
            {
                Managers.Equipment.Equip(_slotIndex);
            }
        }
    }

}
