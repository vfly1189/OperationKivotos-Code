using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SelectScene : BaseScene
{
    public static readonly string[] REQUIRED_RESOURCES = new string[]
    {
        "Prefabs/Characters/Abydos/Hoshino_Select",
        "Prefabs/Characters/Abydos/Nonomi_Select",
        "Prefabs/Characters/Abydos/Shiroko_Select",
        "Prefabs/Characters/Abydos/Serika_Select",
        "Prefabs/Characters/Gehenna/Aru_Select",
        "Prefabs/Characters/Gehenna/Hina_Select",
        "Prefabs/Characters/Gehenna/Ako_Select",
        "Prefabs/Characters/Gehenna/Iori_Select",
        "Prefabs/Characters/Millennium/Toki_Select",
        "Prefabs/Characters/Millennium/Aris_Select",
        "Prefabs/Characters/Millennium/Asuna_Select",
        "Prefabs/Characters/Millennium/Karin_Select",
        "Images/ImageFont/Abydos_ImageFont",
        "Images/ImageFont/Gehenna_ImageFont",
        "Images/ImageFont/Millennium_ImageFont",
        "Images/School_Icon/School_Icon_Abydos",
        "Images/School_Icon/School_Icon_Gehenna",
        "Images/School_Icon/School_Icon_Millennium"
    };

    private Dictionary<string, UnityEngine.Object> _loadedResources = new Dictionary<string, UnityEngine.Object>();

    public bool IsResourcesReady { get; private set; }
    public GameObject modelCamera;

    protected override void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Select;

        Debug.Log("SelectScene Init 호출");

        //SceneManagerEx에서 리소스 가져오기
        if (Managers.SceneEx.LoadedResources != null)
        {
            Debug.Log($"리소스 {Managers.SceneEx.LoadedResources.Length}개 받음");
            SetLoadedResources(Managers.SceneEx.LoadedResources);
            Managers.SceneEx.LoadedResources = null; // 가져갔으니 Clear
        }
        else
        {
            Debug.LogWarning("SelectScene: SceneManagerEx에 저장된 리소스 없음");
        }

        modelCamera = Managers.Resource.Instantiate("UI/SelectScene/ModelCamera");
        modelCamera.name = "@modelCamera";

        GameObject bgSlideshow = Managers.Resource.Instantiate("UI/SelectScene/SelectSceneCanvas");
        bgSlideshow.name = "@SelectSceneCanvas";
    }

    //외부에서 직접 호출
    public void SetLoadedResources(UnityEngine.Object[] resources)
    {
        if (resources == null)
        {
            Debug.LogError("SelectScene: Resources array is null!");
            return;
        }

        for (int i = 0; i < REQUIRED_RESOURCES.Length; i++)
        {
            if (i < resources.Length)
            {
                string fileName = Path.GetFileNameWithoutExtension(REQUIRED_RESOURCES[i]);

                Debug.Log($"파일 이름 : {fileName}");

                // "_Select" 문자열이 있다면 제거
                if (fileName.EndsWith("_Select"))
                {
                    fileName = fileName.Replace("_Select", "");
                }

                if (_loadedResources.ContainsKey(fileName))
                {
                    Debug.LogWarning($"Duplicate resource name: {fileName} at {REQUIRED_RESOURCES[i]}");
                    continue;
                }

                _loadedResources[fileName] = resources[i];
            }
        }

        IsResourcesReady = true;
        Debug.Log($"SelectScene: {_loadedResources.Count} resources loaded");
    }

    public T GetResource<T>(string resourceName) where T : UnityEngine.Object
    {
        if (_loadedResources.TryGetValue(resourceName, out UnityEngine.Object resource))
        {
            return resource as T;
        }

        Debug.LogError($"Resource not found: {resourceName}");
        return null;
    }

    public override void Clear()
    {
        _loadedResources.Clear();
        IsResourcesReady = false;
    }
}
