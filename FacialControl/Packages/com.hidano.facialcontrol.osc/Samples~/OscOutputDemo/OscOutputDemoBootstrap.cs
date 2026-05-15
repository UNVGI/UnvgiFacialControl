using System;
using System.Collections;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Samples.OscOutputDemo
{
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu("FacialControl/Samples/OSC Output Demo Bootstrap")]
    public sealed class OscOutputDemoBootstrap : MonoBehaviour
    {
        [SerializeField]
        private FacialController _facialController;

        [SerializeField]
        private FacialCharacterProfileSO _profile;

        [SerializeField]
        private string _meshObjectName = "ProceduralFace";

        [SerializeField]
        private string[] _blendShapeNames = { "Joy", "Blink", "MouthOpen", "BrowUp" };

        [SerializeField]
        private float _meshScale = 1f;

        private Mesh _mesh;
        private Material _material;

        private void Awake()
        {
            Application.runInBackground = true;

            if (_facialController == null)
            {
                _facialController = GetComponent<FacialController>();
            }

            if (_facialController == null)
            {
                Debug.LogWarning("[OscOutputDemo] FacialController is missing.");
                return;
            }

            SkinnedMeshRenderer renderer = EnsureProceduralRenderer();
            _facialController.SkinnedMeshRenderers = new[] { renderer };
            _facialController.CharacterSO = _profile;
        }

        private void Start()
        {
            if (_facialController != null && _profile != null && !_facialController.IsInitialized)
            {
                _facialController.Initialize();
            }
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }

            if (_material != null)
            {
                Destroy(_material);
            }
        }

        private SkinnedMeshRenderer EnsureProceduralRenderer()
        {
            Transform child = transform.Find(_meshObjectName);
            if (child == null)
            {
                child = new GameObject(_meshObjectName).transform;
                child.SetParent(transform, worldPositionStays: false);
            }

            var renderer = child.GetComponent<SkinnedMeshRenderer>();
            if (renderer == null)
            {
                renderer = child.gameObject.AddComponent<SkinnedMeshRenderer>();
            }

            renderer.sharedMesh = CreateMesh();
            renderer.sharedMaterial = CreateMaterial();
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return renderer;
        }

        private Mesh CreateMesh()
        {
            _mesh = new Mesh
            {
                name = "OscOutputDemoFaceMesh"
            };

            float scale = Mathf.Max(0.01f, _meshScale);
            var vertices = new[]
            {
                new Vector3(-0.5f, -0.4f, 0f) * scale,
                new Vector3(0.5f, -0.4f, 0f) * scale,
                new Vector3(0f, 0.6f, 0f) * scale
            };

            _mesh.vertices = vertices;
            _mesh.triangles = new[] { 0, 1, 2 };
            _mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            _mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 1f)
            };

            string[] names = _blendShapeNames == null || _blendShapeNames.Length == 0
                ? new[] { "Joy", "Blink", "MouthOpen", "BrowUp" }
                : _blendShapeNames;

            for (int i = 0; i < names.Length; i++)
            {
                string blendShapeName = string.IsNullOrWhiteSpace(names[i])
                    ? "BlendShape" + i.ToString()
                    : names[i];

                var deltaVertices = new Vector3[vertices.Length];
                float direction = (i % 2 == 0) ? 1f : -1f;
                deltaVertices[0] = new Vector3(-0.04f * direction, 0.02f * i, 0f);
                deltaVertices[1] = new Vector3(0.04f * direction, 0.02f * i, 0f);
                deltaVertices[2] = new Vector3(0f, 0.05f + (0.01f * i), 0f);
                _mesh.AddBlendShapeFrame(blendShapeName, 100f, deltaVertices, null, null);
            }

            _mesh.RecalculateBounds();
            return _mesh;
        }

        private Material CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                Debug.LogWarning("[OscOutputDemo] No compatible material shader was found.");
                return null;
            }

            _material = new Material(shader);
            _material.name = "OscOutputDemoFaceMaterial";
            _material.color = new Color(0.2f, 0.75f, 0.85f, 1f);
            return _material;
        }
    }

    [Serializable]
    [FacialAdapterBinding("OSC Output Demo Signal")]
    public sealed class OscOutputDemoSignalBinding : AdapterBindingBase
    {
        [SerializeField]
        private int _blendShapeCount = 4;

        [SerializeField]
        private float _cycleSeconds = 4f;

        private DemoSignalState _state;

        public override void OnStart(in AdapterBuildContext ctx)
        {
            if (!AdapterSlug.TryParse(Slug, out AdapterSlug slug))
            {
                Debug.LogWarning("[OscOutputDemo] Demo signal slug is invalid.");
                return;
            }

            int blendShapeCount = Mathf.Max(1, _blendShapeCount);
            _state = new DemoSignalState(Mathf.Max(0.25f, _cycleSeconds));
            ctx.InputSourceRegistry.Register(slug, "blendshape", new DemoBlendShapeSource(slug.Value + ":blendshape", blendShapeCount, _state));
            ctx.InputSourceRegistry.Register(slug, "eye_look", new DemoGazeSource(slug.Value + ":eye_look", _state));
        }
    }

    internal sealed class DemoSignalState
    {
        private const float Tau = 6.28318530718f;

        private readonly float _cycleSeconds;
        private float _time;

        public DemoSignalState(float cycleSeconds)
        {
            _cycleSeconds = cycleSeconds;
        }

        public float Joy { get; private set; }
        public float Blink { get; private set; }
        public float MouthOpen { get; private set; }
        public float BrowUp { get; private set; }
        public float GazeX { get; private set; }
        public float GazeY { get; private set; }

        public void Advance(float deltaTime)
        {
            _time += Mathf.Max(0f, deltaTime);
            float t = (_time / _cycleSeconds) * Tau;

            Joy = 0.5f + (0.5f * Mathf.Sin(t));
            Blink = Mathf.Clamp01(Mathf.Sin(t * 2f) * 1.3f - 0.25f);
            MouthOpen = 0.25f + (0.75f * Mathf.Clamp01(Mathf.Sin(t + 1.4f)));
            BrowUp = 0.35f + (0.65f * Mathf.Clamp01(Mathf.Sin(t - 0.8f)));
            GazeX = Mathf.Sin(t * 0.75f) * 0.8f;
            GazeY = Mathf.Cos(t * 0.5f) * 0.45f;
        }
    }

    internal sealed class DemoBlendShapeSource : IInputSource
    {
        private readonly DemoSignalState _state;
        private readonly BitArray _contributeMask;

        public DemoBlendShapeSource(string id, int blendShapeCount, DemoSignalState state)
        {
            Id = id;
            BlendShapeCount = blendShapeCount;
            _state = state;
            _contributeMask = new BitArray(blendShapeCount, true);
        }

        public string Id { get; }
        public InputSourceType Type => InputSourceType.ValueProvider;
        public int BlendShapeCount { get; }
        public BitArray ContributeMask => _contributeMask;

        public void Tick(float deltaTime)
        {
            _state.Advance(deltaTime);
        }

        public bool TryWriteValues(Span<float> output)
        {
            if (output.Length > 0) output[0] = _state.Joy;
            if (output.Length > 1) output[1] = _state.Blink;
            if (output.Length > 2) output[2] = _state.MouthOpen;
            if (output.Length > 3) output[3] = _state.BrowUp;
            return true;
        }
    }

    internal sealed class DemoGazeSource : IInputSource, IAnalogInputSource
    {
        private readonly DemoSignalState _state;
        private readonly BitArray _contributeMask = new BitArray(0);

        public DemoGazeSource(string id, DemoSignalState state)
        {
            Id = id;
            _state = state;
        }

        public string Id { get; }
        public InputSourceType Type => InputSourceType.ValueProvider;
        public int BlendShapeCount => 0;
        public BitArray ContributeMask => _contributeMask;
        public bool IsValid => true;
        public int AxisCount => 2;

        public void Tick(float deltaTime)
        {
        }

        public bool TryWriteValues(Span<float> output)
        {
            return false;
        }

        public bool TryReadScalar(out float value)
        {
            value = _state.GazeX;
            return true;
        }

        public bool TryReadVector2(out float x, out float y)
        {
            x = _state.GazeX;
            y = _state.GazeY;
            return true;
        }

        public bool TryReadAxes(Span<float> output)
        {
            if (output.Length == 0)
            {
                return false;
            }

            output[0] = _state.GazeX;
            if (output.Length > 1)
            {
                output[1] = _state.GazeY;
            }

            return true;
        }
    }
}
