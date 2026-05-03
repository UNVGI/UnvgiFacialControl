using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hidano.FacialControl.InputSystem.Adapters.ScriptableObject
{
    /// <summary>
    /// Vector2 入力 (左スティック等) で両目を同時駆動するアナログ表情の設定。
    /// 対応する <see cref="Hidano.FacialControl.Adapters.ScriptableObject.Serializable.ExpressionSerializable"/>
    /// (kind=Analog) に対して expressionId で紐づく。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 入力 Vector2 の x 成分が左右方向、y 成分が上下方向の目線回転を駆動する。
    /// 多くのモデルでは目線はボーン操作だが、BlendShape ベースのモデルもあるため両方を任意に併用できる。
    /// </para>
    /// <para>
    /// BlendShape 経路は 4 系統 (LookLeft / LookRight / LookUp / LookDown) の AnimationClip で指定する。
    /// Vector2 の +X / -X / +Y / -Y がそれぞれ LookRight / LookLeft / LookUp / LookDown clip に対応し、
    /// clip 内の各 BlendShape curve の time=0 における値を keyframe weight として線形駆動する。
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class GazeExpressionConfig
    {
        [Tooltip("対応する Expression の ID。Expressions リスト内に id 一致するエントリが存在する必要がある。")]
        public string expressionId;

        [Tooltip("Vector2 入力 (joystick 等) を提供する InputAction の参照。expectedControlType=Vector2 を推奨。")]
        public InputActionReference inputAction;

        // ----------------- ボーン制御 (主) -----------------

        [Tooltip("左目ボーンの Transform 名 (参照モデル配下から名前一致で解決)。空なら無効。")]
        public string leftEyeBonePath;

        [Tooltip("左目ボーンの初期回転 (Euler 度)。アナログ入力 0 のときに保つ姿勢。アナログ入力はこの値に加算される。")]
        public Vector3 leftEyeInitialRotation;

        [Tooltip("右目ボーンの Transform 名。空なら無効。")]
        public string rightEyeBonePath;

        [Tooltip("右目ボーンの初期回転 (Euler 度)。")]
        public Vector3 rightEyeInitialRotation;

        // ----------------- BlendShape 制御 (オプション、4 系統 clip) -----------------

        [Tooltip("input.x < 0 (左方向) の状態を表現する AnimationClip。clip 内 BlendShape curve の time=0 における値を |input.x| で線形駆動する。空なら無効。")]
        public AnimationClip lookLeftClip;

        [Tooltip("input.x > 0 (右方向) の状態を表現する AnimationClip。空なら無効。")]
        public AnimationClip lookRightClip;

        [Tooltip("input.y > 0 (上方向) の状態を表現する AnimationClip。空なら無効。")]
        public AnimationClip lookUpClip;

        [Tooltip("input.y < 0 (下方向) の状態を表現する AnimationClip。空なら無効。")]
        public AnimationClip lookDownClip;

        // ----------------- Editor で焼き付けた BlendShape weight キャッシュ -----------------
        // AnimationClip の curve は runtime API で列挙できないため、Editor (AutoExporter) で
        // GazeClipBlendShapeSampler を介してサンプルし、本配列に永続化する。
        // runtime の BuildAnalogProfile は本キャッシュから AnalogBindingEntry を構築する。

        [HideInInspector]
        public List<GazeBlendShapeSampleEntry> lookLeftSamples = new List<GazeBlendShapeSampleEntry>();

        [HideInInspector]
        public List<GazeBlendShapeSampleEntry> lookRightSamples = new List<GazeBlendShapeSampleEntry>();

        [HideInInspector]
        public List<GazeBlendShapeSampleEntry> lookUpSamples = new List<GazeBlendShapeSampleEntry>();

        [HideInInspector]
        public List<GazeBlendShapeSampleEntry> lookDownSamples = new List<GazeBlendShapeSampleEntry>();
    }
}
