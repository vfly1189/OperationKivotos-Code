using UnityEngine;

public class NoahController : MonoBehaviour, IInteractable
{
    // 인터페이스 구현부 (F키를 누르면 실행됨)
    public async void Interact()
    {
        // 팝업이 이미 열려있으면 무시
        if (Managers.UI.IsPopupOpen) return;

        Debug.Log("노아: 장비를 강화하시겠어요?");

        // 1. UIManager의 비동기 함수로 팝업 호출 (앞서 만든 ShowPopupUIAsync)
        UI_UpgradePopUp popup = await Managers.UI.ShowPopupUIAsync<UI_UpgradePopUp>("UpgradePanel");

        if (popup != null)
        {
            // 현재 조작 중인 캐릭터의 정보를 팝업에 전달
            int leaderID = Managers.Party.GetCurrentCharacter().Stat.GetData().id;
            //popup.RefreshUI(leaderID);
        }
    }
}
