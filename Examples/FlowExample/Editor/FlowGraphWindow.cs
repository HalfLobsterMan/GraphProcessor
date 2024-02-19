#region 注 释

/***
 *
 *  Title:
 *  
 *  Description:
 *  
 *  Date:
 *  Version:
 *  Writer: 半只龙虾人
 *  Github: https://github.com/haloman9527
 *  Blog: https://www.haloman.net/
 *
 */

#endregion

#if UNITY_EDITOR
using CZToolKit.GraphProcessor;
using CZToolKit.GraphProcessor.Editors;
using System.Collections.Generic;
using CZToolKit;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.UIElements;

[CustomView(typeof(FlowGraph))]
public class FlowGraphWindow : BaseGraphWindow
{
    protected override BaseGraphView NewGraphView()
    {
        return new FlowGraphView(Graph, this, new CommandDispatcher());
    }

    protected override void OnKeyDownCallback(KeyDownEvent evt)
    {
        base.OnKeyDownCallback(evt);
        if (evt.commandKey || evt.ctrlKey)
        {
            switch (evt.keyCode)
            {
                case KeyCode.D:
                    Duplicate();
                    evt.StopImmediatePropagation();
                    break;
            }
        }
    }

    private void Duplicate()
    {
        if (GraphView == null)
            return;
        // 收集所有节点，连线
        Dictionary<int, BaseNode> nodes = new Dictionary<int, BaseNode>();
        List<BaseConnection> connections = new List<BaseConnection>();
        List<BaseGroup> groups = new List<BaseGroup>();
        foreach (var item in GraphView.selection)
        {
            switch (item)
            {
                case BaseNodeView nodeView:
                    nodes.Add(nodeView.ViewModel.ID, nodeView.ViewModel.Model);
                    break;
                case BaseConnectionView connectionView:
                    connections.Add(connectionView.ViewModel.Model);
                    break;
                case BaseGroupView groupView:
                    groups.Add(groupView.ViewModel.Model);
                    break;
            }
        }

        GraphView.CommandDispatcher.BeginGroup();

        var nodesStr = Sirenix.Serialization.SerializationUtility.SerializeValue(nodes, DataFormat.Binary);
        var connectionsStr = Sirenix.Serialization.SerializationUtility.SerializeValue(connections, DataFormat.Binary);
        var groupsStr = Sirenix.Serialization.SerializationUtility.SerializeValue(groups, DataFormat.Binary);

        nodes = Sirenix.Serialization.SerializationUtility.DeserializeValue<Dictionary<int, BaseNode>>(nodesStr, DataFormat.Binary);
        connections = Sirenix.Serialization.SerializationUtility.DeserializeValue<List<BaseConnection>>(connectionsStr, DataFormat.Binary);
        groups = Sirenix.Serialization.SerializationUtility.DeserializeValue<List<BaseGroup>>(groupsStr, DataFormat.Binary);

        var graph = GraphView.ViewModel;
        var nodeMaps = new Dictionary<int, BaseNodeProcessor>();

        GraphView.ClearSelection();

        foreach (var pair in nodes)
        {
            pair.Value.id = graph.NewID();
            pair.Value.position += new InternalVector2Int(50, 50);
            var vm = ViewModelFactory.CreateViewModel(pair.Value) as BaseNodeProcessor;
            GraphView.CommandDispatcher.Do(new AddNodeCommand(graph, vm));
            nodeMaps[pair.Key] = vm;
            GraphView.AddToSelection(GraphView.NodeViews[vm.ID]);
        }

        foreach (var connection in connections)
        {
            if (nodeMaps.TryGetValue(connection.fromNode, out var from))
                connection.fromNode = from.ID;

            if (nodeMaps.TryGetValue(connection.toNode, out var to))
                connection.toNode = to.ID;

            var vm = ViewModelFactory.CreateViewModel(connection) as BaseConnectionProcessor;
            GraphView.CommandDispatcher.Do(new ConnectCommand(graph, vm));
            GraphView.AddToSelection(GraphView.ConnectionViews[vm]);
        }

        foreach (var group in groups)
        {
            for (int i = group.nodes.Count - 1; i >= 0; i--)
            {
                if (nodeMaps.TryGetValue(group.nodes[i], out var node))
                    group.nodes[i] = node.ID;
                else
                    group.nodes.RemoveAt(i);
            }

            group.id = graph.NewID();
            GraphView.CommandDispatcher.Do(new AddGroupCommand(graph, group));
            GraphView.AddToSelection(GraphView.GroupViews[group.id]);
        }

        GraphView.CommandDispatcher.EndGroup();
    }
}
#endif