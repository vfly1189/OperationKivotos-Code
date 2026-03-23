using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using UnityEngine;

public class SaveManager
{
    // AES 키/IV는 고정값 (포트폴리오 수준)
    // 실제 서비스라면 키를 하드코딩하면 안 되지만, 단일 기기 로컬 저장엔 충분
    private static readonly string _aesKey = "AbydosRPG_Key128";  // 정확히 16자 (128bit)
    private static readonly string _aesIV = "AbydosRPG_IV1234"; // 정확히 16자


    private string _currentPartyId = "Abydos";
    // SaveManager.cs
    public bool IsReady => !string.IsNullOrEmpty(_currentPartyId);

    // JsonConvert 설정 한 곳에서 관리
    private static readonly JsonSerializerSettings _settings = new JsonSerializerSettings
    {
        Formatting = Newtonsoft.Json.Formatting.None,         // 파일 크기 최소화 (디버그 시 Indented로 변경)
        NullValueHandling = NullValueHandling.Ignore,  // null 필드는 파일에 안 씀
        Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
    };

    public void Init() 
    {
        Debug.Log(Application.persistentDataPath);
    }

    public void SetCurrentParty(string partyId)
    {
        _currentPartyId = partyId;
    }

    // 현재 파티 저장
    public void SaveCurrentParty()
    {
        if (string.IsNullOrEmpty(_currentPartyId))
        {
            Debug.LogError("[SaveManager] CurrentPartyId가 설정되지 않았습니다.");
            return;
        }

        var data = CollectCurrentSaveData();
        WriteToFile(_currentPartyId, data);

        Debug.Log($"[SaveManager] 저장 완료: {GetPath(_currentPartyId)}");
    }

    // 특정 파티 로드
    public bool TryLoadParty(string partyId, out PartySaveData data)
    {
        string path = GetPath(partyId);
        if (!File.Exists(path)) { data = null; return false; }

        byte[] encrypted = File.ReadAllBytes(path);                // ReadAllText → ReadAllBytes
        string json = Decrypt(encrypted);                          // ← 복호화 추가
        data = JsonConvert.DeserializeObject<PartySaveData>(json, _settings);
        return data != null;



        //string path = GetPath(partyId);
        //if (!File.Exists(path))
        //{
        //    data = null;
        //    return false;
        //}

        //string json = File.ReadAllText(path);
        //data = JsonConvert.DeserializeObject<PartySaveData>(json, _settings);
        //return data != null;

    }

    // 새 파티 세이브 생성
    public PartySaveData CreateNewSave(string partyId)
    {
        return new PartySaveData
        {
            partyId = partyId,
            party = new PartyRuntimeData { partyLevel = 1, partyCurrentExp = 0 },
            characters = new System.Collections.Generic.List<CharacterSaveData>(),
            inventory = new InventorySaveData(),
            equipment = new EquipmentSaveData(),
            wallet = new WalletSaveData()
        };
    }

    // 세이브 파일 삭제 (파티 초기화 기능 등에서 사용)
    public void DeleteSave(string partyId)
    {
        string path = GetPath(partyId);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[SaveManager] 세이브 삭제: {path}");
        }
    }

    public bool HasSave(string partyId)
    {
        return File.Exists(GetPath(partyId));
    }

    // ==========================================
    // Private
    // ==========================================

    // 현재 각 Manager에서 데이터를 긁어 PartySaveData로 조립
    private PartySaveData CollectCurrentSaveData()
    {
        return new PartySaveData
        {
            partyId = _currentPartyId,

            party = new PartyRuntimeData
            {
                partyLevel = Managers.Party.PartyLevel,
                partyCurrentExp = Managers.Party.PartyCurrentExp
            },

            inventory = Managers.Inventory.GetSaveData(),
            equipment = Managers.Equipment.GetSaveData(),
            wallet = Managers.Wallet.GetSaveData(),
            characters = CollectCharacterData()    
        };
    }

    private void WriteToFile(string partyId, PartySaveData data)
    {
        string json = JsonConvert.SerializeObject(data, _settings);
        byte[] encrypted = Encrypt(json);                          // ← 암호화 추가
        File.WriteAllBytes(GetPath(partyId), encrypted);           // WriteAllText → WriteAllBytes


        //string json = JsonConvert.SerializeObject(data, _settings);
        //File.WriteAllText(GetPath(partyId), json);

    }

    private string GetPath(string partyId)
    {
        return Path.Combine(Application.persistentDataPath, $"save_{partyId}.json");
    }

    // ==========================================
    // AES 암호화 / 복호화
    // ==========================================
    private byte[] Encrypt(string plainText)
    {
        using Aes aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(_aesKey);
        aes.IV = Encoding.UTF8.GetBytes(_aesIV);

        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
        using var sw = new StreamWriter(cs);
        sw.Write(plainText);
        sw.Close();         // cs.FlushFinalBlock() 포함

        return ms.ToArray();
    }

    private string Decrypt(byte[] cipherBytes)
    {
        using Aes aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(_aesKey);
        aes.IV = Encoding.UTF8.GetBytes(_aesIV);

        using var ms = new MemoryStream(cipherBytes);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }

    private List<CharacterSaveData> CollectCharacterData()
    {
        List<CharacterSaveData> save = new List<CharacterSaveData>();
        List<BaseCharacter> members = Managers.Party.GetMemeber();

        foreach (BaseCharacter member in members)
        {
            save.Add(
                new CharacterSaveData 
                { 
                    characterId = member.Stat.GetID(),
                    weaponLevel = member.Stat.WeaponLevel,
                    currentHp = member.Stat.CurrentHp,
                }
            );
        }

        return save;
    }

    private void LoadCharacterData(List<CharacterSaveData> savedCharacters)
    {
        if (savedCharacters == null || savedCharacters.Count == 0) return;

        List<BaseCharacter> members = Managers.Party.GetMemeber();

        foreach (BaseCharacter member in members)
        {
            int id = member.Stat.GetID();

            // 저장된 데이터에서 ID 일치하는 캐릭터 찾기
            CharacterSaveData saved = savedCharacters.Find(c => c.characterId == id);
            if (saved == null) continue;

            member.Stat.ApplyCharacterSaveData(saved);
        }
    }

    public void ApplySaveDataToManagers(PartySaveData save)
    {
        if (save == null) return;

        Managers.Inventory.LoadSaveData(save.inventory);
        Managers.Wallet.LoadSaveData(save.wallet);
        Managers.Party.InitFromContext(save.party);      // ← 먼저: MaxHp 레벨 기준 세팅
        LoadCharacterData(save.characters);              // ← 그 다음: 올바른 MaxHp 기준으로 HP Clamp
        Managers.Equipment.LoadSaveData(save.equipment); // ← 마지막: 장비 스탯 추가
    }
}
