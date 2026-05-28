using UnityEngine;

public class BossSkill02 : MonoBehaviour
{
    [Header("번개 공격")]
    [SerializeField] private GameObject _attackLightning;

    GameObject _attackInstance;

    void Start()
    {
        GameObject go = Object.Instantiate(_attackLightning, this.gameObject.transform);

        _attackInstance = go;
    }

    public void PlayAttack()
    {
        if (_attackInstance == null)
        {
            Debug.Log("_attackLightning Null!!!");
            return;
        }

        _attackInstance.transform.position = Managers.Party.GetCurrentCharacter().transform.position;

        _attackInstance.GetComponent<ParticleSystem>().Stop();
        _attackInstance.GetComponent<ParticleSystem>().Play();
    }

}
