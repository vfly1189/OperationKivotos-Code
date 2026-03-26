using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class DungeonSequenceDirector : MonoBehaviour
{
    // 맵에서 필요한 참조물 (기존 NormalDungeonMap 역할 포함 가능)
    [SerializeField] private Transform _cameraPoint;
    [SerializeField] private Transform[] _endingPositions;

    private GameObject _curMap;

    private AudioClip _successBgm;
    private AudioClip _victoryVoice;

    public event Action OnClearUI;

    private void OnEnable()
    {
        // 매니저의 클리어 이벤트를 듣고 대기
        Managers.Dungeon.OnDungeonCleared += PlayVictorySequence;
    }

    private void OnDisable()
    {
        Managers.Dungeon.OnDungeonCleared -= PlayVictorySequence;
    }

    public void SetupDirector(GameObject curMap, AudioClip successBgm, AudioClip victoryVoice)
    {
        _curMap = curMap;
        _successBgm = successBgm;
        _victoryVoice = victoryVoice;
    }
    private void PlayVictorySequence()
    {
        Managers.Sound.Play(_successBgm, Define.Sound.Bgm);
        
        CoVictorySequence().Forget();
    }
    void PlayVictoryVoice()
    {
        Managers.Sound.Play(_victoryVoice, Define.Sound.Voice);
    }

    private async UniTaskVoid CoVictorySequence()
    {
        if (Managers.Party.PlayerController != null)
            Managers.Party.PlayerController.VictoryTime = true;

        await UniTask.Delay(System.TimeSpan.FromSeconds(3.0));

        PlayVictoryVoice();
        OnClearUI?.Invoke();

        if (_curMap != null)
        {
            var mapScript = _curMap.GetComponent<IDungeonMap>();
            if (mapScript != null)
            {
                GameObject camObj = mapScript.GetEndingCameraPoint().gameObject;
                if (camObj != null)
                {
                    camObj.SetActive(true);
                    // [핵심 변경] 카메라 오브젝트가 파괴될 때 발동하는 Token을 뽑아서 넘겨줌!
                    var token = camObj.GetCancellationTokenOnDestroy();
                    CoCameraZoomEffect(camObj.transform, token).Forget();
                }

                Transform[] endingPositions = mapScript.GetEndingTransforms();
                List<BaseCharacter> characters = Managers.Party.GetMemeber();
                int index = 0;

                foreach (BaseCharacter character in characters)
                {
                    character.gameObject.SetActive(true);

                    var agent = character.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent != null) agent.enabled = false;

                    var rb = character.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;

                    if (index < endingPositions.Length)
                    {
                        character.transform.position = endingPositions[index].position;

                        if (camObj != null)
                        {
                            Vector3 targetPos = camObj.transform.position;
                            targetPos.y = character.transform.position.y;
                            Vector3 dir = targetPos - character.transform.position;
                            if (dir != Vector3.zero)
                                character.transform.rotation = Quaternion.LookRotation(dir);
                        }
                        index++;
                    }
                    character.Victory();
                }
            }
        }
    }
    private async UniTaskVoid CoCameraZoomEffect(Transform camTr, System.Threading.CancellationToken cancellationToken)
    {
        float duration = 4.0f;
        float timer = 0f;
        Vector3 startPos = camTr.position;
        Vector3 targetPos = startPos + (camTr.forward * 2.0f);

        while (timer < duration)
        {
            // [방어 코드] 혹시라도 토큰이 취소되기 직전에 파괴된 경우를 대비한 null 체크
            if (camTr == null) return;

            timer += Time.deltaTime;
            float t = timer / duration;
            float easeT = (t < 0.5f) ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

            camTr.position = Vector3.Lerp(startPos, targetPos, easeT);

            // [핵심 변경] 대기할 때 cancellationToken을 넘겨주어, 파괴 시 루프를 탈출하게 만듦
            // SuppressCancellationThrow를 쓰면 취소 시 에러 로그 없이 조용히 종료됩니다.
            bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken).SuppressCancellationThrow();
            if (isCanceled) return; // 씬이 넘어가서 카메라가 파괴되면 쿨하게 연출 종료!
        }
        camTr.position = targetPos;
    }
}