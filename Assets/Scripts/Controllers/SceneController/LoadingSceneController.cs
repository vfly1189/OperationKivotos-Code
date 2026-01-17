using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingSceneController : BaseScene
{
    [SerializeField] Slider _progressBar; // 로딩바 UI 연결
    [SerializeField] Text _loadingText;   // "Loading..." 텍스트 연결

    protected override void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Loading;

        // 다음 씬 로딩 시작
        StartCoroutine(LoadSceneAsync());
    }

    IEnumerator LoadSceneAsync()
    {
        // 1. 먼저 이전 씬의 잔재를 확실히 비워줍니다. (GC 호출 포함)
        yield return null;
        Resources.UnloadUnusedAssets(); // 사용 안하는 에셋 메모리 해제
        System.GC.Collect();            // 가비지 컬렉터 강제 호출

        // 2. Managers에 저장해둔 다음 씬 이름을 가져옴
        string nextScene = Managers.Scene.NextSceneName;

        if (string.IsNullOrEmpty(nextScene))
        {
            Debug.LogError("Next Scene Name is Empty!");
            yield break;
        }

        //여기서 이제 비동기로 리소스들 불러오기//
        
        //////////////////////////////////////////



        // 3. 비동기 로딩 시작
        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene);
        op.allowSceneActivation = false; // 로딩 끝나도 바로 넘어가지 않게 막음 (90%에서 멈춤)

        float timer = 0.0f;

        // 4. 로딩 진행률 연출 (너무 빨리 로딩되면 어색하니까 가짜 로딩 시간도 섞음)
        while (!op.isDone)
        {
            yield return null;

            timer += Time.deltaTime;

            // op.progress는 최대 0.9까지만 오름
            if (op.progress < 0.9f)
            {
                _progressBar.value = Mathf.Lerp(_progressBar.value, op.progress, timer);
                if (_progressBar.value >= op.progress)
                    timer = 0f;
            }
            else
            {
                // 로딩은 끝났는데(0.9), 바가 꽉 찰 때까지 조금 더 기다려줌 (연출)
                _progressBar.value = Mathf.Lerp(_progressBar.value, 1f, timer);

                if (_progressBar.value >= 0.99f)
                {
                    _loadingText.text = "Touch to Start"; // 혹은 자동 넘김

                    // 여기서는 1초 뒤 자동 넘김
                    yield return new WaitForSeconds(1.0f);

                    op.allowSceneActivation = true; // 씬 전환 허용
                    yield break;
                }
            }
        }
    }

    public override void Clear()
    {
        // 로딩 씬에서 특별히 지울 건 없음
    }
}
