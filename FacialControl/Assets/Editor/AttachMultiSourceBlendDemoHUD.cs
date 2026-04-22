using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Samples;

namespace Hidano.FacialControl.Samples.EditorTools
{
    /// <summary>
    /// SampleScene に <see cref="MultiSourceBlendDemoHUD"/> コンポーネントを持つ GameObject を
    /// 配置するユーティリティ。batchmode で <c>-executeMethod</c> 経由で呼ばれることを想定。
    /// 既に同名 GameObject が存在する場合は何もせず、シーンを再保存しない（idempotent）。
    /// </summary>
    public static class AttachMultiSourceBlendDemoHUD
    {
        private const string ScenePath = "Assets/Samples/SampleScene.unity";
        private const string HudGameObjectName = "MultiSourceBlendDemoHUD";

        public static void Attach()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[AttachMultiSourceBlendDemoHUD] Scene を開けませんでした: {ScenePath}");
                EditorApplication.Exit(2);
                return;
            }

            var existing = GameObject.Find(HudGameObjectName);
            if (existing != null)
            {
                var hud = existing.GetComponent<MultiSourceBlendDemoHUD>();
                if (hud == null)
                {
                    hud = existing.AddComponent<MultiSourceBlendDemoHUD>();
                }
                WireFacialController(hud, scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[AttachMultiSourceBlendDemoHUD] 既存 GameObject '{HudGameObjectName}' を更新しました。");
                EditorApplication.Exit(0);
                return;
            }

            var go = new GameObject(HudGameObjectName);
            var component = go.AddComponent<MultiSourceBlendDemoHUD>();
            WireFacialController(component, scene);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            if (!saved)
            {
                Debug.LogError("[AttachMultiSourceBlendDemoHUD] シーン保存に失敗。");
                EditorApplication.Exit(3);
                return;
            }
            Debug.Log($"[AttachMultiSourceBlendDemoHUD] GameObject '{HudGameObjectName}' を追加して SampleScene を保存しました。");
            EditorApplication.Exit(0);
        }

        private static void WireFacialController(MultiSourceBlendDemoHUD hud, UnityEngine.SceneManagement.Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            FacialController controller = null;
            foreach (var root in roots)
            {
                controller = root.GetComponentInChildren<FacialController>(includeInactive: true);
                if (controller != null) break;
            }
            if (controller == null)
            {
                Debug.LogWarning("[AttachMultiSourceBlendDemoHUD] シーン内に FacialController が見つかりませんでした。HUD の参照は手動で設定してください。");
                return;
            }

            var serialized = new SerializedObject(hud);
            serialized.FindProperty("_facialController").objectReferenceValue = controller;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[AttachMultiSourceBlendDemoHUD] FacialController 参照を {controller.gameObject.name} に設定しました。");
        }
    }
}
