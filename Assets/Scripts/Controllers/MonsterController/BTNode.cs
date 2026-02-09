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
                    //continue; // 다음 노드 평가 안 하고 Running 반환해도 되지만, 보통 Sequence는 멈춤
                    // (여기서는 간단한 구현을 위해 Running이면 즉시 리턴)
                    _nodeState = NodeState.Running;
                    return _nodeState;
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

public class RandomSelector : Node
{
    protected List<Node> nodes = new List<Node>();
    private int _currentIndex = -1;

    public RandomSelector(List<Node> nodes) { this.nodes = nodes; }

    public override NodeState Evaluate()
    {
        // [수정] 처음 실행(-1)이거나, 이전 실행이 끝났다면(Running이 아님) 새로 뽑기
        // NodeState의 기본값이 Running(0)이라서 _currentIndex == -1 체크가 필수입니다.
        if (_nodeState != NodeState.Running || _currentIndex == -1)
        {
            _currentIndex = Random.Range(0, 5);
        }

        var result = nodes[_currentIndex].Evaluate();

        _nodeState = result;
        return _nodeState;
    }
}

public class WaitNode : Node
{
    private float _duration;
    private float _startTime;
    private bool _isWaiting = false;

    public WaitNode(float duration) { _duration = duration; }

    public override NodeState Evaluate()
    {
        if (!_isWaiting)
        {
            _startTime = Time.time;
            _isWaiting = true;
            _nodeState = NodeState.Running;
        }

        if (Time.time - _startTime >= _duration)
        {
            _isWaiting = false; // 리셋
            _nodeState = NodeState.Success;
            Debug.Log("Wait Node 끝");
            return NodeState.Success;
        }

        return NodeState.Running;
    }
}



public class BossSequence : Node
{
    protected List<Node> nodes = new List<Node>();
    private int _childIndex = 0; // 현재 실행 중인 단계 기억

    public BossSequence(List<Node> nodes) { this.nodes = nodes; }

    public override NodeState Evaluate()
    {
        // 처음부터가 아니라, 기억해둔 _childIndex부터 실행
        for (int i = _childIndex; i < nodes.Count; i++)
        {
            switch (nodes[i].Evaluate())
            {
                case NodeState.Running:
                    _childIndex = i; // 여기서 멈춤 (다음 프레임에 여기서부터 시작)
                    _nodeState = NodeState.Running;
                    return _nodeState;

                case NodeState.Success:
                    continue; // 성공하면 다음 단계로

                case NodeState.Failure:
                    _childIndex = 0; // 실패하면 처음으로 리셋
                    _nodeState = NodeState.Failure;
                    return _nodeState;
            }
        }

        // 끝까지 다 성공함
        _childIndex = 0; // 리셋
        _nodeState = NodeState.Success;
        return _nodeState;
    }
}

// Node 클래스들 (그대로 유지)
public class ActionNode : Node
{
    public delegate NodeState ActionDelegate();
    private ActionDelegate _action;
    public ActionNode(ActionDelegate action) { _action = action; }
    public override NodeState Evaluate() { return _action(); }
}