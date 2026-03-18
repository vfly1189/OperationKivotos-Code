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
    public static Func<InventorySlot, string> UpgradeLevel =>
        slot => slot.IsEquipment ? $"+{slot.EquipInstance.UpgradeLevel}" : string.Empty;

    // 스택 수 (x5)
    public static Func<InventorySlot, string> StackCount =>
        slot => slot.Amount > 1 ? $"x{slot.Amount}" : string.Empty;

    // 강화재료 선택 팝업  외부 상태(selectedCount)를 클로저로 캡처
    public static Func<InventorySlot, string> EnhanceMaterial(Func<int> getSelectedCount) =>
        slot => $"{getSelectedCount()}/{slot.Amount}";

    // 아무것도 안 보여주고 싶을 때
    public static Func<InventorySlot, string> None => _ => string.Empty;
}