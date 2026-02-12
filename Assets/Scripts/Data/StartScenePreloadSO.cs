using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;



[CreateAssetMenu(menuName = "Preload/StartScenePreloadData")]
public class StartScenePreloadSO : SceneDataSO
{
    [Header("UI 프리팹")]
    public GameObject backgroundSlideShow;
    public GameObject tapToStart;
    public GameObject logo;
    public GameObject soundSettingIcon;
    public GameObject exitPopup;
    public GameObject soundSettingPopup;

    [Header("사운드")]
    public AudioClip mainTitleBgm;

    public List<AudioClip> titleVoices = new List<AudioClip>();
}
