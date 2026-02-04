using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SelectScene : BaseScene
{
    [SerializeField] private SelectScenePreloadSO _preloadData;

    private GameObject _modelCamera;
    private GameObject _mainUI;

    protected override void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Select;

        Debug.Log("SelectScene Init 호출");

        CreateModelCamera();
        CreateMainUI();
    }

    void CreateModelCamera()
    {
        GameObject modelCamera = Object.Instantiate(_preloadData.modelCamera);
        modelCamera.name = "@ModelCamera";
        _modelCamera = modelCamera;
    }

    void CreateMainUI()
    {
        GameObject mainUI = Object.Instantiate(_preloadData.mainUI);
        mainUI.name = "@SelectSceneCanvas";
        _mainUI = mainUI;

        //Init 할때 카메라 먼저 만들어줄것
        SelectSceneCanvas selectSceneCanvas = mainUI.GetComponent<SelectSceneCanvas>();
        selectSceneCanvas.SetModelCamera(_modelCamera);
    }


    public override void Clear()
    {
        base.Clear();
    }
}
