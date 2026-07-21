using Cysharp.Threading.Tasks;
using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using UnityEngine;

public class UI_LootNotification : UI_Base
{
    private static UI_LootNotification _instance;
    private static bool _isLoading;

    [Header("Parents for Layout")]
    [SerializeField] Transform _itemPanelRoot;

    [Header("Prefabs")]
    [SerializeField] private GameObject _lootToastPrefab;

    [SerializeField] private UI_GainExpToast _gainExpToast;
    [SerializeField] private UI_GainCreditToast _gainCreditToast;

    // 대기열 큐
    private Queue<LootInfo> _lootQueue = new Queue<LootInfo>();
    private bool _isProcessingQueue = false;

    // 큐에 담을 임시 데이터 구조체
    private struct LootInfo
    {
        public ItemCategory Category;
        public string ItemName;
        public int Amount;
        public string IconKey;
        public Color GradeColor;
    }

    public override void Init()
    {
        // 경험치/크레딧 패널은 처음엔 안 보이게 하거나 투명하게 세팅
        //_gainExpToast.gameObject.SetActive(false);
        //_gainCreditToast.gameObject.SetActive(false);
    }

    /// <summary>
    /// 외부에서 호출하는 함수 (이제 즉시 띄우지 않고 줄을 세웁니다)
    /// </summary>
    public void ShowLootToast(ItemCategory category, string itemName, int amount, string iconKey, Color gradeColor)
    {
        _lootQueue.Enqueue(new LootInfo { Category = category, ItemName = itemName, Amount = amount, IconKey = iconKey, GradeColor = gradeColor });

        // 큐 처리가 안 돌고 있다면 시작시킴
        if (!_isProcessingQueue)
        {
            ProcessLootQueueAsync().Forget();
        }
    }

    // 0.2초 간격으로 큐에서 하나씩 빼서 띄워주는 루프
    private async UniTaskVoid ProcessLootQueueAsync()
    {
        _isProcessingQueue = true;

        while (_lootQueue.Count > 0)
        {
            LootInfo info = _lootQueue.Dequeue();
            await CreateToastItemAsync(info);

            // 한 번 띄우고 0.2초 대기 (이 간격을 주면 와바박 뜨지 않고 부드럽게 팝업됩니다)
            await UniTask.Delay(200);
        }

        _isProcessingQueue = false;
    }


    // 비동기로 아이콘을 로드하도록 변경
    private async UniTask CreateToastItemAsync(LootInfo info)
    {
        GameObject go = Instantiate(_lootToastPrefab, _itemPanelRoot);
        go.transform.localScale = Vector3.one;
        go.transform.SetAsLastSibling();

        UI_LootToastItem toastItem = go.GetComponent<UI_LootToastItem>();


        // 1. 타입 패턴 매칭을 통해 아틀라스 키와 아이콘 이름 분기 처리
        (string atlasKey, string iconName) = info.Category switch
        {
            // itemData가 EquipmentData 타입이면 equip 변수에 할당하고 블록 실행
            ItemCategory.Equipment => ("EquipmentIconAtlas", info.IconKey),

            // itemData가 ConsumableData 타입이면 cons 변수에 할당하고 블록 실행
            ItemCategory.Consumable => ("ConsumablesAtlas", info.IconKey),

            // itemData가 MaterialData 타입이면 mat 변수에 할당하고 블록 실행
            ItemCategory.Material => ("MaterialIconAtlas", info.IconKey),

            // 어떤 타입에도 맞지 않거나 에러 방지용 (기본값)
            _ => ("CommonAtlas", info.IconKey)
        };

        // UI 컴포넌트는 미리 세팅해두고 (이름, 개수, 테두리 색상 등)
        // 아이콘은 로드되는 대로 나중에 들어가도록 처리할 수도 있고, 기다렸다가 넘길 수도 있습니다.
        //Sprite loadedIcon = await Managers.Resource.LoadAsync<Sprite>(info.IconKey);
        // [Phase 3d] 이 토스트는 DontDestroyOnLoad 캔버스에 살면서 씬을 넘나든다 —
        //  아이콘 아틀라스도 Global이어야 한다. Scene(기본값)으로 두면 씬 회전 때
        //  아틀라스가 해제되며 캐시 클론까지 파기돼, 표시 중인 토스트가 빈칸이 된다(R6와 같은 계열).
        Sprite loadedIcon = await Managers.Resource.GetSpriteFromAtlasAsync(
            atlasKey, iconName, ResourceScopeType.Global);

        // 로드되는 동안 삭제되었을 수 있으니 방어 코드
        if (toastItem != null)
        {
            toastItem.Setup(info.ItemName, info.Amount, loadedIcon, info.GradeColor);
        }

        while (_itemPanelRoot.childCount > 3)
        {
            Transform oldChild = _itemPanelRoot.GetChild(0);
            oldChild.SetParent(null);
            Destroy(oldChild.gameObject);
        }
    }

    public void GainExp(int amount)
    {
        _gainExpToast.AddAmount(amount);
    }

    public void GainCredit(int amount)
    {
        _gainCreditToast.AddAmount(amount);
    }

    public static async UniTask PreloadAsync()
    {
        if (_instance != null) return;

        if (_isLoading)
        {
            // 로드가 실패하면 _instance는 null인 채 _isLoading만 내려간다 —
            // 두 조건을 모두 봐야 영구 대기에 빠지지 않는다.
            await UniTask.WaitUntil(() => _instance != null || !_isLoading);
            return;
        }

        _isLoading = true;

        try
        {
            // [R6] Global 스코프 — 이 인스턴스는 DontDestroyOnLoad 캔버스에 붙어 게임 내내 산다.
            //  Scene 스코프로 로드하면 씬 회전에서 핸들만 반납되고 인스턴스는 남아
            //  use-after-release가 된다(던전 왕복 후 재로드도 안 됨). 수명을 일치시킨다.
            _instance = await Managers.UI.MakeSubItemAsync<UI_LootNotification>(
                "UI_LootNotification",
                ResourceScopeType.Global,
                Managers.UI.CanvasSystem.transform
            );

            if (_instance == null) return;   // 로드 실패 — 다음 호출이 다시 시도할 수 있게 둔다

            _instance.Init();
            _instance.transform.localPosition = new Vector3(300, 0, 0);
        }
        finally
        {
            _isLoading = false;   // 실패·예외에도 반드시 복구 (안 그러면 WaitUntil 대기자가 영구 정지)
        }
    }

    // 로드 실패 시 _instance가 null일 수 있다 — 토스트는 부가 연출이므로 조용히 건너뛴다.
    public static async UniTask ShowToast(ItemCategory category, string itemName, int amount, string iconKey, Color gradeColor)
    {
        await PreloadAsync();
        if (_instance == null) return;
        _instance.ShowLootToast(category, itemName, amount, iconKey, gradeColor);
    }

    public static async UniTask ShowGainExp(int amount)
    {
        await PreloadAsync();
        if (_instance == null) return;
        _instance.GainExp(amount);
    }

    public static async UniTask ShowGainCredit(int amount)
    {
        await PreloadAsync();
        if (_instance == null) return;
        _instance.GainCredit(amount);
    }

}
