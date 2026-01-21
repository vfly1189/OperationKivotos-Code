using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

//SceneManger.LoadScene을 안쓰고
//이 클래스를 거쳐서 무조건 Loading 씬을 경유하도록.

public class SceneManagerEx
{
    public BaseScene CurrentScene { get { return GameObject.FindAnyObjectByType<BaseScene>(); } }

    // 다음에 로드할 씬의 이름을 저장해두는 변수
    public string NextSceneName { get; private set; }

    public ResourceLoadRequest LoadRequest { get; set; }

    //임시 저장용 (다음 씬에 전달하기 위해)
    public UnityEngine.Object[] LoadedResources { get; set; }

    //씬별 필요 리소스 매핑 (하드코딩 - 나중에 외부 파일로 교체)
    private static Dictionary<Define.Scene, string[]> _sceneResourceMap = new Dictionary<Define.Scene, string[]>()
    {
         { Define.Scene.Select, SelectScene.REQUIRED_RESOURCES }

    };

    public void LoadScene(Define.Scene type, string[] resoureceToLoad = null)
    {
        // 1. 현재 씬 정리
        if (CurrentScene != null)
            CurrentScene.Clear();

        // 2. resoureceToLoad가 null이면 자동으로 매핑에서 가져오기
        if (resoureceToLoad == null && _sceneResourceMap.ContainsKey(type))
        {
            resoureceToLoad = _sceneResourceMap[type];
            Debug.Log($"Auto-loaded resources for scene: {type}");
        }

        // 3. 로딩할 리소스가 있는지
        if (resoureceToLoad != null && resoureceToLoad.Length > 0)
            LoadRequest = new ResourceLoadRequest(resoureceToLoad);
        else
            LoadRequest = null;

        Managers.Clear(); // Input 등 전역 매니저 정리

        // 4. 다음 씬 정보를 저장하고 'Loading' 씬으로 이동
        NextSceneName = GetSceneName(type);

        SceneManager.LoadScene(GetSceneName(Define.Scene.Loading));
    }

    // Enum -> String 변환 (실제 유니티 씬 파일 이름과 일치시켜야 함)
    string GetSceneName(Define.Scene type)
    {
        string name = System.Enum.GetName(typeof(Define.Scene), type);
        // 예: Start -> "StartScene", Game -> "GameScene" 등으로 네이밍 규칙을 정해도 됨
        return name;
    }

    public void Clear()
    {
        // LoadRequest = null;
        // 씬 전환 시 필요한 정리 로직
    }
}
