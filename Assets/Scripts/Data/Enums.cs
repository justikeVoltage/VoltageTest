/****************************************************
    功能：枚举类型
    作者：ZH
    创建日期：#2025/01/10#
    修改内容：
        1.新增控制器按键类型    2025/03/11 ZH
*****************************************************/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voltage
{
    /// <summary>
    /// 运动模式
    /// </summary>
    public enum EMoveMode
    {
        /// <summary>
        /// 物理刚体运动
        /// </summary>
        MovebyPhysics,
        /// <summary>
        /// 运动学刚体运动
        /// </summary>
        MoveByKinematic,
    }

    /// <summary>
    /// 手部类型
    /// </summary>
    public enum EHandType
    {
        Left,
        Right,
        Both,
    }

    /// <summary>
    /// 控制器按键类型
    /// </summary>
    public enum EControllerButtonType
    {
        Stick,
        Trigger,
        Grip,
        PrimaryButton,
        SecondaryButton,
    }

    /// <summary>
    /// 训练类型
    /// </summary>
    public enum ETrainType
    {
        None,
        /// <summary>
        /// 教学模式
        /// </summary>
        Teach,
        /// <summary>
        /// 训练模式
        /// </summary>
        Train,
    }

    /// <summary>
    /// 游戏对象类型
    /// </summary>
    public enum EGOType
    {
        UI2D,
        GO3D,
        All,
    }

    /// <summary>
    /// 面板显示状态
    /// </summary>
    public enum DisplayStatus
    {
        CloseAll,
        OnlyLeft,
        OnlyRight,
        OpenAll
    }

    /// <summary>
    /// 安装状态
    /// </summary>
    public enum EInstallState
    {
        /// <summary>
        /// 未安装
        /// </summary>
        Uninstall,
        /// <summary>
        /// 安装
        /// </summary>
        Install,
    }

    /// <summary>
    /// 模式类型
    /// </summary>
    public enum EModeType
    {
        /// <summary>
        /// 专业教学
        /// </summary>
        Training,
        /// <summary>
        /// 专业训练
        /// </summary>
        Practice,
        /// <summary>
        /// 效能考核
        /// </summary>
        Timed_Challenge,
        /// <summary>
        /// 专业考核
        /// </summary>
        Assessment,
        /// <summary>
        /// 硬件教学
        /// </summary>
        Device_Training,
    }

    /// <summary>
    /// 模块类型
    /// </summary>
    public enum EModuleType
    {
        /// <summary>
        /// 布线轧带模块
        /// </summary>
        Cable_Routing,
        /// <summary>
        /// 接插模块学习
        /// </summary>
        Connector_Assembly,
        /// <summary>
        /// SOP流程模块学习
        /// </summary>
        SOP_Process,
    }
}