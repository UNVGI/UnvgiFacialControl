using System;
using System.Collections.Generic;
using System.Reflection;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.LipSync.Adapters.Devices;
using Hidano.FacialControl.LipSync.Adapters.PhonemeEntries;
using UnityEngine;

namespace Hidano.FacialControl.LipSync.Adapters
{
    [Serializable]
    [FacialAdapterBinding(displayName: "uLipSync")]
    public sealed class ULipSyncAdapterBinding : AdapterBindingBase
    {
        private const string DefaultSlug = "ulipsync";
        private const string DefaultProfileResourcePath =
            "FacialControl/LipSync/Default uLipSync Profile";

        [SerializeField]
        private DeviceDescriptor _deviceDescriptor;

        [SerializeField]
        private uLipSync.Profile _analyzerProfile;

        [SerializeReference]
        private List<PhonemeEntryBase> _phonemeEntries = new List<PhonemeEntryBase>();

        [SerializeField]
        private string _targetMeshHint;

        [SerializeField]
        private float _maxWeightScale = 1f;

        [NonSerialized]
        private IAsioDriverEnumerator _asioEnumerator;

        [NonSerialized]
        private IMicrophoneDeviceEnumerator _microphoneEnumerator;

        [NonSerialized]
        private AudioSource _audioSource;

        [NonSerialized]
        private uLipSync.uLipSync _analyzer;

        [NonSerialized]
        private uLipSync.uLipSyncMicrophone _microphoneInput;

        [NonSerialized]
        private uLipSync.uLipSyncAsioInput _asioInput;

        [NonSerialized]
        private ULipSyncEventBridge _eventBridge;

        [NonSerialized]
        private ULipSyncProvider _provider;

        [NonSerialized]
        private LipSyncInputSource _inputSource;

        [NonSerialized]
        private bool _started;

        [NonSerialized]
        private bool _addedAudioSource;

        public ULipSyncAdapterBinding()
        {
            Slug = DefaultSlug;
        }

        public uLipSync.uLipSync Analyzer => _analyzer;

        public ULipSyncProvider Provider => _provider;

        public LipSyncInputSource InputSource => _inputSource;

        public bool IsStarted => _started;

        public void Configure(
            DeviceDescriptor deviceDescriptor,
            uLipSync.Profile analyzerProfile,
            IReadOnlyList<PhonemeEntryBase> phonemeEntries,
            IAsioDriverEnumerator asioEnumerator = null,
            IMicrophoneDeviceEnumerator microphoneEnumerator = null)
        {
            _deviceDescriptor = deviceDescriptor;
            _analyzerProfile = analyzerProfile;
            _phonemeEntries = phonemeEntries == null
                ? new List<PhonemeEntryBase>()
                : new List<PhonemeEntryBase>(phonemeEntries);
            _asioEnumerator = asioEnumerator;
            _microphoneEnumerator = microphoneEnumerator;
        }

        public override void OnStart(in AdapterBuildContext ctx)
        {
            if (_started)
            {
                return;
            }

            if (ctx.HostGameObject == null)
            {
                Debug.LogError("[ULipSyncAdapterBinding] HostGameObject is null.");
                return;
            }

            if (_phonemeEntries == null || _phonemeEntries.Count == 0)
            {
                Debug.LogWarning(
                    $"[ULipSyncAdapterBinding] No phoneme entries are configured. Slug='{Slug}'");
                return;
            }

            if (!AdapterSlug.TryParse(Slug, out var slug))
            {
                Debug.LogError(
                    $"[ULipSyncAdapterBinding] Slug '{Slug ?? "<null>"}' is not a valid AdapterSlug.");
                return;
            }

            DeviceResolution resolution = DeviceResolver.Resolve(
                _deviceDescriptor,
                _asioEnumerator ?? new DefaultAsioDriverEnumerator(),
                _microphoneEnumerator ?? new DefaultMicrophoneDeviceEnumerator());
            if (resolution.Kind == DeviceKind.Unresolved)
            {
                Debug.LogError(
                    $"[ULipSyncAdapterBinding] Device '{_deviceDescriptor.DeviceName}' could not be resolved. " +
                    $"DisambiguatorIndex={_deviceDescriptor.DisambiguatorIndex}, " +
                    $"ASIO=[{string.Join(", ", resolution.AvailableAsio)}], " +
                    $"Microphone=[{string.Join(", ", resolution.AvailableMic)}]");
                return;
            }

            uLipSync.Profile profile = ResolveAnalyzerProfile();
            if (profile == null)
            {
                Debug.LogError(
                    "[ULipSyncAdapterBinding] Analyzer profile is not configured and the default profile " +
                    "could not be loaded.");
                return;
            }

            PhonemeSnapshot[] snapshots = BuildSnapshots(ctx);
            try
            {
                _audioSource = ctx.HostGameObject.GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = ctx.HostGameObject.AddComponent<AudioSource>();
                    _addedAudioSource = true;
                }

                _analyzer = ctx.HostGameObject.AddComponent<uLipSync.uLipSync>();
                _analyzer.profile = profile;

                AddInputComponent(ctx.HostGameObject, resolution);

                _eventBridge = new ULipSyncEventBridge(_analyzer);
                _provider = new ULipSyncProvider(_eventBridge, snapshots, ctx.BlendShapeNames.Count);
                _inputSource = new LipSyncInputSource(_provider, ctx.BlendShapeNames.Count);
                ctx.InputSourceRegistry.Register(slug, _inputSource);

                _provider.RequestZeroOutputForNextFrame();
                _started = true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ULipSyncAdapterBinding] OnStart failed: {exception}");
                RollbackStartedResources();
            }
        }

        public override void Dispose()
        {
            RollbackStartedResources();
        }

        private uLipSync.Profile ResolveAnalyzerProfile()
        {
            if (_analyzerProfile != null)
            {
                return _analyzerProfile;
            }

            return Resources.Load<uLipSync.Profile>(DefaultProfileResourcePath);
        }

        private void AddInputComponent(GameObject hostGameObject, DeviceResolution resolution)
        {
            if (resolution.Kind == DeviceKind.Microphone)
            {
                _microphoneInput = hostGameObject.AddComponent<uLipSync.uLipSyncMicrophone>();
                _microphoneInput.index = resolution.ResolvedIndex;
                _microphoneInput.UpdateMicInfo();
                return;
            }

            if (resolution.Kind == DeviceKind.Asio)
            {
                _asioInput = hostGameObject.AddComponent<uLipSync.uLipSyncAsioInput>();
                SetAsioField("lipSync", _analyzer);
                SetAsioField("selectedDeviceIndex", resolution.ResolvedIndex);
            }
        }

        private void SetAsioField(string fieldName, object value)
        {
            if (_asioInput == null)
            {
                return;
            }

            FieldInfo field = typeof(uLipSync.uLipSyncAsioInput).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(_asioInput, value);
        }

        private PhonemeSnapshot[] BuildSnapshots(in AdapterBuildContext ctx)
        {
            var snapshots = new List<PhonemeSnapshot>(_phonemeEntries.Count);
            SkinnedMeshRenderer renderer = null;

            for (int i = 0; i < _phonemeEntries.Count; i++)
            {
                PhonemeEntryBase entry = _phonemeEntries[i];
                if (entry == null)
                {
                    Debug.LogWarning($"[ULipSyncAdapterBinding] Phoneme entry at index {i} is null. Skipping.");
                    continue;
                }

                if (string.IsNullOrEmpty(entry.PhonemeId))
                {
                    Debug.LogWarning(
                        $"[ULipSyncAdapterBinding] PhonemeId is empty at index {i}. Skipping.");
                    continue;
                }

                float[] weights = new float[ctx.BlendShapeNames.Count];
                if (entry is BlendShapePhonemeEntry blendShapeEntry)
                {
                    FillBlendShapeSnapshot(blendShapeEntry, ctx.BlendShapeNames, weights);
                    snapshots.Add(new PhonemeSnapshot(entry.PhonemeId, weights));
                    continue;
                }

                if (entry is AnimationClipPhonemeEntry animationEntry)
                {
                    renderer = renderer == null ? ResolveRenderer(ctx.HostGameObject) : renderer;
                    if (TryFillAnimationClipSnapshot(animationEntry, renderer, ctx, weights))
                    {
                        snapshots.Add(new PhonemeSnapshot(entry.PhonemeId, weights));
                    }
                }
            }

            return snapshots.ToArray();
        }

        private void FillBlendShapeSnapshot(
            BlendShapePhonemeEntry entry,
            IReadOnlyList<string> blendShapeNames,
            float[] weights)
        {
            int index = FindBlendShapeIndex(blendShapeNames, entry.BlendShapeName);
            if (index < 0)
            {
                Debug.LogWarning(
                    $"[ULipSyncAdapterBinding] BlendShape '{entry.BlendShapeName}' could not be resolved.");
                return;
            }

            weights[index] = NormalizeWeight(entry.MaxWeight);
        }

        private bool TryFillAnimationClipSnapshot(
            AnimationClipPhonemeEntry entry,
            SkinnedMeshRenderer renderer,
            in AdapterBuildContext ctx,
            float[] weights)
        {
            if (entry.Clip == null)
            {
                Debug.LogWarning(
                    $"[ULipSyncAdapterBinding] AnimationClip for phoneme '{entry.PhonemeId}' is null. Skipping.");
                return false;
            }

            if (renderer == null || renderer.sharedMesh == null)
            {
                Debug.LogWarning(
                    $"[ULipSyncAdapterBinding] No SkinnedMeshRenderer was found for phoneme '{entry.PhonemeId}'.");
                return false;
            }

            Mesh mesh = renderer.sharedMesh;
            int blendShapeCount = mesh.blendShapeCount;
            var previousWeights = new float[blendShapeCount];
            for (int i = 0; i < blendShapeCount; i++)
            {
                previousWeights[i] = renderer.GetBlendShapeWeight(i);
                renderer.SetBlendShapeWeight(i, 0f);
            }

            entry.Clip.SampleAnimation(ctx.HostGameObject, 0f);

            float scale = NormalizeWeight(entry.MaxWeight);
            for (int i = 0; i < ctx.BlendShapeNames.Count; i++)
            {
                int meshIndex = mesh.GetBlendShapeIndex(ctx.BlendShapeNames[i]);
                if (meshIndex >= 0)
                {
                    weights[i] = Mathf.Clamp01((renderer.GetBlendShapeWeight(meshIndex) / 100f) * scale);
                }
            }

            for (int i = 0; i < blendShapeCount; i++)
            {
                renderer.SetBlendShapeWeight(i, previousWeights[i]);
            }

            return true;
        }

        private SkinnedMeshRenderer ResolveRenderer(GameObject hostGameObject)
        {
            SkinnedMeshRenderer[] renderers = hostGameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(_targetMeshHint))
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (string.Equals(renderers[i].name, _targetMeshHint, StringComparison.Ordinal)
                        || string.Equals(renderers[i].gameObject.name, _targetMeshHint, StringComparison.Ordinal)
                        || string.Equals(GetRelativePath(hostGameObject.transform, renderers[i].transform),
                            _targetMeshHint,
                            StringComparison.Ordinal))
                    {
                        return renderers[i];
                    }
                }

                Debug.LogWarning(
                    $"[ULipSyncAdapterBinding] Target mesh hint '{_targetMeshHint}' was not resolved. " +
                    "Using the first SkinnedMeshRenderer.");
            }

            return renderers[0];
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (root == null || target == null)
            {
                return string.Empty;
            }

            if (root == target)
            {
                return string.Empty;
            }

            var names = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }

            if (current != root)
            {
                return target.name;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private float NormalizeWeight(float maxWeight)
        {
            return Mathf.Clamp01((maxWeight / 100f) * Mathf.Max(0f, _maxWeightScale));
        }

        private static int FindBlendShapeIndex(IReadOnlyList<string> blendShapeNames, string blendShapeName)
        {
            if (string.IsNullOrEmpty(blendShapeName))
            {
                return -1;
            }

            for (int i = 0; i < blendShapeNames.Count; i++)
            {
                if (string.Equals(blendShapeNames[i], blendShapeName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private void RollbackStartedResources()
        {
            _started = false;
            _inputSource = null;

            if (_provider != null)
            {
                _provider.Dispose();
                _provider = null;
            }

            if (_eventBridge != null)
            {
                _eventBridge.Dispose();
                _eventBridge = null;
            }

            if (_microphoneInput != null)
            {
                UnityEngine.Object.Destroy(_microphoneInput);
                _microphoneInput = null;
            }

            if (_asioInput != null)
            {
                _asioInput.StopRecord();
                UnityEngine.Object.Destroy(_asioInput);
                _asioInput = null;
            }

            if (_analyzer != null)
            {
                UnityEngine.Object.Destroy(_analyzer);
                _analyzer = null;
            }

            if (_addedAudioSource && _audioSource != null)
            {
                UnityEngine.Object.Destroy(_audioSource);
            }

            _audioSource = null;
            _addedAudioSource = false;
        }
    }
}
