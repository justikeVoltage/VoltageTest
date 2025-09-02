/****************************************************
    功能：obi Rope快速修改工具
    作者：WH、ZZQ
    创建日期：#2025/06/24#
    修改内容：
        1.增加选中节点内部增删节点、偏移选中节点功能 #2025/07/07#
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;
using Voltage;
using NaughtyAttributes;
using Unity.VisualScripting;
using Unity.Mathematics;
using UnityEngine.Events;

namespace Voltage
{
    public enum ObiPointRemoveMode
    {
        All,
        Middle
    }
    /// <summary>
    /// 按钮在ObiRopePointToolsEditor类中实现
    /// </summary>
    public class ObiRopePointTools : MonoBehaviour
    {
        //公开变量（Inspector面板-通过editor绘制）
        // 蓝图相关属性
        public float m_thickness = 0.00622f;
        public float m_resolution = 0.2f;
        public int m_pooledParticles = 0;
        // 节点生成相关属性
        public float m_RopeLength = 2;
        public int m_InsertPointCount = 2;
        public float m_Mass = 0.1f;
        public int m_Category = 1;
        public int m_Mask = 65521;//代表1, 2, 3层以外的所有obi.categoryNames层

        //节点修改相关属性
        /// <summary> 选定点的起始节点 </summary>
        public ObiParticleGroup m_startGroup = null;
        /// <summary> 选定点的结束节点 </summary>
        public ObiParticleGroup m_endGroup = null;
        /// <summary> 选定点中间要添加的点数 </summary>
        public int m_middlePointCount = 2;
        /// <summary> 选定点组的偏移量 </summary>
        public float m_pointOffset = 0.1f;

        /// <summary> 节点改变时的回调 </summary>
        public UnityEvent onControlPointChanged;

        //内部变量
        /// <summary> 两节点间距占全长的比例（0-1之间） </summary>
        private float IntervalRatio
        {
            get
            {
                float intervalRatio = 1.0f / (m_InsertPointCount + 1);
                return (float)System.Math.Round(intervalRatio, 6);
            }
        }

        /// <summary> 生成节点时两节点间距 </summary>
        private float IntervalSize => (float)System.Math.Round(m_RopeLength / (m_InsertPointCount + 1), 6);
        /// <summary> 节点生成时的初始两节点间距 </summary>
        private float InitIntervalSize;
        /// <summary> 添加点的大概比例列表（0-1之间, 根据添加的节点数量计算） </summary>
        private List<float> MuList
        {
            get
            {
                List<float> muList = new List<float>(m_InsertPointCount);
                for (int i = 0; i < m_InsertPointCount; i++)
                {
                    muList.Add(IntervalRatio * (i + 1));
                }
                return muList;
            }
        }
        /// <summary> 生成节点时计算的节点内切线 </summary>
        private Vector3 InTangent => new Vector3(-Mathf.Min(IntervalSize * 0.5f, 0.25f), 0, 0);
        /// <summary> 生成节点时计算的节点外切线 </summary>
        private Vector3 OutTangent => new Vector3(Mathf.Min(IntervalSize * 0.5f, 0.25f), 0, 0);

        #region Blueprint修改
        public void ModifyBlueprint(ObiRopeBase rope)
        {
            RemoveControlPoint(rope, ObiPointRemoveMode.All);
            ObiRopeBlueprint blueprint;
            if (GetBlueprint(rope, out blueprint) == false)
            {
                return;
            }
            blueprint.thickness = m_thickness;
            blueprint.resolution = m_resolution;
            blueprint.pooledParticles = m_pooledParticles;
        }
        private bool GetBlueprint(ObiRopeBase rope, out ObiRopeBlueprint blueprint)
        {
            if (rope is ObiRope _rope)
            {
                blueprint = _rope.ropeBlueprint;
                if (blueprint == null)
                {
                    Debug.LogError("ObiRope组件缺少有效蓝图");
                    return false;
                }
                return true;
            }
            else
            {
                Debug.LogError("未找到ObiRope组件");
                blueprint = null;
                return false;
            }
        }
        #endregion

        #region 节点生成
        public void ModifyRope(ObiRopeBase rope)
        {
            if (RopeVerify(rope) == false)
            {
                Debug.LogError("未找到正确的Rope相关资源, 退出方法体...");
                return;
            }
            // 1.移除中间的控制点
            if (rope.path.ControlPointCount > 2)
                RemoveControlPoint(rope);
            // 2.设置绳索长度
            SetRopeLength(rope);
            // 3.生成新的节点
            if (m_InsertPointCount < 0)
            {
                Debug.LogError("插入点数不能为负数");
                return;
            }
            List<float> muList = MuList;

            // 3.1遍历muList, 逐个插入点
            for (int i = 0; i < m_InsertPointCount; i++)
            {
                int newIndex = rope.path.InsertControlPoint(muList[i]);
                UtilsVoltage.DebugLog(Color.green, $"插入新顶点, 索引：{newIndex}");
                if (newIndex < 0)
                {
                    Debug.LogError("插入顶点失败");
                    return;
                }
            }
            // 3.2刷新路径事件（必须调用以更新绳索状态）
            rope.path.FlushEvents();
            UtilsVoltage.DebugLog(Color.green, $"当前顶点数：{rope.path.ControlPointCount}");
            // 3.3遍历所有插入的节点, 设置属性
            ResetNodeproperties(rope, 1, rope.path.ControlPointCount - 1);
            // 3.4重新设置节点位置和切线,全部靠近一侧后重新设置点位,以避免切线被挤压
            float MiniatureInterval = IntervalSize / m_InsertPointCount;
            int d = 0;
            for (int i = rope.path.ControlPointCount - 2; i > 0; --i)
            {
                d++;
                rope.path.points.data[i] = new ObiWingedPoint(Vector3.zero, new Vector3((m_RopeLength * 0.5f) - (d * MiniatureInterval), 0, 0), Vector3.zero);
            }
            for (int i = 1; i < rope.path.ControlPointCount - 1; ++i)
            {
                rope.path.points.data[i] = new ObiWingedPoint(InTangent, new Vector3((i * IntervalSize) - (m_RopeLength * 0.5f), 0, 0), OutTangent);
            }
            // 4.重置始末点的切线
            SetRopeLength(rope);

            // 其他设置
            InitIntervalSize = IntervalSize;
        }

        /// <summary>
        /// 根据两端点位信息修改-设置Rope长度、name、property
        /// </summary>
        /// <param name="rope"></param>
        public void SetRopeLength(ObiRopeBase rope)
        {
            if (RopeVerify(rope) == false)
            {
                Debug.LogError("未找到正确的Rope相关资源, 退出方法体...");
                return;
            }
            //始末点设置--长度控制
            int StartIndex = 0;
            int EndIndex = rope.path.ControlPointCount - 1;
            rope.path.points.data[StartIndex] = new ObiWingedPoint(InTangent, new Vector3(-(m_RopeLength * 0.5f), 0, 0), OutTangent);
            rope.path.points.data[EndIndex] = new ObiWingedPoint(InTangent, new Vector3(m_RopeLength * 0.5f, 0, 0), OutTangent);

            rope.path.SetName(StartIndex, "Start");
            rope.path.SetName(EndIndex, "End");

            SetControlPointProperty(rope, StartIndex);
            SetControlPointProperty(rope, EndIndex);
        }
        #endregion

        #region 节点修改
        /// <summary>
        /// 改变选中节点内的节点数量
        /// </summary>
        /// <param name="rope"></param>
        /// <param name="middlePointcount">最终节点数量</param>
        public void AddMiddleControlPoint(ObiRopeBase rope, int middlePointcount)
        {
            if (RopeVerify(rope) == false)
            {
                Debug.LogError("未找到正确的Rope相关资源, 退出方法体...");
                return;
            }
            //获取修改的起始和终止节点的索引
            if (!GetIndexOfSelectPoint(rope, m_startGroup, m_endGroup, out int start, out int end)) return;
            if (start == end) {Debug.LogError("起始节点和终止节点不能相同"); return; }
            if (InitIntervalSize == 0) InitIntervalSize = IntervalSize;
            // UtilsVoltage.DebugLog(Color.yellow, $"初始化长度为{InitIntervalSize} ");

            //移除中间节点
            if (RemoveMiddleControlPoint(rope) == false) return;

            //插入中间节点--重新计算mu值（生成位置参数）
            int _pointCount = rope.path.ControlPointCount;
            float[] mus = new float[middlePointcount];
            for (int i = 0; i < middlePointcount; i++)
            {
                //计算上下两个节点的占rope的比例(增加i个节点后的)
                float lastMu = (float)GetMuByPointIndex(rope, start + i, _pointCount + i);
                float nextMu = (float)GetMuByPointIndex(rope, start + i + 1, _pointCount + i);
                mus[i] = lastMu + (nextMu - lastMu) * 0.1f;
            }
            for (int i = 0; i < mus.Length; i++)
            {
                int newIndex = rope.path.InsertControlPoint(mus[i]);
                if (newIndex < 0)
                {
                    Debug.LogError("插入顶点失败");
                    return;
                }
            }
            //刷新路径事件（必须调用以更新绳索状态）
            rope.path.FlushEvents();
            //重设位置和切线

            Vector3 startPos = rope.path.points.data[start].position;
            Vector3 endPos = rope.path.points.data[start + m_middlePointCount + 1].position;
            float SelectedLength = Mathf.Abs(startPos.x - endPos.x);
            float SelectedInterval = SelectedLength / (m_middlePointCount + 1);
            float MiniatureSelectedInterval = SelectedInterval / m_middlePointCount + 1;
            // UtilsVoltage.DebugLog(Color.yellow, $"开始点为{start}, 终止点为{end}, 选中长度为{SelectedLength}, 选中间隔为{SelectedInterval}, 小间隔为{MiniatureSelectedInterval} ");
            Vector3 SelectedInTangent = new Vector3(-Mathf.Min(SelectedInterval * 0.5f, 0.25f), 0, 0);
            Vector3 SelectedOutTangent = new Vector3(Mathf.Min(SelectedInterval * 0.5f, 0.25f), 0, 0);
            // 重新设置节点位置和切线,全部靠近一侧后重新设置点位,以避免切线被挤压
            // 靠右侧设置增设节点位置
            int d = 0;
            for (int i = start + m_middlePointCount; i > start; --i)
            {
                d++;
                rope.path.points.data[i] = new ObiWingedPoint(Vector3.zero, new Vector3(endPos.x - d * MiniatureSelectedInterval, 0, 0), Vector3.zero);
            }
            // 正常设置增设节点位置和切线
            for (int i = start + 1; i < start + m_middlePointCount + 1; i++)
            {
                rope.path.points.data[i] = new ObiWingedPoint(SelectedInTangent, new Vector3(startPos.x + ((i - start) * SelectedInterval), 0, 0), SelectedOutTangent);
            }
            // 设置选中节点的切线
            rope.path.points.data[start] = new ObiWingedPoint(InTangent, new Vector3(startPos.x, 0, 0), SelectedOutTangent);
            rope.path.points.data[start + m_middlePointCount + 1] = new ObiWingedPoint(SelectedInTangent, new Vector3(endPos.x, 0, 0), OutTangent);
            //重置节点名和属性
            ResetNodeproperties(rope, 1, rope.path.ControlPointCount - 1);
        }
        /// <summary> 移除选中节点内的所有节点 </summary>
        public bool RemoveMiddleControlPoint(ObiRopeBase rope)
        {
            if (RopeVerify(rope) == false)
            {
                Debug.LogError("未找到正确的Rope相关资源, 退出方法体...");
                return false;
            }
            //获取修改的起始和终止节点的索引
            if (!GetIndexOfSelectPoint(rope, m_startGroup, m_endGroup, out int start, out int end)) return false;
            if (start == end) { Debug.LogError("起始节点和终止节点不能相同"); return false; }

            //移除中间节点
            if (start + 1 < end - 1)
            {
                RemoveControlPoint(rope, start + 1, end - 1);
                ResetNodeproperties(rope);
                rope.path.FlushEvents();
                return true;
            }
            else if (start + 1 == end - 1)
            {
                RemoveControlPoint(rope, start + 1);
                ResetNodeproperties(rope);
                rope.path.FlushEvents();
                return true;
            }
            else if (start == end)
            {
                Debug.LogError("起始节点和终止节点不能相同");
                return false;
            }
            else if (start + 1 == end)
            {
                Debug.Log("移除节点中间节点：中间无节点");
            }
            return true;
        }
        /// <summary> 对选中节点组进行偏移 </summary>
        public void OffsetControlPoint(ObiRopeBase rope)
        {
            if (RopeVerify(rope) == false)
            {
                Debug.LogError("未找到正确的Rope相关资源, 退出方法体...");
                return;
            }
            if (m_pointOffset == 0)
            {
                UtilsVoltage.DebugLog(Color.yellow, $"偏移值为0, 无偏移操作");
                return;
            }

            // 获取修改的起始和终止节点的索引
            if (!GetIndexOfSelectPoint(rope, m_startGroup, m_endGroup, out int start, out int end)) return;

            // 偏移危险行为判断
            if (m_pointOffset > 0 && end != rope.path.ControlPointCount - 1)
            {
                //偏移后超过右侧点的位置
                if (rope.path.points.data[end].position.x + m_pointOffset >= rope.path.points.data[end + 1].position.x)
                {
                    Debug.LogError("移动后可能超过后一节点位置, 无法安全偏移");
                    return;
                }
            }
            else if (m_pointOffset < 0 && start != 0)
            {
                //偏移后超过左侧点的位置
                if (rope.path.points.data[start].position.x + m_pointOffset <= rope.path.points.data[start - 1].position.x)
                {
                    Debug.LogError("移动后可能超过前一节点位置, 无法安全偏移");
                    return;
                }
            }


            if (start == end)
            {
                UtilsVoltage.DebugLog(Color.yellow, $"第{start}个节点, 偏移值为{m_pointOffset}");
                // 单节点偏移
                Vector3 oldPos = rope.path.points.data[start].position;
                Vector3 inTangent = rope.path.points.data[start].inTangent;
                Vector3 outTangent = rope.path.points.data[start].outTangent;
                rope.path.points.data[start] = new ObiWingedPoint(inTangent, new Vector3(oldPos.x + m_pointOffset, 0, 0), outTangent);
                UtilsVoltage.DebugLog(Color.yellow, $"第{start}个节点移动{m_pointOffset} 完成");
            }
            else
            {
                UtilsVoltage.DebugLog(Color.yellow, $"开始节点为{start}, 终止节点为{end}, 偏移值为{m_pointOffset} ");
                // 根据偏移数值判断首先移动的节点
                if (m_pointOffset > 0)
                {
                    //右侧首先移动
                    for (int i = end; i >= start; --i)
                    {
                        Vector3 oldPos = rope.path.points.data[i].position;
                        Vector3 inTangent = rope.path.points.data[i].inTangent;
                        Vector3 outTangent = rope.path.points.data[i].outTangent;
                        rope.path.points.data[i] = new ObiWingedPoint(inTangent, new Vector3(oldPos.x + m_pointOffset, 0, 0), outTangent);
                    }
                    UtilsVoltage.DebugLog(Color.yellow, $"选中节点组移动{m_pointOffset} 完成");
                }
                else
                {
                    //判断出界
                    //左侧首先移动
                    for (int i = start; i <= end; ++i)
                    {
                        Vector3 oldPos = rope.path.points.data[i].position;
                        Vector3 inTangent = rope.path.points.data[i].inTangent;
                        Vector3 outTangent = rope.path.points.data[i].outTangent;
                        rope.path.points.data[i] = new ObiWingedPoint(inTangent, new Vector3(oldPos.x + m_pointOffset, 0, 0), outTangent);
                    }
                    UtilsVoltage.DebugLog(Color.yellow, $"选中节点组移动{m_pointOffset} 完成");
                }
            }
            // 刷新路径事件（必须调用以更新绳索状态）
            rope.path.FlushEvents();
        }

        /// <summary>
        /// 根据选择的起始和终止节点,获取其索引并排序
        /// </summary>
        /// <param name="rope"></param>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        public bool GetIndexOfSelectPoint(ObiRopeBase rope, ObiParticleGroup startParticleGroup, ObiParticleGroup endParticleGroup, out int startIndex, out int endIndex)
        {
            startIndex = -1; endIndex = -1;
            if (startParticleGroup == null || endParticleGroup == null)
            {
                Debug.LogError("未选择起始或终止节点");
                return false;
            }
            // if (startParticleGroup == endParticleGroup)
            // {
            //     Debug.LogError("起始节点和终止节点不能相同");
            //     return false;
            // }

            GetIndexOfParticleGroup(rope, startParticleGroup, ref startIndex);
            GetIndexOfParticleGroup(rope, endParticleGroup, ref endIndex);

            if (startIndex >= 0 && endIndex >= 0 && startIndex > endIndex)
            {
                int temp = startIndex;
                startIndex = endIndex;
                endIndex = temp;
            }

            return true;
            // UtilsVoltage.DebugLog(Color.green, $"已找到起始点索引 ({startIndex}) 和终止点索引 ({endIndex}) ");
        }

        /// <summary>  根据ParticleGroup获取其索引 </summary>
        private void GetIndexOfParticleGroup(ObiRopeBase rope, ObiParticleGroup group, ref int index)
        {
            var blueprint = rope.sourceBlueprint;

            for (int i = 0; i < blueprint.groups.Count; ++i)
            {
                if (blueprint.groups[i] == group)
                {
                    index = i;
                    return;
                }
            }
            if (index < 0)
            {
                Debug.LogError($"未在{rope.sourceBlueprint.name}_中找到{group.name}");
            }
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 对rope和蓝图资源进行查找验证
        /// </summary>
        /// <param name="rope">目标Obi绳索实例</param>
        /// <returns></returns>
        private bool RopeVerify(ObiRopeBase rope)
        {
            if (rope == null || rope.path == null)
            {
                Debug.LogError("无效的Obi绳索或路径");
                return false;
            }

            // 获取绳索蓝图（用于Undo记录, 编辑器环境需要）
            var blueprint = rope.sharedBlueprint as ObiRopeBlueprintBase;
            if (blueprint == null)
            {
                Debug.LogError("绳索缺少有效蓝图");
                return false;
            }

            return true;
        }
        /// <summary> 重命名节点名为索引值 </summary>
        /// <param name="rope"></param>
        /// <param name="startIndex">起始节点索引</param>
        /// <param name="endIndex">终止节点索引</param>
        private void ResetNodeproperties(ObiRopeBase rope, int startIndex, int endIndex)
        {
            for (int i = startIndex; i < endIndex; ++i)
            {
                rope.path.SetName(i, i.ToString());
                //rope ControlPoint Property
                SetControlPointProperty(rope, i);
            }
        }
        /// <summary> 重命名节点名为索引值 </summary>
        private void ResetNodeproperties(ObiRopeBase rope)
        {
            for (int i = 1; i < rope.path.ControlPointCount - 1; ++i)
            {
                rope.path.SetName(i, i.ToString());
                //rope ControlPoint Property
                SetControlPointProperty(rope, i);
            }
        }
        /// <summary>
        /// 设置节点属性(质量, 类别, 交互类)
        /// </summary>
        /// <param name="rope">目标Obi绳索实例</param>
        /// <param name="index">节点索引</param>
        private void SetControlPointProperty(ObiRopeBase rope, int index)
        {
            rope.path.masses[index] = m_Mass;
            rope.path.filters[index] = ObiUtils.MakeFilter(ObiUtils.GetMaskFromFilter(rope.path.filters[index]), m_Category);
            rope.path.filters[index] = ObiUtils.MakeFilter(m_Mask, ObiUtils.GetCategoryFromFilter(rope.path.filters[index]));
        }
        /// <summary>
        /// 删除节点
        /// </summary>
        /// <param name="rope">目标Obi绳索实例</param>
        public void RemoveControlPoint(ObiRopeBase rope, ObiPointRemoveMode mode = ObiPointRemoveMode.Middle)
        {
            if (RopeVerify(rope) == false)
            {
                Debug.LogError("未找到正确的Rope相关资源, 退出方法体...");
                return;
            }
            // 移除中间的控制点
            switch (mode)
            {
                case ObiPointRemoveMode.All:
                    UtilsVoltage.DebugLog(Color.green, "重置节点...");
                    rope.path.Clear();
                    break;
                case ObiPointRemoveMode.Middle:
                    UtilsVoltage.DebugLog(Color.green, $"移除中间节点...");
                    RemoveControlPoint(rope, 1, rope.path.ControlPointCount - 2);
                    break;
            }

            InitIntervalSize = IntervalSize;
        }
        private void RemoveControlPoint(ObiRopeBase rope, int StartIndex, int EndIndex)
        {
            int pointCount = rope.path.ControlPointCount;
            if (StartIndex < 0 || StartIndex >= pointCount - 1 || EndIndex < 0 || EndIndex >= pointCount - 1)
            {
                Debug.LogError("索引超出范围");
                return;
            }
            if (StartIndex > EndIndex)
            {
                Debug.LogError("起始索引不能大于终止索引,检查是否存在中间点");
                return;
            }

            onControlPointChanged?.Invoke();
            for (int i = EndIndex; i >= StartIndex; i--)
            {
                rope.path.RemoveControlPoint(i);
            }

        }
        private void RemoveControlPoint(ObiRopeBase rope, int index)
        {
            int pointCount = rope.path.ControlPointCount;
            if (index < 0 || index >= pointCount - 1)
            {
                Debug.LogError("索引超出范围");
            }
            onControlPointChanged?.Invoke();

            rope.path.RemoveControlPoint(index);
        }
        /// <summary>
        /// 根据节点索引获取mu值（此节点和之前所有节点的数量和/全部节点数）
        /// </summary>
        /// <param name="rope"></param>
        /// <param name="index">节点索引</param>
        /// <param name="pointCount">全部节点数量</param>
        /// <returns></returns>
        private float GetMuByPointIndex(ObiRopeBase rope, int index, int pointCount = -1)
        {
            if (pointCount == -1)
                pointCount = rope.path.ControlPointCount;

            if (index < 0 || index >= pointCount - 1)
            {
                Debug.LogError("计算mu值的节点索引超出范围");
                return -1;
            }

            float indexRatio = (index + 1.0f) / pointCount;
            indexRatio = (float)System.Math.Round(indexRatio, 6);//保留小数点后6位
            // Debug.Log($"当节点索引为{index}, 节点总数为{pointCount}时, 节点索引比例: {indexRatio}");
            return indexRatio;
        }
        #endregion
    }
}
