using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.InputSources
{
    /// <summary>
    /// 入力源 id から具体アダプタへ生成ディスパッチを行うファクトリ（Req 3.1, 3.3, 3.7, 5.7）。
    /// 予約 id ごとに options DTO 型をマップし、<see cref="JsonUtility.FromJson(string, System.Type)"/>
    /// 経由で生 JSON 文字列を typed DTO に逆シリアライズする (Critical 2)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 未登録 id に対する <see cref="TryCreate"/> / <see cref="TryDeserializeOptions"/> は
    /// <c>null</c> を返す契約。呼出側（<c>SystemTextJsonParser</c> 等）は null を受け取った場合に
    /// 警告ログを出して当該エントリを skip する (Req 3.3)。
    /// </para>
    /// <para>
    /// 予約 id と DTO 型 / creator の対応:
    /// <list type="bullet">
    ///   <item><c>osc</c> → <see cref="OscOptionsDto"/> → <see cref="OscInputSource"/></item>
    ///   <item><c>lipsync</c> → <see cref="LipSyncOptionsDto"/> → <see cref="LipSyncInputSource"/></item>
    ///   <item><c>controller-expr</c> → <see cref="ExpressionTriggerOptionsDto"/> → <see cref="ControllerExpressionInputSource"/></item>
    ///   <item><c>keyboard-expr</c> → <see cref="ExpressionTriggerOptionsDto"/> → <see cref="KeyboardExpressionInputSource"/></item>
    /// </list>
    /// サードパーティ <c>x-*</c> 拡張は別タスク (7.6) で <c>RegisterExtension</c> API を追加予定。
    /// </para>
    /// </remarks>
    public interface IInputSourceFactory
    {
        /// <summary>
        /// <paramref name="id"/> に対応するアダプタを生成する。
        /// </summary>
        /// <param name="id">入力源識別子。</param>
        /// <param name="options">
        /// <paramref name="id"/> に対応する typed DTO（<see cref="TryDeserializeOptions"/> の出力）。
        /// <c>null</c> の場合は対応 DTO のデフォルト値インスタンスが用いられる。
        /// </param>
        /// <param name="blendShapeCount">アダプタが書込む BlendShape 個数。</param>
        /// <param name="profile">Expression 検索などに用いる <see cref="FacialProfile"/>。</param>
        /// <returns>
        /// 生成に成功した場合は <see cref="IInputSource"/>、
        /// 未登録 id または必須依存が未注入の場合は <c>null</c>。
        /// </returns>
        IInputSource TryCreate(
            InputSourceId id,
            InputSourceOptionsDto options,
            int blendShapeCount,
            FacialProfile profile);

        /// <summary>
        /// <paramref name="id"/> に対応する options JSON サブ文字列を typed DTO に逆シリアライズする。
        /// </summary>
        /// <param name="id">入力源識別子。</param>
        /// <param name="optionsJson">
        /// JSON の <c>options</c> フィールド生文字列（<c>{ ... }</c> ブロックそのもの）。
        /// <c>null</c>・空白のみ・空オブジェクトのときは DTO のデフォルト値が返る。
        /// </param>
        /// <returns>
        /// 登録済 id なら <see cref="InputSourceOptionsDto"/> 派生インスタンス、
        /// 未登録 id なら <c>null</c>。
        /// </returns>
        InputSourceOptionsDto TryDeserializeOptions(InputSourceId id, string optionsJson);

        /// <summary>
        /// <paramref name="id"/> が本ファクトリに登録されているかを判定する。
        /// </summary>
        bool IsRegistered(InputSourceId id);
    }

    /// <summary>
    /// <see cref="IInputSourceFactory"/> の実装。予約 id に対するビルトイン登録を提供する。
    /// </summary>
    public sealed class InputSourceFactory : IInputSourceFactory
    {
        /// <summary>
        /// Expression トリガー型アダプタの既定スタック深度（D-14）。
        /// options の <see cref="ExpressionTriggerOptionsDto.maxStackDepth"/> が 0 のとき適用される。
        /// </summary>
        public const int DefaultMaxStackDepth = 8;

        private readonly OscDoubleBuffer _oscBuffer;
        private readonly ITimeProvider _timeProvider;
        private readonly ILipSyncProvider _lipSyncProvider;
        private readonly IReadOnlyList<string> _blendShapeNames;
        private readonly ExclusionMode _defaultExclusionMode;
        private readonly Dictionary<string, Entry> _entries;

        /// <summary>
        /// <see cref="InputSourceFactory"/> を構築する。
        /// </summary>
        /// <param name="oscBuffer">OSC 受信ダブルバッファ。<c>null</c> の場合 <c>osc</c> アダプタは生成できない。</param>
        /// <param name="timeProvider">現在時刻供給元。<c>null</c> の場合 <c>osc</c> アダプタは生成できない。</param>
        /// <param name="lipSyncProvider">リップシンク値供給元。<c>null</c> の場合 <c>lipsync</c> アダプタは生成できない。</param>
        /// <param name="blendShapeNames">
        /// Expression トリガー型アダプタが参照する BlendShape 名の列。
        /// <c>null</c> の場合は空配列として扱う。
        /// </param>
        /// <param name="defaultExclusionMode">
        /// Expression トリガー型アダプタに与える既定の排他モード（D-12）。
        /// </param>
        public InputSourceFactory(
            OscDoubleBuffer oscBuffer = null,
            ITimeProvider timeProvider = null,
            ILipSyncProvider lipSyncProvider = null,
            IReadOnlyList<string> blendShapeNames = null,
            ExclusionMode defaultExclusionMode = ExclusionMode.LastWins)
        {
            _oscBuffer = oscBuffer;
            _timeProvider = timeProvider;
            _lipSyncProvider = lipSyncProvider;
            _blendShapeNames = blendShapeNames ?? Array.Empty<string>();
            _defaultExclusionMode = defaultExclusionMode;

            _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

            Register(
                OscInputSource.ReservedId,
                typeof(OscOptionsDto),
                () => new OscOptionsDto(),
                (options, blendShapeCount, profile) => CreateOsc(options));

            Register(
                LipSyncInputSource.ReservedId,
                typeof(LipSyncOptionsDto),
                () => new LipSyncOptionsDto(),
                (options, blendShapeCount, profile) => CreateLipSync(blendShapeCount));

            Register(
                ControllerExpressionInputSource.ReservedId,
                typeof(ExpressionTriggerOptionsDto),
                () => new ExpressionTriggerOptionsDto(),
                (options, blendShapeCount, profile) =>
                    CreateController((ExpressionTriggerOptionsDto)options, blendShapeCount, profile));

            Register(
                KeyboardExpressionInputSource.ReservedId,
                typeof(ExpressionTriggerOptionsDto),
                () => new ExpressionTriggerOptionsDto(),
                (options, blendShapeCount, profile) =>
                    CreateKeyboard((ExpressionTriggerOptionsDto)options, blendShapeCount, profile));
        }

        /// <inheritdoc />
        public bool IsRegistered(InputSourceId id)
        {
            return !string.IsNullOrEmpty(id.Value) && _entries.ContainsKey(id.Value);
        }

        /// <inheritdoc />
        public InputSourceOptionsDto TryDeserializeOptions(InputSourceId id, string optionsJson)
        {
            if (string.IsNullOrEmpty(id.Value) || !_entries.TryGetValue(id.Value, out var entry))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(optionsJson))
            {
                return entry.DefaultFactory();
            }

            var deserialized = JsonUtility.FromJson(optionsJson, entry.OptionsType);
            if (deserialized == null)
            {
                return entry.DefaultFactory();
            }

            return (InputSourceOptionsDto)deserialized;
        }

        /// <inheritdoc />
        public IInputSource TryCreate(
            InputSourceId id,
            InputSourceOptionsDto options,
            int blendShapeCount,
            FacialProfile profile)
        {
            if (string.IsNullOrEmpty(id.Value) || !_entries.TryGetValue(id.Value, out var entry))
            {
                return null;
            }

            var effectiveOptions = options ?? entry.DefaultFactory();
            return entry.Creator(effectiveOptions, blendShapeCount, profile);
        }

        private void Register(
            string id,
            Type optionsType,
            Func<InputSourceOptionsDto> defaultFactory,
            Func<InputSourceOptionsDto, int, FacialProfile, IInputSource> creator)
        {
            _entries[id] = new Entry(optionsType, defaultFactory, creator);
        }

        private IInputSource CreateOsc(InputSourceOptionsDto options)
        {
            if (_oscBuffer == null || _timeProvider == null)
            {
                return null;
            }

            var oscOptions = (OscOptionsDto)options;
            return new OscInputSource(_oscBuffer, oscOptions.stalenessSeconds, _timeProvider);
        }

        private IInputSource CreateLipSync(int blendShapeCount)
        {
            if (_lipSyncProvider == null)
            {
                return null;
            }

            return new LipSyncInputSource(_lipSyncProvider, blendShapeCount);
        }

        private IInputSource CreateController(
            ExpressionTriggerOptionsDto options,
            int blendShapeCount,
            FacialProfile profile)
        {
            int depth = options.maxStackDepth > 0 ? options.maxStackDepth : DefaultMaxStackDepth;
            return new ControllerExpressionInputSource(
                blendShapeCount,
                depth,
                _defaultExclusionMode,
                _blendShapeNames,
                profile);
        }

        private IInputSource CreateKeyboard(
            ExpressionTriggerOptionsDto options,
            int blendShapeCount,
            FacialProfile profile)
        {
            int depth = options.maxStackDepth > 0 ? options.maxStackDepth : DefaultMaxStackDepth;
            return new KeyboardExpressionInputSource(
                blendShapeCount,
                depth,
                _defaultExclusionMode,
                _blendShapeNames,
                profile);
        }

        private readonly struct Entry
        {
            public readonly Type OptionsType;
            public readonly Func<InputSourceOptionsDto> DefaultFactory;
            public readonly Func<InputSourceOptionsDto, int, FacialProfile, IInputSource> Creator;

            public Entry(
                Type optionsType,
                Func<InputSourceOptionsDto> defaultFactory,
                Func<InputSourceOptionsDto, int, FacialProfile, IInputSource> creator)
            {
                OptionsType = optionsType;
                DefaultFactory = defaultFactory;
                Creator = creator;
            }
        }
    }
}
