using UnityEngine;

public class PartyInputHandler
{
    private PartyManager _partyManager;

    public PartyInputHandler()
    {
        _partyManager = PartyManager.Instance;
        RegisterInput();
    }

    private void RegisterInput()
    {
        Managers.Input.RegisterAction("Swap_1", () => _partyManager.TrySwap(0));
        Managers.Input.RegisterAction("Swap_2", () => _partyManager.TrySwap(1));
        Managers.Input.RegisterAction("Swap_3", () => _partyManager.TrySwap(2));
        Managers.Input.RegisterAction("Swap_4", () => _partyManager.TrySwap(3));
    }
}
