using UnityEngine;

public class BossDungeonPortal : BasePortal
{
    protected override void ShowDungeonEntranceUI()
    {
        UI_BossDungeonEntrancePopUp popup = Managers.UI.ShowPopupUI<UI_BossDungeonEntrancePopUp>(_entranceUI);
    }
}
