using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public class LoadingScene : BaseScene
{
    // 다운로드 핸들 저장용 (필요시 취소하거나 확인용)
    private AsyncOperationHandle _downloadHandle;

    private LoadingSceneController _loadingUI;
    protected override void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Loading;

        // 로딩 UI 생성
        GameObject loadingObj = Managers.Resource.Instantiate("UI/LoadingScene/LoadingCanvas");
        _loadingUI = loadingObj.GetComponent<LoadingSceneController>();

        Managers.SceneEx.SetActiveCover(false);

        StartCoroutine(LoadProcess());
    }  

    IEnumerator LoadProcess()
    {
        // [1] Managers 정리 (딕셔너리만 비움, UnloadUnusedAssets는 안 함)
        Managers.Resource.Clear();

        // [2] 이전 씬(SelectScene)의 Clear()가 완료되고, 
        //     오브젝트들이 OnDestroy 되고, 
        //     Addressables 내부 정리가 끝날 때까지 충분히 대기
        yield return new WaitForSeconds(1.0f); // 1초 대기 (테스트용, 나중에 0.5초로 줄여도 됨)

        // [3] 이제 안전하게 언로드 시도
        //var unloadOp = Resources.UnloadUnusedAssets();
        //yield return unloadOp; // 완료 대기
        System.GC.Collect();

        // [4]추가 대기 - 번들 언로드가 완전히 마무리될 시간 확보
        yield return new WaitForSeconds(0.5f); // ← 이게 핵심!
        yield return null;
        yield return null;


        // [5] 씬 데이터 준비
        var sceneData = Managers.SceneEx.NextSceneData;
        if (sceneData == null)
        {
            Debug.LogError("NextSceneData가 설정되지 않았습니다!");
            yield break;
        }

        string nextSceneName = Managers.SceneEx.NextSceneName;

        // [6] 프리로딩 (있다면)
        if (sceneData.preloadLabels != null && sceneData.preloadLabels.Length > 0)
        {
            var downloadHandle = Addressables.DownloadDependenciesAsync(
                new List<string>(sceneData.preloadLabels),
                Addressables.MergeMode.Union,
                false
            );

            while (!downloadHandle.IsDone)
            {
                float progress = downloadHandle.PercentComplete * 0.5f;
                _loadingUI.UpdateProgress(progress);
                yield return null;
            }

            if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(downloadHandle);
                yield return null;
            }
        }

        // [7] 씬 로드
        AsyncOperationHandle<SceneInstance> sceneHandle =
            Addressables.LoadSceneAsync(nextSceneName, LoadSceneMode.Single, false);

        while (!sceneHandle.IsDone)
        {
            float sceneProgress = sceneHandle.PercentComplete;
            float totalProgress = 0.5f + (sceneProgress * 0.5f);
            _loadingUI.UpdateProgress(sceneProgress);

            if (sceneHandle.PercentComplete >= 0.9f)
            {
                _loadingUI.UpdateProgress(1f);
                yield return new WaitForSeconds(0.5f);
                yield return sceneHandle.Result.ActivateAsync();
                yield break;
            }
            yield return null;
        }

    }
    public override void Clear()
    {

    }
}
