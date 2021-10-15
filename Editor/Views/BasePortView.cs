﻿#region 注 释
/***
 *
 *  Title:
 *  
 *  Description:
 *  
 *  Date:
 *  Version:
 *  Writer: 半只龙虾人
 *  Github: https://github.com/HalfLobsterMan
 *  Blog: https://www.crosshair.top/
 *
 */
#endregion
using System;
using UnityEditor.Experimental.GraphView;

namespace CZToolKit.GraphProcessor.Editors
{
    public partial class BasePortView
    {
        public BasePortView(BasePort port, Type portType, IEdgeConnectorListener connectorListener) : this(
            orientation: port.orientation == BasePort.Orientation.Horizontal ? Orientation.Horizontal : Orientation.Vertical,
            direction: port.direction == BasePort.Direction.Input ? Direction.Input : Direction.Output,
            capacity: port.capacity == BasePort.Capacity.Single ? Capacity.Single : Capacity.Multi,
            portType, connectorListener)
        {

        }
    }
}