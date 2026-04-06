using UnityEngine;

public class PartyCharacterActivator
{
    private readonly PartyRegistry _registry;

    public PartyCharacterActivator(PartyRegistry registry)
    {
        this._registry = registry;
    }

    public void ActivateCharacter(int index, Vector3 pos, Quaternion rot)
    {
        var character = _registry.Members[index];
        character.transform.SetPositionAndRotation(pos, rot);
        character.gameObject.SetActive(true);
    }

    public void DeactivateCharacter(int index) =>
        _registry.Members[index].gameObject.SetActive(false);

    public void SyncSwap(int prevIdx, int nextIdx)
    {
        var prev = _registry.Members[prevIdx];
        DeactivateCharacter(prevIdx);
        ActivateCharacter(nextIdx, prev.transform.position, prev.transform.rotation);
    }

    public void TeleportAll(Vector3 position)
    {
        foreach (var member in _registry.Members)
        {
            // CharacterController가 있으면 잠깐 끄고 이동 후 복구
            var cc = member.GetComponent<CharacterController>();
            bool wasEnabled = false;

            if (cc != null) { wasEnabled = cc.enabled; cc.enabled = false; }
            member.transform.position = position;
            Physics.SyncTransforms();
            if (cc != null) cc.enabled = wasEnabled;
        }
    }
}