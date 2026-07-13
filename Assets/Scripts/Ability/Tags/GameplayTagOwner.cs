using UnityEngine;

// 엔티티가 보유한 GameplayTag 집합의 저장소(캐스터·대상 공용).
// 풀 재사용·중복 부착에 안전한 EnsureOn 패턴 — StatusRunner/Health/DamageNumberPresenter와 동일.
// 저장만 담당한다. 지속시간 태그의 만료는 StatusRunner가, 게이팅 판독은 AbilityRunner가 한다.
public class GameplayTagOwner : MonoBehaviour
{
    public GameplayTagContainer Owned { get; } = new GameplayTagContainer();

    public static GameplayTagOwner EnsureOn(GameObject go)
    {
        if (go == null) return null;
        return go.GetComponent<GameplayTagOwner>() ?? go.AddComponent<GameplayTagOwner>();
    }

    // 풀 반환/비활성 시 보유 태그 초기화(이월·누수 방지). StatusRunner.OnDisable와 함께 안전망 역할.
    private void OnDisable() => Owned.Clear();
}
