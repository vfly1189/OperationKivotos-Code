using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public struct PatternContext
{
    public SkillSystem owner;          // 시전자 — Anchor.Caster
    public ITargetSource targets;      // 지금 타겟 — Anchor.Target (Fire 순간에 물어본다)
    public IAnchorSet sockets;         // 이름 붙은 점 — Anchor.Socket (없으면 null)
    public PatternMemo memo;           // 패턴 동안의 칠판 — Runner가 채운다(BMC는 비워 둠)
    public CancellationToken cToken;

    public BaseStat stat;
};


// 동작 순서, 파생 x
public class Pattern : MonoBehaviour
{
    [SerializeField] public float _coolDown;

    [Tooltip("시전 중 대상을 바라보며 돈다 — 회전은 컨트롤러가 이 값을 보고 한다 (일반몹 사격)")]
    [SerializeField] public bool _faceTarget;

    public List<PatternAction> _steps;
}
