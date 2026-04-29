using System.Collections.Generic;
using UnityEngine;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.InputSystem
{
    /// <summary>
    /// FacialController と同じ GameObject に配置することで、予約 ID <c>analog-blendshape</c> 用の
    /// <see cref="AnalogBlendShapeInputSource"/> 生成器を <see cref="InputSourceFactory"/> に登録する拡張
    /// （Req 3.8、tasks.md 6.1）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Sources"/> および <see cref="Bindings"/> は通常 <c>FacialAnalogInputBinder</c> から
    /// <see cref="Configure"/> 経由で注入される。注入前に <see cref="ConfigureFactory"/> が呼ばれた場合は
    /// 空 sources / 空 bindings の <see cref="AnalogBlendShapeInputSource"/> を生成する（no-op として振る舞う）。
    /// </para>
    /// <para>
    /// プロファイルの <c>layerInputSources</c> 宣言で <c>analog-blendshape</c> が含まれていない場合は
    /// 本登録は呼出されない（FacialController が宣言ベースで factory を呼ぶため）。
    /// </para>
    /// </remarks>
    [AddComponentMenu("FacialControl/Analog BlendShape Registration")]
    public sealed class AnalogBlendShapeRegistration : MonoBehaviour, IFacialControllerExtension
    {
        private IReadOnlyDictionary<string, IAnalogInputSource> _sources;
        private IReadOnlyList<AnalogBindingEntry> _bindings;

        /// <summary>
        /// 直近に注入された sources / bindings の組を取り込み、次回 <see cref="ConfigureFactory"/>
        /// で <see cref="AnalogBlendShapeInputSource"/> を構築する材料として用いる。
        /// </summary>
        /// <param name="sources">sourceId → <see cref="IAnalogInputSource"/> の辞書（null は空辞書扱い）。</param>
        /// <param name="bindings">バインディング集合（BlendShape 以外も含めて渡してよい、null は空集合扱い）。</param>
        public void Configure(
            IReadOnlyDictionary<string, IAnalogInputSource> sources,
            IReadOnlyList<AnalogBindingEntry> bindings)
        {
            _sources = sources;
            _bindings = bindings;
        }

        /// <summary>
        /// 直近の <see cref="Configure"/> による注入を破棄する。
        /// </summary>
        public void Clear()
        {
            _sources = null;
            _bindings = null;
        }

        /// <inheritdoc />
        public void ConfigureFactory(
            InputSourceFactory factory,
            FacialProfile profile,
            IReadOnlyList<string> blendShapeNames)
        {
            if (factory == null)
            {
                return;
            }

            // 注入前 / 解除済みの呼出は空辞書 + 空 bindings として扱う。
            var sources = _sources
                ?? (IReadOnlyDictionary<string, IAnalogInputSource>)EmptySources;
            var bindings = _bindings ?? (IReadOnlyList<AnalogBindingEntry>)System.Array.Empty<AnalogBindingEntry>();
            var names = blendShapeNames ?? (IReadOnlyList<string>)System.Array.Empty<string>();

            factory.RegisterReserved<AnalogBlendShapeOptionsDto>(
                InputSourceId.Parse(AnalogBlendShapeInputSource.ReservedId),
                (options, blendShapeCount, prof) =>
                {
                    return new AnalogBlendShapeInputSource(
                        InputSourceId.Parse(AnalogBlendShapeInputSource.ReservedId),
                        blendShapeCount,
                        names,
                        sources,
                        bindings);
                });
        }

        private static readonly Dictionary<string, IAnalogInputSource> EmptySources =
            new Dictionary<string, IAnalogInputSource>(System.StringComparer.Ordinal);
    }
}
