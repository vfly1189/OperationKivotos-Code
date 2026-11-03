using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TimelineAction : PatternAction
{
    [SerializeField] private TimelineAsset _timeline;

    private PlayableDirector _director;

    public override void Begin(PatternContext ctx)
    {
        _director = ctx.owner.GetComponent<PlayableDirector>() ;

        _director.Stop();


        _director.playableAsset = _timeline;
        foreach (var track in _timeline.GetOutputTracks())
        {
            switch (track)
            {
                case AnimationTrack _: _director.SetGenericBinding(track, ctx.owner.GetComponent<Animator>()); break;
                case AudioTrack _: _director.SetGenericBinding(track, ctx.owner.GetComponent<AudioSource>()); break;
            }
        }

        _director.RebindPlayableGraphOutputs();
        _director.time = 0;
        _director.Evaluate();
        _director.Play();
    }

    public override NodeState Tick()
    {
        // 다른 코드가 director에 다른 타임라인을 꽂았으면 내 스텝은 끝난 것
        if (_director.playableAsset != _timeline) return NodeState.Success;

        return _director.state == PlayState.Playing ? NodeState.Running : NodeState.Success;
    }

    public override void Cancel()
    {
        _director.Stop();
        _director.time = 0;
        _director = null;
    }
}
