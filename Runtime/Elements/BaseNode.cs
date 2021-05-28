﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace CZToolKit.GraphProcessor
{
    [Serializable]
    public abstract class BaseNode
    {
        #region 静态
        /// <summary> 根据T创建一个节点，并设置位置 </summary>
        public static T CreateNew<T>(Vector2 _position) where T : BaseNode
        {
            return CreateNew(typeof(T), _position) as T;
        }

        /// <summary> 根据_type创建一个节点，并设置位置 </summary>
        public static BaseNode CreateNew(Type _type, Vector2 _position)
        {
            if (!_type.IsSubclassOf(typeof(BaseNode)))
                return null;
            var node = Activator.CreateInstance(_type) as BaseNode;
            node.position = new Rect(_position, new Vector2(100, 100));
            IDAllocation(node);
            node.OnCreated();
            return node;
        }

        /// <summary> 给节点分配一个GUID，这将会覆盖已有GUID </summary>
        public static void IDAllocation(BaseNode _node)
        {
            _node.guid = Guid.NewGuid().ToString();
        }
        #endregion

        [NonSerialized]
        BaseGraph owner;

        /// <summary> 唯一标识 </summary>
        [SerializeField, HideInInspector]
        string guid;
        /// <summary> 位置坐标 </summary>
        [SerializeField, HideInInspector]
        public Rect position;
        /// <summary> 展开状态 </summary>
        [SerializeField, HideInInspector]
        bool expanded = true;
        /// <summary> 锁定状态 </summary>
        [SerializeField, HideInInspector]
        bool locked = false;
        [SerializeField, HideInInspector]
        Dictionary<string, NodePort> ports = new Dictionary<string, NodePort>();

        /// <summary> 节点对象的所有者，即图 </summary>
        public BaseGraph Owner { get { return owner; } }
        public string GUID { get { return guid; } }
        public bool Expanded { get { return expanded; } set { expanded = value; } }
        public bool Locked { get { return locked; } set { locked = value; } }
        public Dictionary<string, NodePort> Ports { get { return ports; } }

        protected BaseNode() { }

        /// <summary> 在节点被创建出来后调用，调用优先级最高 </summary>
        public virtual void OnCreated() { }

        /// <summary> 请不要在其它任何地方调用 </summary>
        internal void Initialize(BaseGraph _graph)
        {
            owner = _graph;
            foreach (var port in Ports.Values)
            {
                port.Owner = this;
            }
        }

        public virtual void InitializeGraphOwner(GraphOwner _graphOwner) { }

        #region Ports
        /// <summary> 通过名字获取一个Input接口 </summary>
        public bool TryGetInputPort(string _fieldName, out NodePort _nodePort)
        {
            if (TryGetPort(_fieldName, out _nodePort) && _nodePort.Direction == PortDirection.Input)
                return true;
            _nodePort = null;
            return false;
        }

        /// <summary> 通过名字获取一个Output接口 </summary>
        public bool TryGetOutputPort(string _fieldName, out NodePort _nodePort)
        {
            if (TryGetPort(_fieldName, out _nodePort) && _nodePort.Direction == PortDirection.Output)
                return true;
            _nodePort = null;
            return false;
        }

        /// <summary> 通过名字获取一个接口 </summary>
        public bool TryGetPort(string _fieldName, out NodePort _nodePort)
        {
            if (Ports.TryGetValue(_fieldName, out _nodePort)) return true;
            else return false;
        }

        /// <summary> 接口是否存在 </summary>
        public bool HasPort(string _fieldName)
        {
            return Ports.ContainsKey(_fieldName);
        }

        #endregion

        #region Inputs/Outputs
        public bool TryGetInputValue<T>(string _fieldName, out T _value, T _fallback = default)
        {
            _value = _fallback;
            if (TryGetInputPort(_fieldName, out NodePort port))
                return port.TryGetConnectValue(ref _value);
            return false;
        }

        public bool TryGetOutputValue<T>(string _fieldName, out T _value, T _fallback = default)
        {
            _value = _fallback;
            if (TryGetOutputPort(_fieldName, out NodePort port))
                return port.TryGetConnectValue(ref _value);
            return false;
        }

        public bool TryGetConnectValue<T>(string _fieldName, out T _value, T _fallback = default)
        {
            _value = _fallback;
            if (TryGetPort(_fieldName, out NodePort port))
                return port.TryGetConnectValue(ref _value);
            return false;
        }

        /// <summary> 通过input或output接口返回相应的值(此方法从外部调用，不在内部使用，仅重写) </summary>
        public virtual bool GetValue<T>(NodePort _port, ref T _value)
        {
            Debug.LogWarning("No GetValue(NodePort port) override defined for " + GetType());
            return false;
        }

        /// <summary> 从外部调用 </summary>
        public virtual void Execute(NodePort _port, params object[] _params) { }

        /// <summary> 调用端口连接的Execute方法 </summary>
        public void ExecuteConnections(string _portName, params object[] _params)
        {
            if (TryGetPort(_portName, out NodePort port))
                ExecuteConnections(port, _params);
        }

        public void ExecuteConnections(NodePort _port, params object[] _params)
        {
            foreach (var targetPort in _port.GetConnections())
            {
                targetPort.Execute(_params);
            }
        }
        #endregion

        public virtual void OnConnected(NodePort _port, NodePort _targetPort) { }

        public virtual void OnDisconnected(NodePort _port, NodePort _targetPort) { }

        /// <summary> 接口动态类型 </summary>
        public virtual Type PortDynamicType(string _portName) { return null; }

        public virtual void DrawGizmos(GraphOwner _graphOwner) { }

        public void ClearConnections()
        {
            foreach (var port in Ports.Values)
            {
                port.EdgeGUIDS.Clear();
            }
        }
    }
}
