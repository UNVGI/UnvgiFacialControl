using System.Collections.Generic;
using System.IO;
using Hidano.FacialControl.Adapters.FileSystem;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    public abstract class FacialCharacterProfileSO : UnityEngine.ScriptableObject, IFacialCharacterProfile
    {
        public const string StreamingAssetsRootFolder = "FacialControl";
        public const string ProfileJsonFileName = "profile.json";

        [SerializeField] protected string _schemaVersion = "2.0";
        [SerializeField] protected List<LayerDefinitionSerializable> _layers = new List<LayerDefinitionSerializable>();
        [SerializeField] protected List<ExpressionSerializable> _expressions = new List<ExpressionSerializable>();
        [SerializeField] protected List<string> _rendererPaths = new List<string>();

#if UNITY_EDITOR
        [SerializeField] protected GameObject _referenceModel;
        public GameObject ReferenceModel { get => _referenceModel; set => _referenceModel = value; }
#endif

        public string CharacterAssetName => name;
        public string SchemaVersion { get => _schemaVersion; set => _schemaVersion = value; }
        public List<LayerDefinitionSerializable> Layers => _layers;
        public List<ExpressionSerializable> Expressions => _expressions;
        public List<string> RendererPaths => _rendererPaths;

        public virtual FacialProfile BuildFallbackProfile()
        {
            return FacialCharacterProfileConverter.ToFacialProfile(
                _schemaVersion, _layers, _expressions, _rendererPaths);
        }

        public static string GetStreamingAssetsProfilePath(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName)) return null;
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, StreamingAssetsRootFolder, assetName, ProfileJsonFileName);
        }

        public virtual FacialProfile LoadProfile()
        {
            string path = GetStreamingAssetsProfilePath(CharacterAssetName);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return BuildFallbackProfile();
            try
            {
                var repo = new FileProfileRepository(new SystemTextJsonParser());
                return repo.LoadProfile(path);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(name + ": StreamingAssets JSON load failed, using SO data. " + ex.Message);
                return BuildFallbackProfile();
            }
        }
    }
}