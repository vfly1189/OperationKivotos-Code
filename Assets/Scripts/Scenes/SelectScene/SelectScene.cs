using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SelectScene : BaseScene
{
    //public static readonly string[] REQUIRED_RESOURCES = new string[]
    //{
    //    "Prefabs/Characters/Abydos/Hoshino_Select",
    //    "Prefabs/Characters/Abydos/Nonomi_Select",
    //    "Prefabs/Characters/Abydos/Shiroko_Select",
    //    "Prefabs/Characters/Abydos/Serika_Select",
    //    "Prefabs/Characters/Gehenna/Aru_Select",
    //    "Prefabs/Characters/Gehenna/Hina_Select",
    //    "Prefabs/Characters/Gehenna/Ako_Select",
    //    "Prefabs/Characters/Gehenna/Iori_Select",
    //    "Prefabs/Characters/Millennium/Toki_Select",
    //    "Prefabs/Characters/Millennium/Aris_Select",
    //    "Prefabs/Characters/Millennium/Asuna_Select",
    //    "Prefabs/Characters/Millennium/Karin_Select",
    //    "Images/ImageFont/Abydos_ImageFont",
    //    "Images/ImageFont/Gehenna_ImageFont",
    //    "Images/ImageFont/Millennium_ImageFont",
    //    "Images/School_Icon/School_Icon_Abydos",
    //    "Images/School_Icon/School_Icon_Gehenna",
    //    "Images/School_Icon/School_Icon_Millennium"
    //};

    [SerializeField] private ScenePreloadDataSO _preloadData;

    public GameObject modelCamera;


    protected override void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Select;

        Debug.Log("SelectScene Init 호출");

        modelCamera = Managers.Resource.Instantiate("UI/SelectScene/ModelCamera");
        modelCamera.name = "@modelCamera";

        GameObject bgSlideshow = Managers.Resource.Instantiate("UI/SelectScene/SelectSceneCanvas");
        bgSlideshow.name = "@SelectSceneCanvas";
    }

    // BaseScene에서 호출할 리소스 경로 반환
    //protected override string[] GetRequiredResources()
    //{
    //    return REQUIRED_RESOURCES;
    //}

    public override void Clear()
    {
        base.Clear();
        // SelectScene 전용 정리 로직 추가 가능
        if (modelCamera != null)
        {
            Destroy(modelCamera);
        }
    }
}
