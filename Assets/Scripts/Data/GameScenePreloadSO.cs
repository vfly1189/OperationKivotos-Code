using UnityEngine;

[CreateAssetMenu(menuName = "Game/GameScenePreloadData")]
public class GameScenePreloadSO : ScriptableObject
{
    [Header("맵")]
    public GameObject mainVillage;

    [Header("메인 UI")]
    public GameObject gameSceneCanvas;
    public GameObject effectStage;

    [Header("입구 포탈")]
    public GameObject normalDungeonPortal;
    public GameObject bossDungeonPortal;

    [Header("상점 캐릭터")]
    public GameObject shopMaster;

    [Header("메인 브금")]
    public AudioClip[] mainBGMs;
}
