using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;



[CreateAssetMenu(menuName = "Preload/StartScenePreloadData")]
public class StartScenePreloadSO : SceneDataSO
{
    [Header("UI 프리팹")]
    public AssetReferenceGameObject backgroundSlideShow;
    public AssetReferenceGameObject tapToStart;
    public AssetReferenceGameObject logo;
    public AssetReferenceGameObject soundSettingIcon;
    public AssetReferenceGameObject exitPopup;
    public AssetReferenceGameObject soundSettingPopup;

    [Header("사운드")]
    public AssetReferenceT<AudioClip> mainTitleBgm;

    public AssetReferenceT<AudioClip>[] titleVoices;
}
