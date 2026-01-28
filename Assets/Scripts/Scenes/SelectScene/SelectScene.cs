using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SelectScene : BaseScene
{
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
