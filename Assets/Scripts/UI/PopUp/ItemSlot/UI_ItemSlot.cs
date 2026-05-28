using Cysharp.Threading.Tasks;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_ItemSlot : UI_Base, IBeginDragHandler
    , IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _defaultBackGround;
    [SerializeField] private Image _itemGradeBackGround;
    [SerializeField] private Image _itemIcon;
    [SerializeField] private TextMeshProUGUI _stackText;
    [SerializeField] private Button _cancelButton; // 강화재료 취소 버튼, 꺼져있으니 사용할거면 켜야됨

    private int _slotIndex;
    private IItemSlotHandler _handler;

    public ItemCategory CurrentCategory => _currentCategory;
    private ItemCategory _currentCategory; // 카테고리도 기억해두면 좋음
    public InventorySlot CurrentSlotData { get; private set; }

    public static UI_ItemSlot DraggingSlot = null;

    // 임시로 화면에 띄울 가짜(Ghost) 아이콘
    private static GameObject _dragGhost;
    private static Image _dragGhostImage;

    // _slotIndex의 getter를 만들어 외부에서 읽을 수 있게 합니다.
    public int SlotIndex => _slotIndex;

    private Func<InventorySlot, string> _subTextFormatter;

    public override void Init()
    {
        if (_cancelButton != null) _cancelButton.gameObject.SetActive(false);
        // 클릭 이벤트 등을 바인딩하려면 여기서
        //_cancelButton.onClick.AddListener(onSlotClick);
    }

    // 신규 메서드 추가
    public void SetCancelActive(bool active, Action onCancel = null)
    {
        if (_cancelButton == null) return;
        _cancelButton.gameObject.SetActive(active);
        _cancelButton.onClick.RemoveAllListeners();
        if (active && onCancel != null)
            _cancelButton.onClick.AddListener(() => onCancel());
    }

    public void SetInfo(InventorySlot slotData, ItemCategory category, int index)
    {
        CurrentSlotData = slotData;
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
            SetItemIcon(itemData);
            SetGradeBackGround(itemData.Grade);

            //if (slotData.IsEquipment)
            //    SetEquipmentUpgradeLevelText(slotData.EquipInstance.UpgradeLevel);
            //else
            //    SetStackText(slotData.Amount);

            RefreshSubText(slotData); // ← 분기 제거, 포맷터 호출로 일원화

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

    private void SetEquipmentUpgradeLevelText(int upgradeLevel)
    {
        _stackText.gameObject.SetActive(true);
        _stackText.text = "+" + upgradeLevel.ToString();  
    }


    private async void SetItemIcon(BaseItemData itemData)
    {
        if (itemData == null) return;

        // 1. 타입 패턴 매칭을 통해 아틀라스 키와 아이콘 이름 분기 처리
        (string atlasKey, string iconName) = itemData switch
        {
            // itemData가 EquipmentData 타입이면 equip 변수에 할당하고 블록 실행
            EquipmentData equip => ("EquipmentIconAtlas", equip.IconKey),

            // itemData가 ConsumableData 타입이면 cons 변수에 할당하고 블록 실행
            ConsumableData cons => ("ConsumablesAtlas", cons.IconKey),

            // itemData가 MaterialData 타입이면 mat 변수에 할당하고 블록 실행
            MaterialData mat => ("MaterialIconAtlas", mat.IconKey),

            // 어떤 타입에도 맞지 않거나 에러 방지용 (기본값)
            _ => ("CommonAtlas", itemData.IconKey)
        };

        // ResourceManager를 통해 비동기로 Sprite 로드
        //Sprite sprite = await Managers.Resource.LoadAsync<Sprite>(iconKey, isGlobal:true);
        Sprite sprite = await Managers.Resource.GetSpriteFromAtlasAsync(atlasKey, iconName);

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

        //Sprite bgSprite = await Managers.Resource.LoadAsync<Sprite>(gradeKey, isGlobal:true);
        Sprite bgSprite = await Managers.Resource.GetSpriteFromAtlasAsync("ItemGradeAtlas", gradeKey);

        if (bgSprite != null && _itemGradeBackGround != null)
        {
            _itemGradeBackGround.sprite = bgSprite;
        }
    }

    public void SetSubTextFormatter(Func<InventorySlot, string> formatter)
    {
        _subTextFormatter = formatter;
    }

    public void SetSubTextStyle(SlotSubTextStyle style)
    {
        _stackText.fontSize = style.FontSize;
        _stackText.alignment = style.Alignment;
        _stackText.color = style.Color;
    }

    // 외부에서 재호출도 가능 (예: 선택수 변경 시 갱신)
    public void RefreshSubText(InventorySlot slotData = null)
    {
        var data = slotData ?? CurrentSlotData;
        if (data == null) return;

        if (_subTextFormatter != null)
        {
            string text = _subTextFormatter(data);
            bool hasText = !string.IsNullOrEmpty(text);
            _stackText.gameObject.SetActive(hasText);
            if (hasText) _stackText.text = text;
        }
        else
        {
            // 포맷터 없으면 기본 동작 유지 (하위 호환)
            if (data.IsEquipment)
                SetEquipmentUpgradeLevelText(data.EquipInstance.UpgradeLevel);
            else
                SetStackText(data.Amount);
        }
    }

    public void CancelButtonOn() { _cancelButton.gameObject.SetActive(true); }
    public void CancelButtonOff() {  _cancelButton.gameObject.SetActive(false); }

    // --- Drag & Drop 구현부 ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 빈칸이면 드래그 불가
        if (CurrentSlotData == null || CurrentSlotData.IsEmpty) return;

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

    public void SetHandler(IItemSlotHandler handler)
    {
        _handler = handler;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (CurrentSlotData == null || CurrentSlotData.IsEmpty) return;

        //  클릭 횟수 관계없이 매 클릭마다 OnSlotClicked 호출
        _handler?.OnSlotClicked(this);

        // 더블클릭은 추가로 발화
        if (eventData.clickCount == 2)
            _handler?.OnSlotDoubleClicked(this);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (DraggingSlot != null && DraggingSlot != this)
            _handler?.OnSlotDrop(DraggingSlot, this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CurrentSlotData != null && !CurrentSlotData.IsEmpty)
            _handler?.OnSlotPointerEnter(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _handler?.OnSlotPointerExit(this);
    }
}
