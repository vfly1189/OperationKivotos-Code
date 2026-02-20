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
        for(int i=0; i<members.Count; i++)
        {
            //BaseCharacter character = members[i];
            //int slotIndex = i;

            //character.Stat.OnHpChanged -= (cur, max) => _slots[slotIndex].UpdateHP(cur, max);
            //character.Stat.OnUltimateStateChanged -= (isReady) => _slots[slotIndex].SetUltimateReady(isReady);

            //// 새로 연결
            //character.Stat.OnHpChanged += (cur, max) => _slots[slotIndex].UpdateHP(cur, max);
            //character.Stat.OnUltimateStateChanged += (isReady) => _slots[slotIndex].SetUltimateReady(isReady);

            //// 초기 상태 동기화
            //_slots[slotIndex].UpdateHP(character.Stat.CurrentHp, character.Stat.MaxHp.Value);

            //bool isReady = (character.Stat.CurrentQSkillCoolTime <= 0) &&
            //               (character.Stat.CurrentEnergy >= character.Stat.MaxEnergy.Value);
            //_slots[slotIndex].SetUltimateReady(isReady);


            BaseCharacter character = members[i];
            int slotIndex = i;

            // 새로 연결 (중복 구독 가능성 있지만, GameScene 재진입 시 캐릭터가 재생성되므로 실제론 문제 없음)
            character.Stat.OnHpChanged += (cur, max) => _slots[slotIndex].UpdateHP(cur, max);
            character.Stat.OnUltimateStateChanged += (isReady) => _slots[slotIndex].SetUltimateReady(isReady);

            // 초기 상태 동기화
            _slots[slotIndex].UpdateHP(character.Stat.CurrentHp, character.Stat.MaxHp.Value);
            bool isReady = (character.Stat.CurrentQSkillCoolTime <= 0) &&
                           (character.Stat.CurrentEnergy >= character.Stat.MaxEnergy.Value);
            _slots[slotIndex].SetUltimateReady(isReady);
        }
    }

    public PartySlotUI GetSlot(int index) => (index >= 0 && index < _slots.Length) ? _slots[index] : null;
}
