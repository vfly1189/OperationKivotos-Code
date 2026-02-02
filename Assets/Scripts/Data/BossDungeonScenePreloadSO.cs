using UnityEngine;

[CreateAssetMenu(menuName = "Preload/BossDungeonScenePreloadData")]
public class BossDungeonScenePreloadSO : ScriptableObject
{
    [Header("¸Ê")]
    public GameObject bossDungeon;

    [Header("º¸½º¶û ·¼¸¯")]
    public GameObject boss;
    public GameObject greenRelic;
    public GameObject redRelic;

    [Header("¸ÞÀÎ UI")]
    public GameObject gameSceneCanvas;
    public GameObject effectStage;

    [Header("ºê±Ý")]
    public AudioClip[] fightingBgms;
}
