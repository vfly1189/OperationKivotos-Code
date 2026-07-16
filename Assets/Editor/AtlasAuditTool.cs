using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.U2D;

// [Phase 0.5 계측] 아틀라스 실체 검사 — "설정"이 아니라 "실제 패킹 결과"를 본다.
//  - 각 SpriteAtlas를 현재 빌드 타깃으로 강제 팩 → 실제 페이지 텍스처의 포맷/크기/메모리 출력
//  - 원본(packable) 텍스처의 임포트 설정/포맷/메모리도 함께 출력
//  - 목적: "인스펙터는 Normal Quality인데 측정은 무압축(436MB)"의 모순 원인 확정
//  - 결과: 콘솔 + MetricsLogs/atlas_audit_*.txt (에이전트가 읽는 파일)
public static class AtlasAuditTool
{
    [MenuItem("Tools/Atlas 포맷 검사 (Phase 0.5)")]
    private static void Audit()
    {
        var guids = AssetDatabase.FindAssets("t:SpriteAtlas");
        var atlases = new List<SpriteAtlas>();
        foreach (var guid in guids)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AssetDatabase.GUIDToAssetPath(guid));
            if (atlas != null) atlases.Add(atlas);
        }

        if (atlases.Count == 0)
        {
            Debug.LogWarning("[AtlasAudit] SpriteAtlas를 찾지 못했습니다.");
            return;
        }

        // 현재 빌드 타깃 기준으로 강제 패킹 (빌드와 같은 조건)
        SpriteAtlasUtility.PackAtlases(atlases.ToArray(), EditorUserBuildSettings.activeBuildTarget);

        var sb = new StringBuilder();
        sb.AppendLine($"# Atlas 포맷 검사 — {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# 빌드 타깃: {EditorUserBuildSettings.activeBuildTarget}, SpritePackerMode: {EditorSettings.spritePackerMode}");
        sb.AppendLine();

        long totalPageBytes = 0;
        long totalSourceBytes = 0;

        foreach (var atlas in atlases)
        {
            sb.AppendLine($"══════ {atlas.name} (스프라이트 {atlas.spriteCount}개) ══════");

            // ---- 설정값 (인스펙터에 보이는 것) ----
            var def = atlas.GetPlatformSettings("DefaultTexturePlatform");
            var standalone = atlas.GetPlatformSettings("Standalone");
            sb.AppendLine($"  [설정] Default: maxSize={def.maxTextureSize}, format={def.format}, compression={def.textureCompression}");
            sb.AppendLine($"  [설정] Standalone(override={standalone.overridden}): maxSize={standalone.maxTextureSize}, format={standalone.format}, compression={standalone.textureCompression}");

            // ---- 실제 패킹된 페이지 (진실) ----
            var sprites = new Sprite[atlas.spriteCount];
            atlas.GetSprites(sprites);

            var pages = new Dictionary<Texture2D, long>();
            foreach (var s in sprites)
            {
                if (s == null || s.texture == null) continue;
                if (!pages.ContainsKey(s.texture))
                    pages[s.texture] = Profiler.GetRuntimeMemorySizeLong(s.texture);
            }

            long atlasPageBytes = 0;
            foreach (var kv in pages)
            {
                var tex = kv.Key;
                atlasPageBytes += kv.Value;
                sb.AppendLine($"  [실제 페이지] {tex.name}: {tex.width}x{tex.height}, 포맷={tex.format}, 메모리={ToMB(kv.Value)}");
            }
            totalPageBytes += atlasPageBytes;
            sb.AppendLine($"  [실제 페이지 합계] {pages.Count}장, {ToMB(atlasPageBytes)}");

            // ---- 원본(packable) 텍스처 ----
            var packables = atlas.GetPackables();
            long sourceBytes = 0;
            int sourceCount = 0;
            string sampleImporterInfo = null;

            foreach (var packable in packables)
            {
                string packablePath = AssetDatabase.GetAssetPath(packable);
                var texturePaths = new List<string>();

                if (AssetDatabase.IsValidFolder(packablePath))
                {
                    foreach (var tGuid in AssetDatabase.FindAssets("t:Texture2D", new[] { packablePath }))
                        texturePaths.Add(AssetDatabase.GUIDToAssetPath(tGuid));
                }
                else
                {
                    texturePaths.Add(packablePath);
                }

                foreach (var texPath in texturePaths)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    if (tex == null) continue;

                    sourceCount++;
                    long bytes = Profiler.GetRuntimeMemorySizeLong(tex);
                    sourceBytes += bytes;

                    if (sampleImporterInfo == null && AssetImporter.GetAtPath(texPath) is TextureImporter importer)
                    {
                        var defSrc = importer.GetDefaultPlatformTextureSettings();
                        sampleImporterInfo = $"maxSize={defSrc.maxTextureSize}, compression={defSrc.textureCompression} (예: {tex.name}: {tex.width}x{tex.height}, 임포트 포맷={tex.format})";
                    }
                }
            }

            totalSourceBytes += sourceBytes;
            sb.AppendLine($"  [원본] {sourceCount}장 합계 {ToMB(sourceBytes)} — 임포터 Default: {sampleImporterInfo}");
            sb.AppendLine();
        }

        sb.AppendLine("══════ 전체 요약 ══════");
        sb.AppendLine($"  실제 페이지 합계: {ToMB(totalPageBytes)}  ← 빌드에서 실제로 쓰는 것");
        sb.AppendLine($"  원본 텍스처 합계: {ToMB(totalSourceBytes)}  ← 에디터 측정(CollectDependencies)이 세는 것");
        sb.AppendLine();
        sb.AppendLine("  해석 가이드:");
        sb.AppendLine("  - 페이지 포맷이 DXT1/DXT5/BC7이면: 아틀라스 압축은 이미 정상, 문제는 원본 임포트 설정 + 측정 방법");
        sb.AppendLine("  - 페이지 포맷이 RGBA32/ARGB32면: 아틀라스 압축이 실제로 적용 안 되고 있음 → 설정 수정 필요");

        string report = sb.ToString();
        Debug.Log("[AtlasAudit]\n" + report);

        string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "MetricsLogs"));
        System.IO.Directory.CreateDirectory(dir);
        string file = System.IO.Path.Combine(dir, $"atlas_audit_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt");
        System.IO.File.WriteAllText(file, report, Encoding.UTF8);
        Debug.Log($"[AtlasAudit] 리포트 저장: {file}");
    }

    private static string ToMB(long bytes) => bytes >= 1L << 20 ? $"{bytes / 1048576f:F1}MB" : $"{bytes / 1024f:F0}KB";
}
