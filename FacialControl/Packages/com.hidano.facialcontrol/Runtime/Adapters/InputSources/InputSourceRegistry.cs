using System;
using System.Collections.Generic;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.InputSources
{
    /// <summary>
    /// slug-keyed <see cref="IInputSource"/> lookup の per-FC 実装。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 内部は <see cref="Dictionary{TKey, TValue}"/> を 1 個保持し、
    /// <c>&lt;slug&gt;</c> / <c>&lt;slug&gt;:&lt;sub&gt;</c> 文字列をそのままキーに格納する
    /// 。
    /// </para>
    /// <para>
    /// 旧ファクトリ経路の (id, options) ディスパッチ / JSON deserialize / reserved id
    /// チェックは本クラスでは保持しない（D-13 廃止）。
    /// </para>
    /// <para>
    /// DD-1 注記: Public API の slug 引数は <see cref="AdapterSlug"/> 値オブジェクトとして受け、
    /// 内部で <see cref="AdapterSlug.Value"/> を string キーに変換する。
    /// </para>
    /// </remarks>
    public sealed class InputSourceRegistry : IInputSourceRegistry
    {
        private const char CompositeSeparator = ':';

        private readonly Dictionary<string, IInputSource> _entries =
            new Dictionary<string, IInputSource>(StringComparer.Ordinal);

        // 挿入順保持の診断用スナップショット。重複登録時は新規追加せず、上書きのみ行う。
        private readonly List<string> _registeredIds = new List<string>();

        /// <inheritdoc />
        public IReadOnlyList<string> RegisteredIds => _registeredIds;

        /// <inheritdoc />
        public void Register(AdapterSlug slug, IInputSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            RegisterInternal(slug.Value, source);
        }

        /// <inheritdoc />
        public void Replace(AdapterSlug slug, IInputSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            ReplaceInternal(slug.Value, source);
        }

        /// <inheritdoc />
        public void Register(AdapterSlug slug, string sub, IInputSource source)
        {
            if (string.IsNullOrEmpty(sub))
            {
                throw new ArgumentException(
                    "sub must not be null or empty for composite id registration.", nameof(sub));
            }
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            string key = slug.Value + CompositeSeparator + sub;
            RegisterInternal(key, source);
        }

        /// <inheritdoc />
        public void Replace(AdapterSlug slug, string sub, IInputSource source)
        {
            if (string.IsNullOrEmpty(sub))
            {
                throw new ArgumentException(
                    "sub must not be null or empty for composite id replacement.", nameof(sub));
            }
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            string key = slug.Value + CompositeSeparator + sub;
            ReplaceInternal(key, source);
        }

        /// <inheritdoc />
        public void Unregister(AdapterSlug slug)
        {
            UnregisterInternal(slug.Value);
        }

        /// <inheritdoc />
        public void Unregister(AdapterSlug slug, string sub)
        {
            if (string.IsNullOrEmpty(sub))
            {
                return;
            }

            UnregisterInternal(slug.Value + CompositeSeparator + sub);
        }

        /// <inheritdoc />
        public bool TryResolve(string layerInputSourceId, out IInputSource source)
        {
            if (string.IsNullOrEmpty(layerInputSourceId))
            {
                source = null;
                return false;
            }

            return _entries.TryGetValue(layerInputSourceId, out source);
        }

        private void RegisterInternal(string key, IInputSource source)
        {
            if (_entries.ContainsKey(key))
            {
                Debug.LogError(
                    $"[InputSourceRegistry] duplicate registration for id '{key}'; later registration wins.");
                _entries[key] = source;
                return;
            }

            _entries.Add(key, source);
            _registeredIds.Add(key);
        }

        private void ReplaceInternal(string key, IInputSource source)
        {
            if (_entries.TryGetValue(key, out var previous))
            {
                _entries[key] = source;
                Debug.Log(
                    $"[InputSourceRegistry] replaced id '{key}' ({previous.GetType().Name} -> {source.GetType().Name}).");
                return;
            }

            _entries.Add(key, source);
            _registeredIds.Add(key);
            Debug.Log(
                $"[InputSourceRegistry] replaced id '{key}' (<unregistered> -> {source.GetType().Name}).");
        }

        private void UnregisterInternal(string key)
        {
            if (string.IsNullOrEmpty(key) || !_entries.Remove(key))
            {
                return;
            }

            for (int i = 0; i < _registeredIds.Count; i++)
            {
                if (string.Equals(_registeredIds[i], key, StringComparison.Ordinal))
                {
                    _registeredIds.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
