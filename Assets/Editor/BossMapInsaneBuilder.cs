using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;

// 보스 던전 맵의 "반전(Insane)" 버전 — 메뉴: Tools/Boss/Build Insane Boss Map
// 코드 0 · 원본 0 수정: 원본 맵의 Prefab Variant를 만들고, 렌더러 머티리얼만 반전 텍스처 버전으로 갈아 끼운다.
//
// 재료 (이미 만들어 둠): Indoor/Insane/<원본 텍스처 이름>_Insane.png — RGB 반전 · 알파 유지 · 임포트 설정은 원본 그대로
// 하는 일
//  1. 맵의 모든 렌더러(꺼진 것 포함) 머티리얼 중, 텍스처에 _Insane 짝이 있는 것만 복사 → Insane/<이름>_Insane.mat
//     (짝이 없는 머티리얼 — 촛불 파티클 · 빈 칸 등 — 은 원본 그대로)
//  2. BossDungeonMap_Insane.prefab = BossDungeonMap의 Variant, 머티리얼 교체만 오버라이드
// 다시 실행하면 머티리얼 · Variant를 같은 경로에 덮어쓴다(GUID 유지 → 참조 안 끊김).
public static class BossMapInsaneBuilder
{
    private const string MapPath     = "Assets/Resources_moved/Prefabs/Map/BossDungeonMap.prefab";
    private const string VariantPath = "Assets/Resources_moved/Prefabs/Map/BossDungeonMap_Insane.prefab";
    private const string InsaneDir   = "Assets/Map/Scenes/Raid/Hieronymus/Indoor/Insane";
    private const string Suffix      = "_Insane";
    private const string InsaneBossPath = "Assets/Resources_moved/Prefabs/Monster/Hieronymus_Boss_Insane.prefab";

    private static void RegisterAddressable(string assetPath, string address)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) { Debug.LogError("[BossMapInsaneBuilder] Addressable 설정 없음"); return; }

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid)) { Debug.LogError($"[BossMapInsaneBuilder] 에셋 없음: {assetPath}"); return; }

        // 원본 맵이 든 그룹(Scene_BossDungeon)에 같이 넣는다
        var mapEntry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(MapPath));
        AddressableAssetGroup group = mapEntry != null ? mapEntry.parentGroup : settings.DefaultGroup;

        var entry = settings.FindAssetEntry(guid);
        if (entry == null || entry.parentGroup != group)
            entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
        entry.address = address;

        EditorUtility.SetDirty(group);
        EditorUtility.SetDirty(settings);
        Debug.Log($"[BossMapInsaneBuilder] Addressable '{address}' → {group.Name}");
    }

    [MenuItem("Tools/Boss/Build Insane Boss Map")]
    public static void Build()
    {
        var map = AssetDatabase.LoadAssetAtPath<GameObject>(MapPath);
        if (map == null) { Debug.LogError($"[BossMapInsaneBuilder] 맵 없음: {MapPath}"); return; }
        if (!AssetDatabase.IsValidFolder(InsaneDir)) { Debug.LogError($"[BossMapInsaneBuilder] 반전 텍스처 폴더 없음: {InsaneDir}"); return; }

        // 꺼진 상태로 미리보기 씬에 꺼내 작업 — 지금 열린 씬을 더럽히지 않는다
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(map, scene);

            var swap = new Dictionary<Material, Material>();
            int renderers = 0, slots = 0;

            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material insane = GetInsane(mats[i], swap);
                    if (insane == null) continue;
                    mats[i] = insane;
                    changed = true;
                    slots++;
                }
                if (!changed) continue;

                r.sharedMaterials = mats;
                renderers++;
            }

            if (swap.Count == 0)
            {
                Debug.LogWarning("[BossMapInsaneBuilder] 바꿀 머티리얼이 없음 — Insane 텍스처 이름 확인");
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(inst, VariantPath, out bool ok);
            if (ok)
            {
                Debug.Log($"[BossMapInsaneBuilder] 완료 — 머티리얼 {swap.Count}개 · 렌더러 {renderers}개({slots}칸) → {VariantPath}");

                // Hard 모드가 주소로 읽는다 (BossDungeonScene 임시 하드코딩) — 원본 맵과 같은 그룹에 등록
                RegisterAddressable(VariantPath, "BossDungeonMap_Insane");
                RegisterAddressable(InsaneBossPath, "Hieronymus_Boss_Insane");
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath));
            }
            else Debug.LogError("[BossMapInsaneBuilder] Variant 저장 실패");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }

        AssetDatabase.SaveAssets();
    }

    // 원본 머티리얼 → 반전 머티리얼 (처음 만나면 만들고 기억). 반전 텍스처 짝이 하나도 없으면 null = 그대로 둠.
    private static Material GetInsane(Material src, Dictionary<Material, Material> cache)
    {
        if (src == null) return null;
        if (cache.TryGetValue(src, out var done)) return done;

        string srcPath = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(srcPath) || !srcPath.EndsWith(".mat")) return null;   // 내장 · 빈 참조

        var copy = new Material(src);
        bool any = false;
        foreach (string prop in src.GetTexturePropertyNames())
        {
            Texture tex = src.GetTexture(prop);
            if (tex == null) continue;

            Texture inv = FindInsaneTexture(tex);
            if (inv == null) continue;

            copy.SetTexture(prop, inv);
            any = true;
        }

        if (!any)
        {
            Object.DestroyImmediate(copy);
            return null;
        }

        string dst = $"{InsaneDir}/{src.name}{Suffix}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(dst);
        Material result;
        if (existing != null)
        {
            existing.shader = copy.shader;
            existing.CopyPropertiesFromMaterial(copy);
            existing.shaderKeywords = copy.shaderKeywords;
            existing.renderQueue = src.renderQueue;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(copy);
            result = existing;
        }
        else
        {
            copy.name = src.name + Suffix;
            AssetDatabase.CreateAsset(copy, dst);
            result = copy;
        }

        cache[src] = result;
        return result;
    }

    private static Texture FindInsaneTexture(Texture tex)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        if (string.IsNullOrEmpty(path)) return null;
        string candidate = $"{InsaneDir}/{Path.GetFileNameWithoutExtension(path)}{Suffix}{Path.GetExtension(path)}";
        return AssetDatabase.LoadAssetAtPath<Texture>(candidate);
    }
}
