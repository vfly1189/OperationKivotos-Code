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
        foreach (var member in _registry.Members)
        {
            member.transform.position = position;
        }
    }
}
