using UnityEngine;

public class NormalDungeonPortal : BasePortal
{
    protected override async void ShowDungeonEntranceUI()
    {
        UI_NormalDungeonEntrancePopUp popup = await Managers.UI.ShowPopupUIAsync<UI_NormalDungeonEntrancePopUp>("NormalDungeonEntranceUI_New");
    }
}
