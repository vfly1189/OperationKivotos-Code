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
    public GameObject bossHPBar;

    [Header("Å¬¸®¾î UI")]
    public GameObject dungeonClearUI;

    [Header("ºê±Ý")]
    public AudioClip[] fightingBgms;
    public AudioClip successBgm;

    [Header("ÃÑ¾Ë")]
    public GameObject bullet;

    [Header("¸ó½ºÅÍ")]
    public GameObject monsterAR;
    public GameObject monsterRL;
    public GameObject monsterTank;
}
