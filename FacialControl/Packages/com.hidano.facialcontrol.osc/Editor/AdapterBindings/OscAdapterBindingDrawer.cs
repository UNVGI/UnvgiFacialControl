using Hidano.FacialControl.Adapters.AdapterBindings;
using Hidano.FacialControl.Editor.Inspector.AdapterBindings;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Osc.Editor.AdapterBindings
{
    /// <summary>
    /// <see cref="OscAdapterBinding"/> の inline UI を提供する UI Toolkit ベースの
    /// <see cref="PropertyDrawer"/>（Req 3.1, 3.2, 3.3, 6.5, 7.4, 7.5, 11.4、design.md `## Adapter PropertyDrawers`）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>[CustomPropertyDrawer(typeof(OscAdapterBinding))]</c> を付与し、core の
    /// <see cref="AdapterBindings.AdapterBindingsListView"/> 配下の
    /// <see cref="UnityEditor.UIElements.PropertyField"/> から自動解決される（Req 3.3）。
    /// core パッケージ側で本 drawer を登録しないこと（Req 3.1）。
    /// </para>
    /// <para>
    /// 表示するフィールド:
    /// <list type="bullet">
    /// <item><description>Slug（<c>AdapterBindingBase</c> の public field）</description></item>
    /// <item><description>Endpoint（<c>OscAdapterBinding._endpoint</c>）</description></item>
    /// <item><description>Port（<c>OscAdapterBinding._port</c>）</description></item>
    /// <item><description>Staleness Seconds（<c>OscAdapterBinding._stalenessSeconds</c>）</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 新規 IMGUI panel は導入しない（Req 11.4）。<c>OnGUI</c> ベースの fallback も実装せず
    /// UI Toolkit のみで完結する。
    /// </para>
    /// </remarks>
    [CustomPropertyDrawer(typeof(OscAdapterBinding))]
    public sealed class OscAdapterBindingDrawer : PropertyDrawer
    {
        private const string SlugFieldName = "Slug";
        private const string EndpointFieldName = "_endpoint";
        private const string PortFieldName = "_port";
        private const string StalenessFieldName = "_stalenessSeconds";

        public const string RootClassName = "facial-control-osc-adapter-binding";

        /// <inheritdoc />
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.AddToClassList(RootClassName);

            AddSlugField(root, property);
            AddBoundField(root, property, EndpointFieldName, "Endpoint");
            AddBoundField(root, property, PortFieldName, "Port");
            AddBoundField(root, property, StalenessFieldName, "Staleness Seconds");

            return root;
        }

        private static void AddSlugField(VisualElement root, SerializedProperty property)
        {
            var slugProp = property.FindPropertyRelative(SlugFieldName);
            if (slugProp == null)
            {
                root.Add(new Label($"<missing field: {SlugFieldName}>"));
                return;
            }
            root.Add(new AdapterBindingSlugField(slugProp, typeof(OscAdapterBinding)));
        }

        private static void AddBoundField(
            VisualElement root,
            SerializedProperty property,
            string relativePath,
            string label)
        {
            var child = property.FindPropertyRelative(relativePath);
            if (child == null)
            {
                // 該当フィールドが見つからない場合でも他フィールドの描画を止めない（Req 3.6 と同思想）。
                root.Add(new Label($"<missing field: {relativePath}>"));
                return;
            }

            var field = new PropertyField(child, label);
            root.Add(field);
        }
    }
}
