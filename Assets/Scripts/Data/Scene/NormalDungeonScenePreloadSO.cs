using UnityEngine;
using UnityEngine.AddressableAssets;
[CreateAssetMenu(menuName = "Preload/NormalDungeonScenePreloadData")]
public class NormalDungeonScenePreloadSO : SceneDataSO
{
    [Header("맵")]
    public AssetReferenceGameObject normalDungeonEasy;
    public AssetReferenceGameObject normalDungeonNormal;
    public AssetReferenceGameObject normalDungeonHard;


    //[Header("몬스터")]
    //public AssetReferenceGameObject monsterAR;
    //public AssetReferenceGameObject monsterRL;
    //public AssetReferenceGameObject monsterTank;

    [Header("메인 UI")]
    //public AssetReferenceGameObject gameSceneCanvas;
    public AssetReferenceGameObject effectStage;

    [Header("클리어 UI")]
    public AssetReferenceGameObject dungeonClearUI;

    [Header("브금")]
    //public AudioClip[] fightingBgms;
    //public AudioClip successBgm;

    public AssetReferenceT<AudioClip>[] fightingBgms;
    public AssetReferenceT<AudioClip> successBgm;

    [Header("총알")]
    public AssetReferenceGameObject bullet;
}
