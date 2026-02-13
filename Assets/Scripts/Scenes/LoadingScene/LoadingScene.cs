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
        //메모리 정리
        Managers.Resource.Clear();
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
        yield return null;

        var sceneData = Managers.SceneEx.NextSceneData;
        if (sceneData == null)
        {
            Debug.LogError("NextSceneData가 설정되지 않았습니다!");
            yield break;
        }

        string nextSceneName = Managers.SceneEx.NextSceneName; // 예: "SelectScene"

        //// 3. 씬 로딩 시작 (비동기, 자동 전환 방지)
        //AsyncOperation sceneOp = SceneManager.LoadSceneAsync(nextSceneName);
        //sceneOp.allowSceneActivation = false;

        //// 4. Addressable 프리로딩 (병렬 처리)
        //float addressableProgress = 0f;
        //bool isAddressableDone = false;

        //// 프리로딩할 라벨이 있다면 다운로드 시작
        //if (sceneData.preloadLabels != null && sceneData.preloadLabels.Length > 0)
        //{
        //    // 여러 라벨을 한꺼번에 다운로드하기 위해 MergeMode.Union 사용
        //    // 예: "SelectCharacter" 라벨이 붙은 모든 에셋을 다운로드(캐싱)
        //    _downloadHandle = Addressables.DownloadDependenciesAsync(
        //        new List<string>(sceneData.preloadLabels),
        //        Addressables.MergeMode.Union,
        //        false // autoReleaseHandle = false (진행률 확인해야 하므로)
        //    );

        //    // 다운로드 완료 대기
        //    while (!_downloadHandle.IsDone)
        //    {
        //        // 진행률 업데이트 (0.0 ~ 1.0)
        //        addressableProgress = _downloadHandle.PercentComplete;

        //        // 전체 로딩바 업데이트 (씬 로딩 50% + 리소스 다운로드 50% 비중)
        //        float totalProgress = (sceneOp.progress * 0.5f) + (addressableProgress * 0.5f);
        //        _loadingUI.UpdateProgress(totalProgress);

        //        yield return null;
        //    }

        //    // 다운로드 성공 여부 체크
        //    if (_downloadHandle.Status == AsyncOperationStatus.Succeeded)
        //    {
        //        Debug.Log("프리로딩 완료!");
        //    }
        //    else
        //    {
        //        Debug.LogError($"프리로딩 실패: {_downloadHandle.OperationException}");
        //    }

        //    // 핸들 해제 (필수)
        //    Addressables.Release(_downloadHandle);
        //    isAddressableDone = true;
        //}
        //else
        //{
        //    // 프리로딩할 게 없으면 바로 완료 처리
        //    addressableProgress = 1f;
        //    isAddressableDone = true;
        //}

        //// 5. 씬 로딩 마무리 대기
        //while (!sceneOp.isDone)
        //{
        //    yield return null;

        //    // Addressable도 끝났고, 씬 로딩도 90% 도달했으면
        //    if (isAddressableDone && sceneOp.progress >= 0.9f)
        //    {
        //        _loadingUI.UpdateProgress(1f);

        //        // 연출용 잠시 대기
        //        yield return new WaitForSeconds(0.5f);

        //        sceneOp.allowSceneActivation = true;
        //        yield break;
        //    }

        //    // 아직 씬 로딩 중이면 진행률 갱신 (이미 위에서 합쳐서 보여줬지만 안전장치)
        //    float currentTotalProgress = (sceneOp.progress * 0.5f) + (addressableProgress * 0.5f);
        //    _loadingUI.UpdateProgress(currentTotalProgress);
        //}

        // 2. [단계 A] 프리로딩(Dependencies 다운로드)
        // 씬 로드 전에 필요한 리소스(라벨 등)를 미리 받습니다.
        if (sceneData != null && sceneData.preloadLabels != null && sceneData.preloadLabels.Length > 0)
        {
            // 라벨 기반 다운로드
            var downloadHandle = Addressables.DownloadDependenciesAsync(
                new List<string>(sceneData.preloadLabels),
                Addressables.MergeMode.Union,
                false
            );

            while (!downloadHandle.IsDone)
            {
                // 프리로딩 진행률 (전체 로딩의 앞쪽 50% 할당)
                float progress = downloadHandle.PercentComplete * 0.5f;
                _loadingUI.UpdateProgress(progress);
                yield return null;
            }
            Addressables.Release(downloadHandle);
        }

        // 3. [단계 B] Addressable 씬 로드
        // loadMode: Single (기존 씬 닫음), activateOnLoad: false (바로 넘어가지 않음)
        AsyncOperationHandle<SceneInstance> sceneHandle =
            Addressables.LoadSceneAsync(nextSceneName, LoadSceneMode.Single, false);

        while (!sceneHandle.IsDone)
        {
            // 씬 로딩 진행률 (나머지 50% 할당)
            // 주의: Addressables 씬 로드는 다운로드 + 로드 과정을 포함하므로 수치가 튈 수 있음
            float sceneProgress = sceneHandle.PercentComplete;

            // 전체 진행률 계산 (앞서 50% 완료했으니 0.5 + sceneProgress * 0.5)
            // (만약 프리로딩이 없었다면 그냥 sceneProgress 사용)
            float totalProgress = 0.5f + (sceneProgress * 0.5f);

            _loadingUI.UpdateProgress(totalProgress);

            // 로딩이 거의 다 되었을 때 (0.9 이상)
            if (sceneHandle.PercentComplete >= 0.9f)
            {
                _loadingUI.UpdateProgress(1f);

                // 연출용 대기
                yield return new WaitForSeconds(0.5f);

                // 씬 활성화 (넘어가기)
                sceneHandle.Result.ActivateAsync();
                yield break;
            }
            yield return null;
        }

    }
    public override void Clear()
    {

    }
}
