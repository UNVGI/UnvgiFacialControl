using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hidano.FacialControl.Adapters.Input;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.InputSystem;

namespace Hidano.FacialControl.Samples.EditorTools
{
    /// <summary>
    /// Multi Source Blend Demo サンプル用の Scene / FacialProfileSO / InputBindingProfileSO を
    /// Assets/Samples 配下に生成し、Packages/.../Samples~ にもミラーするツール。
    /// </summary>
    /// <remarks>
    /// 設計方針:
    /// - サンプルがユーザーの手元で "モデルを配置するだけで動く" 状態にするのが目的。
    /// - Scene には Animator / FacialController / InputFacialControllerExtension /
    ///   FacialInputBinder / MultiSourceBlendDemoHUD を付けた "Character" GameObject を配置。
    /// - HUD の _profileJson フィールドに同梱 JSON (TextAsset) を割り当て、Awake 時に
    ///   FacialController.InitializeWithProfile で起動する。
    /// - FacialProfileSO は設計上のパターン呈示のため空で生成（JsonFilePath 未設定）。
    ///   HUD の TextAsset ブートストラップで初期化するため、必須ではない。
    /// </remarks>
    public static class GenerateMultiSourceBlendDemoSample
    {
        private const string SampleRoot =
            "Assets/Samples/FacialControl InputSystem/0.1.0-preview.1/Multi Source Blend Demo";

        private const string SamplesMirrorRoot =
            "Packages/com.hidano.facialcontrol.inputsystem/Samples~/MultiSourceBlendDemo";

        private const string ScenePath = SampleRoot + "/MultiSourceBlendDemo.unity";
        private const string ProfileSOPath = SampleRoot + "/MultiSourceBlendDemoProfile.asset";
        private const string BindingSOPath = SampleRoot + "/MultiSourceBlendDemoInputBinding.asset";
        private const string JsonAssetPath = SampleRoot + "/multi_source_blend_demo.json";

        private const string DefaultActionAssetPath =
            "Packages/com.hidano.facialcontrol/Runtime/Adapters/Input/FacialControlDefaultActions.inputactions";

        private const string SentinelKey =
            "Hidano.FacialControl.Samples.GenerateMultiSourceBlendDemoSample.Done";

        [InitializeOnLoadMethod]
        private static void AutoRunOnce()
        {
            // InitializeOnLoadMethod はコンパイル直後に呼ばれるためアセット生成には早すぎる。
            // EditorApplication.delayCall で 1 tick 遅らせてから実行する。
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(SentinelKey, false))
                {
                    return;
                }
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                {
                    EditorPrefs.SetBool(SentinelKey, true);
                    return;
                }
                Run();
                EditorPrefs.SetBool(SentinelKey, true);
            };
        }

        [MenuItem("Tools/FacialControl/Generate Multi Source Blend Demo Sample")]
        public static void Run()
        {
            EnsureFolder(SampleRoot);

            var profileSO = CreateOrLoadProfileSO();
            var bindingSO = CreateOrLoadBindingSO();

            // Scene から SO への参照が null シリアライズされないよう、先にディスクへ flush。
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            CreateSampleScene(profileSO, bindingSO);
            MirrorToSamplesTilde();

            Debug.Log(
                "GenerateMultiSourceBlendDemoSample: Scene / FacialProfileSO / InputBindingProfileSO を生成し、" +
                $"Samples~ にミラーしました。Scene: {ScenePath}");
        }

        private static FacialProfileSO CreateOrLoadProfileSO()
        {
            var existing = AssetDatabase.LoadAssetAtPath<FacialProfileSO>(ProfileSOPath);
            if (existing != null)
            {
                return existing;
            }

            var so = ScriptableObject.CreateInstance<FacialProfileSO>();
            so.JsonFilePath = string.Empty;
            so.SchemaVersion = "1.0";
            so.LayerCount = 1;
            so.ExpressionCount = 4;
            so.RendererPaths = new string[0];
            AssetDatabase.CreateAsset(so, ProfileSOPath);
            return so;
        }

        private static InputBindingProfileSO CreateOrLoadBindingSO()
        {
            var existing = AssetDatabase.LoadAssetAtPath<InputBindingProfileSO>(BindingSOPath);
            if (existing != null)
            {
                return existing;
            }

            var so = ScriptableObject.CreateInstance<InputBindingProfileSO>();

            var actionAsset =
                AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(DefaultActionAssetPath);
            if (actionAsset == null)
            {
                Debug.LogWarning(
                    $"GenerateMultiSourceBlendDemoSample: デフォルト InputActionAsset が見つかりません: {DefaultActionAssetPath}");
            }

            var serialized = new SerializedObject(so);
            serialized.FindProperty("_actionAsset").objectReferenceValue = actionAsset;
            serialized.FindProperty("_actionMapName").stringValue = "Expression";
            serialized.FindProperty("_inputSourceCategory").enumValueIndex =
                (int)InputSourceCategory.Keyboard;

            var bindings = serialized.FindProperty("_bindings");
            bindings.arraySize = 0;
            AddBinding(bindings, "Trigger1", "smile");
            AddBinding(bindings, "Trigger2", "angry");
            AddBinding(bindings, "Trigger3", "surprise");
            AddBinding(bindings, "Trigger4", "troubled");

            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(so, BindingSOPath);
            return so;
        }

        private static void AddBinding(SerializedProperty bindings, string actionName, string expressionId)
        {
            bindings.arraySize++;
            var element = bindings.GetArrayElementAtIndex(bindings.arraySize - 1);
            element.FindPropertyRelative("actionName").stringValue = actionName;
            element.FindPropertyRelative("expressionId").stringValue = expressionId;
        }

        private static void CreateSampleScene(FacialProfileSO profileSO, InputBindingProfileSO bindingSO)
        {
            var existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (existing != null)
            {
                Debug.Log($"GenerateMultiSourceBlendDemoSample: Scene は既に存在するためスキップ: {ScenePath}");
                return;
            }

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var character = new GameObject("Character");
            Undo.RegisterCreatedObjectUndo(character, "Create Character");

            var animator = character.AddComponent<Animator>();
            animator.applyRootMotion = false;

            var controller = character.AddComponent<FacialController>();
            controller.ProfileSO = null; // HUD ブートストラップで初期化するため明示的に未設定

            character.AddComponent<InputFacialControllerExtension>();

            var binder = character.AddComponent<FacialInputBinder>();
            {
                var so = new SerializedObject(binder);
                so.FindProperty("_facialController").objectReferenceValue = controller;
                so.FindProperty("_bindingProfile").objectReferenceValue = bindingSO;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var hud = character.AddComponent<MultiSourceBlendDemoHUD>();
            {
                var so = new SerializedObject(hud);
                so.FindProperty("_facialController").objectReferenceValue = controller;
                var jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonAssetPath);
                so.FindProperty("_profileJson").objectReferenceValue = jsonAsset;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            character.transform.position = Vector3.zero;

            // Main Camera を少し引く
            var camera = GameObject.Find("Main Camera");
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 1.4f, -2.5f);
                camera.transform.rotation = Quaternion.Euler(5f, 0f, 0f);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static void MirrorToSamplesTilde()
        {
            var files = new (string src, string dst)[]
            {
                (ProfileSOPath, SamplesMirrorRoot + "/MultiSourceBlendDemoProfile.asset"),
                (ProfileSOPath + ".meta", SamplesMirrorRoot + "/MultiSourceBlendDemoProfile.asset.meta"),
                (BindingSOPath, SamplesMirrorRoot + "/MultiSourceBlendDemoInputBinding.asset"),
                (BindingSOPath + ".meta", SamplesMirrorRoot + "/MultiSourceBlendDemoInputBinding.asset.meta"),
                (ScenePath, SamplesMirrorRoot + "/MultiSourceBlendDemo.unity"),
                (ScenePath + ".meta", SamplesMirrorRoot + "/MultiSourceBlendDemo.unity.meta"),
            };

            Directory.CreateDirectory(SamplesMirrorRoot);

            foreach (var (src, dst) in files)
            {
                if (!File.Exists(src))
                {
                    Debug.LogWarning($"Mirror skip (missing src): {src}");
                    continue;
                }
                File.Copy(src, dst, overwrite: true);
            }

            Debug.Log($"GenerateMultiSourceBlendDemoSample: Samples~ にミラーしました -> {SamplesMirrorRoot}");
        }
    }
}
