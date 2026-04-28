using NUnit.Framework;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.Json
{
    /// <summary>
    /// tasks.md 4.5: <c>bonePoses</c> 欠落 / null / 空配列 JSON の読込に対する
    /// 後方互換契約テスト（Req 1.5, 7.3, 7.5, 10.2, 10.4）。
    ///
    /// <para>
    /// 重要な意図:
    /// <list type="bullet">
    ///     <item>
    ///         <c>inputSources</c> の D-5 必須化ポリシー（欠落で <see cref="System.FormatException"/>）を
    ///         <c>bonePoses</c> には**踏襲しない**。bonePoses は preview.1 でも optional であり、
    ///         未指定 JSON は空 BonePose 配列として正常受理されなければならない。
    ///     </item>
    ///     <item>
    ///         本テストは「うっかり必須化バリデーションを書き加える」回帰の構造的ガードでもある。
    ///         tasks.md スコープ外メモ「<c>bonePoses</c> ブロックに対する必須化バリデーションは絶対に追加しない」を参照。
    ///     </item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// 観測完了条件:
    /// <list type="bullet">
    ///     <item><c>bonePoses</c> フィールド完全欠落 JSON で <see cref="FacialProfile.BonePoses"/> が空配列となり、他フィールドは破壊されない（Req 7.3, 10.2）</item>
    ///     <item><c>bonePoses: null</c> / <c>bonePoses: []</c> の両方も空配列扱い</item>
    ///     <item>JSON Loader が「bonePoses 必須」例外を投げない（<see cref="Assert.DoesNotThrow"/>）</item>
    ///     <item><see cref="JsonSchemaDefinition.SampleProfileJson"/>（既存サンプル、bonePoses 不在）が無修正で従来通り読込まれる</item>
    /// </list>
    /// </para>
    ///
    /// _Requirements: 1.5, 7.3, 7.5, 10.2, 10.4
    /// _Boundary: Adapters.Json.SystemTextJsonParser
    /// _Depends: 4.4 (SystemTextJsonParser の bonePoses ブロック処理)
    /// </summary>
    [TestFixture]
    public class SystemTextJsonParserBonePoseBackwardCompatTests
    {
        private SystemTextJsonParser _parser;

        [SetUp]
        public void SetUp()
        {
            _parser = new SystemTextJsonParser();
        }

        // ================================================================
        // bonePoses フィールド完全欠落: 既存（bone-control 導入前）JSON 互換
        // Req 7.3, 10.2
        // ================================================================

        [Test]
        public void ParseProfile_BonePosesFieldMissing_ReturnsEmptyBonePoses()
        {
            // 既存 JSON は bonePoses フィールドを一切宣言しない。bonePoses は optional のため
            // 空配列で受理される必要がある（必須化エラーを出してはならない）。
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[]
            }";

            var profile = _parser.ParseProfile(json);

            Assert.IsNotNull(profile);
            Assert.AreEqual(0, profile.BonePoses.Length, "bonePoses 不在の JSON は空 BonePose 配列を返す必要がある");
        }

        [Test]
        public void ParseProfile_BonePosesFieldMissing_PreservesOtherFields()
        {
            // bonePoses が存在しなくとも、layers / expressions / inputSources / rendererPaths
            // が破壊されずに正しく読込まれることを保証する（Req 10.2: 他フィールドへの非破壊）。
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""rendererPaths"":[""Armature/Body""],
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":0.5},
                        {""id"":""osc"",""weight"":0.5}
                    ]},
                    {""name"":""lipsync"",""priority"":1,""exclusionMode"":""blend"",""inputSources"":[
                        {""id"":""lipsync"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[
                    {
                        ""id"":""550e8400-e29b-41d4-a716-446655440000"",
                        ""name"":""笑顔"",
                        ""layer"":""emotion"",
                        ""transitionDuration"":0.25,
                        ""transitionCurve"":{""type"":""easeInOut""},
                        ""blendShapeValues"":[
                            {""name"":""Fcl_ALL_Joy"",""value"":1.0}
                        ],
                        ""layerSlots"":[]
                    }
                ]
            }";

            var profile = _parser.ParseProfile(json);

            Assert.AreEqual("1.0", profile.SchemaVersion);
            Assert.AreEqual(0, profile.BonePoses.Length, "bonePoses は空配列");
            Assert.AreEqual(2, profile.Layers.Length, "layers が破壊されない");
            Assert.AreEqual("emotion", profile.Layers.Span[0].Name);
            Assert.AreEqual("lipsync", profile.Layers.Span[1].Name);
            Assert.AreEqual(1, profile.Expressions.Length, "expressions が破壊されない");
            Assert.AreEqual("笑顔", profile.Expressions.Span[0].Name);
            Assert.AreEqual(1, profile.RendererPaths.Length, "rendererPaths が破壊されない");
            Assert.AreEqual("Armature/Body", profile.RendererPaths.Span[0]);

            // inputSources も破壊されない（D-5 で必須化されているが、それと bonePoses 後方互換は独立）
            Assert.AreEqual(2, profile.LayerInputSources.Length);
            Assert.AreEqual(2, profile.LayerInputSources.Span[0].Length);
            Assert.AreEqual("controller-expr", profile.LayerInputSources.Span[0][0].Id);
            Assert.AreEqual("osc", profile.LayerInputSources.Span[0][1].Id);
        }

        [Test]
        public void ParseProfile_BonePosesFieldMissing_DoesNotThrow()
        {
            // Req 7.3 / 10.2 / スコープ外メモ:
            // bonePoses 不在で「必須」例外を絶対に投げてはならない。
            // 本テストは「うっかり必須化バリデーションを書き加える」回帰のガード。
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[]
            }";

            Assert.DoesNotThrow(() => _parser.ParseProfile(json));
        }

        // ================================================================
        // bonePoses: null / [] も空配列扱い
        // ================================================================

        [Test]
        public void ParseProfile_BonePosesNull_ReturnsEmptyBonePoses()
        {
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[],
                ""bonePoses"":null
            }";

            var profile = _parser.ParseProfile(json);

            Assert.IsNotNull(profile);
            Assert.AreEqual(0, profile.BonePoses.Length, "bonePoses: null は空配列扱い");
        }

        [Test]
        public void ParseProfile_BonePosesEmptyArray_ReturnsEmptyBonePoses()
        {
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[],
                ""bonePoses"":[]
            }";

            var profile = _parser.ParseProfile(json);

            Assert.IsNotNull(profile);
            Assert.AreEqual(0, profile.BonePoses.Length, "bonePoses: [] は空配列扱い");
        }

        [Test]
        public void ParseProfile_BonePosesNullOrEmpty_DoesNotThrow()
        {
            var jsonWithNull = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[],
                ""bonePoses"":null
            }";

            var jsonWithEmpty = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[],
                ""bonePoses"":[]
            }";

            Assert.DoesNotThrow(() => _parser.ParseProfile(jsonWithNull));
            Assert.DoesNotThrow(() => _parser.ParseProfile(jsonWithEmpty));
        }

        // ================================================================
        // 既存 JsonSchemaDefinition.SampleProfileJson の継続互換
        // 既存 SampleJsonParseTests の主要アサーションが無修正で通ることを検証
        // ================================================================

        [Test]
        public void ParseProfile_SampleProfileJson_HasEmptyBonePoses()
        {
            // 既存サンプル JSON は bonePoses 不在。BonePoses が空配列で受理されること。
            var profile = _parser.ParseProfile(JsonSchemaDefinition.SampleProfileJson);

            Assert.IsNotNull(profile);
            Assert.AreEqual(0, profile.BonePoses.Length,
                "既存 SampleProfileJson は bonePoses 不在のため、空 BonePoses として読込まれること");
        }

        [Test]
        public void ParseProfile_SampleProfileJson_PreservesExistingBlendShapeProfileStructure()
        {
            // 既存 SampleJsonParseTests と同じ観測点を本テストでも確認することで、
            // bonePoses 後方互換が「既存パイプラインを破壊しない」ことを構造的に保証する。
            var profile = _parser.ParseProfile(JsonSchemaDefinition.SampleProfileJson);

            Assert.AreEqual("1.0", profile.SchemaVersion);

            // 3 レイヤー
            Assert.AreEqual(3, profile.Layers.Length);
            Assert.AreEqual("emotion", profile.Layers.Span[0].Name);
            Assert.AreEqual("lipsync", profile.Layers.Span[1].Name);
            Assert.AreEqual("eye", profile.Layers.Span[2].Name);

            // 3 表情
            Assert.AreEqual(3, profile.Expressions.Length);
            Assert.AreEqual("笑顔", profile.Expressions.Span[0].Name);
            Assert.AreEqual("怒り", profile.Expressions.Span[1].Name);
            Assert.AreEqual("まばたき", profile.Expressions.Span[2].Name);

            // 笑顔の BlendShapeValues が破壊されていない
            var smileBs = profile.Expressions.Span[0].BlendShapeValues;
            Assert.AreEqual(3, smileBs.Length);
            Assert.AreEqual("Fcl_ALL_Joy", smileBs.Span[0].Name);
            Assert.AreEqual(1.0f, smileBs.Span[0].Value, 0.001f);
        }

        [Test]
        public void ParseProfile_SampleProfileJson_RoundTripsWithoutAddingBonePoses()
        {
            // SampleProfileJson → Domain → JSON → Domain の経路でも bonePoses が空のまま保たれる。
            var first = _parser.ParseProfile(JsonSchemaDefinition.SampleProfileJson);
            var serialized = _parser.SerializeProfile(first);
            var second = _parser.ParseProfile(serialized);

            Assert.AreEqual(0, first.BonePoses.Length);
            Assert.AreEqual(0, second.BonePoses.Length, "round-trip 経由でも BonePoses は空のまま");
            Assert.AreEqual(first.Layers.Length, second.Layers.Length);
            Assert.AreEqual(first.Expressions.Length, second.Expressions.Length);
        }
    }
}
