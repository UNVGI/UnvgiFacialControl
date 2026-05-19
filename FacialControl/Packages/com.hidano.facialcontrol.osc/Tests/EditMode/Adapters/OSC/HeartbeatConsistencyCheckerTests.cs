using System.Collections;
using System.Text.RegularExpressions;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Domain.Models;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hidano.FacialControl.Tests.EditMode.Adapters
{
    [TestFixture]
    public sealed class HeartbeatConsistencyCheckerTests
    {
        [Test]
        public void Constructor_WithReceiverMappings_InitializesAllContributeMaskBits()
        {
            var checker = new HeartbeatConsistencyChecker(new[]
            {
                new OscMapping("/avatar/parameters/Happy", "Happy", "emotion"),
                new OscMapping("/avatar/parameters/Angry", "Angry", "emotion")
            });

            Assert.AreEqual(2, checker.BlendShapeCount);
            Assert.IsFalse(checker.HasMismatch);
            AssertMask(checker.SkipMask, false, false);
            AssertMask(checker.ContributeMask, true, true);
        }

        [Test]
        public void Constructor_WithMeshBlendShapeNames_LengthEqualsMeshBlendShapeCount()
        {
            var checker = new HeartbeatConsistencyChecker(
                new[] { "Blink", "Happy", "Extra", "Angry" },
                new[]
                {
                    new OscMapping("/avatar/parameters/Happy", "Happy", "emotion"),
                    new OscMapping("/avatar/parameters/Angry", "Angry", "emotion")
                },
                false);

            Assert.AreEqual(4, checker.BlendShapeCount);
            AssertMask(checker.SkipMask, false, false, false, false);
            AssertMask(checker.ContributeMask, false, true, false, true);
        }

        [Test]
        public void UpdateFromHeartbeat_MappingPresentInSender_ContributeBitSetAtMeshIndex()
        {
            var checker = new HeartbeatConsistencyChecker(
                new[] { "Blink", "Happy", "Angry" },
                new[]
                {
                    new OscMapping("/avatar/parameters/Angry", "Angry", "emotion")
                },
                false);

            checker.UpdateFromHeartbeat(new[] { "Angry" });

            Assert.IsFalse(checker.HasMismatch);
            AssertMask(checker.SkipMask, false, false, false);
            AssertMask(checker.ContributeMask, false, false, true);
        }

        [Test]
        public void UpdateFromHeartbeat_MappingMissingInSender_SkipBitSetAtMeshIndex()
        {
            var checker = new HeartbeatConsistencyChecker(
                new[] { "Blink", "Happy", "Angry" },
                new[]
                {
                    new OscMapping("/avatar/parameters/Happy", "Happy", "emotion"),
                    new OscMapping("/avatar/parameters/Angry", "Angry", "emotion")
                },
                false);

            checker.UpdateFromHeartbeat(new[] { "Happy" });

            Assert.IsTrue(checker.HasMismatch);
            AssertMask(checker.SkipMask, false, false, true);
            AssertMask(checker.ContributeMask, false, true, false);
        }

        [Test]
        public void UpdateFromHeartbeat_PartialMismatch_SkipsOnlyReceiverMissingNames()
        {
            var checker = new HeartbeatConsistencyChecker(new[] { "Happy", "Angry", "Sad" });

            LogAssert.Expect(LogType.Warning, MismatchLog("Extra", "Angry", "Sad"));
            checker.UpdateFromHeartbeat(new[] { "Happy", "Extra" });

            Assert.IsTrue(checker.HasMismatch);
            AssertMask(checker.SkipMask, false, true, true);
            AssertMask(checker.ContributeMask, true, false, false);
        }

        [Test]
        public void UpdateFromHeartbeat_SenderOnlyMismatch_LeavesReceiverContributeMaskEnabled()
        {
            var checker = new HeartbeatConsistencyChecker(new[] { "Happy" });

            LogAssert.Expect(LogType.Warning, MismatchLog("Extra", string.Empty));
            checker.UpdateFromHeartbeat(new[] { "Happy", "Extra" });

            Assert.IsTrue(checker.HasMismatch);
            AssertMask(checker.SkipMask, false);
            AssertMask(checker.ContributeMask, true);
        }

        [Test]
        public void UpdateFromHeartbeat_AllNamesMatch_ClearsPreviousSkipMask()
        {
            var checker = new HeartbeatConsistencyChecker(new[] { "Happy", "Angry" });

            LogAssert.Expect(LogType.Warning, MismatchLog(string.Empty, "Angry"));
            checker.UpdateFromHeartbeat(new[] { "Happy" });

            checker.UpdateFromHeartbeat(new[] { "Angry", "Happy" });

            Assert.IsFalse(checker.HasMismatch);
            AssertMask(checker.SkipMask, false, false);
            AssertMask(checker.ContributeMask, true, true);
        }

        [Test]
        public void UpdateFromHeartbeat_SameMismatchSet_LogsOnlyOnce()
        {
            var checker = new HeartbeatConsistencyChecker(new[] { "Happy", "Angry" });

            LogAssert.Expect(LogType.Warning, MismatchLog("Extra", "Angry"));
            checker.UpdateFromHeartbeat(new[] { "Happy", "Extra" });
            checker.UpdateFromHeartbeat(new[] { "Extra", "Happy" });

            AssertMask(checker.SkipMask, false, true);
        }

        [Test]
        public void UpdateFromHeartbeat_DifferentMismatchSet_LogsAgain()
        {
            var checker = new HeartbeatConsistencyChecker(new[] { "Happy", "Angry", "Sad" });

            LogAssert.Expect(LogType.Warning, MismatchLog(string.Empty, "Angry"));
            checker.UpdateFromHeartbeat(new[] { "Happy", "Sad" });

            LogAssert.Expect(LogType.Warning, MismatchLog(string.Empty, "Sad"));
            checker.UpdateFromHeartbeat(new[] { "Happy", "Angry" });

            AssertMask(checker.SkipMask, false, false, true);
        }

        [Test]
        public void Clear_AfterMismatch_ResetsMaskAndMismatchState()
        {
            var checker = new HeartbeatConsistencyChecker(new[] { "Happy", "Angry" }, warnLogEnabled: false);

            checker.UpdateFromHeartbeat(new[] { "Happy" });
            checker.Clear();

            Assert.IsFalse(checker.HasMismatch);
            AssertMask(checker.SkipMask, false, false);
            AssertMask(checker.ContributeMask, true, true);
        }

        private static Regex MismatchLog(string senderOnlyName, params string[] receiverOnlyNames)
        {
            string pattern = "HeartbeatConsistencyChecker mismatch:";
            if (!string.IsNullOrEmpty(senderOnlyName))
            {
                pattern += ".*senderOnly=.*" + Regex.Escape(senderOnlyName);
            }

            bool addedReceiverOnlyPrefix = false;
            for (int i = 0; i < receiverOnlyNames.Length; i++)
            {
                string name = receiverOnlyNames[i];
                if (!string.IsNullOrEmpty(name))
                {
                    if (!addedReceiverOnlyPrefix)
                    {
                        pattern += ".*receiverOnly=";
                        addedReceiverOnlyPrefix = true;
                    }

                    pattern += ".*" + Regex.Escape(name);
                }
            }

            return new Regex(pattern);
        }

        private static void AssertMask(BitArray mask, params bool[] expected)
        {
            Assert.AreEqual(expected.Length, mask.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], mask[i], $"mask[{i}]");
            }
        }
    }
}
