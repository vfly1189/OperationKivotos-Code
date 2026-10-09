#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 측정 하네스 (계획서 0-5) — HarnessRoute를 고정 속도로 자동 주행한다.
//  시작: 에디터 F10 (다시 누르면 중단) · 빌드 -harness 인자면 Start → Select(새로하기)를 자동으로 넘기고 GameScene 준비 3초 뒤 자동, 끝나면 종료.
//  주행: 위치를 "구간 경과 시간 × 속도"로 정한다(속도를 매 프레임 더하지 않음) → 프레임 시간이 흔들려도 같은 시각에 같은 자리.
//    남은 시간은 다음 구간으로 넘겨 누적 오차가 없다. 걷기 애니메이션은 안 바뀐다(정책 7개에 똑같이 붙는 비용이라 비교엔 무관).
//  측정 규칙: 프레임 상한 해제(targetFrameRate −1 · vSync 0) · 파티 무적 · 공격 없음(사망 · 리스폰 없음). 끝나면 원래대로.
//  실행 순서 −1000: Managers.Update(정책 판정)보다 먼저 위치를 옮겨 같은 프레임에 반영되게.
[DefaultExecutionOrder(-1000)]
public class HarnessRunner : MonoBehaviour
{
    private const float AutoStartDelay = 3f;

    private enum Phase { Idle, Settle, Run }

    private Phase _phase = Phase.Idle;
    private List<HarnessRoute.Leg> _legs;
    private int _index;
    private float _legTime;        // 현재 구간에서 보낸 시간
    private Vector2 _pos;          // 하네스가 정한 플레이어 위치 (x · z)
    private Vector2 _legFrom;
    private float _y;
    private GameScene _scene;      // 시작 때 한 번 잡아 둔다 (SceneEx.CurrentScene도 이제 씬마다 캐시하지만 GameScene 캐스트까지 한 번에)
    private float _readySince = -1f;
    private bool _autoStarted;

    private int _savedFrameRate;
    private int _savedVSync;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[HarnessRunner]");
        DontDestroyOnLoad(go);
        go.AddComponent<HarnessRunner>();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.f10Key.wasPressedThisFrame)
        {
            if (_phase == Phase.Idle) Begin();
            else Stop("중단 (F10)", save: false);
            return;
        }

        if (_phase == Phase.Idle) { TryAutoStart(); return; }

        BaseCharacter character = Managers.Party?.GetCurrentCharacter();
        if (character == null || _scene == null)   // 씬이 바뀌면 GameScene이 파괴돼 null
        {
            Stop("씬을 벗어남", save: false);
            return;
        }

        float dt = Time.deltaTime;
        if (_phase == Phase.Settle)
        {
            _legTime += dt;
            if (_legTime >= HarnessRoute.SettleSeconds) { _phase = Phase.Run; _index = 0; _legTime = 0f; EnterLeg(); }
        }
        else
        {
            Advance(dt);
        }

        if (_phase != Phase.Idle) Place(character);
    }

    private void TryAutoStart()
    {
        if (!HarnessArgs.Harness || _autoStarted) return;

        if (!(Managers.SceneEx.CurrentScene is GameScene scene) || !scene.IsReady) { _readySince = -1f; return; }
        if (_readySince < 0f) _readySince = Time.time;
        if (Time.time - _readySince < AutoStartDelay) return;

        _autoStarted = true;
        Begin();
    }

    private void Begin()
    {
        BaseCharacter character = Managers.Party?.GetCurrentCharacter();
        _scene = Managers.SceneEx.CurrentScene as GameScene;
        if (character == null || _scene == null || !Managers.Spawner.IsRunning)
        {
            GameLog.LogWarning("[Harness] GameScene · 파티 · 스포너가 준비되지 않아 시작하지 않음");
            return;
        }

        _legs = HarnessRoute.Build();
        _pos = HarnessRoute.Start;
        _y = character.transform.position.y;
        _legTime = 0f;
        _phase = Phase.Settle;

        _savedFrameRate = Application.targetFrameRate;
        _savedVSync = QualitySettings.vSyncCount;
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;

        Managers.Party.TeleportParty(new Vector3(_pos.x, _y, _pos.y));
        Managers.Party.SetPartyInvincible(true);

        FieldMetrics.Segment("settle", character.transform.position);
        GameLog.Log($"[Harness] 시작 — 경로 v{HarnessRoute.Version} · 구간 {_legs.Count}개 · 정착 {HarnessRoute.SettleSeconds}초");
    }

    // dt만큼 진행. 구간이 끝나면 남은 시간을 다음 구간에 넘긴다(한 프레임에 여러 구간을 지날 수 있다).
    private void Advance(float dt)
    {
        float remaining = dt;
        while (_phase == Phase.Run && _index < _legs.Count)
        {
            HarnessRoute.Leg leg = _legs[_index];
            switch (leg.Type)
            {
                case HarnessRoute.LegType.Segment:
                    FieldMetrics.Segment(leg.Label, new Vector3(_pos.x, _y, _pos.y));
                    NextLeg();
                    continue;

                case HarnessRoute.LegType.Walk:
                {
                    float duration = Vector2.Distance(_legFrom, leg.To) / leg.Speed;
                    _legTime += remaining;
                    if (_legTime < duration)
                    {
                        _pos = Vector2.Lerp(_legFrom, leg.To, _legTime / duration);
                        return;
                    }
                    _pos = leg.To;
                    remaining = _legTime - duration;
                    NextLeg();
                    continue;
                }

                case HarnessRoute.LegType.Wait:
                    _legTime += remaining;
                    if (_legTime < leg.Seconds) return;
                    remaining = _legTime - leg.Seconds;
                    NextLeg();
                    continue;

                case HarnessRoute.LegType.WaitAggro:
                    _legTime += remaining;
                    int engaged = Managers.Spawner.CountEngaged(leg.SpawnerId);
                    if (engaged == 0 && _legTime < leg.Seconds) return;
                    if (engaged == 0) GameLog.LogWarning($"[Harness] 스포너 {leg.SpawnerId} 어그로 확인 실패({leg.Seconds}초) — 그대로 진행");
                    remaining = 0f;   // 대기 시간은 몹 상태에 달렸으므로 남은 시간을 넘기지 않는다
                    NextLeg();
                    continue;
            }
        }

        if (_index >= _legs.Count) Stop("완료", save: true);
    }

    private void EnterLeg()
    {
        _legFrom = _pos;
        _legTime = 0f;
    }

    private void NextLeg()
    {
        _index++;
        EnterLeg();
    }

    private void Place(BaseCharacter character)
    {
        Vector3 target = new Vector3(_pos.x, _y, _pos.y);
        Vector3 delta = target - character.transform.position;
        delta.y = 0f;
        character.transform.position = target;
        if (delta.sqrMagnitude > 1e-6f) character.transform.rotation = Quaternion.LookRotation(delta);
    }

    private void Stop(string reason, bool save)
    {
        _phase = Phase.Idle;
        _legs = null;
        _scene = null;

        if (save)
        {
            // 상한을 풀어 둔 채로 머리 줄(targetFrameRate · vSync)이 기록되게 저장이 먼저.
            ActivationLog.Save();
            FieldMetrics.Save();
        }

        Application.targetFrameRate = _savedFrameRate;
        QualitySettings.vSyncCount = _savedVSync;
        Managers.Party?.SetPartyInvincible(false);

        GameLog.Log($"[Harness] {reason}" + (save ? " — 로그 저장" : ""));

        if (save && HarnessArgs.Harness && !Application.isEditor) Application.Quit();
    }

#if UNITY_EDITOR
    // 플레이 중 씬 뷰: 경로(회색) · 현재 위치(노랑)
    private void OnDrawGizmos()
    {
        if (_phase == Phase.Idle) return;

        List<Vector2> points = HarnessRoute.Points();
        Gizmos.color = new Color(0.7f, 0.7f, 0.7f);
        for (int i = 1; i < points.Count; i++)
            Gizmos.DrawLine(new Vector3(points[i - 1].x, _y + 0.1f, points[i - 1].y), new Vector3(points[i].x, _y + 0.1f, points[i].y));

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(_pos.x, _y, _pos.y), 0.6f);
    }
#endif
}
#endif
