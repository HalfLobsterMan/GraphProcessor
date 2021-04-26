﻿using CZToolKit.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

using Status = UnityEngine.UIElements.DropdownMenuAction.Status;
using NodeView = UnityEditor.Experimental.GraphView.Node;

namespace CZToolKit.GraphProcessor.Editors
{
    public class BaseNodeView : NodeView
    {
        #region Static
        const string BaseNodeViewStyleFile = "GraphProcessor/Styles/BaseNodeView";

        static Dictionary<Type, FieldInfo[]> NodeDataTypeFieldInfoDic = new Dictionary<Type, FieldInfo[]>();
        static StyleSheet baseNodeViewStyle;
        public static StyleSheet BaseNodeViewStyle
        {
            get
            {
                if (baseNodeViewStyle == null)
                    baseNodeViewStyle = Resources.Load<StyleSheet>(BaseNodeViewStyleFile);
                return baseNodeViewStyle;
            }
        }
        #endregion

        VisualElement topPortContainer;
        VisualElement bottomPortContainer;
        Label titleLabel;

        Dictionary<string, PortView> portViews = new Dictionary<string, PortView>();

        [NonSerialized] List<IconBadge> badges = new List<IconBadge>();

        public VisualElement controlsContainer { get; private set; }
        protected VisualElement inputContainerElement { get; set; }
        public Label TitleLabel
        {
            get
            {
                if (titleLabel == null)
                    titleLabel = titleContainer.Q<Label>("title-label");
                return titleLabel;
            }
        }
        public Dictionary<string, PortView> PortViews { get { return portViews; } }

        public bool Initialized { get; private set; }
        public BaseGraphView Owner { get; private set; }
        public BaseNode NodeData { get; private set; }
        public Type NodeDataType { get; private set; }
        public bool Lockable { get; private set; }
        public override bool expanded
        {
            get { return base.expanded; }
            set
            {
                base.expanded = value;
                if (Initialized)
                    NodeData.Expanded = value;
            }
        }
        protected FieldInfo[] NodeDataTypeFieldInfos
        {
            get
            {
                if (!NodeDataTypeFieldInfoDic.TryGetValue(NodeDataType, out FieldInfo[] fieldInfos))
                {
                    fieldInfos = NodeDataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    NodeDataTypeFieldInfoDic[NodeDataType] = fieldInfos;
                }
                return fieldInfos;
            }
        }

        #region  Initialization
        public void Initialize(BaseGraphView _owner, BaseNode _nodeData)
        {
            if (Initialized) return;

            Owner = _owner;
            NodeData = _nodeData;
            NodeDataType = _nodeData.GetType();

            InitializeView();
            InitializePorts();
            RefreshPorts();
            RefreshExpandedState();

            OnInitialized();
            Initialized = true;
        }

        protected virtual void OnInitialized()
        {
#if !ODIN_INSPECTOR
            ProcessFields();
#endif
        }

        void InitializeView()
        {
            styleSheets.Add(BaseNodeViewStyle);
            styleSheets.Add(PortView.PortViewTypesStyle);

            controlsContainer = new VisualElement { name = "Controls" };
            controlsContainer.AddToClassList("NodeControls");
            controlsContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1);
            mainContainer.Add(controlsContainer);

            topPortContainer = new VisualElement { name = "TopPortContainer" };
            topPortContainer.style.justifyContent = Justify.Center;
            topPortContainer.style.alignItems = Align.Center;
            topPortContainer.style.flexDirection = FlexDirection.Row;
            Insert(0, topPortContainer);

            bottomPortContainer = new VisualElement { name = "BottomPortContainer" };
            bottomPortContainer.style.justifyContent = Justify.Center;
            bottomPortContainer.style.alignItems = Align.Center;
            bottomPortContainer.style.flexDirection = FlexDirection.Row;
            Add(bottomPortContainer);

            inputContainerElement = new VisualElement { name = "input-container" };
            mainContainer.parent.Add(inputContainerElement);
            inputContainerElement.SendToBack();
            inputContainerElement.pickingMode = PickingMode.Ignore;

            title = NodeEditorUtility.GetNodeDisplayName(NodeDataType);
            expanded = NodeData.Expanded;
            SetPosition(NodeData.position);
            Lockable = AttributeCache.TryGetTypeAttribute(NodeDataType, out LockableAttribute lockableAttribute);

            if (AttributeCache.TryGetTypeAttribute(NodeDataType, out NodeTooltipAttribute nodeTooltipAttribute))
                tooltip = nodeTooltipAttribute.Tooltip;
            if (AttributeCache.TryGetTypeAttribute(NodeDataType, out NodeTitleTintAttribute nodeTitleTintAttribute))
            {
                titleContainer.style.backgroundColor = nodeTitleTintAttribute.BackgroundColor;
                TitleLabel.style.color = nodeTitleTintAttribute.BackgroundColor.GetLuminance() > 0.5f && nodeTitleTintAttribute.BackgroundColor.a > 0.5f ? Color.black : Color.white * 0.9f;
            }

            if (AttributeCache.TryGetTypeAttribute(NodeDataType, out TooltipAttribute tooltipAttrib))
                tooltip = tooltipAttrib.tooltip;

            bool showControlOnHover = AttributeCache.TryGetTypeAttribute(NodeDataType, out ShowControlOnHoverAttribute showControlOnHoverAttrib);
            if (showControlOnHover)
            {
                bool mouseOverControls = false;
                controlsContainer.style.display = DisplayStyle.None;
                RegisterCallback<MouseOverEvent>(e =>
                {
                    controlsContainer.style.display = DisplayStyle.Flex;
                    mouseOverControls = true;
                });
                RegisterCallback<MouseOutEvent>(e =>
                {
                    var rect = GetPosition();
                    var graphMousePosition = Owner.contentViewContainer.WorldToLocal(e.mousePosition);
                    if (rect.Contains(graphMousePosition) || !showControlOnHover)
                        return;
                    mouseOverControls = false;
                    schedule.Execute(_ =>
                    {
                        if (!mouseOverControls)
                            controlsContainer.style.display = DisplayStyle.None;
                    }).ExecuteLater(500);
                });
            }

            //Undo.undoRedoPerformed += UpdateFieldValues;
        }

        protected virtual PortView CustomCreatePortView(Orientation _orientation, Direction _direction, NodePort _nodePort, BaseEdgeConnectorListener _listener)
        {
            return null;
        }

        void InitializePorts()
        {
            BaseEdgeConnectorListener listener = Owner.connectorListener;

            foreach (var nodePort in NodeData.Ports)
            {
                Direction direction = nodePort.Value.Direction == PortDirection.Input ? Direction.Input : Direction.Output;
                Orientation orientation =
                    AttributeCache.TryGetFieldAttribute(NodeDataType, nodePort.Value.FieldName, out VerticalAttribute vertical) ?
                    Orientation.Vertical : Orientation.Horizontal;

                PortView portView = CustomCreatePortView(orientation, direction, nodePort.Value, listener);
                if (portView == null)
                    portView = PortView.CreatePV(orientation, direction, nodePort.Value, listener);
                portView.Initialize(this);
                PortViews[nodePort.Key] = portView;
            }

            foreach (var fieldInfo in NodeDataTypeFieldInfos)
            {
                if (AttributeCache.TryGetFieldInfoAttribute(NodeDataType, fieldInfo, out ShowAsDrawer showAsDrawer))
                    AddControlField(fieldInfo, "", true);
            }
        }


        #endregion

        #region API

        public void SetDeletable(bool deletable)
        {
            if (deletable)
                capabilities |= Capabilities.Deletable;
            else
                capabilities &= ~Capabilities.Deletable;
        }

        public void SetMovable(bool movable)
        {
            if (movable)
                capabilities |= Capabilities.Movable;
            else
                capabilities &= ~Capabilities.Movable;
        }

        public void SetSelectable(bool selectable)
        {
            if (selectable)
                capabilities |= Capabilities.Selectable;
            else
                capabilities &= ~Capabilities.Selectable;
        }

        public void AddIcon(Image _icon)
        {
            titleContainer.Insert(titleContainer.IndexOf(TitleLabel), _icon);
        }

        public void AddBadge(IconBadge badge)
        {
            Add(badge);
            badges.Add(badge);
            badge.AttachTo(topContainer, SpriteAlignment.TopRight);
        }

        public void RemoveBadge(Func<IconBadge, bool> callback)
        {
            badges.RemoveAll(b =>
            {
                if (callback(b))
                {
                    b.Detach();
                    b.RemoveFromHierarchy();
                    return true;
                }
                return false;
            });
        }

        #endregion

        #region Private
        void OpenNodeViewScript()
        {
            var script = NodeEditorUtility.FindScriptFromType(GetType());

            if (script != null)
                AssetDatabase.OpenAsset(script.GetInstanceID(), 0, 0);
        }

        void OpenNodeScript()
        {
            var script = NodeEditorUtility.FindScriptFromType(NodeData.GetType());

            if (script != null)
                AssetDatabase.OpenAsset(script.GetInstanceID(), 0, 0);
        }
        #endregion

        #region Callbacks & Overrides

        Dictionary<string, List<(object value, VisualElement target)>> visibleConditions = new Dictionary<string, List<(object value, VisualElement target)>>();
        Dictionary<string, VisualElement> hideElementIfConnected = new Dictionary<string, VisualElement>();
        Dictionary<FieldInfo, List<VisualElement>> fieldControlsMap = new Dictionary<FieldInfo, List<VisualElement>>();


        public override void OnSelected()
        {
            base.OnSelected();
            BringToFront();
        }

        protected virtual void ProcessFields()
        {
            foreach (var fieldInfo in NodeDataTypeFieldInfos)
            {
                // 如果标记了NonSerialized或HideInInspector特性，跳过
                if (fieldInfo.GetCustomAttribute(typeof(System.NonSerializedAttribute)) != null || fieldInfo.GetCustomAttribute(typeof(HideInInspector)) != null)
                    continue;

                // 是否是一个接口
                bool isPort = AttributeCache.TryGetFieldInfoAttribute(NodeDataType, fieldInfo, out PortAttribute portAttrib);
                // 是公开，或者有SerializeField特性
                bool isDisplay = fieldInfo.IsPublic || AttributeCache.TryGetFieldInfoAttribute(NodeDataType, fieldInfo, out SerializeField serializable);
                if (!isDisplay || (isPort && portAttrib.ShowBackValue == ShowBackingValue.Never))
                    continue;

                // 是否是入方向的接口
                bool isInputPort = isPort && portAttrib.Direction == PortDirection.Input;
                bool showAsDrawer = fieldInfo.GetCustomAttribute(typeof(ShowAsDrawer)) != null;
                bool showInputDrawer = isInputPort && showAsDrawer;
                // 把数组排除
                showInputDrawer &= !typeof(IList).IsAssignableFrom(fieldInfo.FieldType);

                if (showInputDrawer)
                    continue;

                var elem = AddControlField(fieldInfo, ObjectNames.NicifyVariableName(fieldInfo.Name), showInputDrawer);
                if (isInputPort)
                {
                    hideElementIfConnected[fieldInfo.Name] = elem;

                    if (PortViews.TryGetValue(fieldInfo.Name, out var pv))
                        if (pv.Edges.Count > 0)
                            elem.style.display = DisplayStyle.None;
                }
            }
        }

        void UpdateFieldVisibility(string fieldName, object newValue)
        {
            if (visibleConditions.TryGetValue(fieldName, out var list))
            {
                foreach (var elem in list)
                {
                    if (newValue.Equals(elem.value))
                        elem.target.style.display = DisplayStyle.Flex;
                    else
                        elem.target.style.display = DisplayStyle.None;
                }
            }
        }

        void UpdateOtherFieldValueSpecific<T>(FieldInfo field, object newValue)
        {
            foreach (var inputField in fieldControlsMap[field])
            {
                var notify = inputField as INotifyValueChanged<T>;
                if (notify != null)
                    notify.SetValueWithoutNotify((T)newValue);
            }
        }

        static MethodInfo specificUpdateOtherFieldValue = typeof(BaseNodeView).GetMethod(nameof(UpdateOtherFieldValueSpecific), BindingFlags.NonPublic | BindingFlags.Instance);
        void UpdateOtherFieldValue(FieldInfo info, object newValue)
        {
            // Warning: Keep in sync with FieldFactory CreateField
            var fieldType = info.FieldType.IsSubclassOf(typeof(UnityEngine.Object)) ? typeof(UnityEngine.Object) : info.FieldType;
            var genericUpdate = specificUpdateOtherFieldValue.MakeGenericMethod(fieldType);

            genericUpdate.Invoke(this, new object[] { info, newValue });
        }

        object GetInputFieldValueSpecific<T>(FieldInfo field)
        {
            if (fieldControlsMap.TryGetValue(field, out var list))
            {
                foreach (var inputField in list)
                {
                    if (inputField is INotifyValueChanged<T> notify)
                        return notify.value;
                }
            }
            return null;
        }

        static MethodInfo specificGetValue = typeof(BaseNodeView).GetMethod(nameof(GetInputFieldValueSpecific), BindingFlags.NonPublic | BindingFlags.Instance);

        protected VisualElement AddControlField(FieldInfo _fieldInfo, string _label = null, bool _showInputDrawer = false, Action valueChangedCallback = null)
        {
            if (_fieldInfo == null)
                return null;

            var fieldDrawer = FieldFactory.CreateField(_showInputDrawer ? "" : _label, _fieldInfo.FieldType, _fieldInfo.GetValue(NodeData), (newValue) =>
             {
                 Owner.RegisterCompleteObjectUndo("Updated " + newValue);
                 _fieldInfo.SetValue(NodeData, newValue);
                 NotifyNodeChanged();
                 valueChangedCallback?.Invoke();
                // 更新可见性
                UpdateFieldVisibility(_fieldInfo.Name, newValue);
                // When you have the node inspector, it's possible to have multiple input fields pointing to the same
                // property. We need to update those manually otherwise they still have the old value in the inspector.
                UpdateOtherFieldValue(_fieldInfo, newValue);
             });



            if (!fieldControlsMap.TryGetValue(_fieldInfo, out var inputFieldList))
                inputFieldList = fieldControlsMap[_fieldInfo] = new List<VisualElement>();
            inputFieldList.Add(fieldDrawer);

            if (fieldDrawer != null)
            {
                if (_showInputDrawer)
                {
                    if (PortViews.TryGetValue(_fieldInfo.Name, out PortView portView))
                    {
                        var box = new VisualElement { name = _fieldInfo.Name };
                        box.AddToClassList("port-input-element");
                        box.Add(fieldDrawer);
                        inputContainerElement.Add(box);
                    }
                }
                else
                    controlsContainer.Add(fieldDrawer);
            }

            return fieldDrawer;
        }

        public virtual void OnPortConnected(PortView _portView, PortView _targetPortView)
        {
            if (_portView.direction == Direction.Input && inputContainerElement?.Q(_portView.FieldName) != null)
                inputContainerElement.Q(_portView.FieldName).AddToClassList("empty");

            if (hideElementIfConnected.TryGetValue(_portView.FieldName, out var elem))
                elem.style.display = DisplayStyle.None;
        }

        public virtual void OnPortDisconnected(PortView _portView, PortView _targetPortView)
        {
            if (_portView.direction == Direction.Input && inputContainerElement?.Q(_portView.FieldName) != null)
                inputContainerElement.Q(_portView.FieldName).RemoveFromClassList("empty");

            if (hideElementIfConnected.TryGetValue(_portView.FieldName, out var elem))
                elem.style.display = DisplayStyle.Flex;
        }

        public override void SetPosition(Rect newPos)
        {
            if (!Initialized)
                base.SetPosition(newPos);
            else if (!NodeData.Locked)
            {
                base.SetPosition(newPos);

                Owner.RegisterCompleteObjectUndo("Moved graph node");
                NodeData.position = newPos;
            }
        }

        public void ChangeLockStatus()
        {
            NodeData.Locked ^= true;
            SetMovable(!NodeData.Locked);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Open Node Script", (e) => OpenNodeScript(), OpenNodeScriptStatus);
            evt.menu.AppendAction("Open Node View Script", (e) => OpenNodeViewScript(), OpenNodeViewScriptStatus);
            if (Lockable)
                evt.menu.AppendAction(NodeData.Locked ? "Unlock" : "Lock", (e) => ChangeLockStatus(), Status.Normal);
        }

        // 按钮状态
        Status OpenNodeScriptStatus(DropdownMenuAction action)
        {
            if (NodeEditorUtility.FindScriptFromType(NodeData.GetType()) != null)
                return Status.Normal;
            return Status.Disabled;
        }

        // 按钮状态
        Status OpenNodeViewScriptStatus(DropdownMenuAction action)
        {
            if (NodeEditorUtility.FindScriptFromType(GetType()) != null)
                return Status.Normal;
            return Status.Disabled;
        }

        public virtual new bool RefreshPorts()
        {
            foreach (var portView in PortViews.Values)
            {
                switch (portView.direction)
                {
                    case Direction.Input:
                        if (portView.orientation == Orientation.Horizontal)
                            inputContainer.Add(portView);
                        else
                            topPortContainer.Add(portView);
                        break;
                    case Direction.Output:
                        if (portView.orientation == Orientation.Horizontal)
                            outputContainer.Add(portView);
                        else
                            bottomPortContainer.Add(portView);
                        break;
                    default:
                        break;
                }
            }
            return base.RefreshPorts();
        }

        /// <summary> Send an event to the graph telling that the content of this node have changed </summary>
        public void NotifyNodeChanged()
        {
            //owner.graphData.NotifyNodeChanged(nodeData);
        }

        #endregion
    }
}