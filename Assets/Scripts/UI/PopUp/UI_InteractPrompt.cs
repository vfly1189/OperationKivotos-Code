using TMPro;
using UnityEngine;

public class UI_InteractPrompt : UI_Base
{
    [SerializeField] private TextMeshProUGUI _pressKey;
    [SerializeField] private TextMeshProUGUI _promptText;

    public override void Init()
    {
        // Init 시점에 현재 할당된 키를 가져와서 텍스트 세팅
        RefreshKeyText();
    }
   
    public void RefreshKeyText()
    { 
        _pressKey.text = Managers.Input.GetKeyName("Interact");
    }

    // NPC마다 "대화하기", "대장장이 앞" 등 텍스트가 다를 수 있으니 변경 함수
    public void SetPromptText(string message)
    {
        _promptText.text = message;
    }
}
