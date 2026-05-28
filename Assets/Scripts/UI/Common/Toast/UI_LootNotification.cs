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
        Sprite loadedIcon = await Managers.Resource.GetSpriteFromAtlasAsync(atlasKey, iconName);

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
            await UniTask.WaitUntil(() => _instance != null);
            return;
        }

        _isLoading = true;

        _instance = await Managers.UI.MakeSubItemAsync<UI_LootNotification>(
            "UI_LootNotification",
            Managers.UI.CanvasSystem.transform
        );

        _instance.Init();
        _instance.transform.localPosition = new Vector3(300, 0, 0);

        _isLoading = false;
    }

    public static async UniTask ShowToast(ItemCategory category, string itemName, int amount, string iconKey, Color gradeColor)
    {
        await PreloadAsync();
        _instance.ShowLootToast(category, itemName, amount, iconKey, gradeColor);
    }

    public static async UniTask ShowGainExp(int amount)
    {
        await PreloadAsync();
        _instance.GainExp(amount);
    }

    public static async UniTask ShowGainCredit(int amount)
    {
        await PreloadAsync();
        _instance.GainCredit(amount);
    }

}
