using System.Collections.Generic;
using Hidano.FacialControl.Adapters.AdapterBindings;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using UnityEngine;

namespace Hidano.FacialControl.Samples.OscReceiverDemo
{
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu("FacialControl/Samples/OSC Receiver Demo Bootstrap")]
    public sealed class OscReceiverDemoBootstrap : MonoBehaviour
    {
        [SerializeField]
        private FacialController _facialController;

        [SerializeField]
        private FacialCharacterProfileSO _profile;

        [SerializeField]
        private string _meshObjectName = "ProceduralFace";

        [SerializeField]
        private float _meshScale = 1f;

        private static readonly string[] s_fallbackBlendShapeNames = { "Joy", "Blink", "MouthOpen", "BrowUp" };

        private Mesh _mesh;
        private Material _material;

        private void Awake()
        {
            UnityEngine.Application.runInBackground = true;

            if (_facialController == null)
            {
                _facialController = GetComponent<FacialController>();
            }

            if (_facialController == null)
            {
                Debug.LogWarning("[OscReceiverDemo] FacialController is missing.");
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
                name = "OscReceiverDemoFaceMesh"
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

            string[] names = ResolveBlendShapeNames();

            for (int i = 0; i < names.Length; i++)
            {
                string blendShapeName = string.IsNullOrWhiteSpace(names[i])
                    ? "BlendShape" + i.ToString()
                    : names[i];

                var deltaVertices = new Vector3[vertices.Length];
                float direction = (i % 2 == 0) ? 1f : -1f;
                deltaVertices[0] = new Vector3(-0.05f * direction, 0.02f * i, 0f);
                deltaVertices[1] = new Vector3(0.05f * direction, 0.02f * i, 0f);
                deltaVertices[2] = new Vector3(0f, 0.08f + (0.015f * i), 0f);
                _mesh.AddBlendShapeFrame(blendShapeName, 100f, deltaVertices, null, null);
            }

            _mesh.RecalculateBounds();
            return _mesh;
        }

        // Profile の OscAdapterBinding が持つ Mappings から Normal_BlendShape 経路の expressionId を抽出して使う。
        // Profile 未設定 / OscAdapterBinding が無い / Normal_BlendShape entry が無い場合はデモ用 fallback を返す。
        private string[] ResolveBlendShapeNames()
        {
            if (_profile == null)
            {
                return s_fallbackBlendShapeNames;
            }

            IReadOnlyList<AdapterBindingBase> bindings = _profile.AdapterBindings;
            if (bindings == null)
            {
                return s_fallbackBlendShapeNames;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i] is OscAdapterBinding receiver && receiver.Mappings != null)
                {
                    var names = new List<string>();
                    for (int j = 0; j < receiver.Mappings.Count; j++)
                    {
                        OscMappingEntry entry = receiver.Mappings[j];
                        if (entry == null) continue;
                        if (entry.mode == OscMappingMode.Normal_BlendShape
                            && !string.IsNullOrWhiteSpace(entry.expressionId))
                        {
                            names.Add(entry.expressionId);
                        }
                    }

                    if (names.Count > 0)
                    {
                        return names.ToArray();
                    }
                }
            }

            return s_fallbackBlendShapeNames;
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
                Debug.LogWarning("[OscReceiverDemo] No compatible material shader was found.");
                return null;
            }

            _material = new Material(shader);
            _material.name = "OscReceiverDemoFaceMaterial";
            _material.color = new Color(0.9f, 0.72f, 0.25f, 1f);
            return _material;
        }
    }
}
