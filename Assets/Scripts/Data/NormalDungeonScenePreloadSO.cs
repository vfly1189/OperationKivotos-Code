using UnityEngine;
[CreateAssetMenu(menuName = "Preload/NormalDungeonScenePreloadData")]
public class NormalDungeonScenePreloadSO : SceneDataSO
{
    [Header("맵")]
    public GameObject normalDungeonEasy;
    public GameObject normalDungeonNormal;
    public GameObject normalDungeonHard;


    [Header("몬스터")]
    public GameObject monsterAR;
    public GameObject monsterRL;
    public GameObject monsterTank;

    [Header("메인 UI")]
    public GameObject gameSceneCanvas;
    public GameObject effectStage;

    [Header("클리어 UI")]
    public GameObject dungeonClearUI;

    [Header("브금")]
    public AudioClip[] fightingBgms;
    public AudioClip successBgm;

    [Header("총알")]
    public GameObject bullet;
}
