using UnityEngine;

public class BossDropTheMeteor : MonoBehaviour
{
    [Header("장판 공격")]
    [SerializeField] private GameObject _attackField;

    GameObject _attackFieldInstance;
    private void Start()
    {
        GameObject go = Object.Instantiate(_attackField, this.gameObject.transform);
        _attackFieldInstance = go;
    }

    public void PlayAttackField()
    {
        if (_attackFieldInstance == null)
        {
            Debug.Log("_attackField Null!!!");
            return;
        }

        _attackFieldInstance.transform.position = Managers.Party.GetCurrentCharacter().transform.position;

        _attackFieldInstance.GetComponent<ParticleSystem>().Stop();
        _attackFieldInstance.GetComponent<ParticleSystem>().Play();
    }
}
