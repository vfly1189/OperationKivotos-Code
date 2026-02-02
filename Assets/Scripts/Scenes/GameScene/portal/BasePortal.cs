using UnityEngine;



public class BasePortal : MonoBehaviour
{
    [SerializeField] protected GameObject _entranceUI;

    private void OnTriggerEnter(Collider other)
    {
        ShowDungeonEntranceUI();
    }
    protected virtual void ShowDungeonEntranceUI() { } 
}
