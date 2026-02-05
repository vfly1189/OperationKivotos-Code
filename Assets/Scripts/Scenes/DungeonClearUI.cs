using UnityEngine;
using UnityEngine.UI;

public class DungeonClearUI : MonoBehaviour
{
    [SerializeField] private Button _dungeonClearConfirmButton;

    void Start()
    {
        _dungeonClearConfirmButton.onClick.AddListener(Confirm);
    }

    public void Confirm()
    {
        Managers.Party.PlayerController.VictoryTime = false;  //승리화면에서는 공격 불가능했던거 이제 풀리게
        Managers.Sound.StopAll();
        Managers.SceneEx.LoadScene(Define.Scene.Game);
    }
}
