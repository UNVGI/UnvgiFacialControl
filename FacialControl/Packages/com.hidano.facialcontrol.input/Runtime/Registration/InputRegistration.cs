using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Input
{
    /// <summary>
    /// FacialControl コアの <see cref="InputSourceFactory"/> に予約 id <c>controller-expr</c>
    /// および <c>keyboard-expr</c> のアダプタ生成器を登録するヘルパー。
    /// 通常は <c>InputFacialControllerExtension</c> MonoBehaviour 経由で自動的に呼ばれるが、
    /// テストや手動配線でも直接利用できる。
    /// </summary>
    public static class InputRegistration
    {
        /// <summary>
        /// Expression トリガー型アダプタの既定スタック深度（D-14）。
        /// options の <see cref="ExpressionTriggerOptionsDto.maxStackDepth"/> が 0 のとき適用される。
        /// </summary>
        public const int DefaultMaxStackDepth = 8;

        /// <summary>
        /// <paramref name="factory"/> に <see cref="ControllerExpressionInputSource"/> および
        /// <see cref="KeyboardExpressionInputSource"/> 生成器を登録する。
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

            factory.RegisterReserved<ExpressionTriggerOptionsDto>(
                InputSourceId.Parse(ControllerExpressionInputSource.ReservedId),
                (options, blendShapeCount, profile) =>
                {
                    int depth = options.maxStackDepth > 0 ? options.maxStackDepth : DefaultMaxStackDepth;
                    return new ControllerExpressionInputSource(
                        blendShapeCount, depth, defaultExclusionMode, names, profile);
                });

            factory.RegisterReserved<ExpressionTriggerOptionsDto>(
                InputSourceId.Parse(KeyboardExpressionInputSource.ReservedId),
                (options, blendShapeCount, profile) =>
                {
                    int depth = options.maxStackDepth > 0 ? options.maxStackDepth : DefaultMaxStackDepth;
                    return new KeyboardExpressionInputSource(
                        blendShapeCount, depth, defaultExclusionMode, names, profile);
                });
        }
    }
}
