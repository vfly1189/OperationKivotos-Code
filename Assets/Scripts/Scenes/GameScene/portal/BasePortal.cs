using UnityEngine;



public class BasePortal : MonoBehaviour
{
    [SerializeField] protected GameObject _entranceUI;


    //던전은 항상 Easy Normal Hard 난이도로 존재함.
    //각 던전의 기본 MapID는 Easy랑 동일하게 설정하고 +1, +2 한값으로 Normal과 Hard를 구분짓게 함.
    [SerializeField] protected int _dungeonGroupID;


    private void OnTriggerEnter(Collider other)
    {
        ShowDungeonEntranceUI();
    }
    protected virtual void ShowDungeonEntranceUI() { } 
}
