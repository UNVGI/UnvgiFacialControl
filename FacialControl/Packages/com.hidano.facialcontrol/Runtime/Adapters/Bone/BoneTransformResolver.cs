using System.Collections.Generic;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.Bone
{
    /// <summary>
    /// 名前から <see cref="UnityEngine.Transform"/> を解決し結果をキャッシュするサービス
    /// (Req 2.1, 2.2, 2.3, 2.4, 2.5)。
    /// </summary>
    /// <remarks>
    /// preview.1: 同名 Transform 複数時は「最初の発見を採用、警告なし」。
    /// 解決失敗時は <see cref="Debug.LogWarning"/> + null 返却（throw しない）。
    /// 同名連続警告は dedupe する。
    /// </remarks>
    public sealed class BoneTransformResolver
    {
        private readonly Transform _root;
        private readonly Dictionary<string, Transform> _cache = new Dictionary<string, Transform>();
        private readonly HashSet<string> _missingWarned = new HashSet<string>();

        public BoneTransformResolver(Transform root)
        {
            _root = root;
        }

        public Transform Resolve(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
            {
                return null;
            }

            if (_cache.TryGetValue(boneName, out var cached))
            {
                return cached;
            }

            if (_missingWarned.Contains(boneName))
            {
                return null;
            }

            var found = FindRecursive(_root, boneName);
            if (found != null)
            {
                _cache[boneName] = found;
                return found;
            }

            _missingWarned.Add(boneName);
            Debug.LogWarning($"[BoneTransformResolver] bone '{boneName}' を解決できませんでした。");
            return null;
        }

        public void Prime(IReadOnlyList<string> boneNames)
        {
            if (boneNames == null)
            {
                return;
            }
            for (int i = 0; i < boneNames.Count; i++)
            {
                _ = Resolve(boneNames[i]);
            }
        }

        private static Transform FindRecursive(Transform node, string name)
        {
            if (node == null)
            {
                return null;
            }
            if (node.name == name)
            {
                return node;
            }
            int count = node.childCount;
            for (int i = 0; i < count; i++)
            {
                var found = FindRecursive(node.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
