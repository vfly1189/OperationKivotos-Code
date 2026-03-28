using UnityEngine;

public class BossDungeonPortal : BasePortal
{
    protected override async void ShowDungeonEntranceUI()
    {
        UI_BossDungeonEntrancePopUp popup = await Managers.UI.ShowPopupUIAsync<UI_BossDungeonEntrancePopUp>("UI_BossDungeonEntrance");
        popup.SetDungeonGroupID(_dungeonGroupID);
    }
}
