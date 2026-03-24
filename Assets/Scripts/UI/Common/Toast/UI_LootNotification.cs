using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class UI_LootNotification : UI_Base
{
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
        public string ItemName;
        public int Amount;
        public Sprite Icon;
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
    public void ShowLootToast(string itemName, int amount, Sprite icon, Color gradeColor)
    {
        _lootQueue.Enqueue(new LootInfo { ItemName = itemName, Amount = amount, Icon = icon, GradeColor = gradeColor });

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
            CreateToastItem(info);

            // 한 번 띄우고 0.2초 대기 (이 간격을 주면 와바박 뜨지 않고 부드럽게 팝업됩니다)
            await UniTask.Delay(200);
        }

        _isProcessingQueue = false;
    }

    // 실제 프리팹을 생성하는 로직
    private void CreateToastItem(LootInfo info)
    {
        GameObject go = Instantiate(_lootToastPrefab, _itemPanelRoot);
        go.transform.localScale = Vector3.one;
        go.transform.SetAsLastSibling();

        UI_LootToastItem toastItem = go.GetComponent<UI_LootToastItem>();
        toastItem.Setup(info.ItemName, info.Amount, info.Icon, info.GradeColor);

        // 초과분 삭제 처리 (부모 끊기)
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
}
