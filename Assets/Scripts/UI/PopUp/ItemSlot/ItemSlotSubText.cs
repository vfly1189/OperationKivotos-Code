using System;
using TMPro;
using UnityEngine;


public struct SlotSubTextStyle
{
    public float FontSize;
    public TextAlignmentOptions Alignment;
    public Color Color;

    // 자주 쓰는 프리셋 정의
    public static SlotSubTextStyle Default => new SlotSubTextStyle
    {
        FontSize = 14f,
        Alignment = TextAlignmentOptions.BottomRight,
        Color = Color.white
    };

    public static SlotSubTextStyle DefaultMaterial => new SlotSubTextStyle
    {
        FontSize = 17f,
        Alignment = TextAlignmentOptions.MidlineRight,
        Color = Color.black
    };

    public static SlotSubTextStyle UpgradeLevel => new SlotSubTextStyle
    {
        FontSize = 20f,
        Alignment = TextAlignmentOptions.MidlineRight,
        Color = Color.black
    };

    public static SlotSubTextStyle MaterialSelect => new SlotSubTextStyle
    {
        FontSize = 12f,
        Alignment = TextAlignmentOptions.MidlineRight,
        Color = Color.black
    };
}

public static class ItemSlotSubText
{
    // 강화 수치 (+3)
    // 기존에 에러 나던 부분 (아마 slot.ItemData.EnhanceLevel 같은 코드가 있을 겁니다)
    public static Func<InventorySlot, string> UpgradeLevel => slot =>
    {
        // 방어 코드 추가!
        if (slot == null || slot.IsEmpty || slot.EquipInstance == null)
            return string.Empty;

        // 예시: 장비 데이터로 캐스팅 후 레벨 가져오기
        if (slot.EquipInstance == null || slot.EquipInstance.UpgradeLevel < 0)
            return string.Empty;

        return $"+{slot.EquipInstance.UpgradeLevel}";
    };

    // 스택 수 (x5)
    public static Func<InventorySlot, string> StackCount => slot =>
    {
        // 방어 코드 추가!
        if (slot == null || slot.IsEmpty)
            return string.Empty;

        return slot.Amount > 1 ? "x" + slot.Amount.ToString() : string.Empty;
    };

    // 강화재료 선택 팝업  외부 상태(selectedCount)를 클로저로 캡처
    public static Func<InventorySlot, string> EnhanceMaterial(Func<int> getSelectedCount) =>
        slot => $"{getSelectedCount()}/{slot.Amount}";

    // 아무것도 안 보여주고 싶을 때
    public static Func<InventorySlot, string> None => _ => string.Empty;
}