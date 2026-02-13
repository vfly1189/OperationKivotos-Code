using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Preload/BossDungeonScenePreloadData")]
public class BossDungeonScenePreloadSO : SceneDataSO
{
    [Header("¸Ê")]
    public AssetReferenceGameObject bossDungeon;

    [Header("º¸½º¶û ·¼¸¯")]
    public AssetReferenceGameObject boss;
    //public AssetReferenceGameObject greenRelic;
    //public AssetReferenceGameObject redRelic;

    [Header("¸ÞÀÎ UI")]
    //public AssetReferenceGameObject gameSceneCanvas;
    public AssetReferenceGameObject effectStage;
    public AssetReferenceGameObject bossHPBar;

    [Header("Å¬¸®¾î UI")]
    public AssetReferenceGameObject dungeonClearUI;

    [Header("ºê±Ý")]
    public AssetReferenceT<AudioClip>[] fightingBgms;
    public AssetReferenceT<AudioClip> successBgm;

    [Header("ÃÑ¾Ë")]
    public AssetReferenceGameObject bullet;

    [Header("¸ó½ºÅÍ")]
    //public AssetReferenceGameObject monsterAR;
    public AssetReferenceGameObject monsterRL;
    //public AssetReferenceGameObject monsterTank;
}
