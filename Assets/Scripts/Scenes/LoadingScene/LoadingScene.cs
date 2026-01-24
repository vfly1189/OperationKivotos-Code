using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingScene : BaseScene
{
    private LoadingSceneController _loadingUI;

    protected override void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Loading;

        // 로딩 UI 생성
        GameObject loadingObj = Managers.Resource.Instantiate("UI/LoadingScene/LoadingCanvas");
        _loadingUI = loadingObj.GetComponent<LoadingSceneController>();

        StartCoroutine(LoadProcess());
    }

    //IEnumerator LoadSceneAsync()
    //{
    //    yield return null;
    //    Resources.UnloadUnusedAssets();
    //    System.GC.Collect();

    //    string nextScene = Managers.SceneEx.NextSceneName;

    //    if (string.IsNullOrEmpty(nextScene))
    //    {
    //        Debug.LogError("Next Scene Name is Empty!");
    //        yield break;
    //    }

    //    // ★ 리소스 로딩 (코루틴)
    //    if (Managers.SceneEx.LoadRequest != null)
    //    {
    //        yield return StartCoroutine(LoadResourcesCoroutine(Managers.SceneEx.LoadRequest));
    //    }

    //    // 씬 로딩
    //    AsyncOperation op = SceneManager.LoadSceneAsync(nextScene);
    //    op.allowSceneActivation = false;

    //    float timer = 0.0f;

    //    while (!op.isDone)
    //    {
    //        yield return null;
    //        timer += Time.deltaTime;

    //        if (op.progress < 0.9f)
    //        {
    //            float progress = 0.5f + (op.progress * 0.5f);
    //            _loadingUI.UpdateProgress(progress);

    //            if (timer >= op.progress)
    //                timer = 0f;
    //        }
    //        else
    //        {
    //            float fakeProgress = Mathf.Lerp(0.95f, 1f, timer);
    //            _loadingUI.UpdateProgress(fakeProgress);

    //            if (fakeProgress >= 0.99f)
    //            {
    //                yield return new WaitForSeconds(0.5f);
    //                op.allowSceneActivation = true;
    //                yield break;
    //            }
    //        }
    //    }
    //}

    //// ★ 별도 코루틴
    //IEnumerator LoadResourcesCoroutine(ResourceLoadRequest request)
    //{
    //    Debug.Log("리소스들 로딩 중 .....");

    //    string[] paths = request.resourePaths;
    //    UnityEngine.Object[] resources = new UnityEngine.Object[paths.Length];

    //    for (int i = 0; i < paths.Length; i++)
    //    {
    //        // 경로로 타입 구분
    //        if (paths[i].StartsWith("Images/"))
    //        {
    //            // Images 폴더 아래는 모두 Sprite로 로드
    //            resources[i] = Resources.Load<Sprite>(paths[i]);

    //            if (resources[i] == null)
    //            {
    //                Debug.LogError($"Failed to load Sprite: {paths[i]}");
    //            }
    //            else
    //            {
    //                Debug.Log($"Loaded Sprite: {paths[i]}");
    //            }
    //        }
    //        else if (paths[i].StartsWith("Prefabs/"))
    //        {
    //            // Prefabs 폴더는 GameObject로 로드
    //            resources[i] = Resources.Load<GameObject>(paths[i]);

    //            if (resources[i] == null)
    //            {
    //                Debug.LogError($"Failed to load GameObject: {paths[i]}");
    //            }
    //            else
    //            {
    //                Debug.Log($"Loaded GameObject: {paths[i]}");
    //            }
    //        }
    //        else
    //        {
    //            // 기타 경로는 UnityEngine.Object로 범용 로드
    //            resources[i] = Resources.Load(paths[i]);

    //            if (resources[i] == null)
    //            {
    //                Debug.LogError($"Failed to load: {paths[i]}");
    //            }
    //            else
    //            {
    //                Debug.Log($"Loaded {resources[i].GetType().Name}: {paths[i]}");
    //            }
    //        }

    //        // 진행률 업데이트
    //        float progress = (float)(i + 1) / paths.Length * 0.5f;
    //        _loadingUI.UpdateProgress(progress);

    //        yield return null;
    //    }

    //    Managers.SceneEx.LoadedResources = resources;
    //    Debug.Log($"리소스 {resources.Length}개 SceneManagerEx에 저장");
    //}

    IEnumerator LoadProcess()
    {
        // 1. 이전 씬 리소스 정리 및 GC
        Managers.Resource.Clear();
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
        yield return null;

        // 2. 다음 씬 정보 가져오기
        string nextScene = Managers.SceneEx.NextSceneName;
        ResourceLoadRequest request = Managers.SceneEx.LoadRequest;

        // 3. 리소스 프리로딩 (있다면)
        if (request != null && request.resourePaths != null && request.resourePaths.Length > 0)
        {
            //Debug.Log("리소스 프리로딩 시작...");

            bool isPreloadComplete = false;

            // ResourceManager에게 로딩 위임
            yield return StartCoroutine(Managers.Resource.CoLoadAllAsync(
                request.resourePaths,
                (progress, fileName) =>
                {
                    // 진행률 업데이트 (0.0 ~ 0.5 구간 할당)
                    _loadingUI.UpdateProgress(progress * 0.5f);
                },
                () =>
                {
                    isPreloadComplete = true;
                }
            ));

            // 안전장치
            yield return new WaitUntil(() => isPreloadComplete);
        }
        else
        {
            _loadingUI.UpdateProgress(0.5f); // 리소스 없으면 바로 50%
        }

        // 4. 씬 전환 (비동기)
        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene);
        op.allowSceneActivation = false;

        float timer = 0.0f;
        while (!op.isDone)
        {
            yield return null;
            timer += Time.deltaTime;

            // 씬 로딩 진행률 (0.5 ~ 1.0 구간 할당)
            // op.progress는 최대 0.9까지 오름
            if (op.progress < 0.9f)
            {
                // 0.5f(기본) + (씬로딩 0~0.9) * 비율조정
                float currentProgress = 0.5f + (op.progress * (0.5f / 0.9f));
                _loadingUI.UpdateProgress(currentProgress);

                if (timer >= op.progress) timer = 0f;
            }
            else
            {
                // 거의 다 됨 (fake loading)
                float fakeProgress = Mathf.Lerp(0.95f, 1f, timer);
                _loadingUI.UpdateProgress(fakeProgress);

                if (fakeProgress >= 0.99f)
                {
                    op.allowSceneActivation = true;
                    yield break;
                }
            }
        }
    }

    public override void Clear()
    {

    }
}
