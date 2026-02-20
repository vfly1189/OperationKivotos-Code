using UnityEngine;
using UnityEngine.AddressableAssets;
[CreateAssetMenu(menuName = "Preload/SelectScenePreloadData")]
public class SelectScenePreloadSO : SceneDataSO
{
    [Header("모델들 찍을 카메라")]
    public AssetReferenceGameObject modelCamera;

    [Header("선택창 메인 UI")]
    public AssetReferenceGameObject mainUI;
}
