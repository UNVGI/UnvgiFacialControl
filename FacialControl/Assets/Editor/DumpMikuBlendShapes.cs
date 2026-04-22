using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hidano.FacialControl.Samples.EditorTools
{
    /// <summary>
    /// SampleScene の HatsuneMiku Prefab に含まれる SkinnedMeshRenderer の BlendShape 名を
    /// テキストファイルにダンプする一回限りのユーティリティ。
    /// batchmode で `-executeMethod Hidano.FacialControl.Samples.EditorTools.DumpMikuBlendShapes.Dump` として呼ぶ。
    /// </summary>
    public static class DumpMikuBlendShapes
    {
        public static void Dump()
        {
            const string prefabGuid = "5588fb1befbdc5344a5457a74467ca94";
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogError($"[DumpMikuBlendShapes] GUID {prefabGuid} に対応する Asset が見つかりません。");
                EditorApplication.Exit(2);
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[DumpMikuBlendShapes] prefab のロードに失敗: {prefabPath}");
                EditorApplication.Exit(3);
                return;
            }

            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var sb = new StringBuilder();
            sb.AppendLine($"# BlendShape names for {prefabPath}");
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMesh == null) continue;
                var mesh = renderer.sharedMesh;
                sb.AppendLine($"## Renderer: {renderer.name} (mesh: {mesh.name}, count: {mesh.blendShapeCount})");
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    sb.AppendLine(mesh.GetBlendShapeName(i));
                }
                sb.AppendLine();
            }

            const string outputPath = "test-results/miku-blendshapes.txt";
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[DumpMikuBlendShapes] dumped {renderers.Length} renderer(s) to {Path.GetFullPath(outputPath)}");
            EditorApplication.Exit(0);
        }
    }
}
