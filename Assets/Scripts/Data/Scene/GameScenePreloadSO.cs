using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Preload/GameScenePreloadData")]
public class GameScenePreloadSO : SceneDataSO
{
    [Header("맵")]
    public AssetReferenceGameObject mainVillage;

    [Header("메인 UI")]
    public AssetReferenceGameObject gameSceneCanvas;
    //public AssetReferenceGameObject effectStage;

    [Header("입구 포탈")]
    public AssetReferenceGameObject normalDungeonPortal;
    public AssetReferenceGameObject bossDungeonPortal;

    [Header("상점 캐릭터")]
    public AssetReferenceGameObject shopMaster;

    [Header("메인 브금")]
    //public AudioClip[] mainBGMs;
    public AssetReferenceT<AudioClip>[] mainBGMs;

    [Header("Pooling 오브젝트")]
    public AssetReferenceGameObject bullet;
    public AssetReferenceGameObject monsterAR;
    public AssetReferenceGameObject monsterRL;
    public AssetReferenceGameObject monsterTank;

}
