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

        StartCoroutine(LoadSceneAsync());
    }

    IEnumerator LoadSceneAsync()
    {
        yield return null;
        Resources.UnloadUnusedAssets();
        System.GC.Collect();

        string nextScene = Managers.Scene.NextSceneName;

        if (string.IsNullOrEmpty(nextScene))
        {
            Debug.LogError("Next Scene Name is Empty!");
            yield break;
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene);
        op.allowSceneActivation = false;

        float timer = 0.0f;

        while (!op.isDone)
        {
            yield return null;
            timer += Time.deltaTime;

            if (op.progress < 0.9f)
            {
                //로딩바 업데이트
                _loadingUI.UpdateProgress(op.progress);

                if (timer >= op.progress)
                    timer = 0f;
            }
            else
            {
                float fakeProgress = Mathf.Lerp(0.9f, 1f, timer);
                _loadingUI.UpdateProgress(fakeProgress);

                if (fakeProgress >= 0.99f)
                {
                    yield return new WaitForSeconds(0.5f);
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
