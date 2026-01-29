using UnityEngine;

public class Define
{
    public enum Scene
    {
        Unknown,        //Default
        Start,          //시작화면
        Loading,        //로딩 전용 씬
        Select,         //캐릭터 선택
        Game,           //인게임
        NormalDungeon,
        BossDungeon,
    }

    public enum Sound
    {
        Bgm,
        Effect,
        Narration,
        MaxCount,
    }

    public enum UIEvent
    {
        Click,
        Drag,
    }

    public enum MouseEvent
    {
        Press,
        Click,
        PointerDown,
        PointerUp,
    }
    public enum CameraMode
    {
        QuarterView,
    }
}
