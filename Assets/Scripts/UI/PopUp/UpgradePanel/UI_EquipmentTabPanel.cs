using System;
using UnityEngine;
using UnityEngine.UI;

public class UI_EquipmentTabPanel : UI_Base
{
    [SerializeField] private Button[] _tabButtons;

    // 부모에게 탭 변경을 알릴 콜백 이벤트
    public Action<EquipmentTabType> OnTabClicked;

    public override void Init()
    {
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            int index = i; //주의: for문 안에서 람다 사용 시 클로저(Closure) 문제 방지를 위해 지역 변수로 복사

            _tabButtons[i].onClick.RemoveAllListeners(); // 중복 구독 방지
            _tabButtons[i].onClick.AddListener(() =>
            {
                // 버튼이 눌리면 해당 인덱스를 Enum으로 캐스팅해서 이벤트 발생
                OnTabClicked?.Invoke((EquipmentTabType)index);
            });
        }
    }
}
