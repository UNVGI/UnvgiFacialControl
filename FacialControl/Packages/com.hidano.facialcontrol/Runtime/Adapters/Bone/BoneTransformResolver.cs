using System.Collections.Generic;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.Bone
{
    /// <summary>
    /// 名前または参照モデル相対 path から <see cref="UnityEngine.Transform"/> を解決し、
    /// 結果をキャッシュするサービス (Req 2.1, 2.2, 2.3, 2.4, 2.5)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 解決対象文字列が <c>'/'</c> を含む場合は <see cref="Transform.Find(string)"/> 互換の
    /// 相対 path として扱い、含まない場合は階層全体を再帰探索する単純名解決として扱う。
    /// 単純名解決のとき同名 Transform が複数ヒットすると最初の発見を採用しつつ警告を 1 回だけ出す
    /// (M-7、preview.2 で導入)。曖昧性を避けたい場合は相対 path を指定する。
    /// </para>
    /// <para>
    /// 解決失敗時は <see cref="Debug.LogWarning"/> + null 返却（throw しない）。
    /// 同一文字列に対する警告は dedupe する。
    /// </para>
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

        public Transform Resolve(string boneNameOrPath)
        {
            if (string.IsNullOrEmpty(boneNameOrPath))
            {
                return null;
            }

            if (_cache.TryGetValue(boneNameOrPath, out var cached))
            {
                return cached;
            }

            if (_missingWarned.Contains(boneNameOrPath))
            {
                return null;
            }

            Transform found = IsRelativePath(boneNameOrPath)
                ? ResolveByRelativePath(boneNameOrPath)
                : FindFirstAndWarnIfDuplicate(boneNameOrPath);

            if (found != null)
            {
                _cache[boneNameOrPath] = found;
                return found;
            }

            _missingWarned.Add(boneNameOrPath);
            Debug.LogWarning($"[BoneTransformResolver] bone '{boneNameOrPath}' を解決できませんでした。");
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

        private static bool IsRelativePath(string s)
        {
            return s.IndexOf('/') >= 0;
        }

        // 相対 path は Transform.Find(string) が '/' 区切りで階層を辿ってくれるので、
        // root.Find に丸投げする。先頭/末尾の '/' は Unity 側で整理される。
        private Transform ResolveByRelativePath(string path)
        {
            if (_root == null) return null;
            return _root.Find(path);
        }

        // 単純名指定の場合は階層全体を歩いて全マッチを数える。複数ヒット時は警告 1 回のみ。
        // 計算量は per-bone 一度きりで cache に乗るためホットパスには影響しない。
        private Transform FindFirstAndWarnIfDuplicate(string boneName)
        {
            Transform first = null;
            int count = 0;
            CollectMatches(_root, boneName, ref first, ref count);

            if (count >= 2)
            {
                Debug.LogWarning(
                    $"[BoneTransformResolver] bone 名 '{boneName}' が階層内に複数 ({count} 件) 存在します。" +
                    " 最初の発見を採用しますが、別ボーンを指したい場合は参照モデル相対 path"
                    + " (例: 'Armature/Hips/Spine/Head/LeftEye') を指定してください。");
            }

            return first;
        }

        private static void CollectMatches(Transform node, string name, ref Transform first, ref int count)
        {
            if (node == null) return;
            if (node.name == name)
            {
                if (first == null) first = node;
                count++;
            }
            int childCount = node.childCount;
            for (int i = 0; i < childCount; i++)
            {
                CollectMatches(node.GetChild(i), name, ref first, ref count);
            }
        }
    }
}
