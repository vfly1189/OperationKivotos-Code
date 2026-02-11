using UnityEngine;

public class PartyCharacterActivator
{
    private PartyRegistry _registry;

    public PartyCharacterActivator(PartyRegistry registry)
    {
        _registry = registry;
    }

    public void ActivateCharacter(int index, Vector3 pos, Quaternion rot)
    {
        var character = _registry.Members[index];
        character.transform.SetPositionAndRotation(pos, rot);
        character.gameObject.SetActive(true);
    }

    public void DeactivateCharacter(int index)
    {
        _registry.Members[index].gameObject.SetActive(false);
    }

    public void SyncSwap(int prevIdx, int nextIdx)
    {
        var prevChar = _registry.Members[prevIdx];
        Vector3 pos = prevChar.transform.position;
        Quaternion rot = prevChar.transform.rotation;

        DeactivateCharacter(prevIdx);
        ActivateCharacter(nextIdx, pos, rot);
    }

    public void TeleportAll(Vector3 position)
    {
        //foreach (var member in _registry.Members)
        //{
        //    member.transform.position = position;
        //}

        foreach (var member in _registry.Members)
        {
            // 1. CharacterController가 있는지 확인
            CharacterController cc = member.GetComponent<CharacterController>();
            bool wasEnabled = false;

            // 2. 있다면 끄기 (중요!)
            if (cc != null)
            {
                wasEnabled = cc.enabled;
                cc.enabled = false;
            }

            // 3. 위치 이동
            member.transform.position = position;

            // 4. 물리 트랜스폼 동기화 (즉시 적용)
            Physics.SyncTransforms();

            // 5. 다시 켜기
            if (cc != null)
            {
                cc.enabled = wasEnabled;
            }
        }
    }
}
