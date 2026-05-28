using Cysharp.Threading.Tasks;
using UnityEngine;

// IItemSlotHandler.cs
public interface IItemSlotHandler
{
    void OnSlotClicked(UI_ItemSlot slot) { }       // C# 8 default 구현
    void OnSlotDoubleClicked(UI_ItemSlot slot) { }  //  항상 void
    void OnSlotDrop(UI_ItemSlot from, UI_ItemSlot to) { }
    void OnSlotPointerEnter(UI_ItemSlot slot, Vector2 screenPos) { }
    void OnSlotPointerExit(UI_ItemSlot slot) { }
}