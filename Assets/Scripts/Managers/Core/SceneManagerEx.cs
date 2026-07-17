// [추가] UniTask
using Cysharp.Threading.Tasks;
using Org.BouncyCastle.Ocsp;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using static NPOI.HSSF.Util.HSSFColor;




public class SceneManagerEx
{
    public readonly struct SceneLoadRequest
    {
        public readonly Define.Scene Target;
        public readonly string SceneName;
        public readonly string[] PreloadLabels;
        public SceneLoadRequest(Define.Scene t, string n, string[] l) { Target = t; SceneName = n; PreloadLabels = l; }
    }
    public SceneLoadRequest Pending { get; private set; }

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

    public async UniTask InitAsync()
    {
        // 글로벌(게임 내내 유지)로 로드하여 캐싱
        _sceneTable = await Managers.Resource.LoadAsync<SceneTableSO>("SceneTable", true);

        if (_sceneTable == null)
        {
            GameLog.LogError("[SceneManagerEx] SceneTableSO 로드 실패!");
        }
    }

    public void LoadScene(Define.Scene type)
    {
        //LoadSceneAsync(type).Forget();

        if(!_sceneTable.TryGet(type, out SceneTableSO.SceneEntry entry))
        { 
            GameLog.LogError($"[SceneEx] 테이블에 없음: {type}"); 
            return; 
        }

        string name = string.IsNullOrEmpty(entry.sceneName) ? type.ToString() : entry.sceneName;
        Pending = new SceneLoadRequest(type, name, entry.preloadLabels);

        ShowCover(true);
        CurrentScene?.Clear(); // 씬의 고유정리만 하는거임 리소스 ㄴㄴ

        SceneManager.LoadScene("Loading");
    }

    string GetSceneName(Define.Scene type)
    {
        return System.Enum.GetName(typeof(Define.Scene), type);
    }

    public void Clear()
    {

    }

    public void ShowCover(bool value) => _transitionUI?.gameObject.SetActive(value);

    // SceneManagerEx  (실제 전환 로직 = 한 곳)
    public async UniTask RunLoadSequenceAsync(LoadingSceneController ui, CancellationToken token)
    {
        SceneLoadRequest rq = Pending;

        Managers.Resource.ChangeSceneScope();
        Managers.Pool.Clear();
        Managers.UI.Clear();

        await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken : token);
        System.GC.Collect();


        // 프리로드
        if (rq.PreloadLabels is { Length: > 0 })
            await Managers.Resource.LoadAsyncPreload(
                rq.PreloadLabels,
                false,
                (key, p) => ui.UpdateProgress(p, key), token
                );

        // 다음 씬 로드(비활성상태로) -> 활성화

        var handle = Addressables.LoadSceneAsync(rq.SceneName, LoadSceneMode.Single, false);
        while (!handle.IsDone) { ui.UpdateProgress(handle.PercentComplete); await UniTask.Yield(token); }

        await UniTask.Delay(300, cancellationToken: token);   // 연출
        await handle.Result.ActivateAsync().ToUniTask(cancellationToken: token);

        ShowCover(false);
    }

    private async UniTaskVoid LoadSceneAsync(Define.Scene type)
    {
        ResourceMetrics.BeginSceneTransition(GetSceneName(type)); // [Phase 0.5 계측] 전환 구간 메모리 피크 샘플링 시작

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

        //SceneDataSO data = _sceneTable.GetSceneData(type);
        SceneDataSO data = null;
        NextSceneData = data;
        NextSceneName = GetSceneName(type);

        // Loading 씬으로 이동
        SceneManager.LoadScene("Loading");
    }
}
