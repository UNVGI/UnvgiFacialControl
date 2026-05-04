using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.AutoExport;
using Hidano.FacialControl.Editor.Sampling;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hidano.FacialControl.InputSystem.Editor.AutoExport
{
    /// <summary>
    /// <see cref="FacialCharacterSO"/> 用の保存検出・自動エクスポート パイプライン。
    /// AnimationClip サンプリング → cachedSnapshot 反映 + Gaze 4 系統 clip サンプリングに加え、
    /// 規約パス <c>StreamingAssets/FacialControl/{SO 名}/profile.json</c> (schema v2.0) と
    /// <c>analog_bindings.json</c> へ JSON を自動エクスポートする
    /// <see cref="UnityEditor.AssetModificationProcessor"/> 派生クラス。
    /// </summary>
    /// <remarks>
    /// <para>
    /// profile.json と AnimationClip サンプリングの汎用部分は core の
    /// <see cref="FacialCharacterProfileExporter"/> に委譲する。本クラスは
    /// <see cref="FacialCharacterSO"/> 固有の追加処理 (Gaze clip サンプリング・
    /// analog_bindings.json 出力) と、保存検出・進捗・abort パイプラインを担う。
    /// </para>
    /// </remarks>
    public static class FacialCharacterSOAutoExporter
    {
        /// <summary>
        /// アナログバインディング規約 JSON ファイル名 (<c>analog_bindings.json</c>)。
        /// </summary>
        public const string AnalogBindingsJsonFileName = "analog_bindings.json";

        /// <summary>
        /// 進捗バー表示の発火しきい値（ミリ秒）。Req 9.5 により 200ms 超で発火する。
        /// </summary>
        public const long ProgressBarThresholdMilliseconds = 200L;

        // ============================================================
        // Test seam: 既定実装を override するためのフック
        // ============================================================

        /// <summary>
        /// 注入用 sampler。null のとき <see cref="AnimationClipExpressionSampler"/> の新規インスタンスを使用する。
        /// </summary>
        public static IExpressionAnimationClipSampler SamplerOverride { get; set; }

        /// <summary>
        /// 注入用 progress bar 表示。null のとき <see cref="EditorUtility.DisplayProgressBar"/> 経路を使用する。
        /// </summary>
        public static IProgressBarPresenter ProgressBarPresenterOverride { get; set; }

        /// <summary>
        /// 注入用 stopwatch。null のとき <see cref="System.Diagnostics.Stopwatch"/> を使用する。
        /// </summary>
        public static IStopwatchProvider StopwatchProviderOverride { get; set; }

        /// <summary>
        /// 注入用 SO loader。null のとき <see cref="AssetDatabase.LoadAssetAtPath{T}(string)"/> を使用する。
        /// テスト時に AssetDatabase 経路を経由せずに目的の SO を返すために使用する。
        /// </summary>
        public static Func<string, FacialCharacterProfileSO> AssetLoaderOverride { get; set; }

        // ============================================================
        // AssetModificationProcessor フック
        // ============================================================

        /// <summary>
        /// <see cref="UnityEditor.AssetModificationProcessor.OnWillSaveAssets"/> 経由で
        /// <see cref="FacialCharacterProfileSO"/> 系アセット保存を検出し、サンプリング → JSON 書き出しを行う。
        /// </summary>
        private sealed class AssetSaveProcessor : UnityEditor.AssetModificationProcessor
        {
            public static string[] OnWillSaveAssets(string[] paths)
            {
                return ProcessAssetSavePaths(paths);
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

            var sampler = ResolveSampler();
            int exported = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] is FacialCharacterProfileSO so)
                {
                    try
                    {
                        FacialCharacterProfileExporter.SampleAnimationClipsIntoCachedSnapshots(so, sampler);
                        SampleGazeClipsIntoConfigs(so);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"FacialCharacterSOAutoExporter: '{so.CharacterAssetName}' の AnimationClip サンプリングに失敗しました: {ex}");
                        continue;
                    }
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
        // 公開 API: OnWillSaveAssets 等価のテスト可能エントリポイント
        // ============================================================

        /// <summary>
        /// <see cref="UnityEditor.AssetModificationProcessor.OnWillSaveAssets"/> と同等の処理を行うテスト可能エントリポイント。
        /// 各 <see cref="FacialCharacterProfileSO"/> 由来の <c>.asset</c> パスについて以下を実行する。
        /// </summary>
        /// <remarks>
        /// <list type="number">
        ///   <item>各 Expression の AnimationClip を <see cref="IExpressionAnimationClipSampler"/> で時刻 0 サンプリングし、
        ///         結果を <see cref="ExpressionSerializable.cachedSnapshot"/> に書き戻す（Req 9.1, 9.2, 9.3）。</item>
        ///   <item>StreamingAssets / FacialControl / {SO 名} / <c>profile.json</c> へ schema v2.0 JSON を書き出す（Req 9.1）。</item>
        ///   <item>処理時間が <see cref="ProgressBarThresholdMilliseconds"/> を超えたら progress bar を発火（Req 9.5）。</item>
        ///   <item>サンプリングで例外が発生した場合は当該 SO の save を abort し、戻り値配列から path を除外する（Req 9.6）。</item>
        /// </list>
        /// </remarks>
        /// <param name="paths">Unity が保存予定のアセットパス配列。null / empty は素通しする。</param>
        /// <returns>filtered な path 配列。サンプリング失敗 SO の path のみ除外される。</returns>
        public static string[] ProcessAssetSavePaths(string[] paths)
        {
            if (paths == null || paths.Length == 0)
            {
                return paths;
            }

            var sampler = ResolveSampler();
            var progressBar = ResolveProgressBarPresenter();
            var stopwatch = ResolveStopwatch();

            int candidateCount = CountCandidates(paths);

            var resultPaths = new List<string>(paths.Length);
            bool anyExported = false;
            int processed = 0;

            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    var path = paths[i];
                    if (!IsAssetCandidatePath(path))
                    {
                        resultPaths.Add(path);
                        continue;
                    }

                    FacialCharacterProfileSO so;
                    try
                    {
                        so = LoadAssetAtPath(path);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"FacialCharacterSOAutoExporter: '{path}' のロードに失敗したためスキップします: {ex.Message}");
                        resultPaths.Add(path);
                        continue;
                    }

                    if (so == null)
                    {
                        // Non-target asset: 素通し
                        resultPaths.Add(path);
                        continue;
                    }

                    MaybeShowProgressBar(progressBar, stopwatch, so.CharacterAssetName, processed, candidateCount);
                    processed++;

                    // --- AnimationClip サンプリング ---
                    bool sampleOk;
                    try
                    {
                        FacialCharacterProfileExporter.SampleAnimationClipsIntoCachedSnapshots(so, sampler);
                        SampleGazeClipsIntoConfigs(so);
                        sampleOk = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"FacialCharacterSOAutoExporter: '{path}' の AnimationClip サンプリングで例外が発生したため、" +
                            $"当該 SO の save を中止します: {ex}");
                        sampleOk = false;
                    }

                    if (!sampleOk)
                    {
                        // Req 9.6: paths から除外して save abort
                        continue;
                    }

                    if (ExportToStreamingAssets(so))
                    {
                        anyExported = true;
                    }
                    resultPaths.Add(path);
                }
            }
            finally
            {
                progressBar.Clear();
            }

            if (anyExported)
            {
                EditorApplication.delayCall += SafeRefreshAssetDatabase;
            }

            return resultPaths.ToArray();
        }

        // ============================================================
        // 公開 API: 単一 SO のエクスポート
        // ============================================================

        /// <summary>
        /// 指定 SO の最新データを規約パス <c>StreamingAssets/FacialControl/{SO 名}/profile.json</c> および
        /// (具象が <see cref="FacialCharacterSO"/> なら) <c>analog_bindings.json</c> に書き出す。
        /// 本メソッドはサンプリング自体は行わないため、呼出側が事前に
        /// <see cref="SampleAnimationClipsIntoCachedSnapshots"/> を呼び出すか、
        /// もしくは Inspector で cachedSnapshot を埋めておく必要がある。
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

            // profile.json は core 側に委譲する。
            bool exportedAny = FacialCharacterProfileExporter.ExportProfileJson(so);

            // analog_bindings.json は FacialCharacterSO 固有の追加出力。
            if (so is FacialCharacterSO inputSO)
            {
                try
                {
                    string analogPath = GetStreamingAssetsAnalogBindingsPath(assetName);
                    if (!string.IsNullOrEmpty(analogPath))
                    {
                        FacialCharacterProfileExporter.EnsureParentDirectory(analogPath);
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
        /// 指定 SO 配下の各 Expression について、AnimationClip があれば sampler で時刻 0 サンプリングを行い、
        /// 結果を <see cref="ExpressionSerializable.cachedSnapshot"/> に書き戻す。
        /// AnimationClip が null の Expression はスキップする。
        /// 1 件でも例外を発生させた場合は呼出側へ伝播し、SO 全体の save を abort させる責務を委ねる（Req 9.6）。
        /// </summary>
        /// <remarks>
        /// 互換のため残すラッパ。実装は core の
        /// <see cref="FacialCharacterProfileExporter.SampleAnimationClipsIntoCachedSnapshots"/> に委譲する。
        /// </remarks>
        /// <param name="so">対象 SO。null は no-op。</param>
        /// <param name="sampler">注入された sampler 実装。</param>
        public static void SampleAnimationClipsIntoCachedSnapshots(
            FacialCharacterProfileSO so,
            IExpressionAnimationClipSampler sampler)
        {
            FacialCharacterProfileExporter.SampleAnimationClipsIntoCachedSnapshots(so, sampler);
        }

        /// <summary>
        /// <see cref="FacialCharacterSO"/> の <see cref="FacialCharacterSO.GazeConfigs"/> 各エントリについて、
        /// 4 系統 (LookLeft / LookRight / LookUp / LookDown) の AnimationClip を <see cref="GazeClipBlendShapeSampler"/> で
        /// 時刻 0 サンプリングし、結果を <see cref="GazeBindingConfig"/> 内の sample 配列に書き戻す。
        /// AnimationClip が null の系統は対応 sample 配列を空に設定する。
        /// 引数が <see cref="FacialCharacterSO"/> でない場合は no-op。
        /// </summary>
        public static void SampleGazeClipsIntoConfigs(FacialCharacterProfileSO so)
        {
            if (so is not FacialCharacterSO inputSO) return;
            var configs = inputSO.GazeConfigs;
            if (configs == null) return;

            for (int i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null) continue;

                cfg.lookLeftSamples = ConvertSamples(GazeClipBlendShapeSampler.Sample(cfg.lookLeftClip));
                cfg.lookRightSamples = ConvertSamples(GazeClipBlendShapeSampler.Sample(cfg.lookRightClip));
                cfg.lookUpSamples = ConvertSamples(GazeClipBlendShapeSampler.Sample(cfg.lookUpClip));
                cfg.lookDownSamples = ConvertSamples(GazeClipBlendShapeSampler.Sample(cfg.lookDownClip));
            }
        }

        private static List<GazeBlendShapeSampleEntry> ConvertSamples(
            IReadOnlyList<GazeClipBlendShapeSampler.BlendShapeSample> source)
        {
            if (source == null || source.Count == 0)
            {
                return new List<GazeBlendShapeSampleEntry>();
            }
            var result = new List<GazeBlendShapeSampleEntry>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                var s = source[i];
                result.Add(new GazeBlendShapeSampleEntry
                {
                    blendShapeName = s.BlendShapeName ?? string.Empty,
                    weight = s.Weight,
                });
            }
            return result;
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
        // 内部ヘルパー: パイプライン共通
        // ============================================================

        private static IExpressionAnimationClipSampler ResolveSampler()
        {
            return SamplerOverride ?? new AnimationClipExpressionSampler();
        }

        private static IProgressBarPresenter ResolveProgressBarPresenter()
        {
            return ProgressBarPresenterOverride ?? DefaultProgressBarPresenter.Instance;
        }

        private static IElapsedStopwatch ResolveStopwatch()
        {
            var provider = StopwatchProviderOverride ?? DefaultStopwatchProvider.Instance;
            return provider.Start();
        }

        private static FacialCharacterProfileSO LoadAssetAtPath(string path)
        {
            var loader = AssetLoaderOverride;
            if (loader != null)
            {
                return loader(path);
            }
            return AssetDatabase.LoadAssetAtPath<FacialCharacterProfileSO>(path);
        }

        private static int CountCandidates(string[] paths)
        {
            int count = 0;
            for (int i = 0; i < paths.Length; i++)
            {
                if (IsAssetCandidatePath(paths[i])) count++;
            }
            return count;
        }

        private static bool IsAssetCandidatePath(string path)
        {
            return !string.IsNullOrEmpty(path)
                && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
        }

        private static void MaybeShowProgressBar(
            IProgressBarPresenter progressBar,
            IElapsedStopwatch stopwatch,
            string assetName,
            int processed,
            int total)
        {
            if (stopwatch.ElapsedMilliseconds <= ProgressBarThresholdMilliseconds)
            {
                return;
            }
            float progress = total > 0 ? Mathf.Clamp01((float)processed / total) : 0f;
            progressBar.Show(
                "FacialControl AutoExporter",
                $"Sampling AnimationClips for '{assetName}'...",
                progress);
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

        // ============================================================
        // Test seam: 内部既定実装
        // ============================================================

        /// <summary>進捗バー表示の抽象。テストで mock 化可能（Req 9.5）。</summary>
        public interface IProgressBarPresenter
        {
            void Show(string title, string info, float progress);
            void Clear();
        }

        /// <summary>経過時間の抽象。テストで fake stopwatch を注入可能。</summary>
        public interface IElapsedStopwatch
        {
            long ElapsedMilliseconds { get; }
        }

        /// <summary><see cref="IElapsedStopwatch"/> を生成する factory。</summary>
        public interface IStopwatchProvider
        {
            IElapsedStopwatch Start();
        }

        private sealed class DefaultProgressBarPresenter : IProgressBarPresenter
        {
            public static readonly DefaultProgressBarPresenter Instance = new DefaultProgressBarPresenter();

            public void Show(string title, string info, float progress)
            {
                EditorUtility.DisplayProgressBar(title, info, progress);
            }

            public void Clear()
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private sealed class DefaultStopwatchProvider : IStopwatchProvider
        {
            public static readonly DefaultStopwatchProvider Instance = new DefaultStopwatchProvider();

            public IElapsedStopwatch Start()
            {
                return new SystemElapsedStopwatch(Stopwatch.StartNew());
            }

            private sealed class SystemElapsedStopwatch : IElapsedStopwatch
            {
                private readonly Stopwatch _stopwatch;
                public SystemElapsedStopwatch(Stopwatch stopwatch) { _stopwatch = stopwatch; }
                public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;
            }
        }
    }
}
