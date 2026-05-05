using System;
using Hidano.FacialControl.Domain.Adapters;
using UnityEngine;
using VContainer.Unity;

namespace Hidano.FacialControl.Adapters.DependencyInjection
{
    /// <summary>
    /// <see cref="AdapterBindingBase"/> を VContainer の Plain C# Entry Point として動かすラッパー。
    /// 各 lifecycle interface を実装し、binding の virtual method に委譲する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 例外 isolation: <c>OnStart</c> / <c>OnTick</c> / <c>OnLateTick</c> / <c>OnFixedTick</c> /
    /// <c>Dispose</c> のいずれかで例外が発生した場合、catch + <c>Debug.LogError</c> + <c>_skipped = true</c>
    /// に遷移する。以降のフレームでは Tick 系を no-op にし、他 binding と core パイプラインの継続を保証する
    /// （Req 13.4-13.5、D-15）。
    /// </para>
    /// <para>
    /// <c>Dispose</c> は <c>_skipped</c> の値に関わらず必ず呼ばれる（VContainer 規約）。Dispose 自体の
    /// 例外も catch + LogError し、後続 host の Dispose 継続を妨げない。
    /// </para>
    /// <para>
    /// DD-3: <c>if (_skipped) return;</c> と通常 dispatch は 0-alloc fast path（Req 9.1）。
    /// 例外発生フレーム単発の <c>Debug.LogError</c> 文字列補間アロケは &lt; 1 KB の許容範囲。
    /// </para>
    /// </remarks>
    public sealed class AdapterBindingHost
        : IStartable, ITickable, ILateTickable, IFixedTickable, IDisposable
    {
        private readonly AdapterBindingBase _binding;
        private readonly AdapterBuildContext _buildContext;
        private bool _skipped;

        /// <summary>
        /// 1 host = 1 binding の対応関係を強制する。
        /// </summary>
        /// <param name="binding">委譲対象の binding 実体（null 不可）。</param>
        /// <param name="buildContext"><see cref="AdapterBindingBase.OnStart"/> に渡す中立コンテキスト。</param>
        /// <exception cref="ArgumentNullException"><paramref name="binding"/> が null の場合。</exception>
        public AdapterBindingHost(AdapterBindingBase binding, AdapterBuildContext buildContext)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            _binding = binding;
            _buildContext = buildContext;
            _skipped = false;
        }

        void IStartable.Start()
        {
            if (_skipped) return;
            try
            {
                _binding.OnStart(in _buildContext);
            }
            catch (Exception ex)
            {
                _skipped = true;
                Debug.LogError($"[FacialControl] AdapterBindingHost '{_binding.GetType().FullName}' failed in OnStart: {ex}");
            }
        }

        void ITickable.Tick()
        {
            if (_skipped) return;
            try
            {
                _binding.OnTick(Time.deltaTime);
            }
            catch (Exception ex)
            {
                _skipped = true;
                Debug.LogError($"[FacialControl] AdapterBindingHost '{_binding.GetType().FullName}' failed in OnTick: {ex}");
            }
        }

        void ILateTickable.LateTick()
        {
            if (_skipped) return;
            try
            {
                _binding.OnLateTick(Time.deltaTime);
            }
            catch (Exception ex)
            {
                _skipped = true;
                Debug.LogError($"[FacialControl] AdapterBindingHost '{_binding.GetType().FullName}' failed in OnLateTick: {ex}");
            }
        }

        void IFixedTickable.FixedTick()
        {
            if (_skipped) return;
            try
            {
                _binding.OnFixedTick(Time.fixedDeltaTime);
            }
            catch (Exception ex)
            {
                _skipped = true;
                Debug.LogError($"[FacialControl] AdapterBindingHost '{_binding.GetType().FullName}' failed in OnFixedTick: {ex}");
            }
        }

        void IDisposable.Dispose()
        {
            try
            {
                _binding.Dispose();
            }
            catch (Exception ex)
            {
                _skipped = true;
                Debug.LogError($"[FacialControl] AdapterBindingHost '{_binding.GetType().FullName}' failed in Dispose: {ex}");
            }
        }
    }
}
