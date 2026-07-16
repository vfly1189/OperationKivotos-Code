using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

// [Phase 0.5c] 대형 이미지 아틀라스 해체 — "1장 필요한데 페이지 전체(64MB) 로드" 제거.
//  1) 원본 텍스처들을 아틀라스가 있던 어드레서블 그룹에 개별 등록 (주소 = 파일명, 라벨 없음 = lazy)
//  2) 아틀라스의 어드레서블 엔트리 제거 + .spriteatlas 삭제
//  소비 코드는 LoadAsync<Sprite>(파일명)으로 이미 전환됨 (UI_Info / UI_EscapeMenu).
public static class AtlasDismantleTool
{
    private struct Target
    {
        public string AtlasPath;
        public string SourceFolder;
    }

    private static readonly Target[] Targets =
    {
        new Target
        {
            AtlasPath = "Assets/Resources_moved/Images/StandingImagesAtlas.spriteatlas",
            SourceFolder = "Assets/Resources_moved/Images/StandingImages",
        },
        new Target
        {
            AtlasPath = "Assets/Resources_moved/Images/EscapeMenuAtlas.spriteatlas",
            SourceFolder = "Assets/Resources_moved/Images/EscapeMenu",
        },
    };

    [MenuItem("Tools/Atlas 해체 (Phase 0.5c)")]
    private static void Dismantle()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AtlasDismantle] Addressables 설정을 찾을 수 없습니다.");
            return;
        }

        var sb = new StringBuilder();

        foreach (var target in Targets)
        {
            string atlasGuid = AssetDatabase.AssetPathToGUID(target.AtlasPath);
            AddressableAssetEntry atlasEntry = string.IsNullOrEmpty(atlasGuid) ? null : settings.FindAssetEntry(atlasGuid);
            AddressableAssetGroup group = atlasEntry != null ? atlasEntry.parentGroup : settings.DefaultGroup;

            // 1. 원본 텍스처 개별 등록 — 주소는 파일명(코드의 로드 키와 동일), 라벨 없음
            int added = 0;
            foreach (var texGuid in AssetDatabase.FindAssets("t:Texture2D", new[] { target.SourceFolder }))
            {
                string texPath = AssetDatabase.GUIDToAssetPath(texGuid);
                var entry = settings.CreateOrMoveEntry(texGuid, group, readOnly: false, postEvent: false);
                entry.address = Path.GetFileNameWithoutExtension(texPath);
                added++;
            }

            // 2. 아틀라스 엔트리 제거 + 에셋 삭제
            if (atlasEntry != null)
                settings.RemoveAssetEntry(atlasGuid, postEvent: false);

            bool deleted = !string.IsNullOrEmpty(atlasGuid) && AssetDatabase.DeleteAsset(target.AtlasPath);

            sb.AppendLine($"[AtlasDismantle] {Path.GetFileNameWithoutExtension(target.AtlasPath)} — " +
                          $"개별 등록 {added}개 (그룹: {group.Name}) / 아틀라스 삭제: {(deleted ? "완료" : "이미 없음/실패")}");
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log(sb.ToString());
    }
}
