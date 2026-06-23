using UnityEngine;
using UnityEngine.SceneManagement;
// [추가] UniTask
using Cysharp.Threading.Tasks;

public class SceneManagerEx
{
    private LoadingSceneController _transitionUI;
    private SceneTableSO _sceneTable;

    public BaseScene CurrentScene { get { return GameObject.FindAnyObjectByType<BaseScene>(); } }
    public SceneDataSO NextSceneData { get; private set; }

    public Define.Scene CurrentSceneType
    {
        get
        {
            if (CurrentScene != null) return CurrentScene._sceneType;
            return Define.Scene.Unknown;
        }
    }

    public string NextSceneName { get; private set; }

    // [핵심 변경 1] 동기 Init을 비동기 InitAsync로 변경 (ResourceManager 활용)
    public async UniTask InitAsync()
    {
        // 글로벌(게임 내내 유지)로 로드하여 캐싱
        _sceneTable = await Managers.Resource.LoadAsync<SceneTableSO>("SceneTable", true);

        if (_sceneTable == null)
        {
            GameLog.LogError("[SceneManagerEx] SceneTableSO 로드 실패!");
        }
    }
    // [핵심 1] 코루틴 대신 UniTaskVoid Fire-and-forget 실행
    public void LoadScene(Define.Scene type, string[] resoureceToLoad = null)
    {
        LoadSceneAsync(type).Forget();
    }

    public void SetActiveCover(bool value)
    {
        if (_transitionUI != null) _transitionUI.gameObject.SetActive(value);
    }

    string GetSceneName(Define.Scene type)
    {
        return System.Enum.GetName(typeof(Define.Scene), type);
    }

    public void Clear()
    {

    }

    // [핵심 2] IEnumerator -> async UniTaskVoid로 변경
    private async UniTaskVoid LoadSceneAsync(Define.Scene type)
    {
        if (_transitionUI != null)
        {
            _transitionUI.gameObject.SetActive(true);
        }

        // [핵심 3] 유니티 1프레임 대기 (렌더링 갱신 시간 확보)
        await UniTask.Yield(PlayerLoopTiming.Update);

        if (CurrentScene != null)
            CurrentScene.Clear();

        Managers.Clear();

        if (_sceneTable == null)
        {
            GameLog.LogError($"[SceneManagerEx] SceneTable이 로드되지 않아 씬 전환 불가: {type}");
            return;
        }

        SceneDataSO data = _sceneTable.GetSceneData(type);
        NextSceneData = data;
        NextSceneName = GetSceneName(type);

        // Loading 씬으로 이동
        SceneManager.LoadScene("Loading");
    }
}
