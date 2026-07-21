// [추가] UniTask
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

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

    public Define.Scene CurrentSceneType
    {
        get
        {
            if (CurrentScene != null) return CurrentScene._sceneType;
            return Define.Scene.Unknown;
        }
    }

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
        if(!_sceneTable.TryGet(type, out SceneTableSO.SceneEntry entry))
        { 
            GameLog.LogError($"[SceneEx] 테이블에 없음: {type}"); 
            return; 
        }

        string name = string.IsNullOrEmpty(entry.sceneName) ? type.ToString() : entry.sceneName;
        Pending = new SceneLoadRequest(type, name, entry.preloadLabels);

        // [Phase 0.5 계측] 전환 피크 샘플링 시작 — 트리거 시점부터 다음 씬 활성화까지.
        //  (RunLoadSequenceAsync의 finally에서 EndSceneTransition으로 종료)
        ResourceMetrics.BeginSceneTransition(name);

        ShowCover(true);
        CurrentScene?.Clear(); // 씬의 고유정리만 하는거임 리소스 ㄴㄴ

        SceneManager.LoadScene("Loading");
    }

    public void Clear()
    {

    }

    public void ShowCover(bool value) => _transitionUI?.gameObject.SetActive(value);

    // SceneManagerEx  (실제 전환 로직 = 한 곳)
    public async UniTask RunLoadSequenceAsync(LoadingSceneController ui, CancellationToken token)
    {
        SceneLoadRequest rq = Pending;

        try
        {
            // [계측] ChangeSceneScope 직전 = 이전 씬 스코프가 아직 핸들을 쥐고 있는 상태
            Managers.Resource.LogAliveReport($"ChangeSceneScope 직전 → {rq.SceneName}");

            Managers.Resource.ChangeSceneScope();   // 이전 Scene 스코프 Dispose → 새 빈 스코프
            Managers.Pool.Clear();
            Managers.UI.Clear();

            await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: token);
            System.GC.Collect();

            // [계측] 회전 직후 새 Scene 스코프는 비어 있어야 함 (이전 씬 핸들 완전 반납 검증)
            Managers.Resource.AssertSceneHandlesCleared("ChangeSceneScope 직후");

            // 프리로드
            if (rq.PreloadLabels is { Length: > 0 })
                await Managers.Resource.LoadAsyncPreload(
                    rq.PreloadLabels,
                    false,
                    (key, p) => ui.UpdateProgress(p, key), token
                    );

            // [계측] 프리로드 완료 시점 스냅샷
            Managers.Resource.LogAliveReport($"프리로드 완료 → {rq.SceneName}");

            // 다음 씬 로드(비활성상태로) -> 활성화
            var handle = Addressables.LoadSceneAsync(rq.SceneName, LoadSceneMode.Single, false);
            while (!handle.IsDone) { ui.UpdateProgress(handle.PercentComplete); await UniTask.Yield(token); }

            await UniTask.Delay(300, cancellationToken: token);   // 연출
            await handle.Result.ActivateAsync().ToUniTask(cancellationToken: token);

            ShowCover(false);
        }
        catch (System.OperationCanceledException)
        {
            // 다음 씬 활성화 순간 Loading 씬이 파괴되며 토큰 취소 → 정상 종료 경로.
            // 미처리 예외 로그 방지를 위해 삼킴.
        }
        finally
        {
            ResourceMetrics.EndSceneTransition(); // [Phase 0.5 계측] 전환 종료 → 피크 리포트 자동 출력
        }
    }

}
