/****************************************************
    功能：自动创建绕圈点位工具
    作者：Assistant
    创建日期：#2025/02/20#
    修改内容：为现有Socket自动生成上方和前方的绕圈点位
*****************************************************/
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using Voltage;

namespace Voltage
{
    [System.Serializable]
    public struct LockIDPair
    {
        public int lockID1;
        public int lockID2;
        
        public LockIDPair(int id1, int id2)
        {
            lockID1 = id1;
            lockID2 = id2;
        }
    }
    
    public class AutoCircularPointTools : MonoBehaviour
    {
        [Header("配置参数")]
        [Tooltip("要生成对应点位的LockID成对列表")]
        public List<LockIDPair> lockIDPairList = new List<LockIDPair>();
        
        [Tooltip("所有SocketBase的集合父物体")]
        public GameObject socketParent;
        
        [Tooltip("用于生成的Socket预制体")]
        public GameObject frameSocketPrefab;
        
        [Header("间距设置")]
        [Tooltip("向上间距")]
        public float upwardSpacing = 0.3f;
        
        [Tooltip("向前间距")]
        public float forwardSpacingZ = 0.2f;
        public float forwardSpacingY = 0.2f;
        
        // 用于保存原始LockID，以便清除时恢复
        private Dictionary<int, int> originalLockIDMap = new Dictionary<int, int>();
        
      // 用于保存原始名称，以便清除时恢复
        private Dictionary<int, string> originalNameMap = new Dictionary<int, string>();
        
        [System.Serializable]
        private class SocketOriginalData
        {
            public SocketBase socket;
            public string originalName;
            public int originalLockID;
        }
        
        [SerializeField]
        private List<SocketOriginalData> originalSocketBackups = new List<SocketOriginalData>();
        
        [Button("自动创建绕圈点位")]
        public void AutoCreateCircularPoints()
        {
            if (frameSocketPrefab == null)
            {
                Debug.LogError("请设置FrameSocket预制体！", this);
                return;
            }
            
            if (socketParent == null)
            {
                Debug.LogError("请设置SocketParent父物体！", this);
                return;
            }
            
            if (lockIDPairList == null || lockIDPairList.Count == 0)
            {
                Debug.LogWarning("LockID成对列表为空，没有要处理的点位", this);
                return;
            }
            
            // 步骤1：获取所有带EditorSocket标签的子物体并建立LockID-SocketBase映射
            Dictionary<int, SocketBase> lockIDToSocketMap = BuildLockIDToSocketMap(true);
            
            if (lockIDToSocketMap.Count == 0)
            {
                Debug.LogWarning("没有找到带'EditorSocket'标签的子物体", this);
                return;
            }
            
            Debug.Log($"找到 {lockIDToSocketMap.Count} 个EditorSocket，开始生成绕圈点位...");
            
            // 步骤2：保存原始LockID映射（以便后续清除时恢复 & 仅针对原始Socket）
            SaveOriginalLockIDs(lockIDToSocketMap);
            
            // 步骤3：先创建所有成对的绕圈点位（使用初始映射，避免相互覆盖）
            int generatedCount = 0;
            foreach (LockIDPair pair in lockIDPairList)
            {
                if (lockIDToSocketMap.TryGetValue(pair.lockID1, out SocketBase socket1) && 
                    lockIDToSocketMap.TryGetValue(pair.lockID2, out SocketBase socket2))
                {
                    CreateCircularPointsForSocketPair(socket1, socket2, pair);
                    generatedCount++;
                }
                else
                {
                    Debug.LogWarning($"未找到LockID为 {pair.lockID1} 或 {pair.lockID2} 的Socket（创建阶段），请检查lockIDPairList");
                }
            }
            
            // 步骤4：对原始Socket执行分段顺延排序（创建完成后再整体顺延）
            ReorderOriginalSocketsAfterCreation(lockIDToSocketMap);
            
            // 步骤5：为所有Socket更新名称后缀为自身LockID
            UpdateAllSocketNamesWithLockID();
            
            Debug.Log($"绕圈点位生成完成！共为 {generatedCount} 对Socket生成了对应的绕圈点位，并完成排序与命名", this);
        }
        
        [Button("清除生成的绕圈点位")]
        public void ClearGeneratedPoints()
        {
            if (socketParent == null)
            {
                Debug.LogError("请设置SocketParent父物体！", this);
                return;
            }

            // 步骤1：还原被顺延的原始LockID
            RestoreOriginalLockIDs();

            // 步骤2：清除生成的绕圈点位
            List<Transform> toDestroy = new List<Transform>();

            // 查找所有带_Up和_Front后缀的子物体（使用GetComponentsInChildren确保递归查找）
            Transform[] allChildren = socketParent.GetComponentsInChildren<Transform>();
            foreach (Transform child in allChildren)
            {
                if (child == socketParent.transform) continue; // 跳过父物体自身

                if (child.name.Contains("_Up") || child.name.Contains("_Front"))
                {
                    toDestroy.Add(child);
                    Debug.Log($"标记删除生成的绕圈点位: {child.name}");
                }
            }

            foreach (Transform obj in toDestroy)
            {
                DestroyImmediate(obj.gameObject);
            }

            // 步骤3：原始Socket信息已在 RestoreOriginalLockIDs 中恢复
            Debug.Log($"已清除 {toDestroy.Count} 个生成的绕圈点位，并恢复原始LockID和名称", this);
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
        
        /// <summary>
        /// 保存原始LockID映射和名称映射
        /// </summary>

        private void SaveOriginalLockIDs(Dictionary<int, SocketBase> lockIDToSocketMap)

        {
            if (lockIDToSocketMap == null || lockIDToSocketMap.Count == 0)
            {
                Debug.LogWarning("没有可记录的Socket，跳过原始数据缓存");
                return;
            }

            // 确保运行时缓存和序列化数据保持一致
            RebuildOriginalLookup();

            int addedCount = 0;

            foreach (var kvp in lockIDToSocketMap)
            {
                SocketBase socket = kvp.Value;
                if (socket == null || socket.m_lock == null)
                    continue;

                bool isNewEntry = !originalSocketBackups.Exists(data => data.socket == socket);
                if (isNewEntry)
                {
                    originalSocketBackups.Add(new SocketOriginalData
                    {
                        socket = socket,
                        originalName = socket.name,
                        originalLockID = socket.m_lock.m_lockId
                    });
                    addedCount++;
                }

                int instanceID = socket.GetInstanceID();

                if (!originalLockIDMap.ContainsKey(instanceID))
                {
                    originalLockIDMap.Add(instanceID, socket.m_lock.m_lockId);
                }

                if (!originalNameMap.ContainsKey(instanceID))
                {
                    originalNameMap.Add(instanceID, socket.name);
                }
            }

            if (addedCount > 0)
            {
                // 重新构建缓存以确保使用记录时刻的原始数据
                RebuildOriginalLookup();
                Debug.Log($"记录了 {originalSocketBackups.Count} 个原始Socket数据");
            }
            else
            {
                Debug.Log("原始Socket数据已存在，跳过重复记录");
            }
        }
        
        /// <summary>
        /// 还原原始LockID和名称
        /// </summary>

        private void RestoreOriginalLockIDs()
        {
            RebuildOriginalLookup();

            if (originalLockIDMap.Count == 0)
            {
                Debug.LogWarning("没有找到保存的原始Socket数据，可能未曾创建绕圈点位");
                return;
            }

            Dictionary<int, SocketBase> currentSocketMap = BuildLockIDToSocketMap(false);
            int lockIdRestoreCount = 0;
            int nameRestoreCount = 0;

            foreach (var socket in currentSocketMap.Values)
            {
                if (socket == null)
                    continue;

                int instanceID = socket.GetInstanceID();

                if (originalLockIDMap.TryGetValue(instanceID, out int originalLockID) && socket.m_lock != null)
                {
                    if (socket.m_lock.m_lockId != originalLockID)
                    {
                        int beforeRestore = socket.m_lock.m_lockId;
#if UNITY_EDITOR
                        UnityEditor.Undo.RecordObject(socket, "Restore Socket LockID");
#endif
                        socket.m_lock.m_lockId = originalLockID;
                        lockIdRestoreCount++;
                        Debug.Log($"还原Socket {socket.name}: LockID {beforeRestore} -> {originalLockID}");
#if UNITY_EDITOR
                        UnityEditor.EditorUtility.SetDirty(socket);
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(socket.gameObject.scene);
#endif
                    }
                }
                else if (socket != null && socket.m_lock == null && originalLockIDMap.ContainsKey(instanceID))
                {
                    Debug.LogWarning($"Socket {socket.name} 丢失Lock组件，无法恢复LockID");
                }

                if (originalNameMap.TryGetValue(instanceID, out string originalName) && socket.name != originalName)
                {
#if UNITY_EDITOR
                    UnityEditor.Undo.RecordObject(socket.gameObject, "Restore Socket Name");
#endif
                    Debug.Log($"还原Socket名称: {socket.name} -> {originalName}");
                    socket.name = originalName;
                    nameRestoreCount++;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(socket.gameObject);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(socket.gameObject.scene);
#endif
                }
            }

            Debug.Log($"已还原 {lockIdRestoreCount} 个Socket的LockID，{nameRestoreCount} 个Socket的名称");
        }
        

        private void RebuildOriginalLookup()
        {
            originalLockIDMap.Clear();
            originalNameMap.Clear();

            for (int i = originalSocketBackups.Count - 1; i >= 0; i--)
            {
                SocketOriginalData data = originalSocketBackups[i];
                if (data == null || data.socket == null)
                {
                    originalSocketBackups.RemoveAt(i);
                    continue;
                }

                int instanceID = data.socket.GetInstanceID();
                originalLockIDMap[instanceID] = data.originalLockID;
                originalNameMap[instanceID] = data.originalName;
            }
        }

        /// <summary>
        /// 在创建完所有绕圈点位后，对原始Socket执行分段顺延排序
        /// </summary>
        private void ReorderOriginalSocketsAfterCreation(Dictionary<int, SocketBase> originalLockIDToSocketMap)
        {
            // 获取当前所有Socket（包括新创建的绕圈点位）
            Dictionary<int, SocketBase> currentSocketMap = BuildLockIDToSocketMap(false);
            
            // 只对原始Socket进行顺延排序，新创建的绕圈点位保持其LockID不变
            foreach (var kvp in originalLockIDToSocketMap)
            {
                int originalLockID = kvp.Key;
                SocketBase originalSocket = kvp.Value;
                
                // 计算有多少对在当前原始LockID之前
                int k = 0;
                foreach (var pair in lockIDPairList)
                {
                    int pairMax = Mathf.Max(pair.lockID1, pair.lockID2);
                    if (pairMax < originalLockID) k++;
                }
                
                int newLockID = originalLockID + 4 * k;
                if (newLockID != originalSocket.m_lock.m_lockId)
                {
                    int before = originalSocket.m_lock.m_lockId;
#if UNITY_EDITOR
                    UnityEditor.Undo.RecordObject(originalSocket, "Reorder Socket LockID");
#endif
                    originalSocket.m_lock.m_lockId = newLockID;
                    Debug.Log($"顺延原始Socket {originalSocket.name}: LockID {before} -> {newLockID}");
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(originalSocket);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(originalSocket.gameObject.scene);
#endif
                }
            }
        }
        
        /// <summary>
        /// 为所有Socket更新名称后缀为自身LockID
        /// </summary>
        private void UpdateAllSocketNamesWithLockID()
        {
            Dictionary<int, SocketBase> currentSocketMap = BuildLockIDToSocketMap(false);
            
            foreach (var kvp in currentSocketMap)
            {
                int lockID = kvp.Key;
                SocketBase socket = kvp.Value;
                
                // 获取基础名称（去掉可能存在的旧LockID后缀）
                string baseName = socket.name;
                int lastUnderscoreIndex = baseName.LastIndexOf('_');
                if (lastUnderscoreIndex > 0)
                {
                    // 检查后缀是否为数字（可能是旧的LockID）
                    string suffix = baseName.Substring(lastUnderscoreIndex + 1);
                    if (int.TryParse(suffix, out int oldLockID))
                    {
                        baseName = baseName.Substring(0, lastUnderscoreIndex);
                    }
                }
                
                // 设置新名称
                string newName = $"{baseName}_{lockID}";
                if (socket.name != newName)
                {
                    Debug.Log($"更新Socket名称: {socket.name} -> {newName}");
                    socket.name = newName;
                }
            }
        }
        
        /// <summary>
        /// 重新排序现有Socket的LockID以为绕圈点位腾出空间（已弃用，保留用于兼容性）
        /// </summary>
        private void ReorderExistingSocketLockIDs(Dictionary<int, SocketBase> lockIDToSocketMap)
        {
            // 分段顺延的精确实现：
            // 对于任意一个原LockID = L，计算在它之前“成对完成”的数量 k，
            // 其中“在它之前”定义为：该对的两端ID都 < L（等价于 max(pair) < L）。
            // 新LockID = L + 4*k（每一对需要为后续留出4个ID：右Front、左Front、左Up、右Up）。

            // 1) 取出并排序所有原始LockID
            List<int> originalLockIDs = new List<int>(lockIDToSocketMap.Keys);
            originalLockIDs.Sort();

            // 2) 预计算每一对的最大ID（用于快速判断“是否在当前ID之前”）
            List<int> pairMaxList = new List<int>(lockIDPairList.Count);
            foreach (var pair in lockIDPairList)
            {
                pairMaxList.Add(Mathf.Max(pair.lockID1, pair.lockID2));
            }
            pairMaxList.Sort();

            // 3) 逐个Socket计算新ID并设置
            foreach (int oldId in originalLockIDs)
            {
                if (!lockIDToSocketMap.TryGetValue(oldId, out var socket))
                    continue;

                // 计算有多少对在当前ID之前（max(pair)<oldId）
                int k = 0;
                for (int i = 0; i < pairMaxList.Count; i++)
                {
                    if (pairMaxList[i] < oldId) k++;
                    else break; // pairMaxList已排序，可提前结束
                }

                int newId = oldId + 4 * k;
                if (newId != socket.m_lock.m_lockId)
                {
                    int before = socket.m_lock.m_lockId;
                    socket.m_lock.m_lockId = newId;
                    Debug.Log($"顺延Socket {socket.name}: LockID {before} -> {newId}");
                }
            }
        }
        
        /// <summary>
        /// 获取当前pair在lockIDPairList中的索引（按pair的最大LockID排序后的索引）
        /// </summary>
        private int GetPairIndex(LockIDPair targetPair)
        {
            // 创建一个按pair最大LockID排序的列表
            List<LockIDPair> sortedPairs = new List<LockIDPair>(lockIDPairList);
            sortedPairs.Sort((pair1, pair2) => 
            {
                int max1 = Mathf.Max(pair1.lockID1, pair1.lockID2);
                int max2 = Mathf.Max(pair2.lockID1, pair2.lockID2);
                return max1.CompareTo(max2);
            });
            
            // 找到目标pair在排序后列表中的索引
            for (int i = 0; i < sortedPairs.Count; i++)
            {
                if ((sortedPairs[i].lockID1 == targetPair.lockID1 && sortedPairs[i].lockID2 == targetPair.lockID2) ||
                    (sortedPairs[i].lockID1 == targetPair.lockID2 && sortedPairs[i].lockID2 == targetPair.lockID1))
                {
                    return i;
                }
            }
            
            return 0; // 如果找不到，返回0
        }
        
        /// <summary>
        /// 为指定的Socket对创建逆时针绕圈点位
        /// </summary>
        private void CreateCircularPointsForSocketPair(SocketBase socket1, SocketBase socket2, LockIDPair pair)
        {
            // 确定左右顺序（假设lockID1在左，lockID2在右）
            SocketBase leftSocket = pair.lockID1 < pair.lockID2 ? socket1 : socket2;
            SocketBase rightSocket = pair.lockID1 < pair.lockID2 ? socket2 : socket1;
            int leftLockID = Mathf.Min(pair.lockID1, pair.lockID2);
            int rightLockID = Mathf.Max(pair.lockID1, pair.lockID2);
            
            // 计算当前pair之前有多少个pair（用于确定LockID偏移）
            int pairIndex = GetPairIndex(pair);
            int baseOffset = pairIndex * 4; // 每个pair需要4个额外的LockID空间
            
            // 逆时针顺序的LockID计算（考虑前面pair的占用空间）：
            // 1. 左Socket (原位置) - 保持原LockID
            // 2. 右Socket (原位置) - 保持原LockID
            // 3. 右Socket的Front点位 (LockID = rightLockID + 1 + baseOffset)
            // 4. 左Socket的Front点位 (LockID = leftLockID + 3 + baseOffset)
            // 5. 左Socket的Up点位 (LockID = leftLockID + 4 + baseOffset)
            // 6. 右Socket的Up点位 (LockID = rightLockID + 4 + baseOffset)
            
            Vector3 leftPos = leftSocket.transform.position;
            Vector3 rightPos = rightSocket.transform.position;
            
            // 创建右Socket的Front点位 (顺序3)
            CreateSingleCircularPoint(
                rightPos + Vector3.up * forwardSpacingY + Vector3.forward * forwardSpacingZ,
                rightSocket.name + "_Front",
                rightLockID + 1 + baseOffset
            );
            
            // 创建左Socket的Front点位 (顺序4)
            CreateSingleCircularPoint(
                leftPos + Vector3.up * forwardSpacingY + Vector3.forward * forwardSpacingZ,
                leftSocket.name + "_Front",
                leftLockID + 3 + baseOffset
            );
            
            // 创建左Socket的Up点位 (顺序5)
            CreateSingleCircularPoint(
                leftPos + Vector3.up * upwardSpacing,
                leftSocket.name + "_Up",
                leftLockID + 4 + baseOffset
            );
            
            // 创建右Socket的Up点位 (顺序6)
            CreateSingleCircularPoint(
                rightPos + Vector3.up * upwardSpacing,
                rightSocket.name + "_Up",
                rightLockID + 4 + baseOffset
            );
            
            Debug.Log($"为Socket对 ({leftSocket.name}:{leftLockID}, {rightSocket.name}:{rightLockID}) 生成了逆时针绕圈点位，baseOffset={baseOffset}");
        }
        
        /// <summary>
        /// 创建单个绕圈点位
        /// </summary>
        private void CreateSingleCircularPoint(Vector3 position, string name, int newLockID)
        {
            // 实例化预制体
            GameObject newSocket = Instantiate(frameSocketPrefab, socketParent.transform);
            newSocket.transform.position = position;
            newSocket.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);
            newSocket.name = name;
            newSocket.tag = "EditorSocket";
            
            // 添加Rigidbody组件
            Rigidbody rb = newSocket.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = newSocket.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            
            // 添加BoxCollider组件
            BoxCollider boxCollider = newSocket.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = newSocket.AddComponent<BoxCollider>();
            }
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(10f, 10f, 10f);
            
            // 配置SocketBase组件
            SocketBase socketBase = newSocket.GetComponent<SocketBase>();
            if (socketBase == null)
            {
                socketBase = newSocket.AddComponent<SocketBase>();
            }
            
            // 设置LockID
            if (socketBase.m_lock == null)
            {
                // 如果没有Lock组件，需要创建一个
                Debug.LogWarning($"预制体 {frameSocketPrefab.name} 缺少Lock组件，将创建默认Lock");
                socketBase.m_lock = new LockWithSocket();
            }
            
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(socketBase, "Set Socket LockID");
#endif
            socketBase.m_lock.m_lockId = newLockID;
            // 设置MatchMode和MatchState
            socketBase.m_lock.m_matchMode = MatchMode.LockId;
            socketBase.m_lock.m_matchState = false;
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(socketBase);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(socketBase.gameObject.scene);
#endif
            // 设置TargetTransform为自身
            socketBase.m_targetTransform = newSocket.transform;
            
            Debug.Log($"创建绕圈点位: {name}, 位置: {position}, LockID: {newLockID}");
        }
        
        /// <summary>
        /// 验证配置
        /// </summary>
        private void OnValidate()
        {
            // 确保间距值为正数
            upwardSpacing = Mathf.Max(0.01f, upwardSpacing);
            forwardSpacingY = Mathf.Max(0.01f, forwardSpacingY);
            forwardSpacingZ = Mathf.Max(0.01f, forwardSpacingZ);
        }
        
        /// <summary>
        /// 在Scene视图中显示预览
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (lockIDPairList == null || lockIDPairList.Count == 0 || socketParent == null) return;
            
            // 获取LockID映射
            Dictionary<int, SocketBase> lockIDToSocketMap = BuildLockIDToSocketMap(false);
            
            foreach (LockIDPair pair in lockIDPairList)
            {
                if (lockIDToSocketMap.TryGetValue(pair.lockID1, out SocketBase socket1) && 
                    lockIDToSocketMap.TryGetValue(pair.lockID2, out SocketBase socket2))
                {
                    // 确定左右顺序
                    SocketBase leftSocket = pair.lockID1 < pair.lockID2 ? socket1 : socket2;
                    SocketBase rightSocket = pair.lockID1 < pair.lockID2 ? socket2 : socket1;
                    
                    Vector3 leftPos = leftSocket.transform.position;
                    Vector3 rightPos = rightSocket.transform.position;
                    
                    // 绘制逆时针绕圈路径预览
                    Gizmos.color = Color.green;
                    
                    // 1. 左Socket原位置
                    Gizmos.DrawWireSphere(leftPos, 0.1f);
                    
                    // 2. 右Socket原位置  
                    Vector3 pos2 = rightPos;
                    Gizmos.DrawWireSphere(pos2, 0.1f);
                    Gizmos.DrawLine(leftPos, pos2);
                    
                    // 3. 右Socket的Front点位
                    Vector3 pos3 = rightPos + Vector3.up * forwardSpacingY + Vector3.forward * forwardSpacingZ;
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireCube(pos3, Vector3.one * 0.08f);
                    Gizmos.DrawLine(pos2, pos3);
                    
                    // 4. 左Socket的Front点位
                    Vector3 pos4 = leftPos + Vector3.up * forwardSpacingY + Vector3.forward * forwardSpacingZ;
                    Gizmos.DrawWireCube(pos4, Vector3.one * 0.08f);
                    Gizmos.DrawLine(pos3, pos4);
                    
                    // 5. 左Socket的Up点位
                    Vector3 pos5 = leftPos + Vector3.up * upwardSpacing;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireCube(pos5, Vector3.one * 0.08f);
                    Gizmos.DrawLine(pos4, pos5);
                    
                    // 6. 右Socket的Up点位
                    Vector3 pos6 = rightPos + Vector3.up * upwardSpacing;
                    Gizmos.DrawWireCube(pos6, Vector3.one * 0.08f);
                    Gizmos.DrawLine(pos5, pos6);
                    
                    // 绘制连接线返回起点（完成循环）
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(pos6, leftPos);
                }
            }
        }
    }
}


