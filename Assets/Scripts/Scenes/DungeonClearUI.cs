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
        Managers.Sound.StopAll();
        Managers.SceneEx.LoadScene(Define.Scene.Game);
    }
}
