using UnityEngine;

public class PartyInputHandler
{
    private PartyManager _partyManager;
    private readonly PartySwapController _swapController;

    public PartyInputHandler(PartySwapController swapController)
    {
        _swapController = swapController;
        RegisterInput();
    }

    private void RegisterInput()
    {
        Managers.Input.RegisterAction(InputIntent.Swap1, () => _swapController.TrySwap(0));
        Managers.Input.RegisterAction(InputIntent.Swap2, () => _swapController.TrySwap(1));
        Managers.Input.RegisterAction(InputIntent.Swap3, () => _swapController.TrySwap(2));
        Managers.Input.RegisterAction(InputIntent.Swap4, () => _swapController.TrySwap(3));
    }
}
