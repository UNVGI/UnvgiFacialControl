using Hidano.FacialControl.Adapters.AdapterBindings;
using Hidano.FacialControl.Editor.Inspector.AdapterBindings;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Osc.Editor.AdapterBindings
{
    /// <summary>
    /// <see cref="OscAdapterBinding"/> の inline UI を提供する UI Toolkit ベースの
    /// <see cref="PropertyDrawer"/>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>[CustomPropertyDrawer(typeof(OscAdapterBinding))]</c> を付与し、core の
    /// <see cref="AdapterBindings.AdapterBindingsListView"/> 配下の
    /// <see cref="UnityEditor.UIElements.PropertyField"/> から自動解決される。
    /// core パッケージ側で本 drawer を登録しないこと。
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
    /// 新規 IMGUI panel は導入しない。<c>OnGUI</c> ベースの fallback も実装せず
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
        private const string MappingsFieldName = "_mappings";
        private const string FailSafeModeFieldName = "_failSafeMode";
        private const string ConsistencyCheckWarnLogFieldName = "_consistencyCheckWarnLog";
        private const string BundleModeFieldName = "_bundleMode";
        private const string BundleAccumulationTimeoutMsFieldName = "_bundleAccumulationTimeoutMs";

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
            AddBoundField(root, property, MappingsFieldName, "Mappings");
            AddBoundField(root, property, FailSafeModeFieldName, "Fail Safe Mode");
            AddBoundField(root, property, ConsistencyCheckWarnLogFieldName, "Consistency Check Warn Log");
            AddBoundField(root, property, BundleModeFieldName, "Bundle Mode");
            AddBoundField(root, property, BundleAccumulationTimeoutMsFieldName, "Bundle Accumulation Timeout Ms");

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
                // 該当フィールドが見つからない場合でも他フィールドの描画を止めない。
                root.Add(new Label($"<missing field: {relativePath}>"));
                return;
            }

            var field = new PropertyField(child, label);
            root.Add(field);
        }
    }
}
