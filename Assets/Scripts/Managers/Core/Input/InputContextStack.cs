using System.Collections.Generic;

/// <summary>
/// 입력 컨텍스트 스택. 스택이 비어있으면 Gameplay(암묵적 기본 바닥).
///
/// UI 팝업이 열리면 Push(UI), 닫히면 Pop → 이전 컨텍스트로 자동 복귀.
/// "닫으면 어디로 돌아가지?"를 호출부가 기억할 필요 없이 스택이 순서를 대신 관리한다(중첩 UI 대응).
///
/// 주의: UIManager._popupStack 과 동일한 지점(ShowPopupUIAsync / ClosePopupUI)에서만
///       구동되므로 서로 어긋나지 않는다. 팝업이 아닌 컨텍스트(컷신 등)도 여기로 확장 가능.
/// </summary>
public class InputContextStack
{
    private readonly Stack<InputContext> _stack = new Stack<InputContext>();

    public InputContext Current => _stack.Count > 0 ? _stack.Peek() : InputContext.Gameplay;
    public bool IsGameplay => Current == InputContext.Gameplay;

    public void Push(InputContext ctx) => _stack.Push(ctx);

    public void Pop()
    {
        if (_stack.Count > 0)
            _stack.Pop();
    }

    public void Clear() => _stack.Clear();
}
