using System;
using System.IO;
using Hidano.FacialControl.Adapters.FileSystem;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;
using UnityEditor;
using UnityEngine;

namespace Hidano.FacialControl.InputSystem.Editor.AutoExport
{
    /// <summary>
    /// <see cref="FacialCharacterProfileSO"/> 系アセットの保存 (Save) を検出し、
    /// 規約パス <c>StreamingAssets/FacialControl/{SO 名}/profile.json</c> および
    /// <c>analog_bindings.json</c> へ JSON を自動エクスポートする
    /// <see cref="UnityEditor.AssetModificationProcessor"/> 派生クラス。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 3-B モデル: ランタイムは常に StreamingAssets/FacialControl/{SO 名}/profile.json から表情データを読み込む。
    /// ユーザーは JSON ファイル本体を直接編集せず Inspector のみを操作するため、
    /// SO アセット保存時に裏で JSON を最新化することで、ビルド後の差し替えを常に可能に保つ。
    /// </para>
    /// <para>
    /// 例外は <see cref="Debug.LogWarning"/> で報告し、複数 SO の処理が中断されないように個別 SO 単位で握りつぶす。
    /// </para>
    /// </remarks>
    public static class FacialCharacterSOAutoExporter
    {
        /// <summary>
        /// アナログバインディング規約 JSON ファイル名 (<c>analog_bindings.json</c>)。
        /// </summary>
        public const string AnalogBindingsJsonFileName = "analog_bindings.json";

        // ============================================================
        // AssetModificationProcessor フック
        // ============================================================

        /// <summary>
        /// <see cref="UnityEditor.AssetModificationProcessor.OnWillSaveAssets"/> 経由で
        /// <see cref="FacialCharacterProfileSO"/> 系アセット保存を検出し、StreamingAssets 配下へ JSON を書き出す。
        /// </summary>
        private sealed class AssetSaveProcessor : UnityEditor.AssetModificationProcessor
        {
            /// <summary>
            /// アセット保存直前に Unity が呼ぶ。受信した paths は素通しで返す (フィルタはしない)。
            /// </summary>
            public static string[] OnWillSaveAssets(string[] paths)
            {
                if (paths == null || paths.Length == 0)
                {
                    return paths;
                }

                bool anyExported = false;
                for (int i = 0; i < paths.Length; i++)
                {
                    var path = paths[i];
                    if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    FacialCharacterProfileSO so;
                    try
                    {
                        so = AssetDatabase.LoadAssetAtPath<FacialCharacterProfileSO>(path);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"FacialCharacterSOAutoExporter: '{path}' のロードに失敗したためスキップします: {ex.Message}");
                        continue;
                    }

                    if (so == null)
                    {
                        continue;
                    }

                    if (ExportToStreamingAssets(so))
                    {
                        anyExported = true;
                    }
                }

                if (anyExported)
                {
                    // StreamingAssets 配下の JSON を Unity に認識させる。OnWillSaveAssets 中に同期 Refresh を
                    // 呼ぶと再帰的な Save ループに入る恐れがあるため、保存完了後に遅延実行する。
                    EditorApplication.delayCall += SafeRefreshAssetDatabase;
                }

                return paths;
            }
        }

        // ============================================================
        // メニュー (補助): 選択中の SO を即時エクスポート
        // ============================================================

        /// <summary>
        /// Project ウィンドウで選択中の <see cref="FacialCharacterProfileSO"/> を StreamingAssets へ即時エクスポートする。
        /// </summary>
        [MenuItem("Tools/FacialControl/Force Export Selected Character SO")]
        private static void ForceExportSelected()
        {
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                Debug.LogWarning(
                    "FacialCharacterSOAutoExporter: Project ウィンドウで FacialCharacterProfileSO 派生アセットを選択してください。");
                return;
            }

            int exported = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] is FacialCharacterProfileSO so)
                {
                    if (ExportToStreamingAssets(so))
                    {
                        exported++;
                    }
                }
            }

            if (exported == 0)
            {
                Debug.LogWarning(
                    "FacialCharacterSOAutoExporter: 選択中アセットに FacialCharacterProfileSO 派生は含まれていません。");
                return;
            }

            SafeRefreshAssetDatabase();
            Debug.Log(
                $"FacialCharacterSOAutoExporter: {exported} 件の SO を StreamingAssets に書き出しました。");
        }

        [MenuItem("Tools/FacialControl/Force Export Selected Character SO", validate = true)]
        private static bool ForceExportSelectedValidate()
        {
            return Selection.activeObject is FacialCharacterProfileSO;
        }

        // ============================================================
        // 公開 API: 単一 SO のエクスポート
        // ============================================================

        /// <summary>
        /// 指定 SO の最新データを規約パス <c>StreamingAssets/FacialControl/{SO 名}/profile.json</c> および
        /// (具象が <see cref="FacialCharacterSO"/> なら) <c>analog_bindings.json</c> に書き出す。
        /// </summary>
        /// <param name="so">エクスポート対象 SO。null の場合は何もせず false を返す。</param>
        /// <returns>少なくとも 1 ファイルを書き出せた場合 true。</returns>
        public static bool ExportToStreamingAssets(FacialCharacterProfileSO so)
        {
            if (so == null)
            {
                return false;
            }

            string assetName = so.CharacterAssetName;
            if (string.IsNullOrWhiteSpace(assetName))
            {
                Debug.LogWarning(
                    "FacialCharacterSOAutoExporter: SO 名が空のため StreamingAssets エクスポートをスキップします。"
                    + " SO アセットを保存し名前を確定させてください。");
                return false;
            }

            bool exportedAny = false;

            // --- profile.json ---
            try
            {
                string profilePath = FacialCharacterProfileSO.GetStreamingAssetsProfilePath(assetName);
                if (!string.IsNullOrEmpty(profilePath))
                {
                    EnsureParentDirectory(profilePath);
                    var profile = so.BuildFallbackProfile();
                    var repo = new FileProfileRepository(new SystemTextJsonParser());
                    repo.SaveProfile(profilePath, profile);
                    exportedAny = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"FacialCharacterSOAutoExporter: '{assetName}' の profile.json エクスポートに失敗しました: {ex.Message}");
            }

            // --- analog_bindings.json (具象が FacialCharacterSO の場合のみ) ---
            if (so is FacialCharacterSO inputSO)
            {
                try
                {
                    string analogPath = GetStreamingAssetsAnalogBindingsPath(assetName);
                    if (!string.IsNullOrEmpty(analogPath))
                    {
                        EnsureParentDirectory(analogPath);
                        AnalogInputBindingProfile analogProfile = inputSO.BuildAnalogProfile();
                        string analogJson = AnalogInputBindingJsonLoader.Save(analogProfile);
                        File.WriteAllText(analogPath, analogJson, System.Text.Encoding.UTF8);
                        exportedAny = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"FacialCharacterSOAutoExporter: '{assetName}' の analog_bindings.json エクスポートに失敗しました: {ex.Message}");
                }
            }

            return exportedAny;
        }

        /// <summary>
        /// <see cref="FacialCharacterProfileSO.StreamingAssetsRootFolder"/> + SO 名 +
        /// <see cref="AnalogBindingsJsonFileName"/> の規約パスを返す。
        /// </summary>
        /// <param name="assetName">SO 名。空白なら null を返す。</param>
        public static string GetStreamingAssetsAnalogBindingsPath(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }
            return Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                FacialCharacterProfileSO.StreamingAssetsRootFolder,
                assetName,
                AnalogBindingsJsonFileName);
        }

        // ============================================================
        // 内部ヘルパー
        // ============================================================

        private static void EnsureParentDirectory(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void SafeRefreshAssetDatabase()
        {
            try
            {
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"FacialCharacterSOAutoExporter: AssetDatabase.Refresh() に失敗しました: {ex.Message}");
            }
        }
    }
}
