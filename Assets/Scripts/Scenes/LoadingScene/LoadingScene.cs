// [추가]
using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
public class LoadingScene : BaseScene
{

    [SerializeField] private LoadingSceneController _loadingUI;

    protected override void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Loading;

        Managers.SceneEx.RunLoadSequenceAsync(_loadingUI, this.GetCancellationTokenOnDestroy()).Forget();

        //LoadProcessAsync().Forget();
        //LoadProcessAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid LoadProcessAsync(CancellationToken token)
    {

        try
        {
            // 1. 씬 전환 직후의 엔진 불안정 상태를 방지하기 위해 0.1초 대기
            await UniTask.Delay(100);

            // [Phase 0 계측] 해제 직전, 살아있는 핸들 전체 기록 (Baseline: 씬 왕복 시 global 누적 관찰)
            Managers.Resource.LogAliveReport($"씬 전환 직전 → {Managers.SceneEx.NextSceneName}");

            Managers.Resource.Clear();
            Managers.Pool.Clear();
            Managers.UI.Clear();

            // [Phase 0 계측] Clear 직후 씬 버킷은 반드시 비어 있어야 함
            Managers.Resource.AssertSceneHandlesCleared("LoadingScene Clear 직후");

            // 2. 안 쓰는 에셋 메모리에서 해제 (UniTask로 대기)
            AsyncOperation unloadOp = Resources.UnloadUnusedAssets();
            await unloadOp.ToUniTask();

            System.GC.Collect();

            // 3. 다음 씬 데이터 확인
            var sceneData = Managers.SceneEx.NextSceneData;
            if (sceneData == null)
            {
                GameLog.LogError("NextSceneData가 설정되지 않았습니다!");
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

            // [Phase 0 계측] 프리로드 완료 시점 스냅샷
            Managers.Resource.LogAliveReport($"프리로드 완료 → {nextSceneName}");

            // 5. 다음 씬 구조만 메모리에 로드 (활성화는 안 함: activateOnLoad = false)
            var sceneHandle = Addressables.LoadSceneAsync(nextSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single, false);

            while (!sceneHandle.IsDone)
            {
                _loadingUI.UpdateProgress(sceneHandle.PercentComplete);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (sceneHandle.Status != AsyncOperationStatus.Succeeded)
            {
                GameLog.LogError($"[LoadingScene] 씬 로드 실패: {nextSceneName}");
                return;
            }

            // 6. 로드 완료 연출 및 씬 활성화
            _loadingUI.UpdateProgress(1f); // 게이지 꽉 채우기
            await UniTask.Delay(500); // 0.5초 대기 (너무 순식간에 넘어가면 어색하므로)

            // Addressables 전용 씬 활성화 호출
            await sceneHandle.Result.ActivateAsync().ToUniTask(cancellationToken: token);
        }
        catch (System.OperationCanceledException)
        {

        }
        finally
        {
            ResourceMetrics.EndSceneTransition(); // [Phase 0.5 계측] 전환 종료 → 피크 리포트 자동 출력
        }
    }
    public override void Clear()
    {

    }


}
