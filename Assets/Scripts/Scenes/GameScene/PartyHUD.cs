using NUnit.Framework.Constraints;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;

public class PartyHUD : MonoBehaviour
{
    [SerializeField] private PartySlotUI[] _slots;

    public void Init(SchoolDataSO schoolData)
    {
        for (int i = 0; i < 4; i++)
        {
            CharacterDataSO charDataSO = schoolData.characters[i];
            _slots[i].SetCharacterName(charDataSO.nameKR);
            _slots[i].SetEmblem(charDataSO.Emblem);
        }
    }

    //연결해줘야되는 이벤트들
    //체력 변경, 궁극기 차징 여부
    public void ConnectPartyEvents(List<BaseCharacter> members)
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (i >= _slots.Length) break;

            // [핵심 2] 이벤트 구독 처리를 슬롯 내부 함수로 위임하여 람다 제거
            _slots[i].SubscribeToCharacter(members[i]);
        }
    }

    public PartySlotUI GetSlot(int index) => (index >= 0 && index < _slots.Length) ? _slots[index] : null;
}
