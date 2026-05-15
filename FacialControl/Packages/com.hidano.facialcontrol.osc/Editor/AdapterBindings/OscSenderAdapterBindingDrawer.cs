using Hidano.FacialControl.Adapters.AdapterBindings;
using Hidano.FacialControl.Editor.Inspector.AdapterBindings;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Osc.Editor.AdapterBindings
{
    [CustomPropertyDrawer(typeof(OscSenderAdapterBinding))]
    public sealed class OscSenderAdapterBindingDrawer : PropertyDrawer
    {
        private const string SlugFieldName = "Slug";
        private const string EndpointsFieldName = "_endpoints";
        private const string BlendShapeNamesFieldName = "_blendShapeNames";
        private const string HeartbeatIntervalSecondsFieldName = "_heartbeatIntervalSeconds";

        public const string RootClassName = "facial-control-osc-sender-adapter-binding";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.AddToClassList(RootClassName);

            AddSlugField(root, property);
            AddBoundField(root, property, EndpointsFieldName, "Endpoints");
            AddBoundField(root, property, BlendShapeNamesFieldName, "BlendShape Names");
            AddHeartbeatIntervalField(root, property);

            return root;
        }

        private static void AddSlugField(VisualElement root, SerializedProperty property)
        {
            SerializedProperty slugProp = property.FindPropertyRelative(SlugFieldName);
            if (slugProp == null)
            {
                root.Add(new Label($"<missing field: {SlugFieldName}>"));
                return;
            }

            root.Add(new AdapterBindingSlugField(slugProp, typeof(OscSenderAdapterBinding)));
        }

        private static void AddBoundField(
            VisualElement root,
            SerializedProperty property,
            string relativePath,
            string label)
        {
            SerializedProperty child = property.FindPropertyRelative(relativePath);
            if (child == null)
            {
                root.Add(new Label($"<missing field: {relativePath}>"));
                return;
            }

            root.Add(new PropertyField(child, label));
        }

        private static void AddHeartbeatIntervalField(VisualElement root, SerializedProperty property)
        {
            SerializedProperty child = property.FindPropertyRelative(HeartbeatIntervalSecondsFieldName);
            if (child == null)
            {
                root.Add(new Label($"<missing field: {HeartbeatIntervalSecondsFieldName}>"));
                return;
            }

            var slider = new Slider(
                "Heartbeat Interval Seconds",
                OscSenderAdapterBinding.MinHeartbeatIntervalSeconds,
                OscSenderAdapterBinding.MaxHeartbeatIntervalSeconds)
            {
                showInputField = true
            };
            slider.BindProperty(child);
            root.Add(slider);
        }
    }
}
