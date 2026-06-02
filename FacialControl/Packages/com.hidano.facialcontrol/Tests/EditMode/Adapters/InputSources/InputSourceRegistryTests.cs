using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Tests.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.InputSources
{
    /// <summary>
    /// task 3.4 の観測可能完了条件: <see cref="IInputSourceRegistry"/> /
    /// <see cref="InputSourceRegistry"/> の <c>Register(slug, source)</c> /
    /// <c>Register(slug, sub, source)</c> / <c>TryResolve("slug")</c> /
    /// <c>TryResolve("slug:sub")</c> の 4 系統を network なしで検証する。
    /// 重複 register が「LogError + 後勝ち」になること、未登録 id 解決が <c>false</c>
    /// を返すこと、<see cref="IInputSourceRegistry.RegisteredIds"/> 列挙が安定であることを
    /// 併せて assert する。
    /// </summary>
    /// <remarks>
    /// 本ファイルは Red 段階のテストであり、<see cref="InputSourceRegistry"/> 実装が
    /// 未作成のためコンパイル時に CS0246 (型 / 名前空間 が見つからない) が発生して Red 状態
    /// となる（task 3.5 の Green 化対象）。<see cref="IInputSourceRegistry"/> の
    /// <c>Register</c> / <c>TryResolve</c> / <c>RegisteredIds</c> API もまだ追加されていない
    /// ため、Register 呼出 / プロパティ参照側でも CS1061 / CS0117 が発生する。
    /// </remarks>
    [TestFixture]
    public class InputSourceRegistryTests
    {
        private static readonly Regex DuplicateLogPattern =
            new Regex("InputSourceRegistry.*duplicate", RegexOptions.IgnoreCase);

        private static readonly Regex ReplaceLogPattern =
            new Regex("InputSourceRegistry.*replaced", RegexOptions.IgnoreCase);

        /// <summary>
        /// 識別だけ可能な最小の <see cref="IInputSource"/>。registry の lookup で
        /// instance identity が維持されるかどうかの検証用。
        /// </summary>
        private sealed class StubInputSource : IInputSource
        {
            public StubInputSource(string id)
            {
                Id = id;
                Type = InputSourceType.ValueProvider;
                BlendShapeCount = 0;
                ContributeMask = ContributeMaskTestHelper.AllSetContributeMask(BlendShapeCount);
            }

            public string Id { get; }
            public InputSourceType Type { get; }
            public int BlendShapeCount { get; }
            public BitArray ContributeMask { get; }

            public void Tick(float deltaTime) { }
            public bool TryWriteValues(Span<float> output) => false;
        }

        // ---------------------------------------------------------------
        // Register(slug, source) → TryResolve("slug")
        // ---------------------------------------------------------------

        [Test]
        public void Register_PrimarySlug_TryResolveBySlugReturnsRegisteredSource()
        {
            var registry = new InputSourceRegistry();
            var source = new StubInputSource("osc-primary");
            var slug = AdapterSlug.Parse("osc");

            registry.Register(slug, source);

            Assert.IsTrue(registry.TryResolve("osc", out var resolved));
            Assert.AreSame(source, resolved);
        }

        [Test]
        public void Register_PrimarySlug_TryResolveByCompositeFormReturnsFalse()
        {
            // primary 単独登録時に <slug>:<sub> 形式での解決は false を返すべき。
            var registry = new InputSourceRegistry();
            registry.Register(AdapterSlug.Parse("osc"), new StubInputSource("osc"));

            Assert.IsFalse(registry.TryResolve("osc:vrchat", out var resolved));
            Assert.IsNull(resolved);
        }

        // ---------------------------------------------------------------
        // Register(slug, sub, source) → TryResolve("slug:sub")
        // ---------------------------------------------------------------

        [Test]
        public void Register_CompositeSlug_TryResolveByCompositeIdReturnsRegisteredSource()
        {
            var registry = new InputSourceRegistry();
            var source = new StubInputSource("osc-vrchat");
            var slug = AdapterSlug.Parse("osc");

            registry.Register(slug, "vrchat", source);

            Assert.IsTrue(registry.TryResolve("osc:vrchat", out var resolved));
            Assert.AreSame(source, resolved);
        }

        [Test]
        public void Register_CompositeSlug_TryResolvePrimaryFormReturnsFalse()
        {
            // composite のみ登録時に primary <slug> 解決は false を返すべき。
            var registry = new InputSourceRegistry();
            registry.Register(AdapterSlug.Parse("osc"), "vrchat", new StubInputSource("osc:vrchat"));

            Assert.IsFalse(registry.TryResolve("osc", out var resolved));
            Assert.IsNull(resolved);
        }

        [Test]
        public void Register_PrimaryAndCompositeForSameSlug_BothCoexist()
        {
            // primary と <slug>:<sub> は別キーなので衝突せず両方解決可能。
            var registry = new InputSourceRegistry();
            var primary = new StubInputSource("osc");
            var composite = new StubInputSource("osc:vmc");
            var slug = AdapterSlug.Parse("osc");

            registry.Register(slug, primary);
            registry.Register(slug, "vmc", composite);

            Assert.IsTrue(registry.TryResolve("osc", out var p));
            Assert.AreSame(primary, p);
            Assert.IsTrue(registry.TryResolve("osc:vmc", out var c));
            Assert.AreSame(composite, c);
        }

        // ---------------------------------------------------------------
        // 重複 register: LogError + 後勝ち
        // ---------------------------------------------------------------

        [Test]
        public void Register_DuplicatePrimarySlug_LogsErrorAndOverwrites()
        {
            LogAssert.Expect(LogType.Error, DuplicateLogPattern);
            var registry = new InputSourceRegistry();
            var first = new StubInputSource("first");
            var second = new StubInputSource("second");
            var slug = AdapterSlug.Parse("osc");

            registry.Register(slug, first);
            registry.Register(slug, second);

            Assert.IsTrue(registry.TryResolve("osc", out var resolved));
            Assert.AreSame(second, resolved, "後勝ち: 2 回目の Register が勝つこと");
        }

        [Test]
        public void Register_DuplicateCompositeSlug_LogsErrorAndOverwrites()
        {
            LogAssert.Expect(LogType.Error, DuplicateLogPattern);
            var registry = new InputSourceRegistry();
            var first = new StubInputSource("first");
            var second = new StubInputSource("second");
            var slug = AdapterSlug.Parse("osc");

            registry.Register(slug, "vrchat", first);
            registry.Register(slug, "vrchat", second);

            Assert.IsTrue(registry.TryResolve("osc:vrchat", out var resolved));
            Assert.AreSame(second, resolved, "後勝ち: 2 回目の Register が勝つこと");
        }

        // ---------------------------------------------------------------
        // 未登録 id 解決
        // ---------------------------------------------------------------

        // ---------------------------------------------------------------
        // Replace: Log + 差し替え / 新規登録
        // ---------------------------------------------------------------

        [Test]
        public void Replace_ExistingPrimarySlug_LogsAndPreservesRegisteredIdOrder()
        {
            LogAssert.Expect(LogType.Log, ReplaceLogPattern);
            var registry = new InputSourceRegistry();
            var first = new StubInputSource("first");
            var second = new StubInputSource("second");
            var slug = AdapterSlug.Parse("osc");
            registry.Register(slug, first);
            registry.Register(AdapterSlug.Parse("input-system"), new StubInputSource("other"));

            registry.Replace(slug, second);

            Assert.IsTrue(registry.TryResolve("osc", out var resolved));
            Assert.AreSame(second, resolved);
            CollectionAssert.AreEqual(new[] { "osc", "input-system" }, registry.RegisteredIds);
        }

        [Test]
        public void Replace_UnregisteredPrimarySlug_LogsAndRegistersNewSource()
        {
            LogAssert.Expect(LogType.Log, ReplaceLogPattern);
            var registry = new InputSourceRegistry();
            var source = new StubInputSource("new-primary");
            var slug = AdapterSlug.Parse("osc");

            registry.Replace(slug, source);

            Assert.IsTrue(registry.TryResolve("osc", out var resolved));
            Assert.AreSame(source, resolved);
            CollectionAssert.AreEqual(new[] { "osc" }, registry.RegisteredIds);
        }

        [Test]
        public void Replace_ExistingCompositeSlug_LogsAndPreservesRegisteredIdOrder()
        {
            LogAssert.Expect(LogType.Log, ReplaceLogPattern);
            var registry = new InputSourceRegistry();
            var first = new StubInputSource("first");
            var second = new StubInputSource("second");
            var slug = AdapterSlug.Parse("osc");
            registry.Register(slug, "vrchat", first);
            registry.Register(AdapterSlug.Parse("input-system"), new StubInputSource("other"));

            registry.Replace(slug, "vrchat", second);

            Assert.IsTrue(registry.TryResolve("osc:vrchat", out var resolved));
            Assert.AreSame(second, resolved);
            CollectionAssert.AreEqual(new[] { "osc:vrchat", "input-system" }, registry.RegisteredIds);
        }

        [Test]
        public void Replace_UnregisteredCompositeSlug_LogsAndRegistersNewSource()
        {
            LogAssert.Expect(LogType.Log, ReplaceLogPattern);
            var registry = new InputSourceRegistry();
            var source = new StubInputSource("new-composite");
            var slug = AdapterSlug.Parse("osc");

            registry.Replace(slug, "vrchat", source);

            Assert.IsTrue(registry.TryResolve("osc:vrchat", out var resolved));
            Assert.AreSame(source, resolved);
            CollectionAssert.AreEqual(new[] { "osc:vrchat" }, registry.RegisteredIds);
        }

        [Test]
        public void TryResolve_UnregisteredPrimaryId_ReturnsFalseAndNull()
        {
            var registry = new InputSourceRegistry();

            Assert.IsFalse(registry.TryResolve("missing", out var resolved));
            Assert.IsNull(resolved);
        }

        [Test]
        public void TryResolve_UnregisteredCompositeId_ReturnsFalseAndNull()
        {
            var registry = new InputSourceRegistry();
            registry.Register(AdapterSlug.Parse("osc"), new StubInputSource("primary"));

            Assert.IsFalse(registry.TryResolve("osc:unknown-sub", out var resolved));
            Assert.IsNull(resolved);
        }

        [Test]
        public void TryResolve_NullOrEmptyId_ReturnsFalseAndNull()
        {
            var registry = new InputSourceRegistry();
            registry.Register(AdapterSlug.Parse("osc"), new StubInputSource("primary"));

            Assert.IsFalse(registry.TryResolve(null, out var nullResolved));
            Assert.IsNull(nullResolved);

            Assert.IsFalse(registry.TryResolve(string.Empty, out var emptyResolved));
            Assert.IsNull(emptyResolved);
        }

        [Test]
        public void Unregister_PrimarySlug_RemovesRegisteredSource()
        {
            var registry = new InputSourceRegistry();
            var slug = AdapterSlug.Parse("osc");
            registry.Register(slug, new StubInputSource("primary"));

            registry.Unregister(slug);

            Assert.IsFalse(registry.TryResolve("osc", out var resolved));
            Assert.IsNull(resolved);
            CollectionAssert.DoesNotContain(registry.RegisteredIds, "osc");
        }

        [Test]
        public void Unregister_UnregisteredSlug_DoesNotThrow()
        {
            var registry = new InputSourceRegistry();

            Assert.DoesNotThrow(() => registry.Unregister(AdapterSlug.Parse("missing")));
            Assert.AreEqual(0, registry.RegisteredIds.Count);
        }

        // ---------------------------------------------------------------
        // RegisteredIds
        // ---------------------------------------------------------------

        [Test]
        public void RegisteredIds_EmptyRegistry_IsEmpty()
        {
            var registry = new InputSourceRegistry();

            Assert.IsNotNull(registry.RegisteredIds);
            Assert.AreEqual(0, registry.RegisteredIds.Count);
        }

        [Test]
        public void RegisteredIds_AfterMixedRegistration_ContainsAllKeys()
        {
            var registry = new InputSourceRegistry();
            registry.Register(AdapterSlug.Parse("osc"), new StubInputSource("a"));
            registry.Register(AdapterSlug.Parse("osc"), "vrchat", new StubInputSource("b"));
            registry.Register(AdapterSlug.Parse("input-system"), new StubInputSource("c"));

            var ids = registry.RegisteredIds;
            Assert.AreEqual(3, ids.Count);
            CollectionAssert.Contains(ids, "osc");
            CollectionAssert.Contains(ids, "osc:vrchat");
            CollectionAssert.Contains(ids, "input-system");
        }

        [Test]
        public void RegisteredIds_EnumerationIsStableAcrossMultipleSnapshots()
        {
            // 同じ registry 状態に対する 2 回の列挙が同じ順序を返すことを確認する。
            // Dictionary の挿入順保持に依存する実装でも sort を伴う実装でも、
            // 「同一状態 → 同一列挙順序」という安定性は満たすべき契約。
            var registry = new InputSourceRegistry();
            registry.Register(AdapterSlug.Parse("osc"), new StubInputSource("a"));
            registry.Register(AdapterSlug.Parse("input-system"), new StubInputSource("b"));
            registry.Register(AdapterSlug.Parse("osc"), "vrchat", new StubInputSource("c"));

            var first = new List<string>(registry.RegisteredIds);
            var second = new List<string>(registry.RegisteredIds);

            CollectionAssert.AreEqual(first, second,
                "同一状態の RegisteredIds は呼び出し間で同じ列挙順序を返すべき");
        }

        [Test]
        public void RegisteredIds_AfterDuplicateRegister_DoesNotDuplicate()
        {
            LogAssert.Expect(LogType.Error, DuplicateLogPattern);
            var registry = new InputSourceRegistry();
            var slug = AdapterSlug.Parse("osc");
            registry.Register(slug, new StubInputSource("first"));
            registry.Register(slug, new StubInputSource("second"));

            Assert.AreEqual(1, registry.RegisteredIds.Count,
                "後勝ち上書きでも RegisteredIds は同じ key を 2 回列挙してはならない");
        }

        // ---------------------------------------------------------------
        // null source の防御
        // ---------------------------------------------------------------

        [Test]
        public void Register_NullPrimarySource_Throws()
        {
            var registry = new InputSourceRegistry();
            Assert.Throws<ArgumentNullException>(
                () => registry.Register(AdapterSlug.Parse("osc"), source: null));
        }

        [Test]
        public void Register_NullCompositeSource_Throws()
        {
            var registry = new InputSourceRegistry();
            Assert.Throws<ArgumentNullException>(
                () => registry.Register(AdapterSlug.Parse("osc"), "vrchat", source: null));
        }

        [Test]
        public void Replace_NullPrimarySource_Throws()
        {
            var registry = new InputSourceRegistry();
            Assert.Throws<ArgumentNullException>(
                () => registry.Replace(AdapterSlug.Parse("osc"), source: null));
        }

        [Test]
        public void Replace_NullCompositeSource_Throws()
        {
            var registry = new InputSourceRegistry();
            Assert.Throws<ArgumentNullException>(
                () => registry.Replace(AdapterSlug.Parse("osc"), "vrchat", source: null));
        }

        [Test]
        public void Register_NullOrEmptySub_Throws()
        {
            var registry = new InputSourceRegistry();
            Assert.Throws<ArgumentException>(
                () => registry.Register(
                    AdapterSlug.Parse("osc"), sub: null, source: new StubInputSource("x")));
            Assert.Throws<ArgumentException>(
                () => registry.Register(
                    AdapterSlug.Parse("osc"), sub: string.Empty, source: new StubInputSource("x")));
        }

        [Test]
        public void Replace_NullOrEmptySub_Throws()
        {
            var registry = new InputSourceRegistry();
            Assert.Throws<ArgumentException>(
                () => registry.Replace(
                    AdapterSlug.Parse("osc"), sub: null, source: new StubInputSource("x")));
            Assert.Throws<ArgumentException>(
                () => registry.Replace(
                    AdapterSlug.Parse("osc"), sub: string.Empty, source: new StubInputSource("x")));
        }

        // ---------------------------------------------------------------
        // 中立 interface 経由でも同じ契約が観測できること
        // ---------------------------------------------------------------

        [Test]
        public void IInputSourceRegistry_InterfaceContract_IsHonoredByImplementation()
        {
            IInputSourceRegistry registry = new InputSourceRegistry();
            var primary = new StubInputSource("primary");
            var composite = new StubInputSource("composite");
            var replacedPrimary = new StubInputSource("replaced-primary");
            var replacedComposite = new StubInputSource("replaced-composite");
            var slug = AdapterSlug.Parse("osc");

            registry.Register(slug, primary);
            registry.Register(slug, "vrchat", composite);
            LogAssert.Expect(LogType.Log, ReplaceLogPattern);
            LogAssert.Expect(LogType.Log, ReplaceLogPattern);
            registry.Replace(slug, replacedPrimary);
            registry.Replace(slug, "vrchat", replacedComposite);

            Assert.IsTrue(registry.TryResolve("osc", out var p));
            Assert.AreSame(replacedPrimary, p);
            Assert.IsTrue(registry.TryResolve("osc:vrchat", out var c));
            Assert.AreSame(replacedComposite, c);
            Assert.AreEqual(2, registry.RegisteredIds.Count);

            registry.Unregister(slug);
            Assert.IsFalse(registry.TryResolve("osc", out var removed));
            Assert.IsNull(removed);
            Assert.IsTrue(registry.TryResolve("osc:vrchat", out c));
            Assert.AreSame(replacedComposite, c);
            Assert.AreEqual(1, registry.RegisteredIds.Count);
        }
    }
}
