using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;
using UnityEngine;

namespace Hidano.FacialControl.InputSystem.Adapters.Bone
{
    /// <summary>
    /// <see cref="GazeExpressionConfig"/> を毎フレーム評価し、左右目ボーンに直接 localRotation を書込む
    /// 目線ボーン専用 provider。アナログ入力 (Vector2) を yaw / pitch 角度に変換し、
    /// 設定された外側/内側/上下の角度制限と各ボーンの参照モデル時取得 local 軸を用いて
    /// <c>Quaternion.AngleAxis</c> 合成で姿勢を計算する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// AnalogBonePoseProvider と異なり <see cref="IBonePoseProvider"/> 経由で snapshot を流す
    /// のではなく、bone の <see cref="Transform.localRotation"/> を直接書換える。これは目線回転が
    /// Euler 加算では正しく表現できず、各ボーン固有の rest pose と parent frame 軸を用いた
    /// quaternion 合成が必要なためである。
    /// </para>
    /// <para>
    /// 適用順は <c>localRotation = AngleAxis(yaw, yawAxisLocal) * AngleAxis(pitch, pitchAxisLocal) * Euler(rest)</c>。
    /// yaw を最も外側 (最後に適用) にすることで、視線左右が常に「水平」に動くように見える。
    /// </para>
    /// <para>
    /// 左右非対称制限: input.x が外側方向のとき outerYawAngle、内側方向のとき innerYawAngle で線形駆動する。
    /// 「外側」はキャラの鼻から見て当該の眼が遠ざかる側、すなわち左目では input.x &lt; 0、右目では input.x &gt; 0 の方向。
    /// </para>
    /// <para>
    /// <see cref="Dispose"/> 時には書込み開始前のオリジナル localRotation に各ボーンを復元する。
    /// </para>
    /// </remarks>
    public sealed class GazeBonePoseProvider : IDisposable
    {
        private readonly IReadOnlyDictionary<string, IAnalogInputSource> _sources;
        private readonly BoneTransformResolver _resolver;
        private readonly EyeBinding[] _bindings;
        private bool _disposed;

        /// <summary>
        /// <see cref="GazeBonePoseProvider"/> を構築する。
        /// </summary>
        /// <param name="resolver">ボーン名から Transform を解決するリゾルバー (FacialController と同じものを共有)。</param>
        /// <param name="sources">sourceId → <see cref="IAnalogInputSource"/> の辞書。</param>
        /// <param name="gazeConfigs">対象 GazeConfig のリスト。expressionId 毎に 1 件。</param>
        public GazeBonePoseProvider(
            BoneTransformResolver resolver,
            IReadOnlyDictionary<string, IAnalogInputSource> sources,
            IReadOnlyList<GazeExpressionConfig> gazeConfigs)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _sources = sources ?? throw new ArgumentNullException(nameof(sources));
            if (gazeConfigs == null) throw new ArgumentNullException(nameof(gazeConfigs));

            var list = new List<EyeBinding>(gazeConfigs.Count * 2);
            for (int i = 0; i < gazeConfigs.Count; i++)
            {
                var cfg = gazeConfigs[i];
                if (cfg == null) continue;
                if (cfg.inputAction == null || cfg.inputAction.action == null) continue;

                string sourceId = cfg.inputAction.action.name;
                if (string.IsNullOrWhiteSpace(sourceId)) continue;

                if (!_sources.TryGetValue(sourceId, out var source) || source == null)
                {
                    Debug.LogWarning(
                        $"[GazeBonePoseProvider] source '{sourceId}' が解決できないため、"
                        + $"GazeConfig (expressionId='{cfg.expressionId}') の目線ボーン制御をスキップします。");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(cfg.leftEyeBonePath))
                {
                    list.Add(new EyeBinding(
                        source,
                        cfg.leftEyeBonePath,
                        Quaternion.Euler(cfg.leftEyeInitialRotation),
                        SafeNormalize(cfg.leftEyeYawAxisLocal, Vector3.up),
                        SafeNormalize(cfg.leftEyePitchAxisLocal, Vector3.right),
                        isLeftEye: true,
                        cfg.outerYawAngle,
                        cfg.innerYawAngle,
                        cfg.lookUpAngle,
                        cfg.lookDownAngle));
                }
                if (!string.IsNullOrWhiteSpace(cfg.rightEyeBonePath))
                {
                    list.Add(new EyeBinding(
                        source,
                        cfg.rightEyeBonePath,
                        Quaternion.Euler(cfg.rightEyeInitialRotation),
                        SafeNormalize(cfg.rightEyeYawAxisLocal, Vector3.up),
                        SafeNormalize(cfg.rightEyePitchAxisLocal, Vector3.right),
                        isLeftEye: false,
                        cfg.outerYawAngle,
                        cfg.innerYawAngle,
                        cfg.lookUpAngle,
                        cfg.lookDownAngle));
                }
            }

            _bindings = list.Count == 0 ? Array.Empty<EyeBinding>() : list.ToArray();
        }

        /// <summary>
        /// per-frame に呼出され、各 GazeConfig の入力を読んで両目の <see cref="Transform.localRotation"/> を計算/書込みする。
        /// </summary>
        public void Apply()
        {
            if (_disposed)
            {
                return;
            }

            for (int i = 0; i < _bindings.Length; i++)
            {
                ref var b = ref _bindings[i];
                var target = ResolveTarget(ref b);
                if (target == null)
                {
                    continue;
                }

                if (!b.HasInitialSnapshot)
                {
                    b.InitialLocalRotation = target.localRotation;
                    b.HasInitialSnapshot = true;
                }

                if (!TryReadInputXY(b.Source, out float ix, out float iy))
                {
                    target.localRotation = b.RestRotation;
                    continue;
                }

                ix = Mathf.Clamp(ix, -1f, 1f);
                iy = Mathf.Clamp(iy, -1f, 1f);

                float yawDeg;
                if (b.IsLeftEye)
                {
                    // 左目: input.x > 0 (右へ視線) は内側方向、input.x < 0 (左へ視線) は外側方向。
                    yawDeg = ix >= 0f
                        ? ix * b.InnerYawAngle
                        : ix * b.OuterYawAngle;
                }
                else
                {
                    // 右目: input.x > 0 (右へ視線) は外側方向、input.x < 0 (左へ視線) は内側方向。
                    yawDeg = ix >= 0f
                        ? ix * b.OuterYawAngle
                        : ix * b.InnerYawAngle;
                }

                float pitchDeg = iy >= 0f
                    ? iy * b.LookUpAngle
                    : iy * b.LookDownAngle;

                var yawRot = Quaternion.AngleAxis(yawDeg, b.YawAxisLocal);
                var pitchRot = Quaternion.AngleAxis(pitchDeg, b.PitchAxisLocal);

                target.localRotation = yawRot * pitchRot * b.RestRotation;
            }
        }

        /// <summary>
        /// 書込中だった bone の <see cref="Transform.localRotation"/> を最初の書込み直前の値に戻す。
        /// </summary>
        public void RestoreInitialRotations()
        {
            for (int i = 0; i < _bindings.Length; i++)
            {
                ref var b = ref _bindings[i];
                if (!b.HasInitialSnapshot)
                {
                    continue;
                }
                var target = ResolveTarget(ref b);
                if (target == null)
                {
                    continue;
                }
                target.localRotation = b.InitialLocalRotation;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            RestoreInitialRotations();
            _disposed = true;
        }

        private Transform ResolveTarget(ref EyeBinding b)
        {
            if (b.CachedTarget != null)
            {
                return b.CachedTarget;
            }
            b.CachedTarget = _resolver.Resolve(b.BonePath);
            return b.CachedTarget;
        }

        private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
        {
            if (v.sqrMagnitude < 1e-8f)
            {
                return fallback;
            }
            return v.normalized;
        }

        private static bool TryReadInputXY(IAnalogInputSource source, out float x, out float y)
        {
            if (!source.IsValid)
            {
                x = 0f;
                y = 0f;
                return false;
            }

            if (source.AxisCount >= 2)
            {
                if (source.TryReadVector2(out x, out y))
                {
                    return true;
                }
                x = 0f;
                y = 0f;
                return false;
            }

            // scalar 入力は y のみ駆動する想定にしておく (yaw のみのケースは現状想定外)。
            if (source.TryReadScalar(out float v))
            {
                x = v;
                y = 0f;
                return true;
            }

            x = 0f;
            y = 0f;
            return false;
        }

        private struct EyeBinding
        {
            public readonly IAnalogInputSource Source;
            public readonly string BonePath;
            public readonly Quaternion RestRotation;
            public readonly Vector3 YawAxisLocal;
            public readonly Vector3 PitchAxisLocal;
            public readonly bool IsLeftEye;
            public readonly float OuterYawAngle;
            public readonly float InnerYawAngle;
            public readonly float LookUpAngle;
            public readonly float LookDownAngle;

            public Transform CachedTarget;
            public Quaternion InitialLocalRotation;
            public bool HasInitialSnapshot;

            public EyeBinding(
                IAnalogInputSource source,
                string bonePath,
                Quaternion restRotation,
                Vector3 yawAxisLocal,
                Vector3 pitchAxisLocal,
                bool isLeftEye,
                float outerYawAngle,
                float innerYawAngle,
                float lookUpAngle,
                float lookDownAngle)
            {
                Source = source;
                BonePath = bonePath;
                RestRotation = restRotation;
                YawAxisLocal = yawAxisLocal;
                PitchAxisLocal = pitchAxisLocal;
                IsLeftEye = isLeftEye;
                OuterYawAngle = Mathf.Max(0f, outerYawAngle);
                InnerYawAngle = Mathf.Max(0f, innerYawAngle);
                LookUpAngle = Mathf.Max(0f, lookUpAngle);
                LookDownAngle = Mathf.Max(0f, lookDownAngle);

                CachedTarget = null;
                InitialLocalRotation = Quaternion.identity;
                HasInitialSnapshot = false;
            }
        }
    }
}
