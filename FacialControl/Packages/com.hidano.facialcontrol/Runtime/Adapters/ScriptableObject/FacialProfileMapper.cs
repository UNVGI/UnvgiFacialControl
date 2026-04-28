using System;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.ScriptableObject
{
    /// <summary>
    /// FacialProfile ⟷ FacialProfileSO の変換を担当するマッパー。
    /// SO の JSON ファイルパスを経由してプロファイルの読み込み・保存を行い、
    /// SO の表示用フィールド（スキーマバージョン、レイヤー数、Expression 数）を更新する。
    /// </summary>
    public sealed class FacialProfileMapper
    {
        private readonly IProfileRepository _repository;

        /// <summary>
        /// FacialProfileMapper を生成する。
        /// </summary>
        /// <param name="repository">プロファイルの永続化リポジトリ</param>
        public FacialProfileMapper(IProfileRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// FacialProfileSO の JSON ファイルパスからプロファイルを読み込む。
        /// SO → JSON パス取得 → リポジトリ経由でパース のフロー。
        /// </summary>
        /// <param name="so">読み込み元の ScriptableObject</param>
        /// <returns>パースされた FacialProfile</returns>
        public FacialProfile ToProfile(FacialProfileSO so)
        {
            if (so == null)
                throw new ArgumentNullException(nameof(so));
            ValidateJsonFilePath(so.JsonFilePath);

            return _repository.LoadProfile(so.JsonFilePath);
        }

        /// <summary>
        /// FacialProfile の情報で FacialProfileSO の表示用フィールドを更新する。
        /// JsonFilePath は変更しない。
        /// </summary>
        /// <param name="so">更新対象の ScriptableObject</param>
        /// <param name="profile">更新元のプロファイル</param>
        public void UpdateSO(FacialProfileSO so, FacialProfile profile)
        {
            if (so == null)
                throw new ArgumentNullException(nameof(so));

            so.SchemaVersion = profile.SchemaVersion;
            so.LayerCount = profile.Layers.Length;
            so.ExpressionCount = profile.Expressions.Length;
            so.RendererPaths = profile.RendererPaths.ToArray();
            so.BonePoses = ToSerializableBonePoses(profile.BonePoses);
        }

        /// <summary>
        /// Domain BonePose 配列を Serializable 形式に変換する。
        /// 空配列の場合は空配列を返す（null は返さない、Req 10.1）。
        /// </summary>
        /// <param name="domain">Domain BonePose 配列</param>
        /// <returns>Serializable BonePose 配列（null 不返却）</returns>
        public static BonePoseSerializable[] ToSerializableBonePoses(ReadOnlyMemory<BonePose> domain)
        {
            if (domain.IsEmpty)
                return Array.Empty<BonePoseSerializable>();

            var span = domain.Span;
            var result = new BonePoseSerializable[span.Length];
            for (int i = 0; i < span.Length; i++)
            {
                var pose = span[i];
                var entriesSpan = pose.Entries.Span;
                var serialEntries = new BonePoseEntrySerializable[entriesSpan.Length];
                for (int j = 0; j < entriesSpan.Length; j++)
                {
                    var entry = entriesSpan[j];
                    serialEntries[j] = new BonePoseEntrySerializable
                    {
                        boneName = entry.BoneName,
                        eulerXYZ = new Vector3(entry.EulerX, entry.EulerY, entry.EulerZ),
                    };
                }

                result[i] = new BonePoseSerializable
                {
                    id = pose.Id,
                    entries = serialEntries,
                };
            }

            return result;
        }

        /// <summary>
        /// Serializable BonePose 配列を Domain BonePose 配列に変換する。
        /// null / 空配列の場合は空配列を返す（Req 10.1）。
        /// </summary>
        /// <param name="serializable">Serializable BonePose 配列（null 許容）</param>
        /// <returns>Domain BonePose 配列（null 不返却）</returns>
        public static BonePose[] ToDomainBonePoses(BonePoseSerializable[] serializable)
        {
            if (serializable == null || serializable.Length == 0)
                return Array.Empty<BonePose>();

            var result = new BonePose[serializable.Length];
            for (int i = 0; i < serializable.Length; i++)
            {
                var serial = serializable[i];
                if (serial == null)
                {
                    result[i] = new BonePose(string.Empty, Array.Empty<BonePoseEntry>());
                    continue;
                }

                BonePoseEntry[] domainEntries;
                if (serial.entries == null || serial.entries.Length == 0)
                {
                    domainEntries = Array.Empty<BonePoseEntry>();
                }
                else
                {
                    domainEntries = new BonePoseEntry[serial.entries.Length];
                    for (int j = 0; j < serial.entries.Length; j++)
                    {
                        var serialEntry = serial.entries[j];
                        if (serialEntry == null)
                        {
                            domainEntries[j] = new BonePoseEntry(string.Empty, 0f, 0f, 0f);
                        }
                        else
                        {
                            domainEntries[j] = new BonePoseEntry(
                                serialEntry.boneName,
                                serialEntry.eulerXYZ.x,
                                serialEntry.eulerXYZ.y,
                                serialEntry.eulerXYZ.z);
                        }
                    }
                }

                result[i] = new BonePose(serial.id, domainEntries);
            }

            return result;
        }

        /// <summary>
        /// FacialProfileSO の JSON ファイルパスからプロファイルを読み込み、
        /// SO の表示用フィールドも同時に更新する。
        /// </summary>
        /// <param name="so">対象の ScriptableObject</param>
        /// <returns>読み込まれた FacialProfile</returns>
        public FacialProfile LoadAndUpdateSO(FacialProfileSO so)
        {
            var profile = ToProfile(so);
            UpdateSO(so, profile);
            return profile;
        }

        /// <summary>
        /// FacialProfile を FacialProfileSO の JSON ファイルパスに保存し、
        /// SO の表示用フィールドも更新する。
        /// </summary>
        /// <param name="so">保存先パスを持つ ScriptableObject</param>
        /// <param name="profile">保存するプロファイル</param>
        public void SaveFromSO(FacialProfileSO so, FacialProfile profile)
        {
            if (so == null)
                throw new ArgumentNullException(nameof(so));
            ValidateJsonFilePath(so.JsonFilePath);

            _repository.SaveProfile(so.JsonFilePath, profile);
            UpdateSO(so, profile);
        }

        private static void ValidateJsonFilePath(string jsonFilePath)
        {
            if (string.IsNullOrWhiteSpace(jsonFilePath))
                throw new ArgumentException(
                    "FacialProfileSO の JsonFilePath が設定されていません。",
                    nameof(jsonFilePath));
        }
    }
}
