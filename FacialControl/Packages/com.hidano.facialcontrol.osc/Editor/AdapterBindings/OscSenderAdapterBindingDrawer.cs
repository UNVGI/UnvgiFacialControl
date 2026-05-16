using System;
using System.Collections.Generic;
using System.Globalization;
using Hidano.FacialControl.Adapters.AdapterBindings;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Common;
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
        private const string GazeExpressionIdsFieldName = "_gazeExpressionIds";
        private const string HeartbeatIntervalSecondsFieldName = "_heartbeatIntervalSeconds";
        private const string SuppressLoopbackFieldName = "_suppressLoopback";
        private const string EndpointHostFieldName = "endpoint";
        private const string EndpointPortFieldName = "port";
        private const string EndpointEnabledFieldName = "enabled";
        private const string EndpointPresetFieldName = "preset";
        private const int MaxUdpPort = 65535;
        private const float EndpointRowHeight = 136f;

        public const string RootClassName = "facial-control-osc-sender-adapter-binding";
        public const string EndpointListName = "osc-sender-endpoint-list";
        public const string EndpointRowClassName = "osc-sender-endpoint-row";
        public const string EndpointSkippedClassName = "osc-sender-endpoint-skip-target";
        public const string EndpointHostFieldElementName = "osc-sender-endpoint-host";
        public const string EndpointPortFieldElementName = "osc-sender-endpoint-port";
        public const string EndpointEnabledFieldElementName = "osc-sender-endpoint-enabled";
        public const string EndpointPresetFieldElementName = "osc-sender-endpoint-preset";
        public const string EndpointWarningName = "osc-sender-endpoint-warning";
        public const string SuppressLoopbackFieldElementName = "osc-sender-suppress-loopback";
        public const string BlendShapeNamesFieldElementName = "osc-sender-blend-shape-names";
        public const string GazeExpressionIdsFieldElementName = "osc-sender-gaze-expression-ids";
        public const string HeartbeatIntervalFieldElementName = "osc-sender-heartbeat-interval";
        public const string HeartbeatWarningName = "osc-sender-heartbeat-warning";
        public const string IdentityContainerName = "osc-sender-identity";
        public const string IdentityUuidFieldName = "osc-sender-identity-uuid";
        public const string IdentityStartedAtFieldName = "osc-sender-identity-started-at";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.AddToClassList(RootClassName);

            AddSlugField(root, property);
            AddEndpointList(root, property);
            AddBoundField(root, property, SuppressLoopbackFieldName, "Suppress Loopback", SuppressLoopbackFieldElementName);
            AddBoundField(root, property, BlendShapeNamesFieldName, "BlendShape Mapping Names", BlendShapeNamesFieldElementName);
            AddBoundField(root, property, GazeExpressionIdsFieldName, "Gaze Expression Ids", GazeExpressionIdsFieldElementName);
            AddHeartbeatIntervalField(root, property);
            AddSenderIdentityReadout(root, property);

            return root;
        }

        private static void AddEndpointList(VisualElement root, SerializedProperty property)
        {
            SerializedProperty endpointsProp = property.FindPropertyRelative(EndpointsFieldName);
            if (endpointsProp == null || !endpointsProp.isArray)
            {
                root.Add(new Label($"<missing field: {EndpointsFieldName}>"));
                return;
            }

            var indexProxy = new List<int>();
            RebuildIndexProxy(indexProxy, endpointsProp);

            var listView = new ListView
            {
                name = EndpointListName,
                fixedItemHeight = EndpointRowHeight,
                showAddRemoveFooter = true,
                showBorder = true,
                showFoldoutHeader = true,
                headerTitle = "Endpoints",
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                selectionType = SelectionType.Single,
                makeItem = CreateEndpointRow,
                bindItem = (element, index) => BindEndpointRow(element, index, property, indexProxy),
                unbindItem = (element, _) => element.Clear(),
            };
            listView.style.marginTop = 4;
            listView.style.minHeight = 104f;
            listView.SetViewController(new SafeListViewController());
            listView.itemsSource = indexProxy;

            listView.itemsAdded += indices =>
            {
                var sorted = new List<int>(indices);
                sorted.Sort();
                for (int i = 0; i < sorted.Count; i++)
                {
                    InsertEndpoint(property, sorted[i], indexProxy, listView);
                }
            };
            listView.itemsRemoved += indices =>
            {
                var sorted = new List<int>(indices);
                sorted.Sort();
                for (int i = sorted.Count - 1; i >= 0; i--)
                {
                    RemoveEndpoint(property, sorted[i], indexProxy, listView);
                }
            };
            listView.itemIndexChanged += (fromIndex, toIndex) =>
            {
                MoveEndpoint(property, fromIndex, toIndex, indexProxy, listView);
            };

            root.Add(listView);
        }

        private static VisualElement CreateEndpointRow()
        {
            var row = new VisualElement();
            row.AddToClassList(EndpointRowClassName);
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom = 4;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            return row;
        }

        private static void BindEndpointRow(
            VisualElement row,
            int visualIndex,
            SerializedProperty bindingProperty,
            List<int> indexProxy)
        {
            row.Clear();
            row.AddToClassList(EndpointRowClassName);

            SerializedProperty endpoints = ResolveEndpointListProperty(bindingProperty);
            int propertyIndex = ResolvePropertyIndex(visualIndex, indexProxy);
            if (endpoints == null || propertyIndex < 0 || propertyIndex >= endpoints.arraySize)
            {
                row.Add(new Label("<missing endpoint>"));
                return;
            }

            SerializedProperty endpoint = endpoints.GetArrayElementAtIndex(propertyIndex);
            SerializedProperty hostProp = endpoint.FindPropertyRelative(EndpointHostFieldName);
            SerializedProperty portProp = endpoint.FindPropertyRelative(EndpointPortFieldName);
            SerializedProperty enabledProp = endpoint.FindPropertyRelative(EndpointEnabledFieldName);
            SerializedProperty presetProp = endpoint.FindPropertyRelative(EndpointPresetFieldName);

            var fields = new VisualElement();
            fields.style.flexDirection = FlexDirection.Row;
            fields.style.alignItems = Align.FlexStart;

            AddEndpointEnabledField(fields, enabledProp);
            TextField hostField = AddEndpointHostField(fields, hostProp);
            IntegerField portField = AddEndpointPortField(fields, portProp);
            AddEndpointPresetField(fields, presetProp);
            row.Add(fields);

            var warning = new HelpBox(string.Empty, HelpBoxMessageType.Warning)
            {
                name = EndpointWarningName,
            };
            row.Add(warning);

            RefreshEndpointWarning(
                row,
                warning,
                hostProp != null ? hostProp.stringValue : string.Empty,
                portProp != null ? portProp.intValue : 0);

            hostField?.RegisterValueChangedCallback(evt =>
            {
                RefreshEndpointWarning(row, warning, evt.newValue, portField != null ? portField.value : 0);
            });
            portField?.RegisterValueChangedCallback(evt =>
            {
                RefreshEndpointWarning(row, warning, hostField != null ? hostField.value : string.Empty, evt.newValue);
            });
        }

        private static Toggle AddEndpointEnabledField(VisualElement parent, SerializedProperty property)
        {
            if (property == null)
            {
                parent.Add(new Label($"<missing field: {EndpointEnabledFieldName}>"));
                return null;
            }

            var field = new Toggle("Enabled")
            {
                name = EndpointEnabledFieldElementName,
            };
            field.style.minWidth = 82f;
            field.BindProperty(property);
            parent.Add(field);
            return field;
        }

        private static TextField AddEndpointHostField(VisualElement parent, SerializedProperty property)
        {
            if (property == null)
            {
                parent.Add(new Label($"<missing field: {EndpointHostFieldName}>"));
                return null;
            }

            var field = new TextField("IP / Host")
            {
                name = EndpointHostFieldElementName,
            };
            field.style.flexGrow = 1f;
            field.style.marginRight = 4f;
            field.BindProperty(property);
            parent.Add(field);
            return field;
        }

        private static IntegerField AddEndpointPortField(VisualElement parent, SerializedProperty property)
        {
            if (property == null)
            {
                parent.Add(new Label($"<missing field: {EndpointPortFieldName}>"));
                return null;
            }

            var field = new IntegerField("Port")
            {
                name = EndpointPortFieldElementName,
            };
            field.style.width = 96f;
            field.style.marginRight = 4f;
            field.BindProperty(property);
            parent.Add(field);
            return field;
        }

        private static EnumField AddEndpointPresetField(VisualElement parent, SerializedProperty property)
        {
            if (property == null)
            {
                parent.Add(new Label($"<missing field: {EndpointPresetFieldName}>"));
                return null;
            }

            var field = new EnumField("Preset", AddressPresetKind.VRChat)
            {
                name = EndpointPresetFieldElementName,
            };
            field.style.width = 132f;
            field.BindProperty(property);
            parent.Add(field);
            return field;
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
            string label,
            string elementName)
        {
            SerializedProperty child = property.FindPropertyRelative(relativePath);
            if (child == null)
            {
                root.Add(new Label($"<missing field: {relativePath}>"));
                return;
            }

            root.Add(new PropertyField(child, label)
            {
                name = elementName,
            });
        }

        private static void AddHeartbeatIntervalField(VisualElement root, SerializedProperty property)
        {
            SerializedProperty child = property.FindPropertyRelative(HeartbeatIntervalSecondsFieldName);
            if (child == null)
            {
                root.Add(new Label($"<missing field: {HeartbeatIntervalSecondsFieldName}>"));
                return;
            }

            var field = new FloatField("Heartbeat Interval Seconds")
            {
                name = HeartbeatIntervalFieldElementName,
            };
            field.BindProperty(child);
            root.Add(field);

            var warning = new HelpBox(string.Empty, HelpBoxMessageType.Warning)
            {
                name = HeartbeatWarningName,
            };
            root.Add(warning);
            RefreshHeartbeatWarning(warning, child.floatValue);
            field.RegisterValueChangedCallback(evt => RefreshHeartbeatWarning(warning, evt.newValue));
        }

        private static void AddSenderIdentityReadout(VisualElement root, SerializedProperty property)
        {
            var container = new Foldout
            {
                name = IdentityContainerName,
                text = "Sender Identity",
                value = true,
            };

            var uuidField = new TextField("Startup UUID")
            {
                name = IdentityUuidFieldName,
            };
            uuidField.SetEnabled(false);
            container.Add(uuidField);

            var startedAtField = new TextField("Started At Unix Ms")
            {
                name = IdentityStartedAtFieldName,
            };
            startedAtField.SetEnabled(false);
            container.Add(startedAtField);

            RefreshIdentityReadout(uuidField, startedAtField, property);
            container.schedule.Execute(() =>
            {
                RefreshIdentityReadout(uuidField, startedAtField, property);
            }).Every(1000);

            root.Add(container);
        }

        private static void InsertEndpoint(
            SerializedProperty bindingProperty,
            int index,
            List<int> indexProxy,
            ListView listView)
        {
            SerializedProperty endpoints = ResolveEndpointListProperty(bindingProperty);
            if (endpoints == null)
            {
                return;
            }

            if (index < 0 || index > endpoints.arraySize)
            {
                index = endpoints.arraySize;
            }

            SerializedObject serializedObject = endpoints.serializedObject;
            serializedObject.Update();
            endpoints = ResolveEndpointListProperty(bindingProperty);
            endpoints.InsertArrayElementAtIndex(index);
            InitializeEndpoint(endpoints.GetArrayElementAtIndex(index));
            ApplyModifiedPropertiesAndRefresh(serializedObject, bindingProperty, indexProxy, listView);
        }

        private static void RemoveEndpoint(
            SerializedProperty bindingProperty,
            int index,
            List<int> indexProxy,
            ListView listView)
        {
            SerializedProperty endpoints = ResolveEndpointListProperty(bindingProperty);
            if (endpoints == null || index < 0 || index >= endpoints.arraySize)
            {
                return;
            }

            SerializedObject serializedObject = endpoints.serializedObject;
            serializedObject.Update();
            endpoints = ResolveEndpointListProperty(bindingProperty);
            endpoints.DeleteArrayElementAtIndex(index);
            ApplyModifiedPropertiesAndRefresh(serializedObject, bindingProperty, indexProxy, listView);
        }

        private static void MoveEndpoint(
            SerializedProperty bindingProperty,
            int fromIndex,
            int toIndex,
            List<int> indexProxy,
            ListView listView)
        {
            SerializedProperty endpoints = ResolveEndpointListProperty(bindingProperty);
            if (endpoints == null || fromIndex < 0 || fromIndex >= endpoints.arraySize)
            {
                return;
            }

            if (toIndex < 0)
            {
                toIndex = 0;
            }
            if (toIndex >= endpoints.arraySize)
            {
                toIndex = endpoints.arraySize - 1;
            }
            if (fromIndex == toIndex)
            {
                return;
            }

            SerializedObject serializedObject = endpoints.serializedObject;
            serializedObject.Update();
            endpoints = ResolveEndpointListProperty(bindingProperty);
            endpoints.MoveArrayElement(fromIndex, toIndex);
            ApplyModifiedPropertiesAndRefresh(serializedObject, bindingProperty, indexProxy, listView);
        }

        private static void InitializeEndpoint(SerializedProperty endpoint)
        {
            if (endpoint == null)
            {
                return;
            }

            SerializedProperty host = endpoint.FindPropertyRelative(EndpointHostFieldName);
            SerializedProperty port = endpoint.FindPropertyRelative(EndpointPortFieldName);
            SerializedProperty enabled = endpoint.FindPropertyRelative(EndpointEnabledFieldName);
            SerializedProperty preset = endpoint.FindPropertyRelative(EndpointPresetFieldName);

            if (host != null) host.stringValue = OscSenderEndpointConfig.DefaultEndpoint;
            if (port != null) port.intValue = OscConfiguration.DefaultSendPort;
            if (enabled != null) enabled.boolValue = true;
            if (preset != null) preset.enumValueIndex = (int)AddressPresetKind.VRChat;
        }

        private static void ApplyModifiedPropertiesAndRefresh(
            SerializedObject serializedObject,
            SerializedProperty bindingProperty,
            List<int> indexProxy,
            ListView listView)
        {
            serializedObject.ApplyModifiedProperties();
            if (serializedObject.targetObject != null)
            {
                EditorUtility.SetDirty(serializedObject.targetObject);
            }

            serializedObject.Update();
            RebuildIndexProxy(indexProxy, ResolveEndpointListProperty(bindingProperty));
            ClampSelection(listView, indexProxy.Count);
            listView?.Rebuild();
        }

        private static SerializedProperty ResolveEndpointListProperty(SerializedProperty bindingProperty)
        {
            if (bindingProperty == null)
            {
                return null;
            }

            SerializedObject serializedObject = bindingProperty.serializedObject;
            SerializedProperty refreshedBinding = serializedObject.FindProperty(bindingProperty.propertyPath);
            if (refreshedBinding == null)
            {
                refreshedBinding = bindingProperty;
            }

            return refreshedBinding.FindPropertyRelative(EndpointsFieldName);
        }

        private static int ResolvePropertyIndex(int visualIndex, List<int> indexProxy)
        {
            if (indexProxy != null && visualIndex >= 0 && visualIndex < indexProxy.Count)
            {
                return indexProxy[visualIndex];
            }

            return visualIndex;
        }

        private static void RebuildIndexProxy(List<int> indexProxy, SerializedProperty arrayProperty)
        {
            indexProxy.Clear();
            if (arrayProperty == null || !arrayProperty.isArray)
            {
                return;
            }

            for (int i = 0; i < arrayProperty.arraySize; i++)
            {
                indexProxy.Add(i);
            }
        }

        private static void ClampSelection(ListView listView, int count)
        {
            if (listView == null)
            {
                return;
            }

            if (count <= 0 || listView.selectedIndex < 0 || listView.selectedIndex >= count)
            {
                listView.ClearSelection();
            }
        }

        private static void RefreshEndpointWarning(
            VisualElement row,
            HelpBox warning,
            string host,
            int port)
        {
            if (warning == null)
            {
                return;
            }

            bool hostInvalid = string.IsNullOrWhiteSpace(host);
            bool portInvalid = port <= 0 || port > MaxUdpPort;
            if (!hostInvalid && !portInvalid)
            {
                warning.text = string.Empty;
                warning.style.display = DisplayStyle.None;
                row?.RemoveFromClassList(EndpointSkippedClassName);
                return;
            }

            string message;
            if (hostInvalid && portInvalid)
            {
                message = "IP/host is empty and port is outside 1-65535. This endpoint is marked as skipped.";
            }
            else if (hostInvalid)
            {
                message = "IP/host is empty. This endpoint is marked as skipped.";
            }
            else
            {
                message = "Port is outside 1-65535. This endpoint is marked as skipped.";
            }

            warning.text = message;
            warning.style.display = DisplayStyle.Flex;
            row?.AddToClassList(EndpointSkippedClassName);
        }

        private static void RefreshHeartbeatWarning(HelpBox warning, float value)
        {
            if (warning == null)
            {
                return;
            }

            if (!float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= OscSenderAdapterBinding.MinHeartbeatIntervalSeconds
                && value <= OscSenderAdapterBinding.MaxHeartbeatIntervalSeconds)
            {
                warning.text = string.Empty;
                warning.style.display = DisplayStyle.None;
                return;
            }

            warning.text = string.Format(
                CultureInfo.InvariantCulture,
                "Heartbeat interval must be between {0:0.###} and {1:0.###} seconds. The sender will clamp this value at start.",
                OscSenderAdapterBinding.MinHeartbeatIntervalSeconds,
                OscSenderAdapterBinding.MaxHeartbeatIntervalSeconds);
            warning.style.display = DisplayStyle.Flex;
        }

        private static void RefreshIdentityReadout(
            TextField uuidField,
            TextField startedAtField,
            SerializedProperty property)
        {
            string uuid = string.Empty;
            string startedAt = string.Empty;

            OscSenderAdapterBinding binding = TryGetBindingInstance(property);
            if (binding != null)
            {
                SenderIdentity identity = binding.Identity;
                if (identity.Uuid != Guid.Empty)
                {
                    uuid = identity.Uuid.ToString("D");
                    startedAt = identity.StartedAtUnixMs.ToString(CultureInfo.InvariantCulture);
                }
            }

            uuidField?.SetValueWithoutNotify(uuid);
            startedAtField?.SetValueWithoutNotify(startedAt);
        }

        private static OscSenderAdapterBinding TryGetBindingInstance(SerializedProperty property)
        {
            if (property == null)
            {
                return null;
            }

            try
            {
                if (property.propertyType == SerializedPropertyType.ManagedReference)
                {
                    return property.managedReferenceValue as OscSenderAdapterBinding;
                }

                return property.boxedValue as OscSenderAdapterBinding;
            }
            catch
            {
                return null;
            }
        }
    }
}
