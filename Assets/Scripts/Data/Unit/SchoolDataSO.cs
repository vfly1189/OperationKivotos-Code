using UnityEngine;

[CreateAssetMenu(fileName = "SchoolData", menuName = "Game/SchoolData")]
public class SchoolDataSO : ScriptableObject
{
    public string schoolNameEN;
    public string schoolNameKR;
    public Sprite schoolLogo;

    [Header("UI Resources")]
    public Sprite schoolIcon;      // "School_Icon_..."
    public Sprite schoolNameFont;  // "..._ImageFont"

    [Header("Audio")]
    public AudioClip themeBGM;     // "..._Theme"

    // 데이터 자체를 들고 있는 게 아니라, 만들어둔 SO를 연결(Link)만 함
    public CharacterDataSO[] characters;
}