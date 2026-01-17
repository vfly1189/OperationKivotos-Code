using UnityEngine;
using UnityEngine.SceneManagement;

//SceneManger.LoadScene을 안쓰고
//이 클래스를 거쳐서 무조건 Loading 씬을 경유하도록.

public class SceneManagerEx
{
    public BaseScene CurrentScene { get { return GameObject.FindAnyObjectByType<BaseScene>(); } }

    // 다음에 로드할 씬의 이름을 저장해두는 변수
    public string NextSceneName { get; private set; }

    public void LoadScene(Define.Scene type)
    {
        // 1. 현재 씬 정리
        if (CurrentScene != null)
            CurrentScene.Clear();

        Managers.Clear(); // Input 등 전역 매니저 정리

        // 2. 다음 씬 정보를 저장하고 'Loading' 씬으로 이동
        NextSceneName = GetSceneName(type);

        //Debug.Log($"다음씬은 : {NextSceneName}");

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
        // 씬 전환 시 필요한 정리 로직
    }
}
