using System;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class FieldManager
{
    private Transform _currentSpawnPoint;
    private bool _isRespawning = false;

    // 필드 진입 시 (GameScene.Init 등에서) 호출
    public void Init(Transform spawnPoint)
    {
        _currentSpawnPoint = spawnPoint;
        _isRespawning = false;

        // 필드 전용 파티 전멸 이벤트 구독
        Managers.Party.OnPartyWiped -= HandleFieldPartyWipe;
        Managers.Party.OnPartyWiped += HandleFieldPartyWipe;

        Debug.Log("[FieldManager] 필드 매니저 초기화 완료");
    }

    private void HandleFieldPartyWipe()
    {
        if (_isRespawning) return;

        // 전멸 시 비동기 부활 시퀀스 시작
        RespawnSequenceAsync().Forget();
    }

    private async UniTaskVoid RespawnSequenceAsync()
    {
        _isRespawning = true;
        Debug.Log("[FieldManager] 파티 전멸. 스폰 지점으로 부활 시퀀스 시작...");

        // 1. 혹시 열려있는 팝업 UI가 있다면 모두 닫기 (선택사항)
        Managers.UI.CloseAllPopupUI();

        // 2. 화면 암전 (Fade Out) 대기
        // TODO: UI 매니저 등에 Fade 캔버스를 만들어 호출하시면 됩니다.
        // await Managers.UI.FadeOutAsync(1.0f);
        await UniTask.Delay(TimeSpan.FromSeconds(1.0f));

        // 3. 파티 부활 및 위치 이동 (ResetPartyForNewScene이 체력 회복과 부활을 모두 처리함)
        if (_currentSpawnPoint != null)
        {
            Managers.Party.ResetPartyForNewScene(_currentSpawnPoint);
        }
        else
        {
            Debug.LogWarning("[FieldManager] 스폰 지점이 없습니다! 원위치에서 부활합니다.");
            // 스폰 지점이 없으면 현재 리더의 위치에서 제자리 부활
            var leader = Managers.Party.GetCurrentCharacter();
            if (leader != null)
                Managers.Party.ResetPartyForNewScene(leader.transform);
        }

        // 4. 화면 밝아짐 (Fade In) 대기
        // await Managers.UI.FadeInAsync(1.0f);
        await UniTask.Delay(TimeSpan.FromSeconds(1.0f));

        Debug.Log("[FieldManager] 부활 시퀀스 종료");
        _isRespawning = false;
    }

    // 필드를 떠날 때 (던전 진입 등) 호출
    public void Clear()
    {
        Managers.Party.OnPartyWiped -= HandleFieldPartyWipe;
        _currentSpawnPoint = null;
        _isRespawning = false;
    }
}