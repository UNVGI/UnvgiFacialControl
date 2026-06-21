using UnityEditor;
using UnityEngine;

namespace Hidano.FacialControl.Editor.Windows.Routing
{
    /// <summary>
    /// 入力ソース配線エディタの配置先となる専用ウィンドウ。
    /// </summary>
    public sealed class RoutingEditorWindow : EditorWindow
    {
        [MenuItem("Window/FacialControl/Routing Editor")]
        private static void OpenEmptyWindow()
        {
            Open(null);
        }

        public static RoutingEditorWindow Open(ScriptableObject profile)
        {
            RoutingEditorWindow window = GetWindow<RoutingEditorWindow>();
            window.titleContent = new GUIContent("Routing Editor");
            window.minSize = new Vector2(640f, 360f);
            window.Show();
            return window;
        }
    }
}
