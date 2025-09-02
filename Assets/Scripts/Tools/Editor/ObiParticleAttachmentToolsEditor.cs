using System.Collections;
using System.Collections.Generic;
using Obi;
using UnityEditor;
using UnityEngine;
using Voltage;

[CustomEditor(typeof(ObiParticleAttachmentTools))]
public class ObiParticleAttachmentToolsEditor : Editor
{
    private ObiParticleAttachmentTools TargetObj;
    private SerializedProperty m_TargetObjectsProp;
    void OnEnable()
    {
        TargetObj = (ObiParticleAttachmentTools)this.target;
        m_TargetObjectsProp = serializedObject.FindProperty("m_TargetObjects");
    }
    public override void OnInspectorGUI()
    {
        // base.OnInspectorGUI();
        DrawInspector();
    }

    private void DrawInspector()
    {
        ObiActor obiActor = TargetObj.GetComponent<ObiActor>();

        EditorGUILayout.BeginVertical();

        EditorGUILayout.LabelField("全局参数设置", EditorStyles.boldLabel);
        TargetObj.m_AttachmentType = (ObiParticleAttachment.AttachmentType)EditorGUILayout.EnumPopup("Attachment Type", TargetObj.m_AttachmentType);
        EditorGUILayout.LabelField("附着对象设置", EditorStyles.boldLabel);
        TargetObj.m_TargetObjectPrefab = (GameObject)EditorGUILayout.ObjectField("Target Object Prefab", TargetObj.m_TargetObjectPrefab, typeof(GameObject), true);
        TargetObj.m_TargetObjectName = EditorGUILayout.TextField("Target Object Name", TargetObj.m_TargetObjectName);

        serializedObject.Update();
        EditorGUILayout.PropertyField(m_TargetObjectsProp, new GUIContent("Target Objects"), true);
        serializedObject.ApplyModifiedProperties();

        GUILayout.Space(10);
        if (GUILayout.Button("重置粒子附着组件"))
        {
            TargetObj.AddParticleAttachmentComponent(obiActor);
        }
        if (GUILayout.Button("设置粒子附着目标"))
        {
            TargetObj.SetParticleAttachmentTarget(obiActor);
        }
        if (GUILayout.Button("清除粒子附着(组件和预制体)"))
        {
            TargetObj.ClearParticleAttachment(obiActor);
        }

        EditorGUILayout.EndVertical();
    }
}
