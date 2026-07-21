// [추가]
using Cysharp.Threading.Tasks;
using UnityEngine;

public class LoadingScene : BaseScene
{

    [SerializeField] private LoadingSceneController _loadingUI;

    protected override void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Loading;

        // 전환 오케스트레이션은 영속 POCO(SceneManagerEx)가 소유. Loading 씬은 트리거+UI 전달만.
        Managers.SceneEx.RunLoadSequenceAsync(_loadingUI, this.GetCancellationTokenOnDestroy()).Forget();
    }

    public override void Clear()
    {

    }
}
