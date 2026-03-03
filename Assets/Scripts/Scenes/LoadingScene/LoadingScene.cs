using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
// [추가]
using Cysharp.Threading.Tasks;
public class LoadingScene : BaseScene
{
    // 다운로드 핸들 저장용 (필요시 취소하거나 확인용)
    private AsyncOperationHandle _downloadHandle;

    [SerializeField] private LoadingSceneController _loadingUI;

    // [핵심 1] 코루틴 대신 async UniTaskVoid로 선언
    protected override void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Loading;

        // 코루틴 실행이 아니라 UniTask 함수를 바로 호출 (잊혀짐 방지를 위해 Forget 사용 권장)
        LoadProcessAsync().Forget();
    }

    // [핵심 2] IEnumerator -> async UniTask
    private async UniTaskVoid LoadProcessAsync()
    {
        // 1. 씬 전환 직후의 엔진 불안정 상태를 방지하기 위해 0.1초 대기
        await UniTask.Delay(100);

        Managers.Resource.Clear();
        Managers.Pool.Clear();

        // 2. 안 쓰는 에셋 메모리에서 해제 (UniTask로 대기)
        AsyncOperation unloadOp = Resources.UnloadUnusedAssets();
        await unloadOp.ToUniTask();

        // [정리 2] C# 가비지 컬렉터 강제 호출로 좀비 메모리 완벽 청소
        System.GC.Collect();

        // 3. 다음 씬 데이터 확인
        var sceneData = Managers.SceneEx.NextSceneData;
        if (sceneData == null)
        {
            Debug.LogError("NextSceneData가 설정되지 않았습니다!");
            return;
        }

        string nextSceneName = Managers.SceneEx.NextSceneName;

        // 4. 프리로딩 라벨이 있다면 다운로드 및 메모리 로드 진행
        if (sceneData.preloadLabels != null && sceneData.preloadLabels.Length > 0)
        {
            await Managers.Resource.LoadDependenciesAsync(
                sceneData.preloadLabels,
                false,
                (fileName, progress) =>
                {
                    // 0% ~ 100% UI 업데이트
                    _loadingUI.UpdateProgress(progress * 1.0f, fileName);
                }
            );
        }

        // 5. 다음 씬 구조만 메모리에 로드 (활성화는 안 함: activateOnLoad = false)
        var sceneHandle = Addressables.LoadSceneAsync(nextSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single, false);

        // [정리 3] 0.9f 체크 제거. Addressables는 100% 완료될 때까지 기다려야 Result(SceneInstance)가 나옴.
        while (!sceneHandle.IsDone)
        {
            _loadingUI.UpdateProgress(sceneHandle.PercentComplete);
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        // 6. 로드 완료 연출 및 씬 활성화
        _loadingUI.UpdateProgress(1f); // 게이지 꽉 채우기
        await UniTask.Delay(500); // 0.5초 대기 (너무 순식간에 넘어가면 어색하므로)

        // Addressables 전용 씬 활성화 호출
        await sceneHandle.Result.ActivateAsync().ToUniTask();
    }
    public override void Clear()
    {

    }
}
