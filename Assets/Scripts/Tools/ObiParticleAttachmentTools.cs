/*****************************************************
    功能：obi粒子附着快速设置工具
    作者：ZZQ
    创建日期：#2025/06/30#
    修改内容：
        1.
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using Obi;
using UnityEngine;
using UnityEditor;
using Voltage;
using System.Linq;
using UnityEditor.Events;
using Autohand;
using System.Net.Mail;

/// <summary>
/// 按钮在ObiParticleAttachmentToolsEditor类中实现
/// </summary>
public class ObiParticleAttachmentTools : MonoBehaviour
{
    public GameObject m_TargetObjectPrefab;
    public ObiParticleAttachment.AttachmentType m_AttachmentType = ObiParticleAttachment.AttachmentType.Static;
    public string m_TargetObjectName = "AttachmentObject";
    public List<GameObject> m_TargetObjects = new List<GameObject>();

    // 内部变量
    public GameObject m_targetParent = null;
    private Dictionary<int, ObiParticleAttachment> m_AttachmentComponents = new Dictionary<int, ObiParticleAttachment>();

    [InitializeOnLoadMethod]
    static void onEnableOrCompile()
    {
        // 这里可以添加您希望在脚本编译或修改后执行的逻辑
        // 节点信息改变时，清除粒子附着
        if (Application.isPlaying)
            return;
        List<ObiRopePointTools> obiRopePointTools = new List<ObiRopePointTools>();
        obiRopePointTools = FindObjectsByType<ObiRopePointTools>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
        foreach (var obiRopePointTool in obiRopePointTools)
        {
            ObiParticleAttachmentTools ParticleAttachmentTools = obiRopePointTool.GetComponent<ObiParticleAttachmentTools>();
            ObiActor obiActor = ParticleAttachmentTools.GetComponent<ObiActor>();
            if (ParticleAttachmentTools != null && obiActor != null)
                obiRopePointTool.onControlPointChanged.AddListener(() => ParticleAttachmentTools.ClearParticleAttachment(obiActor));
        }
    }
    /// <summary>
    /// 重置粒子附着组件
    /// </summary>
    /// <param name="obiActor"></param>
    public void AddParticleAttachmentComponent(ObiActor obiActor)
    {
        ObiActorBlueprint blueprint;
        if (!ObiValidate(obiActor, out blueprint))
            return;
        //销毁现有的附着组件和对象
        DestoryAttachmentComponent(obiActor);
        DestroyAttachmentTargetObjects();

        //添加并修改附着组件
        UtilsVoltage.DebugLog(Color.green, $"{blueprint.name}中共有{blueprint.groups.Count}个粒子组。",this);
        CheckAttachmentComponent(obiActor);
    }
    /// <summary>
    /// 设置粒子附着目标
    /// </summary>
    /// <param name="obiActor"></param>
    public void SetParticleAttachmentTarget(ObiActor obiActor)
    {
        ObiActorBlueprint blueprint;
        if (!ObiValidate(obiActor, out blueprint))
            return;
        
        //检查(和增加场景中已经设置的)附着组件
        CheckAttachmentComponent(obiActor);
        
        //销毁现有的附着对象
        DestroyAttachmentTargetObjects();

        // 智能过滤有效的粒子组
        List<int> validGroupIndices = new List<int>();
        if (blueprint is ObiRopeBlueprint ropeBlueprint)
        {
            int controlCount = ropeBlueprint.path.ControlPointCount;
            Debug.Log($"📊 检查粒子组：总数={blueprint.groups.Count}, 控制点数={controlCount}");
            
            for (int i = 0; i < blueprint.groups.Count; i++)
            {
                var group = blueprint.groups[i];
                string groupName = group.name;
                
                // 过滤掉不需要的粒子组（历史残留）
                if (groupName.ToLower().Contains("control point") && 
                    !System.Text.RegularExpressions.Regex.IsMatch(groupName, @"^\d+$")) // 不是纯数字命名
                {
                    Debug.Log($"⏭️ 跳过历史粒子组: {groupName}");
                    continue;
                }
                
                if (groupName.ToLower().Contains("start") || groupName.ToLower().Contains("end"))
                {
                    Debug.Log($"⏭️ 跳过Start/End粒子组: {groupName}");
                    continue;
                }
                
                // 检查索引是否超出控制点范围
                if (i >= controlCount)
                {
                    Debug.Log($"⏭️ 跳过超出控制点范围的粒子组 {i}: {groupName}");
                    continue;
                }
                
                validGroupIndices.Add(i);
                Debug.Log($"✅ 有效粒子组 {i}: {groupName}");
            }
        }
        else
        {
            // 非Rope蓝图，使用原有逻辑
            for (int i = 0; i < blueprint.groups.Count; i++)
            {
                validGroupIndices.Add(i);
            }
        }

        Debug.Log($"🎯 将为 {validGroupIndices.Count} 个有效粒子组创建附着对象");

        // 创建并设置所有附着对象（仅对有效粒子组）
        foreach (int i in validGroupIndices)
        {
            // 设置附着组件的目标
            if (m_AttachmentComponents.TryGetValue(i, out var attach))
            {
                if (attach.target == null)
                {
                    if (m_TargetObjectPrefab == null)
                    {
                        UtilsVoltage.DebugLog(Color.red, $"请先设置附着对象预制体", this);
                        return;
                    }
                    // 创建附着对象父级
                    if (m_TargetObjectPrefab != null && m_targetParent == null)
                    {
                        var parentTransform = obiActor.transform.parent;
                        m_targetParent = new GameObject("AttachmentObjects");
                        m_targetParent.transform.SetParent(parentTransform, false);
                    }
                    // 附着对象设置
                    GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(m_TargetObjectPrefab, m_targetParent.transform);
                    go.name = $"{m_TargetObjectName}_{blueprint.groups[i].name}";
                    // 设定缩放
                    go.transform.localScale = new Vector3(0.015f, 0.015f, 0.015f);
                    SetAttachmentTargetPosition(go, blueprint, i);
                    CheckAttachmentTargetComponent(go);
                    // 确保Grabbable_Voltage的开始抓取为true
                    var gv = go.GetComponent<Grabbable_Voltage>();
                    if (gv != null)
                    {
                        gv.m_StartGrabState = true;
                    }
                    m_TargetObjects.Add(go);
                    attach.target = go.transform;
                }

                if (attach.target.TryGetComponent<ObiControl_Grabbable>(out var _obicontrol))
                {
                    _obicontrol.m_particleAttachmentTransforms.RemoveAll(item => item == null);
                    _obicontrol.m_particleAttachmentList.RemoveAll(item => item == null);
                    
                    if (!_obicontrol.m_particleAttachmentTransforms.Contains(obiActor as ObiRope))
                        _obicontrol.m_particleAttachmentTransforms.Add(obiActor as ObiRope);
                    if(!_obicontrol.m_particleAttachmentList.Contains(attach))
                    _obicontrol.m_particleAttachmentList.Add(attach);
                }
            }
            else
                Debug.LogError($"索引 {i} 的附着组件不存在, 请先添加粒子附着组件。");
        }
    }
    /// <summary>
    /// 清除粒子附着——清除所有附着组件和对象
    /// </summary>
    public void ClearParticleAttachment(ObiActor obiActor)
    {
        ObiActorBlueprint blueprint;
        if (!ObiValidate(obiActor, out blueprint))
            return;

        //销毁现有的附着组件和对象
        DestoryAttachmentComponent(obiActor);
        DestroyAttachmentTargetObjects();
    }
    /// <summary>
    /// 销毁现有的附着组件
    /// </summary>
    public void DestoryAttachmentComponent(ObiActor obiActor)
    {
        //销毁所有记录的附着组件
        if (m_AttachmentComponents.Count > 0)
        {
            foreach (var attachment in m_AttachmentComponents.Values)
            {
                if (Application.isPlaying)
                    Destroy(attachment);
                else
                    DestroyImmediate(attachment);
            }
            m_AttachmentComponents.Clear();
        }
        //防止其余的附着组件影响到粒子设置
        ObiParticleAttachment[] attachments = obiActor.gameObject.GetComponents<ObiParticleAttachment>();
        if (attachments.Length != 0)
        {
            UtilsVoltage.DebugLog(Color.green, $"销毁:{name}_所有附着组件", this);
            foreach (var attachment in attachments)
            {
                if (Application.isPlaying)
                    Destroy(attachment);
                else
                    DestroyImmediate(attachment);
            }
        }
    }
    /// <summary>
    /// 销毁附着对象和父对象
    /// </summary>
    private void DestroyAttachmentTargetObjects()
    {
        //销毁附着对象
        if (m_TargetObjects.Count > 0)
        {
            UtilsVoltage.DebugLog(Color.green, $"销毁:{name}_所有附着对象");
            foreach (var targetObject in m_TargetObjects)
            {
                if (Application.isPlaying)
                    Destroy(targetObject);
                else
                    DestroyImmediate(targetObject);
            }
            m_TargetObjects.Clear();
        }
        //销毁附着对象的父级
        if (m_targetParent != null)
        {
            UtilsVoltage.DebugLog(Color.green, $"销毁:{name}_默认附着父对象");
            if (Application.isPlaying)
                Destroy(m_targetParent);
            else
                DestroyImmediate(m_targetParent);
        }
    }
    private void SetAttachmentTargetPosition(GameObject go, ObiActorBlueprint blueprint, int index)
    {
        if (blueprint is ObiRopeBlueprint ropeBlueprint)
        {
            if (index < 0 || index >= ropeBlueprint.path.ControlPointCount)
            {
                Debug.LogError($"索引 {index} 超出范围, Rope 粒子数量: {ropeBlueprint.path.ControlPointCount}");
                return;
            }

            ObiWingedPoint ropePointdata = ropeBlueprint.path.points.data[index];
            UtilsVoltage.DebugLog(Color.green, $"设置附着对象:{go.name} localPosition:{ropePointdata.position}", go.transform);
            go.transform.localPosition = ropePointdata.position;
        }
    }
    public bool ObiValidate(ObiActor obiActor, out ObiActorBlueprint blueprint)
    {
        blueprint = null;
        if (obiActor == null)
        {
            Debug.LogError("请确保该对象包含ObiActor组件(如ObiRope)。");
            return false;
        }
        blueprint = obiActor.sourceBlueprint;
        if (blueprint == null)
        {
            Debug.LogError("未找到有效的蓝图, 请确保ObiActor已正确设置蓝图。");
            return false;
        }
        if (blueprint.groups == null || blueprint.groups.Count == 0)
        {
            Debug.LogError("蓝图中没有有效的粒子组。请确保至少有一个粒子组存在。");
            return false;
        }
        return true;
    }
    private void CheckAttachmentComponent(ObiActor obiActor)
    {
        ObiActorBlueprint blueprint;
        if (!ObiValidate(obiActor, out blueprint))
            return;

        m_AttachmentComponents.Clear();

        ObiParticleAttachment[] _obiparticleattachments = obiActor.gameObject.GetComponents<ObiParticleAttachment>();
        bool[] AttachmentFilled = new bool[blueprint.groups.Count];

        for (int i = 0; i < blueprint.groups.Count; i++)
        {
            if (_obiparticleattachments.Length > 0)
            {
                foreach (var attachment in _obiparticleattachments)
                {
                    if (attachment.particleGroup == blueprint.groups[i])
                    {
                        UtilsVoltage.DebugLog(Color.green, $"粒子组: {blueprint.groups[i].name}({i}), 已找到附着组件");
                        m_AttachmentComponents.Add(i, attachment);
                        AttachmentFilled[i] = true;
                        break;
                    }
                }
            }
            if (!AttachmentFilled[i])
            {
                //为场景中不存在附着组件的粒子组创建附着组件
                ObiParticleAttachment attachmentComponent = gameObject.AddComponent<ObiParticleAttachment>();
                attachmentComponent.particleGroup = blueprint.groups[i];
                attachmentComponent.attachmentType = m_AttachmentType;
                if (!m_AttachmentComponents.ContainsKey(i))
                    m_AttachmentComponents.Add(i, attachmentComponent);
                UtilsVoltage.DebugLog(Color.green, $"粒子组: {blueprint.groups[i].name}({i}), 已创建附着组件");
            }
        }
        //销毁多余的附着组件
        if (_obiparticleattachments.Length > 0)
        {
            foreach (var attachment in _obiparticleattachments)
            {
                if (!m_AttachmentComponents.ContainsValue(attachment))
                {
                    if (Application.isPlaying)
                        Destroy(attachment);
                    else
                        DestroyImmediate(attachment);
                }
            }
        }
    }
    private void CheckAttachmentTargetComponent(GameObject go)
    {
        go.GetOrAddComponent<ObiControl_Grabbable>();
        Collider _collider = go.GetComponent<Collider>() ?? go.AddComponent<SphereCollider>();
        go.GetOrAddComponent<Rigidbody>();

        go.GetOrAddComponent<ObiCollider>().Filter = -458744;//设置附着对象碰撞层(自身3，不与1，2碰撞)
        go.GetOrAddComponent<ObiRigidbody>();
        go.GetOrAddComponent<Grabbable>();
    }

    #region Public API for external tools
    
    /// <summary>
    /// 为指定的Plug对象设置KeyID
    /// </summary>
    /// <param name="plugObject">Plug对象</param>
    /// <param name="keyID">要设置的KeyID</param>
    /// <returns>是否设置成功</returns>
    public bool SetPlugKeyID(GameObject plugObject, int keyID)
    {
        if (plugObject == null)
        {
            Debug.LogError("Plug对象为空，无法设置KeyID");
            return false;
        }

        // 优先尝试使用 Plug_Grabbable 的公共方法
        Plug_Grabbable plugGrabbable = plugObject.GetComponent<Plug_Grabbable>();
        if (plugGrabbable != null)
        {
            Debug.Log($"🔧 使用Plug_Grabbable的公共方法设置KeyID");
            return plugGrabbable.SetPlugKeyID(keyID);
        }

        // 如果没有 Plug_Grabbable，尝试添加 PlugBase 组件
        PlugBase plugBase = plugObject.GetComponent<PlugBase>();
        if (plugBase == null)
        {
            Debug.Log($"为 {plugObject.name} 添加PlugBase组件");
            plugBase = plugObject.AddComponent<PlugBase>();
        }

        // 通过反射设置KeyID（作为备选方案）
        Debug.LogWarning($"⚠️ 回退到反射方法设置KeyID");
        return SetPlugKeyIDByReflection(plugBase, keyID);
    }

    /// <summary>
    /// 批量设置Plug对象的KeyID
    /// </summary>
    /// <param name="keyIDs">KeyID列表，按顺序对应m_TargetObjects中的对象</param>
    /// <returns>成功设置的数量</returns>
    public int SetPlugKeyIDs(List<int> keyIDs)
    {
        if (m_TargetObjects == null || m_TargetObjects.Count == 0)
        {
            Debug.LogWarning("没有Plug对象需要设置KeyID");
            return 0;
        }

        int minCount = Mathf.Min(m_TargetObjects.Count, keyIDs.Count);
        int successCount = 0;

        for (int i = 0; i < minCount; i++)
        {
            if (SetPlugKeyID(m_TargetObjects[i], keyIDs[i]))
            {
                successCount++;
                Debug.Log($"✅ 成功设置Plug {m_TargetObjects[i].name} 的KeyID为 {keyIDs[i]}");
            }
            else
            {
                Debug.LogError($"❌ 设置Plug {m_TargetObjects[i].name} 的KeyID失败");
            }
        }

        Debug.Log($"🎉 批量设置KeyID完成！成功设置 {successCount}/{minCount} 个Plug对象");
        return successCount;
    }

    /// <summary>
    /// 通过反射设置PlugBase的KeyID
    /// </summary>
    /// <param name="plugBase">PlugBase组件</param>
    /// <param name="keyID">要设置的KeyID</param>
    /// <returns>是否设置成功</returns>
    private bool SetPlugKeyIDByReflection(PlugBase plugBase, int keyID)
    {
        try
        {
            Debug.LogWarning($"🔧 使用反射方法设置KeyID: {keyID} (建议使用Plug_Grabbable.SetPlugKeyID方法)");
            
            // 通过反射访问私有字段 m_key
            var keyField = plugBase.GetType().GetField("m_key", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (keyField == null)
            {
                Debug.LogError($"❌ 无法找到PlugBase的m_key字段");
                return false;
            }

            // 创建新的KeyWithPlug实例
            KeyWithPlug newKey = new KeyWithPlug();
            
            // 设置KeyWithPlug的私有字段 m_keyId
            var keyIdField = newKey.GetType().GetField("m_keyId", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (keyIdField == null)
            {
                Debug.LogError($"无法找到KeyWithPlug的m_keyId字段");
                return false;
            }

            // 设置KeyID
            keyIdField.SetValue(newKey, keyID);
            
            // 设置匹配模式为LockId
            var matchModeField = newKey.GetType().GetField("m_matchMode", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (matchModeField != null)
            {
                matchModeField.SetValue(newKey, MatchMode.LockId);
            }

            // 将新的Key赋值给PlugBase
            keyField.SetValue(plugBase, newKey);
            
            // 验证设置是否成功
            var verifyKey = (KeyBase)keyField.GetValue(plugBase);
            if (verifyKey != null && verifyKey.KeyId == keyID)
            {
                return true;
            }
            else
            {
                Debug.LogError($"KeyID设置验证失败，期望: {keyID}, 实际: {(verifyKey?.KeyId ?? -1)}");
                return false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"设置KeyID时发生异常: {ex.Message}");
            return false;
        }
    }
    
    #endregion
}
