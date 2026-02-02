using UnityEngine;
using UnityEngine.UI;

public class UI_BossDungeonEntrancePopUp : UI_PopUp
{
    [SerializeField] private Button _enterButton;
    [SerializeField] private Button _cancelButton;

    public override void Init()
    {
       base.Init();

        if(_enterButton != null) _enterButton.onClick.AddListener(OnEnterClicked);
        if(_cancelButton != null) _cancelButton.onClick.AddListener(OnCancelClicked);
    }

    void OnCancelClicked()
    {
        ClosePopupUI();
    }

    void OnEnterClicked()
    {
        Managers.SceneEx.LoadScene(Define.Scene.BossDungeon);
        ClosePopupUI();
    }
}
