using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.InputSystem
{
    /// <summary>
    /// FacialControl コアの <see cref="InputSourceFactory"/> に
    /// Expression トリガー型アダプタ生成器を登録するヘルパー。
    /// 通常は <c>InputFacialControllerExtension</c> MonoBehaviour 経由で自動的に呼ばれるが、
    /// テストや手動配線でも直接利用できる。
    /// </summary>
    /// <remarks>
    /// tasks.md 4.6 で <c>KeyboardExpressionInputSource</c> / <c>ControllerExpressionInputSource</c> は
    /// <see cref="ExpressionTriggerInputSource"/> 1 種に統合された。後方互換のため、JSON Layer
    /// <c>inputSources[]</c> 宣言で従来通り両予約 id (<c>controller-expr</c> / <c>keyboard-expr</c>)
    /// を併用できるよう、両 id を同じ <see cref="ExpressionTriggerInputSource"/> に解決する。
    /// device 別の dispatch は <see cref="ExpressionInputSourceAdapter"/> +
    /// <see cref="Hidano.FacialControl.Adapters.Input.InputDeviceCategorizer"/> が行う。
    /// </remarks>
    public static class InputRegistration
    {
        /// <summary>
        /// Expression トリガー型アダプタの既定スタック深度（D-14）。
        /// options の <see cref="ExpressionTriggerOptionsDto.maxStackDepth"/> が 0 のとき適用される。
        /// </summary>
        public const int DefaultMaxStackDepth = 8;

        /// <summary>
        /// <paramref name="factory"/> に <see cref="ExpressionTriggerInputSource"/> 生成器を
        /// 予約 id <c>controller-expr</c> / <c>keyboard-expr</c> の双方で登録する。
        /// </summary>
        /// <param name="factory">登録対象のファクトリ。</param>
        /// <param name="blendShapeNames">BlendShape 名の列（名前→インデックス解決用）。</param>
        /// <param name="defaultExclusionMode">Expression トリガー型アダプタに与える既定の排他モード。</param>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> が null。</exception>
        public static void Register(
            InputSourceFactory factory,
            IReadOnlyList<string> blendShapeNames,
            ExclusionMode defaultExclusionMode = ExclusionMode.LastWins)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            var names = blendShapeNames ?? Array.Empty<string>();

            RegisterReservedId(
                factory,
                InputSourceId.Parse(ExpressionTriggerInputSource.ControllerReservedId),
                names,
                defaultExclusionMode);
            RegisterReservedId(
                factory,
                InputSourceId.Parse(ExpressionTriggerInputSource.KeyboardReservedId),
                names,
                defaultExclusionMode);
        }

        private static void RegisterReservedId(
            InputSourceFactory factory,
            InputSourceId reservedId,
            IReadOnlyList<string> names,
            ExclusionMode defaultExclusionMode)
        {
            factory.RegisterReserved<ExpressionTriggerOptionsDto>(
                reservedId,
                (options, blendShapeCount, profile) =>
                {
                    int depth = options.maxStackDepth > 0 ? options.maxStackDepth : DefaultMaxStackDepth;
                    return new ExpressionTriggerInputSource(
                        reservedId, blendShapeCount, depth, defaultExclusionMode, names, profile);
                });
        }
    }
}
