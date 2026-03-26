using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_LootToastItem : MonoBehaviour
{
    [SerializeField] private Image _backGround;
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _itemNameText;
    [SerializeField] private TextMeshProUGUI _amountText;

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _rectTransform;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _rectTransform = GetComponent<RectTransform>();
    }

    public void Setup(string itemName, int amount, Sprite icon, Color gradeColor)
    {
        _itemNameText.text = itemName;
        _itemNameText.color = gradeColor;
        _amountText.text = $"x {amount}";

        if (icon != null)
        {
            _icon.sprite = icon;
        }

        PlayShowAndHideAnimation().Forget();
    }

    private async UniTaskVoid PlayShowAndHideAnimation()
    {
        _canvasGroup.alpha = 0f;
        _rectTransform.localScale = new Vector3(1f, 0.5f, 1f);

        // 2. Fade In +  커지는 연출 (0.2초)
        float fadeInTime = 0.2f;
        float timer = 0f;
        while (timer < fadeInTime)
        {
            timer += Time.deltaTime;
            float t = timer / fadeInTime;

            _canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

            // Y축 스케일을 0.5 -> 1.0으로 키우면서 부드럽게 자리가 밀려나는 느낌을 줌
            float scaleY = Mathf.Lerp(0.5f, 1f, t);
            _rectTransform.localScale = new Vector3(1f, scaleY, 1f);

            await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
        }

        _canvasGroup.alpha = 1f;
        _rectTransform.localScale = Vector3.one;

        // 3. 유지 시간 (2.5초 대기)
        await UniTask.Delay(2500, cancellationToken: this.GetCancellationTokenOnDestroy());

        // 4. Fade Out 연출 (0.5초)
        float fadeOutTime = 0.5f;
        timer = 0f;
        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeOutTime);
            await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
        }

        // 5. 완전히 투명해지면 삭제
        Destroy(gameObject);
    }
}
