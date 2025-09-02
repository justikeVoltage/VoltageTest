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

        // 创建并设置所有附着对象
        for (int i = 0; i < blueprint.groups.Count; i++)
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
                    SetAttachmentTargetPosition(go, blueprint, i);
                    CheckAttachmentTargetComponent(go);
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
}
