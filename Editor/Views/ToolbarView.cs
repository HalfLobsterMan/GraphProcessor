using System.Collections.Generic;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditor;
using System.Linq;
using System;

namespace CZToolKit.GraphProcessor.Editors
{

    public class ToolbarView : Toolbar
    {
        protected enum ElementType
        {
            Button,
            Toggle,
            DropDownButton,
        }

        protected class ToolbarButtonData
        {
            public GUIContent content;
            public ElementType type;
            public bool value;
            public bool visible = true;
            public Action buttonCallback;
            public Action<bool> toggleCallback;
        }

        List<ToolbarButtonData> leftButtonDatas = new List<ToolbarButtonData>();
        List<ToolbarButtonData> rightButtonDatas = new List<ToolbarButtonData>();
        protected BaseGraphWindow graphWindow;

        ToolbarButtonData showParameters;

        public ToolbarView(BaseGraphWindow _graphWindow)
        {
            name = "ToolbarView";
            graphWindow = _graphWindow;

            leftButtonDatas.Clear();
            rightButtonDatas.Clear();

            IMGUIContainer container = new IMGUIContainer(DrawImGUIToolbar);
            container.StretchToParentSize();
            Add(container);
        }

        public void AddButton(string name, Action callback, bool left = true)
            => AddButton(new GUIContent(name), callback, left);

        public void AddButton(GUIContent content, Action callback, bool left = true)
        {
            var data = new ToolbarButtonData
            {
                content = content,
                type = ElementType.Button,
                buttonCallback = callback
            };
            (left ? leftButtonDatas : rightButtonDatas).Add(data);
        }

        public void AddToggle(string name, bool defaultValue, Action<bool> callback, bool left = true)
            => AddToggle(new GUIContent(name), defaultValue, callback, left);

        public void AddToggle(GUIContent content, bool defaultValue, Action<bool> callback, bool left = true)
        {
            var data = new ToolbarButtonData
            {
                content = content,
                type = ElementType.Toggle,
                value = defaultValue,
                toggleCallback = callback
            };
            (left ? leftButtonDatas : rightButtonDatas).Add(data);
        }

        public void AddDropDownButton(string name, Action callback, bool left = true)
            => AddDropDownButton(new GUIContent(name), callback, left);

        public void AddDropDownButton(GUIContent content, Action callback, bool left = true)
        {
            var data = new ToolbarButtonData
            {
                content = content,
                type = ElementType.DropDownButton,
                buttonCallback = callback
            };
            (left ? leftButtonDatas : rightButtonDatas).Add(data);
            //return data;
        }

        /// <summary> Also works for toggles </summary>
        public void RemoveButton(string name, bool left)
        {
            (left ? leftButtonDatas : rightButtonDatas).RemoveAll(b => b.content.text == name);
        }

        /// <summary> Hide the button </summary>
        /// <param name="name">Display name of the button</param>
        protected void HideButton(string name)
        {
            leftButtonDatas.Concat(rightButtonDatas).All(b =>
            {
                if (b.content.text == name)
                    b.visible = false;
                return true;
            });
        }

        /// <summary> Show the button </summary>
        /// <param name="name">Display name of the button</param>
        protected void ShowButton(string name)
        {
            leftButtonDatas.Concat(rightButtonDatas).All(b =>
            {
                if (b.content.text == name)
                    b.visible = true;
                return true;
            });
        }

        void DrawImGUIButtonList(List<ToolbarButtonData> buttons)
        {
            foreach (var button in buttons)
            {
                if (!button.visible)
                    continue;

                switch (button.type)
                {
                    case ElementType.Button:
                        if (GUILayout.Button(button.content, EditorStyles.toolbarButton) && button.buttonCallback != null)
                            button.buttonCallback();
                        break;
                    case ElementType.Toggle:
                        EditorGUI.BeginChangeCheck();
                        button.value = GUILayout.Toggle(button.value, button.content, EditorStyles.toolbarButton);
                        if (EditorGUI.EndChangeCheck() && button.toggleCallback != null)
                            button.toggleCallback(button.value);
                        break;
                    case ElementType.DropDownButton:
                        if (EditorGUILayout.DropdownButton(button.content, FocusType.Passive, EditorStyles.toolbarDropDown))
                            button.buttonCallback();
                        break;
                }
            }
        }

        protected virtual void DrawImGUIToolbar()
        {
            GUILayout.BeginHorizontal();

            DrawImGUIButtonList(leftButtonDatas);

            GUILayout.FlexibleSpace();

            DrawImGUIButtonList(rightButtonDatas);

            GUILayout.EndHorizontal();
        }
    }
}
