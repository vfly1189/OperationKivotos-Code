using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_UltimateSkillUI : MonoBehaviour
{
    [Header("UI Components")]
    public Image energyFillImage; // 내부 원 (Vertical Fill)
    public Image cdRingImage;     // 테두리 링 (Radial 360 Fill)
    public TextMeshProUGUI coolTimeText;
    public GameObject readyEffect; // 꽉 찼을 때 반짝이는 이펙트 (선택)
    public Image readyGlow;

    [Header("Status")]
    public float maxEnergy = 80f;
    public float currentEnergy = 0f;
    public float cooldownTime = 7f;
    public float currentCooldown = 7f;

    void Update()
    {
        // 1. 에너지 업데이트 (밑에서 차오름)
        // 에너지가 꽉 차지 않았을 때만 게이지 갱신
        currentEnergy += Time.deltaTime * 10.0f;
        energyFillImage.fillAmount = currentEnergy / maxEnergy;
        //Debug.Log($"에너지비율 : {energyFillImage.fillAmount}");

        // 2. 쿨타임 업데이트 (빙글 돌아 사라짐)
        if (currentCooldown > 0)
        {
            currentCooldown -= Time.deltaTime;
            coolTimeText.text = currentCooldown.ToString();
            cdRingImage.fillAmount = currentCooldown / cooldownTime;
        }
        else
        {      
            cdRingImage.fillAmount = 0; // 쿨타임 끝
        }

        // 3. 궁극기 준비 완료 효과 (에너지 100% + 쿨타임 0)
        bool isReady = (currentEnergy >= maxEnergy) && (currentCooldown <= 0);

        if (isReady)
        {
            readyGlow.gameObject.SetActive(true);

            // 1. 회전: 초당 30도씩 회전
            //readyGlow.transform.Rotate(0, 0, 30 * Time.deltaTime);

            // 2. 깜빡임: Alpha값이 0.5 ~ 1.0 사이를 왔다갔다 (Sin 함수 사용)
            float alpha = 0.75f + Mathf.Sin(Time.time * 2f) * 0.25f;
            Color c = readyGlow.color;
            c.a = alpha;
            readyGlow.color = c;
        }
        else
        {
            readyGlow.gameObject.SetActive(false);
        }

        //if (readyEffect != null)
        //   readyEffect.SetActive(isReady);
    }
}
