using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.Json
{
    /// <summary>
    /// tasks.md 4.3: <see cref="SystemTextJsonParser"/> が
    /// <c>bonePoses</c> ブロックを <see cref="FacialProfile.BonePoses"/> に
    /// Domain として正しく取り込み、不正エントリは警告 + skip + 続行する
    /// 契約テスト（Red、EditMode）(Req 7.1, 7.2, 7.4, 7.5)。
    ///
    /// <para>
    /// 観測完了条件:
    /// <list type="bullet">
    ///     <item><c>bonePoses</c> 付き JSON が <see cref="FacialProfile.BonePoses"/> に正しく Domain として乗る (Req 7.1, 7.2)</item>
    ///     <item><c>boneName</c> 欠落 / null / 空エントリは警告 + skip + 続行 (Req 7.4)</item>
    ///     <item><c>eulerXYZ</c> 不在 / 欠損エントリは警告 + skip (Req 7.4)</item>
    ///     <item><see cref="JsonSchemaDefinition.Profile.BonePoses"/> 定数が存在する</item>
    /// </list>
    /// </para>
    ///
    /// _Requirements: 7.1, 7.2, 7.4, 7.5
    /// _Boundary: Adapters.Json.SystemTextJsonParser, JsonSchemaDefinition
    /// _Depends: 3.2 (FacialProfile.BonePoses), 4.2 (BonePoseDto / BonePoseEntryDto)
    /// </summary>
    [TestFixture]
    public class SystemTextJsonParserBonePoseTests
    {
        private SystemTextJsonParser _parser;

        [SetUp]
        public void SetUp()
        {
            _parser = new SystemTextJsonParser();
        }

        // ================================================================
        // 正常系: bonePoses ブロックが Domain.BonePoses に乗る (Req 7.1, 7.2)
        // ================================================================

        [Test]
        public void ParseProfile_WithBonePosesBlock_PopulatesDomainBonePoses()
        {
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[],
                ""bonePoses"":[
                    {
                        ""id"":""default-gaze"",
                        ""entries"":[
                            {""boneName"":""LeftEye"",""eulerXYZ"":{""x"":0.0,""y"":0.0,""z"":0.0}},
                            {""boneName"":""RightEye"",""eulerXYZ"":{""x"":1.5,""y"":-2.5,""z"":3.5}}
                        ]
                    }
                ]
            }";

            var profile = _parser.ParseProfile(json);

            Assert.AreEqual(1, profile.BonePoses.Length, "bonePoses が 1 件読み込まれること");

            var poses = profile.BonePoses.Span;
            Assert.AreEqual("default-gaze", poses[0].Id);

            var entries = poses[0].Entries.Span;
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual("LeftEye", entries[0].BoneName);
            Assert.AreEqual(0.0f, entries[0].EulerX);
            Assert.AreEqual(0.0f, entries[0].EulerY);
            Assert.AreEqual(0.0f, entries[0].EulerZ);
            Assert.AreEqual("RightEye", entries[1].BoneName);
            Assert.AreEqual(1.5f, entries[1].EulerX);
            Assert.AreEqual(-2.5f, entries[1].EulerY);
            Assert.AreEqual(3.5f, entries[1].EulerZ);
        }

        [Test]
        public void ParseProfile_WithMultipleBonePoses_PreservesOrderAndValues()
        {
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[],
                ""bonePoses"":[
                    {""id"":""look-left"",""entries"":[
                        {""boneName"":""LeftEye"",""eulerXYZ"":{""x"":0.0,""y"":-15.0,""z"":0.0}}
                    ]},
                    {""id"":""look-right"",""entries"":[
                        {""boneName"":""RightEye"",""eulerXYZ"":{""x"":0.0,""y"":15.0,""z"":0.0}}
                    ]}
                ]
            }";

            var profile = _parser.ParseProfile(json);

            Assert.AreEqual(2, profile.BonePoses.Length);
            var poses = profile.BonePoses.Span;
            Assert.AreEqual("look-left", poses[0].Id);
            Assert.AreEqual("look-right", poses[1].Id);
            Assert.AreEqual(-15.0f, poses[0].Entries.Span[0].EulerY);
            Assert.AreEqual(15.0f, poses[1].Entries.Span[0].EulerY);
        }

        [Test]
        public void ParseProfile_WithMultiByteBoneNames_SurvivesParsing()
        {
            // Req 2.2 と整合: 多バイト boneName が parser を通っても壊れない
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[],
                ""bonePoses"":[
                    {""id"":""multi-byte"",""entries"":[
                        {""boneName"":""頭"",""eulerXYZ"":{""x"":1.0,""y"":2.0,""z"":3.0}},
                        {""boneName"":""左目_あ"",""eulerXYZ"":{""x"":0.0,""y"":4.5,""z"":0.0}}
                    ]}
                ]
            }";

            var profile = _parser.ParseProfile(json);

            Assert.AreEqual(1, profile.BonePoses.Length);
            var entries = profile.BonePoses.Span[0].Entries.Span;
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual("頭", entries[0].BoneName);
            Assert.AreEqual("左目_あ", entries[1].BoneName);
            Assert.AreEqual(4.5f, entries[1].EulerY);
        }

        [Test]
        public void ParseProfile_WithEmptyEntriesArray_ProducesEmptyBonePose()
        {
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[],
                ""bonePoses"":[
                    {""id"":""empty-pose"",""entries"":[]}
                ]
            }";

            var profile = _parser.ParseProfile(json);

            Assert.AreEqual(1, profile.BonePoses.Length);
            var poses = profile.BonePoses.Span;
            Assert.AreEqual("empty-pose", poses[0].Id);
            Assert.AreEqual(0, poses[0].Entries.Length);
        }

        // ================================================================
        // 不正エントリ: boneName 欠落 / null / 空 (Req 7.4)
        // ================================================================

        [Test]
        public void ParseProfile_EntryWithEmptyBoneName_LogsWarningAndSkips()
        {
            // Req 7.4: 空 boneName は警告 + skip + 続行（throw しない）
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[],
                ""bonePoses"":[
                    {""id"":""partial"",""entries"":[
                        {""boneName"":"""",""eulerXYZ"":{""x"":1.0,""y"":2.0,""z"":3.0}},
                        {""boneName"":""LeftEye"",""eulerXYZ"":{""x"":4.0,""y"":5.0,""z"":6.0}}
                    ]}
                ]
            }";

            LogAssert.Expect(LogType.Warning, new Regex("bonePoses"));

            var profile = _parser.ParseProfile(json);

            Assert.AreEqual(1, profile.BonePoses.Length);
            var entries = profile.BonePoses.Span[0].Entries.Span;
            Assert.AreEqual(1, entries.Length, "空 boneName は skip され、有効な 1 件のみ残ること");
            Assert.AreEqual("LeftEye", entries[0].BoneName);
        }

        [Test]
        public void ParseProfile_EntryWithMissingBoneName_LogsWarningAndSkips()
        {
            // boneName フィールドが JSON 上に存在しない → JsonUtility では null/empty として読まれる
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[],
                ""bonePoses"":[
                    {""id"":""partial"",""entries"":[
                        {""eulerXYZ"":{""x"":1.0,""y"":2.0,""z"":3.0}},
                        {""boneName"":""RightEye"",""eulerXYZ"":{""x"":0.0,""y"":7.0,""z"":0.0}}
                    ]}
                ]
            }";

            LogAssert.Expect(LogType.Warning, new Regex("bonePoses"));

            var profile = _parser.ParseProfile(json);

            Assert.AreEqual(1, profile.BonePoses.Length);
            var entries = profile.BonePoses.Span[0].Entries.Span;
            Assert.AreEqual(1, entries.Length);
            Assert.AreEqual("RightEye", entries[0].BoneName);
        }

        [Test]
        public void ParseProfile_EntryWithWhitespaceBoneName_LogsWarningAndSkips()
        {
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[],
                ""bonePoses"":[
                    {""id"":""ws"",""entries"":[
                        {""boneName"":""   "",""eulerXYZ"":{""x"":1.0,""y"":2.0,""z"":3.0}},
                        {""boneName"":""Head"",""eulerXYZ"":{""x"":0.0,""y"":0.0,""z"":0.0}}
                    ]}
                ]
            }";

            LogAssert.Expect(LogType.Warning, new Regex("bonePoses"));

            var profile = _parser.ParseProfile(json);

            Assert.AreEqual(1, profile.BonePoses.Length);
            var entries = profile.BonePoses.Span[0].Entries.Span;
            Assert.AreEqual(1, entries.Length);
            Assert.AreEqual("Head", entries[0].BoneName);
        }

        // ================================================================
        // 不正エントリ: eulerXYZ 不在のエントリは警告 + skip (Req 7.4)
        // ================================================================
        // NOTE: JsonUtility は eulerXYZ フィールド不在のとき struct 既定値 (0,0,0) を埋め、
        //       フィールド有無を判別できない。本テストでは「不正な BonePose 全体（空 entries 配列）」
        //       および「同名 boneName 重複」など Domain ctor が ArgumentException を投げる
        //       不正ペイロードに対して、parser が警告 + skip + 続行することを担保する。

        [Test]
        public void ParseProfile_PoseWithDuplicateBoneNames_LogsWarningAndSkipsPose()
        {
            // Req 1.7 / 7.4: BonePose ctor が同名 boneName 重複を ArgumentException で弾くため、
            // parser はその BonePose 全体を警告 + skip + 続行する（throw しない）。
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[],
                ""bonePoses"":[
                    {""id"":""dup"",""entries"":[
                        {""boneName"":""Head"",""eulerXYZ"":{""x"":1.0,""y"":0.0,""z"":0.0}},
                        {""boneName"":""Head"",""eulerXYZ"":{""x"":2.0,""y"":0.0,""z"":0.0}}
                    ]},
                    {""id"":""ok"",""entries"":[
                        {""boneName"":""LeftEye"",""eulerXYZ"":{""x"":0.0,""y"":5.0,""z"":0.0}}
                    ]}
                ]
            }";

            LogAssert.Expect(LogType.Warning, new Regex("bonePoses"));

            var profile = _parser.ParseProfile(json);

            // 重複 boneName の pose は skip され、有効な pose 1 件のみ残ること
            Assert.AreEqual(1, profile.BonePoses.Length);
            Assert.AreEqual("ok", profile.BonePoses.Span[0].Id);
        }

        [Test]
        public void ParseProfile_BonePosesWithInvalidEntry_DoesNotThrow()
        {
            // Req 7.4 全体保証: 不正エントリで例外が漏れないこと
            var json = @"{
                ""schemaVersion"":""1.0"",
                ""layers"":[
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"":[],
                ""bonePoses"":[
                    {""id"":""partial"",""entries"":[
                        {""boneName"":"""",""eulerXYZ"":{""x"":0.0,""y"":0.0,""z"":0.0}}
                    ]}
                ]
            }";

            LogAssert.Expect(LogType.Warning, new Regex("bonePoses"));

            Assert.DoesNotThrow(() => _parser.ParseProfile(json));
        }

        // ================================================================
        // JsonSchemaDefinition.Profile.BonePoses 定数の存在
        // ================================================================

        [Test]
        public void JsonSchemaDefinition_Profile_BonePoses_ConstantEqualsBonePoses()
        {
            // タスク観測条件: JsonSchemaDefinition.Profile.BonePoses 定数が追加されていること
            Assert.AreEqual("bonePoses", JsonSchemaDefinition.Profile.BonePoses);
        }

        [Test]
        public void JsonSchemaDefinition_Profile_BonePose_FieldNames_AreDefined()
        {
            // BonePose のフィールド名定数（id / entries）と
            // BonePoseEntry のフィールド名定数（boneName / eulerXYZ）が定義されていること
            Assert.AreEqual("id", JsonSchemaDefinition.Profile.BonePose.Id);
            Assert.AreEqual("entries", JsonSchemaDefinition.Profile.BonePose.Entries);
            Assert.AreEqual("boneName", JsonSchemaDefinition.Profile.BonePoseEntry.BoneName);
            Assert.AreEqual("eulerXYZ", JsonSchemaDefinition.Profile.BonePoseEntry.EulerXYZ);
        }
    }
}
