using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Adapters.Json.Dto;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.Json
{
    /// <summary>
    /// tasks.md 7.3: <see cref="SystemTextJsonParser"/> が
    /// <c>layers[].inputSources[]</c> の不正エントリに対して
    /// 警告 + skip / last-wins を適用し、profile ロードを継続することの契約テスト
    /// (Req 1.7, 3.3, 3.4, D-5, D-6)。
    /// <para>
    /// 検証シナリオ:
    /// <list type="bullet">
    ///     <item>regex 違反 (<c>[a-zA-Z0-9_.-]{1,64}</c> 不一致 / <c>legacy</c>) → 警告 + skip</item>
    ///     <item>未知 id (予約でも <c>x-</c> プレフィックスでもない) → 警告 + skip</item>
    ///     <item>同一レイヤー内の重複 id → 警告 + 最後の出現を採用 (last-wins)</item>
    /// </list>
    /// いずれの場合も <see cref="SystemTextJsonParser.ParseLayerInputSources"/> は
    /// 例外を投げず、他の有効エントリと他レイヤーは正常に返される。
    /// </para>
    /// </summary>
    [TestFixture]
    public class SystemTextJsonParserInputSourceValidationTests
    {
        private SystemTextJsonParser _parser;

        [SetUp]
        public void SetUp()
        {
            _parser = new SystemTextJsonParser();
        }

        // ================================================================
        // regex 違反 → 警告 + skip (Req 1.7, 3.3)
        // ================================================================

        [Test]
        public void ParseLayerInputSources_IdViolatesRegex_LogsWarningAndSkips()
        {
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""invalid id with space"",""weight"":1.0},
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[]
            }";

            LogAssert.Expect(LogType.Warning, new Regex("inputSources.*invalid id with space"));

            var result = _parser.ParseLayerInputSources(json);

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(1, result[0].Length, "regex 違反エントリは skip されること");
            Assert.AreEqual("controller-expr", result[0][0].id);
        }

        [Test]
        public void ParseLayerInputSources_IdIsLegacy_LogsWarningAndSkips()
        {
            // D-5: legacy フォールバック廃止。InputSourceId は 'legacy' を受理しない。
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""legacy"",""weight"":1.0},
                        {""id"":""osc"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[]
            }";

            LogAssert.Expect(LogType.Warning, new Regex("inputSources.*legacy"));

            var result = _parser.ParseLayerInputSources(json);

            Assert.AreEqual(1, result[0].Length);
            Assert.AreEqual("osc", result[0][0].id);
        }

        [Test]
        public void ParseLayerInputSources_IdExceedsMaxLength_LogsWarningAndSkips()
        {
            // 65 文字 ID は regex {1,64} に違反する。
            string tooLong = new string('a', 65);
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""" + tooLong + @""",""weight"":1.0},
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[]
            }";

            LogAssert.Expect(LogType.Warning, new Regex("inputSources"));

            var result = _parser.ParseLayerInputSources(json);

            Assert.AreEqual(1, result[0].Length);
            Assert.AreEqual("controller-expr", result[0][0].id);
        }

        // ================================================================
        // 未知 id (Factory 未登録 = 予約でも x- でもない) → 警告 + skip (Req 3.3)
        // ================================================================

        [Test]
        public void ParseLayerInputSources_UnknownId_LogsWarningAndSkips()
        {
            // 'unknown-source' は予約 ID でも x- 拡張でもない。Factory 未登録扱い。
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""unknown-source"",""weight"":1.0},
                        {""id"":""osc"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[]
            }";

            LogAssert.Expect(LogType.Warning, new Regex("inputSources.*unknown-source"));

            var result = _parser.ParseLayerInputSources(json);

            Assert.AreEqual(1, result[0].Length);
            Assert.AreEqual("osc", result[0][0].id, "未知 id は skip、予約 id は通過");
        }

        [Test]
        public void ParseLayerInputSources_ReservedAndExtensionIds_BothAccepted()
        {
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":0.5},
                        {""id"":""x-custom-sensor"",""weight"":0.5}
                    ]}
                ],
                ""expressions"":[]
            }";

            var result = _parser.ParseLayerInputSources(json);

            Assert.AreEqual(2, result[0].Length);
            Assert.AreEqual("controller-expr", result[0][0].id);
            Assert.AreEqual("x-custom-sensor", result[0][1].id);
            LogAssert.NoUnexpectedReceived();
        }

        // ================================================================
        // 重複 id → 警告 + last-wins (Req 3.4)
        // ================================================================

        [Test]
        public void ParseLayerInputSources_DuplicateId_LogsWarningAndKeepsLast()
        {
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""osc"",""weight"":0.25,""options"":{""stalenessSeconds"":1.0}},
                        {""id"":""controller-expr"",""weight"":1.0},
                        {""id"":""osc"",""weight"":0.75,""options"":{""stalenessSeconds"":2.5}}
                    ]}
                ],
                ""expressions"":[]
            }";

            LogAssert.Expect(LogType.Warning, new Regex("重複.*osc|osc.*重複|duplicate.*osc|osc.*duplicate"));

            var result = _parser.ParseLayerInputSources(json);

            Assert.AreEqual(2, result[0].Length, "重複 id は 1 エントリに畳まれる (last-wins)");

            var oscEntry = result[0].Single(e => e.id == "osc");
            Assert.AreEqual(0.75f, oscEntry.weight, "最後の出現の weight が採用される");
            Assert.IsTrue(oscEntry.optionsJson.Contains("2.5"),
                $"最後の出現の options が採用される。実際: {oscEntry.optionsJson}");
        }

        [Test]
        public void ParseLayerInputSources_DuplicateId_PreservesRelativeOrderOfLastOccurrence()
        {
            // 入力: [A1, B, A2, C]  → 出力: [B, A2, C] (A は最後の出現位置で保持)
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""osc"",""weight"":0.1},
                        {""id"":""lipsync"",""weight"":0.2},
                        {""id"":""osc"",""weight"":0.9},
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[]
            }";

            LogAssert.Expect(LogType.Warning, new Regex("osc"));

            var result = _parser.ParseLayerInputSources(json);

            Assert.AreEqual(3, result[0].Length);
            Assert.AreEqual("lipsync", result[0][0].id);
            Assert.AreEqual("osc", result[0][1].id);
            Assert.AreEqual(0.9f, result[0][1].weight);
            Assert.AreEqual("controller-expr", result[0][2].id);
        }

        [Test]
        public void ParseLayerInputSources_DuplicateIdAcrossLayers_NotTreatedAsDuplicate()
        {
            // 異なるレイヤーなら同 id は重複ではない。
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""osc"",""weight"":1.0}
                    ]},
                    {""name"":""lipsync"",""priority"":1,""exclusionMode"":""blend"",""inputSources"":[
                        {""id"":""osc"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[]
            }";

            var result = _parser.ParseLayerInputSources(json);

            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(1, result[0].Length);
            Assert.AreEqual(1, result[1].Length);
            LogAssert.NoUnexpectedReceived();
        }

        // ================================================================
        // ロード継続性 (Req 3.3)
        // ================================================================

        [Test]
        public void ParseLayerInputSources_MixedInvalidEntries_DoesNotAbortLoad()
        {
            // 1 つのレイヤーに regex 違反 / 未知 / 重複 が混在していても parse は成功し、
            // 有効な最後の 'osc' エントリだけが残る。
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""bad id"",""weight"":1.0},
                        {""id"":""unknown-thing"",""weight"":1.0},
                        {""id"":""osc"",""weight"":0.3},
                        {""id"":""osc"",""weight"":0.7}
                    ]}
                ],
                ""expressions"":[]
            }";

            LogAssert.Expect(LogType.Warning, new Regex("bad id"));
            LogAssert.Expect(LogType.Warning, new Regex("unknown-thing"));
            LogAssert.Expect(LogType.Warning, new Regex("osc"));

            InputSourceDto[][] result = null;
            Assert.DoesNotThrow(() => result = _parser.ParseLayerInputSources(json));

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(1, result[0].Length, "有効な最後の osc エントリのみが残る");
            Assert.AreEqual("osc", result[0][0].id);
            Assert.AreEqual(0.7f, result[0][0].weight);
        }
    }
}
