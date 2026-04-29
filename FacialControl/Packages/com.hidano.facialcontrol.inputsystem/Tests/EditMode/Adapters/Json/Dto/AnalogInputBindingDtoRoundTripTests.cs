using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Hidano.FacialControl.Adapters.Json.Dto;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.Json.Dto
{
    [TestFixture]
    public class AnalogInputBindingDtoRoundTripTests
    {
        [Test]
        public void ProfileDto_JsonUtilityRoundTrip_PreservesAllFields()
        {
            var dto = new AnalogInputBindingProfileDto
            {
                version = "1.0.0",
                bindings = new List<AnalogBindingEntryDto>
                {
                    new AnalogBindingEntryDto
                    {
                        sourceId = "analog-bonepose.right_stick",
                        sourceAxis = 1,
                        targetKind = "bonepose",
                        targetIdentifier = "RightEye",
                        targetAxis = "Y",
                        mapping = new AnalogMappingDto
                        {
                            deadZone = 0.1f,
                            scale = 30.0f,
                            offset = 0.5f,
                            curveType = "linear",
                            curveKeyFrames = new List<CurveKeyFrameDto>(),
                            invert = false,
                            min = -45.0f,
                            max = 45.0f
                        }
                    }
                }
            };

            var json = JsonUtility.ToJson(dto, prettyPrint: false);
            var loaded = JsonUtility.FromJson<AnalogInputBindingProfileDto>(json);

            Assert.AreEqual(dto.version, loaded.version);
            Assert.AreEqual(1, loaded.bindings.Count);

            var src = dto.bindings[0];
            var dst = loaded.bindings[0];
            Assert.AreEqual(src.sourceId, dst.sourceId);
            Assert.AreEqual(src.sourceAxis, dst.sourceAxis);
            Assert.AreEqual(src.targetKind, dst.targetKind);
            Assert.AreEqual(src.targetIdentifier, dst.targetIdentifier);
            Assert.AreEqual(src.targetAxis, dst.targetAxis);

            Assert.AreEqual(src.mapping.deadZone, dst.mapping.deadZone, 1e-5f);
            Assert.AreEqual(src.mapping.scale, dst.mapping.scale, 1e-5f);
            Assert.AreEqual(src.mapping.offset, dst.mapping.offset, 1e-5f);
            Assert.AreEqual(src.mapping.curveType, dst.mapping.curveType);
            Assert.AreEqual(src.mapping.invert, dst.mapping.invert);
            Assert.AreEqual(src.mapping.min, dst.mapping.min, 1e-5f);
            Assert.AreEqual(src.mapping.max, dst.mapping.max, 1e-5f);
        }

        [Test]
        public void CurveKeyFrameDto_JsonUtilityRoundTrip_PreservesAllFields()
        {
            var mapping = new AnalogMappingDto
            {
                deadZone = 0f,
                scale = 1f,
                offset = 0f,
                curveType = "custom",
                curveKeyFrames = new List<CurveKeyFrameDto>
                {
                    new CurveKeyFrameDto
                    {
                        time = 0.25f, value = 0.5f,
                        inTangent = 1.0f, outTangent = 2.0f,
                        inWeight = 0.3f, outWeight = 0.4f,
                        weightedMode = 3
                    }
                },
                invert = true,
                min = -1f,
                max = 1f
            };

            var json = JsonUtility.ToJson(mapping, prettyPrint: false);
            var loaded = JsonUtility.FromJson<AnalogMappingDto>(json);

            Assert.AreEqual(1, loaded.curveKeyFrames.Count);
            var k = loaded.curveKeyFrames[0];
            Assert.AreEqual(0.25f, k.time, 1e-5f);
            Assert.AreEqual(0.5f, k.value, 1e-5f);
            Assert.AreEqual(1.0f, k.inTangent, 1e-5f);
            Assert.AreEqual(2.0f, k.outTangent, 1e-5f);
            Assert.AreEqual(0.3f, k.inWeight, 1e-5f);
            Assert.AreEqual(0.4f, k.outWeight, 1e-5f);
            Assert.AreEqual(3, k.weightedMode);
            Assert.AreEqual(true, loaded.invert);
        }

        [Test]
        public void ProfileDto_EmptyBindings_RoundTrip()
        {
            var dto = new AnalogInputBindingProfileDto
            {
                version = "0.1.0",
                bindings = new List<AnalogBindingEntryDto>()
            };

            var json = JsonUtility.ToJson(dto, prettyPrint: false);
            var loaded = JsonUtility.FromJson<AnalogInputBindingProfileDto>(json);

            Assert.AreEqual("0.1.0", loaded.version);
            Assert.IsNotNull(loaded.bindings);
            Assert.AreEqual(0, loaded.bindings.Count);
        }
    }
}
