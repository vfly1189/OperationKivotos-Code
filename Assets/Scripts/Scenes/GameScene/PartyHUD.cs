using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class PartyHUD : MonoBehaviour
{
    [SerializeField] private PartySlotUI[] _slots;

    public async UniTask Init(SchoolDataSO schoolData)
    {
        var loadTasks = new List<UniTask>();

        for (int i = 0; i < 4; i++)
        {
            if (i >= schoolData.characters.Length) break;

            CharacterDataSO charDataSO = schoolData.characters[i];
            _slots[i].SetCharacterName(charDataSO.nameKR);

            if (charDataSO.Emblem != null && charDataSO.Emblem.RuntimeKeyIsValid())
            {
                int index = i; // Å¬·ÎÀú Ä¸Ã³
                loadTasks.Add(LoadEmblemAsync(charDataSO.Emblem, index));
            }
        }

        await UniTask.WhenAll(loadTasks);
    }

    private async UniTask LoadEmblemAsync(AssetReferenceSprite emblemRef, int index)
    {
        Sprite loadedSprite = await Managers.Resource.LoadAsync<Sprite>(emblemRef);
        if (loadedSprite != null)
        {
            _slots[index].SetEmblem(loadedSprite);
        }
    }

    public void ConnectPartyEvents(List<BaseCharacter> members)
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (i >= _slots.Length) break;
            _slots[i].SubscribeToCharacter(members[i]);
        }
    }

    public PartySlotUI GetSlot(int index) => (index >= 0 && index < _slots.Length) ? _slots[index] : null;
}
