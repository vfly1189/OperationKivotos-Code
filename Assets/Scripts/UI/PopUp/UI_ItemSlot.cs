using TMPro;
using Unity.Android.Gradle.Manifest;
using UnityEngine;
using UnityEngine.UI;

public class UI_ItemSlot : UI_Base
{
    [SerializeField] private Image _backGround;
    [SerializeField] private Image _itemIcon;
    [SerializeField] private TextMeshProUGUI _stackText;

    private InventorySlot _currentSlotData;
    private ItemCategory _currentCategory; // 카테고리도 기억해두면 좋음
    public override void Init()
    {
        // 클릭 이벤트 등을 바인딩하려면 여기서
    }

    public void SetInfo(InventorySlot slotData, ItemCategory category)
    {
        _currentSlotData = slotData;
        _currentCategory = category;

        // 1. DataManager에서 분기 처리된 헬퍼 함수를 통해 가져오기
        BaseItemData itemData = Managers.Data.GetItemData(slotData.itemID, category);

        if (itemData == null)
        {
            Debug.LogError($"[UI_ItemSlot] 아이템 데이터를 찾을 수 없습니다. ID: {slotData.itemID}");
            return;
        }

        // 2. 수량 텍스트 처리
        SetStackText(slotData.Amount);

        // 3. 아이콘 셋팅 (Addressable 비동기 로드)
        SetItemIcon(itemData.IconKey);

        // 4. 배경색(등급) 셋팅
        SetGradeBackGround(itemData.Grade);
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

        Sprite bgSprite = await Managers.Resource.LoadAsync<Sprite>(gradeKey);
        if (bgSprite != null && _backGround != null)
        {
            _backGround.sprite = bgSprite;
        }
    }
}
