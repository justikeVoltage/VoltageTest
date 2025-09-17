using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using Obi;

namespace Voltage
{
    public class AutoControlPointAndPlugGeneration : MonoBehaviour
    {
        [Header("基础引用")]
        [Tooltip("所有SocketBase的集合父物体")]
        public GameObject socketParent;
        
        [Header("Obi Rope集成")]
        [Tooltip("目标Obi Rope对象")]
        public ObiRope targetObiRope;
        
        [Tooltip("用于生成的Plug预制体")]
        public GameObject plugPrefab;
        
        [Header("工具组件引用")]
        [Tooltip("ObiRopePointTools组件引用")]
        public ObiRopePointTools obiRopePointTools;
        
        [Tooltip("ObiParticleAttachmentTools组件引用")]
        public ObiParticleAttachmentTools obiParticleAttachmentTools;

        [Button("温和同步Rope蓝图（保留核心结构）")]
        public void SyncRopeBlueprint()
        {
            if (targetObiRope == null)
            {
                Debug.LogError("❌ 未设置targetObiRope");
                return;
            }

            var blueprint = targetObiRope.ropeBlueprint;
            if (blueprint == null)
            {
                Debug.LogError("❌ Rope缺少蓝图");
                return;
            }

            Debug.Log($"🔄 开始温和同步蓝图");
            Debug.Log($"📊 当前状态 - 控制点数量: {targetObiRope.path.ControlPointCount}, 粒子组数量: {blueprint.groups.Count}");
            
            // 显示当前粒子组名称
            for (int i = 0; i < blueprint.groups.Count; i++)
            {
                Debug.Log($"粒子组 {i}: {blueprint.groups[i].name}");
            }
            
            // 温和的重新生成：不清空，直接重新生成
            try
            {
                blueprint.Generate();
                Debug.Log($"✅ 蓝图温和重新生成完成");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ 蓝图重新生成失败: {ex.Message}");
                Debug.LogError("建议：先确保Rope路径至少有2个控制点");
                return;
            }
            
            Debug.Log($"📊 新状态 - 控制点数量: {targetObiRope.path.ControlPointCount}, 粒子组数量: {blueprint.groups.Count}");
            
            // 显示新的粒子组名称
            for (int i = 0; i < blueprint.groups.Count; i++)
            {
                Debug.Log($"新粒子组 {i}: {blueprint.groups[i].name}");
            }
            
            // 验证数量匹配
            if (blueprint.groups.Count != targetObiRope.path.ControlPointCount)
            {
                Debug.LogWarning($"⚠️ 警告：粒子组数量({blueprint.groups.Count})与控制点数量({targetObiRope.path.ControlPointCount})仍不匹配");
                Debug.LogWarning("这可能是正常现象，Obi会根据需要创建额外的粒子组");
            }
            else
            {
                Debug.Log("✅ 粒子组数量与控制点数量已匹配");
            }
        }

        [Button("自动生成Plug附着点位")]
        public void AutoGeneratePlugAttachments()
        {
            if (!ValidatePlugGenerationSetup()) return;
            
            // 步骤1：获取所有Socket的LockID映射并排序
            Dictionary<int, SocketBase> lockIDToSocketMap = BuildLockIDToSocketMap(true);
            if (lockIDToSocketMap.Count == 0)
            {
                Debug.LogWarning("没有找到Socket点位", this);
                return;
            }
            
            // 步骤2：按LockID排序获取Socket位置列表
            List<int> sortedLockIDs = new List<int>(lockIDToSocketMap.Keys);
            sortedLockIDs.Sort();
            
            // 步骤3：计算Rope长度和控制点位置
            CreateRopeControlPoints(sortedLockIDs, lockIDToSocketMap);
            
            // 步骤4：创建粒子附着组件和Plug对象
            CreatePlugAttachments(sortedLockIDs);
            
            Debug.Log($"Plug附着点位生成完成！共生成了 {sortedLockIDs.Count} 个Plug附着点", this);
        }

        /// <summary>
        /// 建立LockID到SocketBase的映射关系
        /// </summary>
        private Dictionary<int, SocketBase> BuildLockIDToSocketMap(bool printLogs = false)
        {
            Dictionary<int, SocketBase> lockIDToSocketMap = new Dictionary<int, SocketBase>();
            
            // 获取所有带"EditorSocket"标签的子物体
            Transform[] allChildren = socketParent.GetComponentsInChildren<Transform>();
            
            foreach (Transform child in allChildren)
            {
                if (child == socketParent.transform) continue; // 跳过父物体自身
                
                if (child.CompareTag("EditorSocket"))
                {
                    SocketBase socketBase = child.GetComponent<SocketBase>();
                    if (socketBase != null && socketBase.m_lock != null)
                    {
                        int lockID = socketBase.m_lock.m_lockId;
                        
                        if (!lockIDToSocketMap.ContainsKey(lockID))
                        {
                            lockIDToSocketMap.Add(lockID, socketBase);
                            if (printLogs)
                                Debug.Log($"找到EditorSocket: {child.name}, LockID: {lockID}");
                        }
                        else
                        {
                            if (printLogs)
                                Debug.LogWarning($"发现重复的LockID: {lockID} 在物体 {child.name}");
                        }
                    }
                    else
                    {
                        if (printLogs)
                            Debug.LogWarning($"物体 {child.name} 标签为EditorSocket但缺少SocketBase组件或Lock配置");
                    }
                }
            }
            
            return lockIDToSocketMap;
        }

        #region Plug附着点位生成相关方法

        /// <summary>
        /// 验证Plug生成设置
        /// </summary>
        private bool ValidatePlugGenerationSetup()
        {
            if (targetObiRope == null)
            {
                Debug.LogError("请设置目标Obi Rope对象！", this);
                return false;
            }
            
            if (plugPrefab == null)
            {
                Debug.LogError("请设置Plug预制体！", this);
                return false;
            }
            
            if (socketParent == null)
            {
                Debug.LogError("请设置SocketParent父物体！", this);
                return false;
            }
            
            // 自动获取组件引用
            if (obiRopePointTools == null)
            {
                obiRopePointTools = targetObiRope.GetComponent<ObiRopePointTools>();
                if (obiRopePointTools == null)
                {
                    Debug.LogError("目标Obi Rope缺少ObiRopePointTools组件！", this);
                    return false;
                }
            }
            
            if (obiParticleAttachmentTools == null)
            {
                obiParticleAttachmentTools = targetObiRope.GetComponent<ObiParticleAttachmentTools>();
                if (obiParticleAttachmentTools == null)
                {
                    Debug.LogError("目标Obi Rope缺少ObiParticleAttachmentTools组件！", this);
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// 根据Socket间距创建地面Rope控制点
        /// </summary>
        private void CreateRopeControlPoints(List<int> sortedLockIDs, Dictionary<int, SocketBase> lockIDToSocketMap)
        {
            if (sortedLockIDs.Count < 2)
            {
                Debug.LogError("至少需要2个Socket点位才能创建Rope路径", this);
                return;
            }
            
            // 计算基于Socket间距的总Rope长度
            float totalLength = CalculateSocketBasedRopeLength(sortedLockIDs, lockIDToSocketMap);
            
            // 设置ObiRopePointTools参数
            obiRopePointTools.m_RopeLength = totalLength;
            obiRopePointTools.m_InsertPointCount = Mathf.Max(0, sortedLockIDs.Count - 2); // 减去首尾两个点
            
            Debug.Log($"准备创建Rope控制点：目标数量={sortedLockIDs.Count}，插入数量={obiRopePointTools.m_InsertPointCount}");
            
            // 清除现有的控制点并重新生成
            obiRopePointTools.Public_RemoveAllMiddleControlPoints(targetObiRope);
            obiRopePointTools.ModifyRope(targetObiRope);
            
            // 验证控制点数量
            int actualControlPointCount = targetObiRope.path.ControlPointCount;
            Debug.Log($"实际创建的控制点数量: {actualControlPointCount}，期望数量: {sortedLockIDs.Count}");
            
            if (actualControlPointCount != sortedLockIDs.Count)
            {
                Debug.LogWarning($"⚠️ 控制点数量不匹配！实际: {actualControlPointCount}, 期望: {sortedLockIDs.Count}");
                Debug.LogWarning("这可能导致粒子组数量与控制点数量不匹配的问题");
            }
            
            // 根据Socket间距设置地面Rope布局
            SetGroundRopeLayoutBasedOnSocketDistances(sortedLockIDs, lockIDToSocketMap);
            
            Debug.Log($"创建了地面Rope，{actualControlPointCount} 个控制点，总长度 {totalLength:F2}m", this);
        }

        /// <summary>
        /// 计算基于Socket间距的Rope长度
        /// </summary>
        private float CalculateSocketBasedRopeLength(List<int> sortedLockIDs, Dictionary<int, SocketBase> lockIDToSocketMap)
        {
            float totalLength = 0f;
            
            for (int i = 0; i < sortedLockIDs.Count - 1; i++)
            {
                Vector3 currentPos = lockIDToSocketMap[sortedLockIDs[i]].transform.position;
                Vector3 nextPos = lockIDToSocketMap[sortedLockIDs[i + 1]].transform.position;
                float distance = Vector3.Distance(currentPos, nextPos);
                totalLength += distance;
                
                Debug.Log($"Socket {sortedLockIDs[i]} 到 Socket {sortedLockIDs[i + 1]} 距离: {distance:F2}m");
            }
            
            // 添加一些余量以确保绳索不会过紧
            totalLength *= 1.02f;
            
            Debug.Log($"总Rope长度（含余量）: {totalLength:F2}m");
            return totalLength;
        }

        /// <summary>
        /// 根据Socket间距设置地面Rope布局
        /// </summary>
        private void SetGroundRopeLayoutBasedOnSocketDistances(List<int> sortedLockIDs, Dictionary<int, SocketBase> lockIDToSocketMap)
        {
            if (targetObiRope.path.ControlPointCount != sortedLockIDs.Count)
            {
                Debug.LogError($"控制点数量不匹配：期望 {sortedLockIDs.Count}，实际 {targetObiRope.path.ControlPointCount}", this);
                return;
            }
            
            // 地面Rope起始位置：与LockID为1的Socket的X坐标对齐
            SocketBase firstSocket = lockIDToSocketMap[sortedLockIDs[0]];
            Vector3 firstSocketWorldPos = firstSocket.transform.position;
            // 转换为Rope的本地坐标，但只取X值，Y和Z保持为0
            Vector3 firstSocketLocalPos = targetObiRope.transform.InverseTransformPoint(firstSocketWorldPos);
            Vector3 startPos = new Vector3(firstSocketLocalPos.x, 0f, 0f);
            Vector3 currentPos = startPos;
            
            Debug.Log($"起始位置设置为LockID {sortedLockIDs[0]} Socket的X坐标: {startPos.x:F2}");
            
            for (int i = 0; i < sortedLockIDs.Count; i++)
            {
                int lockID = sortedLockIDs[i];
                
                // 第一个控制点在起始位置
                if (i == 0)
                {
                    currentPos = startPos;
                }
                else
                {
                    // 计算当前Socket与前一个Socket的3D间距，但只应用到X轴
                    Vector3 prevSocketPos = lockIDToSocketMap[sortedLockIDs[i-1]].transform.position;
                    Vector3 currSocketPos = lockIDToSocketMap[sortedLockIDs[i]].transform.position;
                    float socketDistance = Vector3.Distance(prevSocketPos, currSocketPos);
                    
                    // 在X轴上累加间距，Y和Z保持为0（地面水平布局）
                    currentPos += Vector3.right * socketDistance;
                    
                    Debug.Log($"Socket {sortedLockIDs[i-1]} 到 Socket {sortedLockIDs[i]} 间距: {socketDistance:F2}m，控制点X位置: {currentPos.x:F2}");
                }
                
                // 确保Y=0（地面），Z=0（对齐）
                Vector3 localPosition = new Vector3(currentPos.x, 0f, 0f);
                
                // 获取当前控制点的切线信息（保持原有的切线）
                ObiWingedPoint currentPoint = targetObiRope.path.points.data[i];
                
                // 更新控制点位置，保持切线不变
                targetObiRope.path.points.data[i] = new ObiWingedPoint(
                    new Vector3(-0.03f, 0f, 0f),
                    localPosition,
                    new Vector3(0.03f, 0f, 0f)
                );
                
                // 设置控制点名称为对应的LockID
                targetObiRope.path.SetName(i, lockID.ToString());
                
                // 设置控制点属性（通过公开包装方法调用受保护实现）
                obiRopePointTools.Public_SetControlPointProperty(targetObiRope, i);
                
                Debug.Log($"设置地面控制点 {i}: LockID={lockID}, 位置={localPosition}", this);
            }
            
            // 刷新路径事件
            targetObiRope.path.FlushEvents();
        }

        /// <summary>
        /// 创建Plug附着点
        /// </summary>
        private void CreatePlugAttachments(List<int> sortedLockIDs)
        {
            // 检查Rope的控制点数量是否与sortedLockIDs匹配
            int ropeControlPointCount = targetObiRope.path.ControlPointCount;
            Debug.Log($"Rope控制点数量: {ropeControlPointCount}, sortedLockIDs数量: {sortedLockIDs.Count}");
            
            if (ropeControlPointCount != sortedLockIDs.Count)
            {
                Debug.LogError($"Rope控制点数量({ropeControlPointCount})与Socket数量({sortedLockIDs.Count})不匹配！无法创建Plug附着点");
                return;
            }
            
            // 设置ObiParticleAttachmentTools的预制体
            obiParticleAttachmentTools.m_TargetObjectPrefab = plugPrefab;
            obiParticleAttachmentTools.m_TargetObjectName = "PlugAttachment";
            
            // 添加粒子附着组件
            obiParticleAttachmentTools.AddParticleAttachmentComponent(targetObiRope);
            
            // 设置附着目标对象
            obiParticleAttachmentTools.SetParticleAttachmentTarget(targetObiRope);
            
            // 配置生成的Plug对象的KeyID
            ConfigurePlugKeyIDs(sortedLockIDs);
        }

        /// <summary>
        /// 配置Plug对象的KeyID
        /// </summary>
        private void ConfigurePlugKeyIDs(List<int> sortedLockIDs)
        {
            Debug.Log($"🔑 开始配置Plug对象的KeyID...");
            Debug.Log($"📊 Plug对象数量: {obiParticleAttachmentTools.m_TargetObjects.Count}");
            Debug.Log($"📊 sortedLockIDs数量: {sortedLockIDs.Count}");
            Debug.Log($"📊 sortedLockIDs内容: [{string.Join(", ", sortedLockIDs)}]");
            
            if (obiParticleAttachmentTools.m_TargetObjects.Count != sortedLockIDs.Count)
            {
                Debug.LogWarning($"⚠️ Plug对象数量与LockID数量不匹配：{obiParticleAttachmentTools.m_TargetObjects.Count} vs {sortedLockIDs.Count}", this);
            }
            
        // 为每个Plug对象添加必要的组件
        for (int i = 0; i < obiParticleAttachmentTools.m_TargetObjects.Count; i++)
        {
            GameObject plugObject = obiParticleAttachmentTools.m_TargetObjects[i];
            
            // 获取或添加Grabbable_Voltage组件
            Grabbable_Voltage grabbable = plugObject.GetComponent<Grabbable_Voltage>();
            if (grabbable == null)
            {
                Debug.Log($"➕ 为 {plugObject.name} 添加Grabbable_Voltage组件");
                grabbable = plugObject.AddComponent<Grabbable_Voltage>();
            }
            else
            {
                Debug.Log($"✅ {plugObject.name} 已有Grabbable_Voltage组件");
            }
            
            // 确保有Plug_Grabbable组件
            Plug_Grabbable plugGrabbable = plugObject.GetComponent<Plug_Grabbable>();
            if (plugGrabbable == null)
            {
                Debug.Log($"➕ 为 {plugObject.name} 添加Plug_Grabbable组件");
                plugGrabbable = plugObject.AddComponent<Plug_Grabbable>();
            }
            else
            {
                Debug.Log($"✅ {plugObject.name} 已有Plug_Grabbable组件");
            }
        }
            
            // 使用ObiParticleAttachmentTools的公共方法批量设置KeyID
            int successCount = obiParticleAttachmentTools.SetPlugKeyIDs(sortedLockIDs);
            
            // 更新对象名称
            int minCount = Mathf.Min(obiParticleAttachmentTools.m_TargetObjects.Count, sortedLockIDs.Count);
            for (int i = 0; i < minCount; i++)
            {
                GameObject plugObject = obiParticleAttachmentTools.m_TargetObjects[i];
                int correspondingLockID = sortedLockIDs[i];
                plugObject.name = $"Plug_LockID_{correspondingLockID}";
            }
            
            Debug.Log($"🎉 ConfigurePlugKeyIDs方法执行完成！成功设置 {successCount}/{minCount} 个Plug对象的KeyID");
        }

        #endregion
    }
}
