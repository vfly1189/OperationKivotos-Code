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

    IEnumerator LoadProcess()
    {
        //메모리 정리
        Managers.Resource.Clear();
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
        yield return null;

        //다음 씬 정보
        string nextScene = Managers.SceneEx.NextSceneName;

        // 씬 비동기 로드 시작
        // 이 함수가 호출될 때, 다음 씬에 연결된 SO와 프리팹들이 메모리로 올라감
        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene);
        op.allowSceneActivation = false;

        float timer = 0.0f;

        while (!op.isDone)
        {
            yield return null;
            timer += Time.deltaTime;

            if (op.progress < 0.9f)
            {
                // op.progress는 0 ~ 0.9 까지 증가
                // 이를 0 ~ 1.0 으로 보간하여 UI에 표시
                float progressValue = Mathf.Lerp(_loadingUI.SliderValue, op.progress / 0.9f, timer);

                // 너무 빨리 차면 재미없으니 최소 시간 보장 (선택사항)
                if (progressValue >= op.progress / 0.9f) timer = 0f;

                _loadingUI.UpdateProgress(progressValue);
            }
            else
            {
                // 로딩 완료 (90% 도달)
                _loadingUI.UpdateProgress(1f);

                // 1초 정도 대기 후 입장 (UX)
                if (timer > 1.0f)
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
