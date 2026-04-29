using System;
using System.IO;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.ScriptableObject
{
    /// <summary>
    /// アナログ入力バインディングプロファイルを永続化する ScriptableObject (Req 6.1, 6.2, 6.4, 6.6, 6.7, 9.3)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 離散トリガー側 <see cref="InputBindingProfileSO"/> とは独立した別アセットとして並走する。
    /// 内部に JSON 文字列を保持し、ランタイムから <see cref="ToDomain"/> 経由でパースする
    /// （JSON ファースト方針、Req 6.4）。
    /// </para>
    /// <para>
    /// Editor の Inspector からは <see cref="ImportJson"/> / <see cref="ExportJson"/> を介して
    /// 外部 JSON ファイルとの同期を行う。
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "NewAnalogInputBindingProfile",
        menuName = "FacialControl/Analog Input Binding Profile",
        order = 2)]
    public sealed class AnalogInputBindingProfileSO : UnityEngine.ScriptableObject
    {
        [Tooltip("バインディング定義の JSON テキスト本体。")]
        [SerializeField, TextArea(8, 50)]
        private string _jsonText;

        [Tooltip("StreamingAssets 配下の任意 JSON パス（参照用、現状 ToDomain では未使用）。")]
        [SerializeField]
        private string _streamingAssetPath;

        /// <summary>JSON テキスト本体。getter/setter ともに公開。</summary>
        public string JsonText
        {
            get => _jsonText;
            set => _jsonText = value;
        }

        /// <summary>
        /// 保持している JSON 文字列を <see cref="AnalogInputBindingProfile"/> に変換する
        /// （ランタイム呼出可、Req 6.4）。
        /// </summary>
        public AnalogInputBindingProfile ToDomain()
        {
            return AnalogInputBindingJsonLoader.Load(_jsonText);
        }

        /// <summary>
        /// 指定パスから JSON ファイルを読み込み、内部 <c>_jsonText</c> を上書きする。
        /// </summary>
        /// <param name="path">読み込み元 JSON ファイルパス。</param>
        public void ImportJson(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning("AnalogInputBindingProfileSO.ImportJson: path が空のためスキップします。");
                return;
            }

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    $"AnalogInputBindingProfileSO.ImportJson: ファイルが存在しません: {path}");
                return;
            }

            try
            {
                _jsonText = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"AnalogInputBindingProfileSO.ImportJson: 読み込みに失敗しました ({path}): {ex.Message}");
            }
        }

        /// <summary>
        /// 内部 <c>_jsonText</c> を指定パスへ書き出す。親ディレクトリが無い場合は作成する。
        /// </summary>
        /// <param name="path">書き出し先 JSON ファイルパス。</param>
        public void ExportJson(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning("AnalogInputBindingProfileSO.ExportJson: path が空のためスキップします。");
                return;
            }

            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(path, _jsonText ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"AnalogInputBindingProfileSO.ExportJson: 書き出しに失敗しました ({path}): {ex.Message}");
            }
        }
    }
}
