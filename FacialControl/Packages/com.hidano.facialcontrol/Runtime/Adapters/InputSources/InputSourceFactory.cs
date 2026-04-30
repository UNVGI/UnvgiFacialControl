using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;
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
    /// コア標準のビルトイン登録は <c>lipsync</c> のみ（<see cref="LipSyncInputSource"/>）。
    /// <c>osc</c> / <c>controller-expr</c> / <c>keyboard-expr</c> は公式サブパッケージ
    /// (<c>com.hidano.facialcontrol.osc</c> / <c>com.hidano.facialcontrol.inputsystem</c>) が
    /// <see cref="RegisterReserved{TOptions}"/> 経由で追加登録する。
    /// サードパーティ <c>x-*</c> 拡張は <see cref="RegisterExtension{TOptions}"/>
    /// で typed DTO と creator を同時に登録する（Req 1.7, 3.7）。
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

        private readonly ILipSyncProvider _lipSyncProvider;
        private readonly Dictionary<string, Entry> _entries;

        /// <summary>
        /// <see cref="InputSourceFactory"/> を構築する。
        /// コア標準では <c>lipsync</c> のみビルトイン登録される。
        /// <c>osc</c> / <c>controller-expr</c> / <c>keyboard-expr</c> は対応サブパッケージ
        /// (<c>com.hidano.facialcontrol.osc</c> / <c>com.hidano.facialcontrol.inputsystem</c>) の
        /// <c>Register(...)</c> ヘルパー経由で <see cref="RegisterReserved{TOptions}"/> を呼ぶこと。
        /// </summary>
        /// <param name="lipSyncProvider">
        /// リップシンク値供給元。<c>null</c> の場合 <c>lipsync</c> アダプタの <see cref="TryCreate"/>
        /// は <c>null</c> を返す。
        /// </param>
        public InputSourceFactory(ILipSyncProvider lipSyncProvider = null)
        {
            _lipSyncProvider = lipSyncProvider;

            _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

            Register(
                LipSyncInputSource.ReservedId,
                typeof(LipSyncOptionsDto),
                () => new LipSyncOptionsDto(),
                (options, blendShapeCount, profile) => CreateLipSync(blendShapeCount));
        
            Register(
                CoreControllerExpressionSource.ReservedId,
                typeof(ExpressionTriggerCoreFallbackOptionsDto),
                () => new ExpressionTriggerCoreFallbackOptionsDto(),
                (options, blendShapeCount, profile) =>
                {
                    var opts = options as ExpressionTriggerCoreFallbackOptionsDto ?? new ExpressionTriggerCoreFallbackOptionsDto();
                    int depth = opts.maxStackDepth > 0 ? opts.maxStackDepth : DefaultMaxStackDepth;
                    var mode = GetFirstLayerExclusionMode(profile);
                    return new CoreControllerExpressionSource(blendShapeCount, depth, mode, System.Array.Empty<string>(), profile);
                });

            Register(
                CoreKeyboardExpressionSource.ReservedId,
                typeof(ExpressionTriggerCoreFallbackOptionsDto),
                () => new ExpressionTriggerCoreFallbackOptionsDto(),
                (options, blendShapeCount, profile) =>
                {
                    var opts = options as ExpressionTriggerCoreFallbackOptionsDto ?? new ExpressionTriggerCoreFallbackOptionsDto();
                    int depth = opts.maxStackDepth > 0 ? opts.maxStackDepth : DefaultMaxStackDepth;
                    var mode = GetFirstLayerExclusionMode(profile);
                    return new CoreKeyboardExpressionSource(blendShapeCount, depth, mode, System.Array.Empty<string>(), profile);
                });
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

        /// <summary>
        /// 公式サブパッケージ（<c>com.hidano.facialcontrol.osc</c> /
        /// <c>com.hidano.facialcontrol.inputsystem</c> 等）から予約 id 含む任意 id でアダプタを登録する。
        /// 第三者拡張は <see cref="RegisterExtension{TOptions}"/>（<c>x-*</c> プレフィックス必須）を使うこと。
        /// </summary>
        /// <typeparam name="TOptions">
        /// アダプタが用いる options DTO 型。<see cref="InputSourceOptionsDto"/> 派生で、
        /// JsonUtility でデシリアライズ可能な public 無引数コンストラクタを持つ必要がある。
        /// </typeparam>
        /// <param name="id">登録対象の識別子。予約 id も含む任意 id。</param>
        /// <param name="creator">
        /// 型付き options・BlendShape 個数・<see cref="FacialProfile"/> から
        /// <see cref="IInputSource"/> を生成する関数。必須依存が未注入の場合は <c>null</c> 返却で skip される契約。
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="id"/> が未初期化（<c>default</c>）の場合。
        /// </exception>
        /// <exception cref="ArgumentNullException"><paramref name="creator"/> が <c>null</c> の場合。</exception>
        /// <remarks>
        /// 同 id の再登録は後勝ち（警告なし）。サブパッケージ側で予約 id を登録 / 上書きするための公式 API。
        /// </remarks>
        public void RegisterReserved<TOptions>(
            InputSourceId id,
            Func<TOptions, int, FacialProfile, IInputSource> creator)
            where TOptions : InputSourceOptionsDto, new()
        {
            if (string.IsNullOrEmpty(id.Value))
            {
                throw new ArgumentException(
                    "RegisterReserved requires a valid InputSourceId (Value must not be null or empty).",
                    nameof(id));
            }

            if (creator == null)
            {
                throw new ArgumentNullException(nameof(creator));
            }

            Register(
                id.Value,
                typeof(TOptions),
                () => new TOptions(),
                (options, blendShapeCount, profile) =>
                {
                    var typedOptions = options as TOptions ?? new TOptions();
                    return creator(typedOptions, blendShapeCount, profile);
                });
        }

        /// <summary>
        /// サードパーティ (<c>x-*</c> プレフィックス推奨) 拡張アダプタを登録する (Req 1.7, 3.7)。
        /// </summary>
        /// <typeparam name="TOptions">
        /// 拡張アダプタが用いる options DTO 型。<see cref="InputSourceOptionsDto"/> 派生で、
        /// JsonUtility でデシリアライズ可能な public 無引数コンストラクタを持つ必要がある。
        /// </typeparam>
        /// <param name="id">登録対象の識別子。予約 id は登録できず警告 + no-op となる。</param>
        /// <param name="creator">
        /// 型付き options・BlendShape 個数・<see cref="FacialProfile"/> から
        /// <see cref="IInputSource"/> を生成する関数。
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="id"/> が未初期化（<c>default</c>）で <see cref="InputSourceId.Value"/>
        /// が <c>null</c>／空の場合。
        /// </exception>
        /// <exception cref="ArgumentNullException"><paramref name="creator"/> が <c>null</c> の場合。</exception>
        /// <remarks>
        /// <para>
        /// 予約 id (<c>osc</c> / <c>lipsync</c> / <c>controller-expr</c> / <c>keyboard-expr</c> /
        /// <c>input</c>) の上書きは許容せず、警告ログを出して既存のビルトイン登録を維持する。
        /// </para>
        /// <para>
        /// 同一の拡張 id を再登録した場合は後勝ち（警告なし）。開発時の差替えを容易にするための選択。
        /// </para>
        /// </remarks>
        public void RegisterExtension<TOptions>(
            InputSourceId id,
            Func<TOptions, int, FacialProfile, IInputSource> creator)
            where TOptions : InputSourceOptionsDto, new()
        {
            if (string.IsNullOrEmpty(id.Value))
            {
                throw new ArgumentException(
                    "RegisterExtension requires a valid InputSourceId (Value must not be null or empty).",
                    nameof(id));
            }

            if (creator == null)
            {
                throw new ArgumentNullException(nameof(creator));
            }

            if (id.IsReserved)
            {
                Debug.LogWarning(
                    $"[InputSourceFactory] Reserved id '{id.Value}' cannot be overridden by " +
                    "RegisterExtension. Use a 'x-' prefix for third-party extensions (Req 1.7).");
                return;
            }

            Register(
                id.Value,
                typeof(TOptions),
                () => new TOptions(),
                (options, blendShapeCount, profile) =>
                {
                    var typedOptions = options as TOptions ?? new TOptions();
                    return creator(typedOptions, blendShapeCount, profile);
                });
        }

        private void Register(
            string id,
            Type optionsType,
            Func<InputSourceOptionsDto> defaultFactory,
            Func<InputSourceOptionsDto, int, FacialProfile, IInputSource> creator)
        {
            _entries[id] = new Entry(optionsType, defaultFactory, creator);
        }

        private IInputSource CreateLipSync(int blendShapeCount)
        {
            if (_lipSyncProvider == null)
            {
                return null;
            }

            return new LipSyncInputSource(_lipSyncProvider, blendShapeCount);
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

        private static ExclusionMode GetFirstLayerExclusionMode(FacialProfile profile)
        {
            var layers = profile.Layers;
            if (layers.Length > 0)
            {
                return layers.Span[0].ExclusionMode;
            }
            return ExclusionMode.LastWins;
        }

        /// <summary>
        /// コアパッケージ内の controller-expr コアフォールバック実装。
        /// InputSystem サブパッケージが RegisterReserved で上書きしない限り、
        /// controller-expr 宣言時にこのシンプルな実装が使用される。
        /// </summary>
        private sealed class CoreControllerExpressionSource : ExpressionTriggerInputSourceBase
        {
            public const string ReservedId = "controller-expr";

            public CoreControllerExpressionSource(
                int blendShapeCount,
                int maxStackDepth,
                ExclusionMode exclusionMode,
                System.Collections.Generic.IReadOnlyList<string> blendShapeNames,
                FacialProfile profile)
                : base(InputSourceId.Parse(ReservedId), blendShapeCount, maxStackDepth, exclusionMode, blendShapeNames, profile)
            {
            }
        }

        /// <summary>
        /// コアパッケージ内の keyboard-expr コアフォールバック実装。
        /// InputSystem サブパッケージが RegisterReserved で上書きしない限り、
        /// keyboard-expr 宣言時にこのシンプルな実装が使用される。
        /// </summary>
        private sealed class CoreKeyboardExpressionSource : ExpressionTriggerInputSourceBase
        {
            public const string ReservedId = "keyboard-expr";

            public CoreKeyboardExpressionSource(
                int blendShapeCount,
                int maxStackDepth,
                ExclusionMode exclusionMode,
                System.Collections.Generic.IReadOnlyList<string> blendShapeNames,
                FacialProfile profile)
                : base(InputSourceId.Parse(ReservedId), blendShapeCount, maxStackDepth, exclusionMode, blendShapeNames, profile)
            {
            }
        }

        /// <summary>
        /// controller-expr / keyboard-expr のコアフォールバック登録用 DTO。
        /// maxStackDepth が 0 以下の場合は InputSourceFactory.DefaultMaxStackDepth が使用される。
        /// </summary>
        [System.Serializable]
        private sealed class ExpressionTriggerCoreFallbackOptionsDto : InputSourceOptionsDto
        {
            public int maxStackDepth = 0;
        }
    }
}
