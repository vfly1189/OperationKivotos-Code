using UnityEngine;

public class NoahController : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform _nameTextTr;
    [SerializeField] private GameObject _nameUIPrefab;

    private UI_UnitName _nameUIl;

    public async void Start()
    {
        //머리위에 이름 태그
        if (_nameUIPrefab != null)
        {
            GameObject canvasObj = GameObject.Find("@GameSceneCanvas");
            Transform uiParent = null;

            if (canvasObj != null)
            {
                uiParent = canvasObj.transform;
            }

            _nameUIl = await Managers.UI.MakeSubItemAsync<UI_UnitName>("UnitName", uiParent);
            // 타겟 세팅
            _nameUIl.SetTarget(_nameTextTr, "노아");
        }     
    }

    // 인터페이스 구현부 (F키를 누르면 실행됨)
    public async void Interact()
    {
        if (Managers.UI.IsPopupOpen) return;

        // 1. UIManager의 비동기 함수로 팝업 호출 (앞서 만든 ShowPopupUIAsync)
        UI_UpgradePopUp popup = await Managers.UI.ShowPopupUIAsync<UI_UpgradePopUp>("UpgradePanel");

        if (popup != null)
        {
            // 현재 조작 중인 캐릭터의 정보를 팝업에 전달
            int leaderID = Managers.Party.GetCurrentCharacter().Stat.GetData().id;
        }
    }

    public void OnTargetEnter(BaseCharacter character)
    {
        // TODO: 머리 위에 "F키로 상호작용" UI 띄우기
        Debug.Log("노아 영역 진입: F키 상호작용 UI 활성화");
    }

    public void OnTargetExit(BaseCharacter character)
    {
        // TODO: "F키로 상호작용" UI 숨기기
    }
}
