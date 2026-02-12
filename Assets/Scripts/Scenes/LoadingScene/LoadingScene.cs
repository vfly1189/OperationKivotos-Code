using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
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

        ////다음 씬 정보
        //string nextScene = Managers.SceneEx.NextSceneName;


        //// 씬 비동기 로드 시작
        //// 이 함수가 호출될 때, 다음 씬에 연결된 SO와 프리팹들이 메모리로 올라감
        //AsyncOperation op = SceneManager.LoadSceneAsync(nextScene);
        //op.allowSceneActivation = false;

        //float timer = 0.0f;

        //while (!op.isDone)
        //{
        //    yield return null;
        //    timer += Time.deltaTime;

        //    if (op.progress < 0.9f)
        //    {
        //        // op.progress는 0 ~ 0.9 까지 증가
        //        // 이를 0 ~ 1.0 으로 보간하여 UI에 표시
        //        float progressValue = Mathf.Lerp(_loadingUI.SliderValue, op.progress / 0.9f, timer);

        //        // 너무 빨리 차면 재미없으니 최소 시간 보장 (선택사항)
        //        if (progressValue >= op.progress / 0.9f) timer = 0f;

        //        _loadingUI.UpdateProgress(progressValue);
        //    }
        //    else
        //    {
        //        // 로딩 완료 (90% 도달)
        //        _loadingUI.UpdateProgress(1f);

        //        // 1초 정도 대기 후 입장 (UX)
        //        if (timer > 1.0f)
        //        {
        //            op.allowSceneActivation = true;
        //            yield break;
        //        }
        //    }
        //}



        var sceneData = Managers.SceneEx.NextSceneData;
        if (sceneData == null)
        {
            Debug.LogError("NextSceneData가 설정되지 않았습니다!");
            yield break;
        }

        string nextSceneName = Managers.SceneEx.NextSceneName; // 예: "SelectScene"

        // 3. 씬 로딩 시작 (비동기, 자동 전환 방지)
        AsyncOperation sceneOp = SceneManager.LoadSceneAsync(nextSceneName);
        sceneOp.allowSceneActivation = false;

        // 4. Addressable 프리로딩 (병렬 처리)
        float addressableProgress = 0f;
        bool isAddressableDone = false;

        // 프리로딩할 라벨이 있다면 다운로드 시작
        if (sceneData.preloadLabels != null && sceneData.preloadLabels.Length > 0)
        {
            // 여러 라벨을 한꺼번에 다운로드하기 위해 MergeMode.Union 사용
            // 예: "SelectCharacter" 라벨이 붙은 모든 에셋을 다운로드(캐싱)
            _downloadHandle = Addressables.DownloadDependenciesAsync(
                new List<string>(sceneData.preloadLabels),
                Addressables.MergeMode.Union,
                false // autoReleaseHandle = false (진행률 확인해야 하므로)
            );

            // 다운로드 완료 대기
            while (!_downloadHandle.IsDone)
            {
                // 진행률 업데이트 (0.0 ~ 1.0)
                addressableProgress = _downloadHandle.PercentComplete;

                // 전체 로딩바 업데이트 (씬 로딩 50% + 리소스 다운로드 50% 비중)
                float totalProgress = (sceneOp.progress * 0.5f) + (addressableProgress * 0.5f);
                _loadingUI.UpdateProgress(totalProgress);

                yield return null;
            }

            // 다운로드 성공 여부 체크
            if (_downloadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log("프리로딩 완료!");
            }
            else
            {
                Debug.LogError($"프리로딩 실패: {_downloadHandle.OperationException}");
            }

            // 핸들 해제 (필수)
            Addressables.Release(_downloadHandle);
            isAddressableDone = true;
        }
        else
        {
            // 프리로딩할 게 없으면 바로 완료 처리
            addressableProgress = 1f;
            isAddressableDone = true;
        }

        // 5. 씬 로딩 마무리 대기
        while (!sceneOp.isDone)
        {
            yield return null;

            // Addressable도 끝났고, 씬 로딩도 90% 도달했으면
            if (isAddressableDone && sceneOp.progress >= 0.9f)
            {
                _loadingUI.UpdateProgress(1f);

                // 연출용 잠시 대기
                yield return new WaitForSeconds(0.5f);

                sceneOp.allowSceneActivation = true;
                yield break;
            }

            // 아직 씬 로딩 중이면 진행률 갱신 (이미 위에서 합쳐서 보여줬지만 안전장치)
            float currentTotalProgress = (sceneOp.progress * 0.5f) + (addressableProgress * 0.5f);
            _loadingUI.UpdateProgress(currentTotalProgress);
        }

    }
    public override void Clear()
    {

    }
}
