using System.Collections.Generic;
using UnityEngine;


public enum NodeState
{
    Running, // 실행 중 (아직 안 끝남)
    Success, // 성공
    Failure  // 실패
}

public abstract class Node
{
    protected NodeState _nodeState;
    public NodeState nodeState => _nodeState;

    public abstract NodeState Evaluate();
}

// [Selector]: 자식 중 하나라도 성공하면 성공 (OR 조건)
// 예: "공격할 수 있나? -> 공격", 아니면 "움직일 수 있나? -> 이동"
public class Selector : Node
{
    protected List<Node> nodes = new List<Node>();

    public Selector(List<Node> nodes) { this.nodes = nodes; }

    public override NodeState Evaluate()
    {
        foreach (var node in nodes)
        {
            switch (node.Evaluate())
            {
                case NodeState.Running:
                    _nodeState = NodeState.Running;
                    return _nodeState;
                case NodeState.Success:
                    _nodeState = NodeState.Success;
                    return _nodeState;
                case NodeState.Failure:
                    continue; // 다음 자식 시도
            }
        }
        _nodeState = NodeState.Failure;
        return _nodeState;
    }
}

// [Sequence]: 모든 자식이 성공해야 성공 (AND 조건)
// 예: "적이 감지됨" AND "사거리 내" -> "공격"
public class Sequence : Node
{
    protected List<Node> nodes = new List<Node>();

    public Sequence(List<Node> nodes) { this.nodes = nodes; }

    public override NodeState Evaluate()
    {
        bool isAnyChildRunning = false;

        foreach (var node in nodes)
        {
            switch (node.Evaluate())
            {
                case NodeState.Running:
                    isAnyChildRunning = true;
                    continue; // 다음 노드 평가 안 하고 Running 반환해도 되지만, 보통 Sequence는 멈춤
                    // (여기서는 간단한 구현을 위해 Running이면 즉시 리턴)
                    //_nodeState = NodeState.Running;
                    //return _nodeState;
                case NodeState.Success:
                    continue; // 다음 단계로
                case NodeState.Failure:
                    _nodeState = NodeState.Failure;
                    return _nodeState;
            }
        }
        _nodeState = isAnyChildRunning ? NodeState.Running : NodeState.Success;
        return _nodeState;
    }
}