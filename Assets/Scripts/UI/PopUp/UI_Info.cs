using Cysharp.Threading.Tasks;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class UI_Info : UI_PopUp
{
    //HP, ATK, DEF, SPD, CR, CD, ERR
    [SerializeField] TextMeshProUGUI[] _statTexts;

    //무기정보
    [SerializeField] Image _weaponImage;
    [SerializeField] TextMeshProUGUI _weaponLevel;
    [SerializeField] TextMeshProUGUI _weaponName;
    [SerializeField] Image _standingImage;

    [SerializeField] UI_EquipSlot[] _equipSlots;


    //캐싱용
    private BaseCharacter _curCharacter;

    const float _standingImageWidthOriginal = 250.0f;
    const float _standingImageHeightOriginal = 310.0f;


    public override void Init()
    {
        base.Init();

        //Managers.UI.PreloadTooltip().Forget();
        // [최적화 2] 수동 비율 계산 대신 유니티 내장 기능 사용
        // Image 컴포넌트의 Preserve Aspect를 켜면 부모 Rect 내에서 비율을 스스로 유지함
        //_standingImage.preserveAspect = true;


        // 초기 화면 그리기
        RefreshUI();

        Managers.Equipment.OnEquipmentChanged -= RefreshEquipSlot;
        Managers.Equipment.OnEquipmentChanged += RefreshEquipSlot;
    }

    private void SetStat()
    {
        _curCharacter = Managers.Party.GetCurrentCharacter();
        CharacterStat stat = _curCharacter.Stat; // 접근 뎁스 줄이기 (캐싱)

        // [최적화 1] $"..."와 ToString() 이중 할당 방지 및 포맷팅 간소화
        _statTexts[0].text = stat.MaxHp.Value.ToString("N0");
        _statTexts[1].text = stat.Attack.Value.ToString("N0");
        _statTexts[2].text = stat.Defense.Value.ToString("N0");
        _statTexts[3].text = stat.MoveSpeed.Value.ToString("N0");

        // 백분율(%) 표시는 곱하기 100 대신 "P0"(소수점 없는 퍼센트) 포맷을 쓰면 GC 발생을 줄이고 깔끔합니다.
        // 예: 0.15 -> "15%"
        _statTexts[4].text = stat.CritRate.Value.ToString("P0");
        _statTexts[5].text = stat.CritDamage.Value.ToString("P0");
        _statTexts[6].text = stat.EnergyRecharge.Value.ToString("P0");
    }

    private async UniTask SetWeaponInfo()
    {
        WeaponDataSO weaponData = _curCharacter.Stat.WeaponData;
        if (weaponData == null) return;

        // [최적화 3] 팝업이 로딩 중 닫힐 때를 대비해 Cancellation Token 주입
        var token = this.GetCancellationTokenOnDestroy();

        Sprite icon = await Managers.Resource.GetSpriteFromAtlasAsync("WeaponIconAtlas", weaponData.GetWeaponIconName())
                                           .AttachExternalCancellation(token);

        _weaponImage.sprite = icon;

        // 메모리 할당 최소화를 위해 + 기호를 직접 붙이거나 Concat 사용
        _weaponLevel.text = string.Concat("+", _curCharacter.Stat.WeaponLevel.ToString());
        _weaponName.text = weaponData.weaponName;
    }

    private async UniTask SetCharacterStandingImage()
    {
        // string.Concat이 + 연산자보다 가비지(GC)를 덜 만듭니다.
        string key = string.Concat("Img_", _curCharacter.Stat.GetNameKey(), "_Standing");

        var token = this.GetCancellationTokenOnDestroy();
        Sprite standingImage = await Managers.Resource.GetSpriteFromAtlasAsync("StandingImagesAtlas", key)
                                                    .AttachExternalCancellation(token);

        _standingImage.sprite = standingImage;

        float pixelWidth = standingImage.rect.width;
        float pixelHeight = standingImage.rect.height;


        float widthRatio = pixelWidth / _standingImageWidthOriginal;
        float heightRatio = pixelHeight / _standingImageHeightOriginal;

        float maxRatio = Mathf.Max(widthRatio, heightRatio);

        _standingImage.rectTransform.sizeDelta = new Vector2(pixelWidth / maxRatio, pixelHeight / maxRatio);


        // [최적화 2-1] 기존의 복잡했던 크기/비율 수학 계산 제거! 
        // Inspector에서 Image 컴포넌트의 Preserve Aspect를 체크하거나 Init()에서 켜두면,
        // _standingImage의 RectTransform 크기(예: 250x310) 내에서 찌그러지지 않고 가장 예쁘게 꽉 찹니다.
    }

    private void SetEquipment()
    {
        // 캐싱하여 매 반복문마다 Managers.Equipment에 접근하는 오버헤드 방지
        var equippedItems = Managers.Equipment._equippedItem;

        for (int i = 0; i < _equipSlots.Length; i++)
        {
            EquipType type = (EquipType)i;
            _equipSlots[i].SetInfo(equippedItems[type]);
            _equipSlots[i].SetType(type);
        }
    }

    private void RefreshEquipSlot(EquipType equipType, InventorySlot newSlot, InventorySlot oldSlot)
    {
        // 장착이든, 해제든, 교체든 
        // 그냥 최종적으로 착용하게 된 장비(newSlot)의 상태로 UI 슬롯을 덮어씌우면 끝입니다. (null이면 알아서 빈 아이콘 처리됨)
        _equipSlots[(int)equipType].SetInfo(newSlot);

        // 장비가 바뀌었으니 스탯 UI도 갱신
        SetStat();
    }

    private void RefreshUI()
    {
        //캐싱 해놔서 순서 중요
        SetStat();
        SetEquipment();

        SetWeaponInfo().Forget();
        SetCharacterStandingImage().Forget();
    }

    private void OnDestroy()
    {
        // 매니저 이벤트 해제 시 안전하게 체크
        if (Managers.Equipment != null)
        {
            Managers.Equipment.OnEquipmentChanged -= RefreshEquipSlot;
        }
    }

}
