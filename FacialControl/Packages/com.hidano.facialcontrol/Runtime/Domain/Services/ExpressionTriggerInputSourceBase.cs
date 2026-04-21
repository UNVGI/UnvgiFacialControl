using System;
using System.Collections.Generic;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Domain.Services
{
    /// <summary>
    /// Expression トリガー型入力源の共通基底 (D-1 ハイブリッドモデル)。
    /// 自身専用の Expression スタックと <see cref="TransitionCalculator"/> 状態を保持し、
    /// <see cref="TriggerOn"/> / <see cref="TriggerOff"/> によるスタック操作と
    /// <see cref="Tick"/> による遷移進行・<see cref="TryWriteValues"/> による補間結果書込を提供する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本基底は Req 1.6 / 1.8 を満たすためのインスタンス独立性を確保する。
    /// 同レイヤーに複数の <see cref="ExpressionTriggerInputSourceBase"/> 派生 (例: controller-expr /
    /// keyboard-expr) を配置しても、各インスタンスの Expression スタックと
    /// 遷移状態は互いに干渉しない。
    /// </para>
    /// <para>
    /// カテゴリ排他 (LastWins / Blend) は本インスタンス内部の Expression 集合に対してのみ適用される。
    /// 他インスタンスや他レイヤーとの相互作用は <see cref="LayerInputSourceAggregator"/> 側の責務。
    /// </para>
    /// <para>
    /// Invariants:
    /// <list type="bullet">
    ///   <item><see cref="Id"/> / <see cref="Type"/> / <see cref="BlendShapeCount"/> は構築後不変。</item>
    ///   <item><see cref="Type"/> は常に <see cref="InputSourceType.ExpressionTrigger"/>。</item>
    ///   <item>内部スタック深度は 0〜<c>maxStackDepth</c> の範囲 (超過時は最古を drop)。</item>
    /// </list>
    /// </para>
    /// </remarks>
    public abstract class ExpressionTriggerInputSourceBase : IInputSource
    {
        private const float DefaultReleaseTransitionDuration = 0.25f;

        /// <summary>入力源識別子 (生涯不変)。<see cref="IInputSource.Id"/> の実体。</summary>
        public string Id { get; }

        /// <summary>入力源種別。本基底では常に <see cref="InputSourceType.ExpressionTrigger"/>。</summary>
        public InputSourceType Type => InputSourceType.ExpressionTrigger;

        /// <summary>本入力源が書込む BlendShape の個数 (構築後不変)。</summary>
        public int BlendShapeCount { get; }

        /// <summary>内部スタックの最大深度 (構築時指定)。</summary>
        protected int MaxStackDepth { get; }

        /// <summary>本入力源に適用されるレイヤー排他モード (構築時指定)。</summary>
        protected ExclusionMode ExclusionMode { get; }

        private readonly string[] _blendShapeNames;
        private readonly FacialProfile _profile;
        private readonly List<string> _activeExpressionIds;

        private readonly float[] _snapshotValues;
        private readonly float[] _targetValues;
        private readonly float[] _currentValues;
        private float _elapsedTime;
        private float _duration;
        private TransitionCurve _curve;
        private bool _isComplete;
        private bool _hasWarnedStackDepthExceeded;

        /// <summary>
        /// 現在アクティブな Expression の ID リストを読取専用で公開する (診断/テスト用)。
        /// スタックの末尾が最新 Trigger (LIFO における top)。
        /// </summary>
        protected IReadOnlyList<string> ActiveExpressionIds => _activeExpressionIds;

        /// <summary>
        /// 現在の補間済み BlendShape 値 (長さ <see cref="BlendShapeCount"/>)。診断/テスト用。
        /// </summary>
        protected ReadOnlySpan<float> CurrentValues => _currentValues;

        /// <summary>
        /// Expression トリガー型の基底を構築する。
        /// </summary>
        /// <param name="id">入力源識別子 (<see cref="InputSourceId"/> 規約に従う)。</param>
        /// <param name="blendShapeCount">書込む BlendShape 個数 (0 以上)。</param>
        /// <param name="maxStackDepth">Expression スタックの最大深度 (1 以上)。</param>
        /// <param name="exclusionMode">本インスタンス内部で適用する排他モード。</param>
        /// <param name="blendShapeNames">BlendShape 名の列 (名前→インデックス解決用)。</param>
        /// <param name="profile">Expression 検索に用いる <see cref="FacialProfile"/>。</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="blendShapeCount"/> が負、または <paramref name="maxStackDepth"/> が 1 未満の場合。
        /// </exception>
        /// <exception cref="ArgumentNullException"><paramref name="blendShapeNames"/> が null の場合。</exception>
        protected ExpressionTriggerInputSourceBase(
            InputSourceId id,
            int blendShapeCount,
            int maxStackDepth,
            ExclusionMode exclusionMode,
            IReadOnlyList<string> blendShapeNames,
            FacialProfile profile)
        {
            if (blendShapeCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blendShapeCount), blendShapeCount,
                    "blendShapeCount は 0 以上を指定してください。");
            }

            if (maxStackDepth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxStackDepth), maxStackDepth,
                    "maxStackDepth は 1 以上を指定してください。");
            }

            if (blendShapeNames == null)
            {
                throw new ArgumentNullException(nameof(blendShapeNames));
            }

            Id = id.Value;
            BlendShapeCount = blendShapeCount;
            MaxStackDepth = maxStackDepth;
            ExclusionMode = exclusionMode;

            int nameCount = blendShapeNames.Count;
            _blendShapeNames = new string[nameCount];
            for (int i = 0; i < nameCount; i++)
            {
                _blendShapeNames[i] = blendShapeNames[i];
            }

            _profile = profile;
            _activeExpressionIds = new List<string>(maxStackDepth);

            _snapshotValues = new float[blendShapeCount];
            _targetValues = new float[blendShapeCount];
            _currentValues = new float[blendShapeCount];
            _elapsedTime = 0f;
            _duration = 0f;
            _curve = TransitionCurve.Linear;
            _isComplete = true;
        }

        /// <summary>
        /// 指定 Expression ID を内部スタックに push する。
        /// 既に積まれている場合は一旦 remove してから末尾に再配置する (LIFO 位置を最新化)。
        /// 深度が <see cref="MaxStackDepth"/> を超える場合は最古の Expression が自動 drop され、
        /// 当該インスタンスで初回のみ <see cref="UnityEngine.Debug.LogWarning(object)"/> が出る
        /// (per-instance 1 回のみ、Req 1.6)。
        /// push 後、現在値をスナップショットとして新ターゲットへの遷移を開始する。
        /// </summary>
        /// <param name="expressionId">push する Expression の ID。</param>
        /// <exception cref="ArgumentNullException"><paramref name="expressionId"/> が null の場合。</exception>
        public void TriggerOn(string expressionId)
        {
            if (expressionId == null)
            {
                throw new ArgumentNullException(nameof(expressionId));
            }

            _activeExpressionIds.Remove(expressionId);

            while (_activeExpressionIds.Count >= MaxStackDepth)
            {
                OnStackDepthExceeded();
                WarnStackDepthExceededOnce();
                _activeExpressionIds.RemoveAt(0);
            }

            _activeExpressionIds.Add(expressionId);
            StartTransition();
        }

        /// <summary>
        /// 指定 Expression ID を内部スタックから remove する。
        /// 削除に成功した場合のみ、現在値スナップショットから新ターゲットへの遷移を開始する
        /// (存在しない ID の指定は静かに無視される)。
        /// </summary>
        /// <param name="expressionId">remove する Expression の ID。</param>
        /// <exception cref="ArgumentNullException"><paramref name="expressionId"/> が null の場合。</exception>
        public void TriggerOff(string expressionId)
        {
            if (expressionId == null)
            {
                throw new ArgumentNullException(nameof(expressionId));
            }

            if (_activeExpressionIds.Remove(expressionId))
            {
                StartTransition();
            }
        }

        /// <summary>
        /// 1 フレーム分の時間進行。遷移中であれば <see cref="TransitionCalculator.ComputeBlendWeight"/>
        /// により進行度を計算し、<see cref="ExclusionResolver.ResolveLastWins"/> でクロスフェード
        /// して内部 <c>CurrentValues</c> を更新する。
        /// </summary>
        /// <param name="deltaTime">前フレームからの経過秒数 (&gt;= 0)。</param>
        public void Tick(float deltaTime)
        {
            if (_isComplete)
            {
                return;
            }

            if (deltaTime > 0f)
            {
                _elapsedTime += deltaTime;
            }

            float weight = TransitionCalculator.ComputeBlendWeight(_curve, _elapsedTime, _duration);
            ExclusionResolver.ResolveLastWins(_snapshotValues, _targetValues, weight, _currentValues);

            if (_duration <= 0f || _elapsedTime >= _duration)
            {
                Array.Copy(_targetValues, _currentValues, BlendShapeCount);
                _isComplete = true;
            }
        }

        /// <summary>
        /// 現在の補間済み BlendShape 値を <paramref name="output"/> に書込む。
        /// 空スタックかつ遷移完了済みの場合は <c>false</c> を返し <paramref name="output"/> を変更しない。
        /// </summary>
        /// <param name="output">書込先バッファ。長さ不足時は overlap のみ書込む。</param>
        /// <returns>書込んだ場合 true、無効 (空スタック + 遷移完了) なら false。</returns>
        public bool TryWriteValues(Span<float> output)
        {
            if (_activeExpressionIds.Count == 0 && _isComplete)
            {
                return false;
            }

            int len = Math.Min(output.Length, BlendShapeCount);
            for (int i = 0; i < len; i++)
            {
                output[i] = _currentValues[i];
            }
            return true;
        }

        /// <summary>
        /// スタック深度超過時に派生クラスで追加処理を行うためのフック。
        /// 基底実装は何もしない。基底は別途 <see cref="WarnStackDepthExceededOnce"/> で
        /// per-instance 1 回の <see cref="UnityEngine.Debug.LogWarning(object)"/> を行う。
        /// </summary>
        protected virtual void OnStackDepthExceeded()
        {
            // 基底では no-op。警告ログは WarnStackDepthExceededOnce が担う。
        }

        private void WarnStackDepthExceededOnce()
        {
            if (_hasWarnedStackDepthExceeded)
            {
                return;
            }
            _hasWarnedStackDepthExceeded = true;
            UnityEngine.Debug.LogWarning(
                $"[ExpressionTriggerInputSource] id='{Id}': Expression stack depth exceeded " +
                $"(maxStackDepth={MaxStackDepth}). Dropping oldest expression. " +
                "This warning is emitted only once per instance.");
        }

        private void StartTransition()
        {
            Array.Copy(_currentValues, _snapshotValues, BlendShapeCount);
            Array.Clear(_targetValues, 0, BlendShapeCount);

            if (_activeExpressionIds.Count == 0)
            {
                _duration = DefaultReleaseTransitionDuration;
                _curve = TransitionCurve.Linear;
                _elapsedTime = 0f;
                _isComplete = false;
                return;
            }

            Expression? lastExpression = null;
            int lastIdx = _activeExpressionIds.Count - 1;

            if (ExclusionMode == ExclusionMode.LastWins)
            {
                string lastId = _activeExpressionIds[lastIdx];
                lastExpression = _profile.FindExpressionById(lastId);
                if (lastExpression.HasValue)
                {
                    MapBlendShapeValues(lastExpression.Value, _targetValues);
                }
            }
            else
            {
                for (int i = 0; i < _activeExpressionIds.Count; i++)
                {
                    var expr = _profile.FindExpressionById(_activeExpressionIds[i]);
                    if (expr.HasValue)
                    {
                        MapBlendShapeValuesAdditive(expr.Value, _targetValues);
                    }
                }
                lastExpression = _profile.FindExpressionById(_activeExpressionIds[lastIdx]);
            }

            if (lastExpression.HasValue)
            {
                _duration = lastExpression.Value.TransitionDuration;
                _curve = lastExpression.Value.TransitionCurve;
            }
            else
            {
                _duration = DefaultReleaseTransitionDuration;
                _curve = TransitionCurve.Linear;
            }

            _elapsedTime = 0f;
            _isComplete = false;
        }

        private void MapBlendShapeValues(Expression expression, float[] target)
        {
            var bsSpan = expression.BlendShapeValues.Span;
            for (int v = 0; v < bsSpan.Length; v++)
            {
                int idx = FindBlendShapeIndex(bsSpan[v].Name);
                if (idx >= 0)
                {
                    target[idx] = bsSpan[v].Value;
                }
            }
        }

        private void MapBlendShapeValuesAdditive(Expression expression, float[] target)
        {
            var bsSpan = expression.BlendShapeValues.Span;
            for (int v = 0; v < bsSpan.Length; v++)
            {
                int idx = FindBlendShapeIndex(bsSpan[v].Name);
                if (idx >= 0)
                {
                    target[idx] = Clamp01(target[idx] + bsSpan[v].Value);
                }
            }
        }

        private int FindBlendShapeIndex(string name)
        {
            for (int i = 0; i < _blendShapeNames.Length; i++)
            {
                if (_blendShapeNames[i] == name)
                {
                    return i;
                }
            }
            return -1;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
