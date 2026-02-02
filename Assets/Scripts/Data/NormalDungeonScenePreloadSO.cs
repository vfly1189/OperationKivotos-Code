using UnityEngine;
[CreateAssetMenu(menuName = "Preload/NormalDungeonScenePreloadData")]
public class NormalDungeonScenePreloadSO : ScriptableObject
{
    [Header("∏ ")]
    public GameObject normalDungeonEasy;
    public GameObject normalDungeonNormal;
    public GameObject normalDungeonHard;


    [Header("∏ÛΩ∫≈Õ")]
    public GameObject monsterAR;
    public GameObject monsterRL;
    public GameObject monsterTank;

    [Header("∏ﬁ¿Œ UI")]
    public GameObject gameSceneCanvas;
    public GameObject effectStage;

    [Header("∫Í±›")]
    public AudioClip[] fightingBgms;

    [Header("√—æÀ")]
    public GameObject bullet;
}
