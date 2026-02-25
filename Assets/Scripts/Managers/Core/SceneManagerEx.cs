using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

//SceneManger.LoadScene을 안쓰고
//이 클래스를 거쳐서 무조건 Loading 씬을 경유하도록.

public class SceneManagerEx
{
    private LoadingSceneController _transitionUI; // 전역 트랜지션 UI 캐싱

    // Inspector에서 SceneTableSO를 연결
    private SceneTableSO _sceneTable;

    public BaseScene CurrentScene { get { return GameObject.FindAnyObjectByType<BaseScene>(); } }

    // 다음 씬으로 넘겨줄 데이터를 임시 저장
    public SceneDataSO NextSceneData { get; private set; }

    public Define.Scene CurrentSceneType
    {
        get
        {
            if (CurrentScene != null)
                return CurrentScene._sceneType; // BaseScene에 있는 _sceneType 리턴

            return Define.Scene.Unknown; // 혹은 Default값
        }
    }

    // 다음에 로드할 씬의 이름을 저장해두는 변수
    public string NextSceneName { get; private set; }

    public void Init()
    {
        //// 게임 시작 시(StartScene) 딱 한 번만 생성
        //if (_transitionUI == null)
        //{
        //    GameObject go = Managers.Resource.Instantiate("UI/LoadingScene/LoadingCover_Zero");
        //    _transitionUI = go.GetComponent<LoadingSceneController>();
        //    Object.DontDestroyOnLoad(go);
        //    go.SetActive(false);
        //}

        // Resources 폴더에서 로드 (확장자 제외)
        _sceneTable = Resources.Load<SceneTableSO>("Data/Scene/SceneTable");
    }


    public void LoadScene(Define.Scene type, string[] resoureceToLoad = null)
    {
        Managers.Start_Coroutine(CoLoadScene(type));
    }

    public void SetActiveCover(bool value)
    {
        _transitionUI.gameObject.SetActive(value);
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

    }

    IEnumerator CoLoadScene(Define.Scene type)
    {
        // 1. [핵심] 커버 켜고 1프레임 대기 (화면에 확실히 그려지도록)
        if (_transitionUI != null)
        {
            _transitionUI.gameObject.SetActive(true);
        }

        // 1프레임 대기 (중요: 커버가 그려질 시간 확보)
        yield return null;

        // 2. 현재 씬 정리
        if (CurrentScene != null)
            CurrentScene.Clear();

        Managers.Clear();

        // 3. 데이터 준비
        SceneDataSO data = _sceneTable.GetSceneData(type);
        NextSceneData = data;
        NextSceneName = GetSceneName(type);

        // 4. Loading 씬으로 이동
        SceneManager.LoadScene("Loading");
    }
}
