using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
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

    // ==========================================
    // [중요] 비동기 세이브 (Safe Save & ThreadPool)
    // ==========================================
    public async UniTask SaveCurrentPartyAsync()
    {
        if (string.IsNullOrEmpty(_currentPartyId))
        {
            Debug.LogError("[SaveManager] CurrentPartyId가 설정되지 않았습니다.");
            return;
        }

        // 1. 메인 스레드에서 데이터 긁어오기 (유니티 API는 메인 스레드에서만 접근 가능)
        PartySaveData data = CollectCurrentSaveData();

        string finalPath = GetPath(_currentPartyId);
        string tempPath = finalPath + ".tmp"; // 임시 파일 경로
        string backupPath = finalPath + ".bak"; // 백업 파일 경로 (선택사항)

        try
        {
            // 2. 무거운 작업(JSON 변환, 암호화, 파일 쓰기)을 백그라운드 스레드로 넘김
            await UniTask.RunOnThreadPool(() =>
            {
                // JSON 직렬화
                string json = JsonConvert.SerializeObject(data, _settings);
                // AES 암호화
                byte[] encrypted = Encrypt(json);

                // [Safe Save 1단계] 임시 파일(temp)에 먼저 씀
                File.WriteAllBytes(tempPath, encrypted);

                // [Safe Save 2단계] 기존 세이브 파일이 있다면 교체 작업 진행
                if (File.Exists(finalPath))
                {
                    // File.Replace는 temp를 final로 덮어쓰고, 기존 final을 backup으로 뺌
                    // (플랫폼에 따라 Replace가 안 통할 수 있으므로 try-catch로 대비)
                    File.Replace(tempPath, finalPath, backupPath, ignoreMetadataErrors: true);
                }
                else
                {
                    // 기존 파일이 없으면 그냥 temp를 final로 이름 변경
                    File.Move(tempPath, finalPath);
                }
            });

            Debug.Log($"[SaveManager] 비동기 안전 저장 완료: {finalPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] 세이브 저장 중 오류 발생: {ex.Message}");
            // 저장이 실패했다면 찌꺼기 temp 파일 삭제
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
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
    }

    // ==========================================
    // 비동기 로드 (스레드 분리)
    // ==========================================
    public async UniTask<PartySaveData> LoadPartyAsync(string partyId)
    {
        string finalPath = GetPath(partyId);
        string backupPath = finalPath + ".bak";

        if (!File.Exists(finalPath))
        {
            // 메인 파일이 없는데 백업 파일이 있다면 (저장 중 튕겼을 때 복구)
            if (File.Exists(backupPath))
            {
                Debug.LogWarning("[SaveManager] 메인 세이브가 없어 백업 파일에서 복구합니다.");
                File.Copy(backupPath, finalPath);
            }
            else
            {
                return null;
            }
        }

        try
        {
            // 로딩도 스레드 풀에서 수행
            PartySaveData resultData = null;
            await UniTask.RunOnThreadPool(() =>
            {
                byte[] encrypted = File.ReadAllBytes(finalPath);
                string json = Decrypt(encrypted);
                resultData = JsonConvert.DeserializeObject<PartySaveData>(json, _settings);
            });

            return resultData;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] 세이브 로드 실패: {ex.Message}");
            return null;
        }
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

    private string GetPath(string partyId) => Path.Combine(Application.persistentDataPath, $"save_{partyId}.json");

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
        List<BaseCharacter> members = Managers.Party.GetMember();

 
        foreach (BaseCharacter member in members)
        {
            save.Add(
                new CharacterSaveData
                {
                    characterId = member.Stat.GetID(),
                    weaponLevel = member.Stat.WeaponLevel,
                    currentHp = member.Stat.CurrentHp <= 0 ? 1 : member.Stat.CurrentHp
                }
            );
 
        }

        return save;
    }

    private void LoadCharacterData(List<CharacterSaveData> savedCharacters)
    {
        if (savedCharacters == null || savedCharacters.Count == 0) return;

        List<BaseCharacter> members = Managers.Party.GetMember();

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
