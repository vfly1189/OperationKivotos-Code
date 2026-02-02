using UnityEngine;

public class NormalDungeonPortal : BasePortal
{
    protected override void ShowDungeonEntranceUI()
    {
        UI_NormalDungeonEntrancePopUp popup = Managers.UI.ShowPopupUI<UI_NormalDungeonEntrancePopUp>(_entranceUI);
    }
}
