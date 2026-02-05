using System.Collections.Generic;
using UnityEngine;


// 순수하게 파티 멤버 목록만 관리 (데이터 컨테이너 역할)
public class PartyRegistry
{
    public List<BaseCharacter> Members { get; private set; }
    public int CurrentIndex { get; private set; }

    public BaseCharacter GetCurrent() => Members[CurrentIndex];
    public void SetCurrentIndex(int index) => CurrentIndex = index;

    public PartyRegistry()
    {
        Members = new List<BaseCharacter>();
        CurrentIndex = 0;
    }

    public void Init(List<BaseCharacter> characters)
    {
        Members = characters;
        CurrentIndex = 0;
    }


    public int FindNextAliveIndex()
    {
        for (int i = 1; i < Members.Count; i++)
        {
            int checkIndex = (CurrentIndex + i) % Members.Count;
            if (!Members[checkIndex].Stat.IsDead)
                return checkIndex;
        }
        return -1;      
    }
}
