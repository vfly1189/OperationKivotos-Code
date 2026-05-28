using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Preload/BossDungeonScenePreloadData")]
public class BossDungeonScenePreloadSO : SceneDataSO
{
    [Header("맵")]
    public AssetReferenceGameObject bossDungeon;

    [Header("보스랑 렐릭")]
    public AssetReferenceGameObject boss;
    //public AssetReferenceGameObject greenRelic;
    //public AssetReferenceGameObject redRelic;

    [Header("메인 UI")]
    //public AssetReferenceGameObject gameSceneCanvas;
    public AssetReferenceGameObject effectStage;
    public AssetReferenceGameObject bossHPBar;

    [Header("클리어 UI")]
    public AssetReferenceGameObject dungeonClearUI;

    [Header("브금")]
    public AssetReferenceT<AudioClip>[] fightingBgms;
    public AssetReferenceT<AudioClip> successBgm;

    [Header("총알")]
    public AssetReferenceGameObject bullet;

    [Header("몬스터")]
    //public AssetReferenceGameObject monsterAR;
    public AssetReferenceGameObject monsterRL;
    //public AssetReferenceGameObject monsterTank;
}
