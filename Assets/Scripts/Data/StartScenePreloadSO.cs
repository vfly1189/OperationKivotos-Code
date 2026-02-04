using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Preload/StartScenePreloadData")]
public class StartScenePreloadSO : ScriptableObject
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

    // 리스트로 선언하면 인스펙터에서 드래그로 한 번에 여러 개 넣을 수 있습니다.
    // 폴더에 있는 파일들을 다중 선택 후 잠금(Lock) 걸린 인스펙터의 리스트 이름 위로 드래그하면 한 번에 들어갑니다.
    public List<AudioClip> titleVoices = new List<AudioClip>();
}
