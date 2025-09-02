using System;
using System.Collections;
using System.Collections.Generic;
using Obi;
using UnityEditor;
using UnityEngine;
using Voltage;

[CustomEditor(typeof(ObiRopePointTools))]
public class ObiRopePointToolsEditor : Editor
{
    private ObiRopePointTools TargetObj;
    private bool showBlueprintEditing = true; // 蓝图编辑方法折叠状态
    private bool showRopePointGenerating = true; // 节点生成方法折叠状态
    private bool showRopePointModifying = true; // 节点修改方法折叠状态


    private void OnEnable()
    {
        this.TargetObj = (ObiRopePointTools)this.target;
    }

    public override void OnInspectorGUI()
    {
        // base.OnInspectorGUI();

        DrawInspector();
    }

    private void DrawInspector()
    {
        ObiRopeBase rope = this.TargetObj.GetComponent<ObiRopeBase>();

        EditorGUILayout.BeginVertical();
        // 居中显示标签
        GUIStyle centeredLabel = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
        };
        EditorGUILayout.LabelField("Rope Modify Tools", centeredLabel);

        if (!BlueprintEditingMethod(rope)) return;

        GUILayout.Space(10);

        if (!RopePointGeneratingMethod(rope)) return;

        GUILayout.Space(10);
        if (!RopePointModifyingMethod(rope)) return;

        EditorGUILayout.EndVertical();
    }
    
    #region 蓝图编辑方法
    /// <summary> 蓝图编辑方法 </summary>
    private bool BlueprintEditingMethod(ObiRopeBase rope)
    {
        showBlueprintEditing = EditorGUILayout.Foldout(showBlueprintEditing, "蓝图修改", true, EditorStyles.boldLabel);
        if (showBlueprintEditing)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            if (rope == null)
            {
                UtilsVoltage.DebugLog(Color.yellow, "没有获取到 ObiRopeBase 组件");
                return false;
            }

            TargetObj.m_thickness = EditorGUILayout.FloatField("Thickness", TargetObj.m_thickness);
            TargetObj.m_resolution = EditorGUILayout.Slider("Resolution", TargetObj.m_resolution, 0, 1);
            TargetObj.m_pooledParticles = EditorGUILayout.IntField("Pooled Particles", TargetObj.m_pooledParticles);

            GUILayout.Space(6);
            if (GUILayout.Button("一键修改蓝图"))
            {
                Undo.RecordObject(rope.sourceBlueprint, "Modify blueprint");
                TargetObj.ModifyBlueprint(rope);
                // 通过序列化属性所做的修改应用到目标对象上，并触发Unity内部的更新机制
                serializedObject.ApplyModifiedProperties();
                // 标记指定的目标对象为已修改，确保其状态会被保存到场景文件或资源文件中
                EditorUtility.SetDirty(this.TargetObj);
            }

            EditorGUILayout.EndVertical();
        }

        return true;
    }
    #endregion

    #region 节点生成方法
    /// <summary> 节点生成方法 </summary>
    private bool RopePointGeneratingMethod(ObiRopeBase rope)
    {
        showRopePointGenerating = EditorGUILayout.Foldout(showRopePointGenerating, "节点生成", true, EditorStyles.boldLabel);
        if (showRopePointGenerating)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            if (rope == null)
                return false;

            TargetObj.m_RopeLength = EditorGUILayout.FloatField("Rope Length", TargetObj.m_RopeLength);
            TargetObj.m_InsertPointCount = EditorGUILayout.IntField("Insert Point Count", TargetObj.m_InsertPointCount);
            TargetObj.m_Mass = EditorGUILayout.Slider("Point Mass", TargetObj.m_Mass, 0, 2);
            TargetObj.m_Category = EditorGUILayout.Popup("Point Category", TargetObj.m_Category, ObiUtils.categoryNames, GUILayout.MinWidth(94));
            TargetObj.m_Mask = EditorGUILayout.MaskField("Point Collides with", TargetObj.m_Mask, ObiUtils.categoryNames, GUILayout.MinWidth(94));


            GUILayout.Space(6);
            if (GUILayout.Button("一键修改节点"))
            {
                TargetObj.ModifyRope(rope);
                // 通过序列化属性所做的修改应用到目标对象上，并触发Unity内部的更新机制
                serializedObject.ApplyModifiedProperties();
                // 标记指定的目标对象为已修改，确保其状态会被保存到场景文件或资源文件中
                EditorUtility.SetDirty(this.TargetObj);
            }

            if (GUILayout.Button("清除所有控制点"))
            {
                TargetObj.RemoveControlPoint(rope, Voltage.ObiPointRemoveMode.All);
                // 通过序列化属性所做的修改应用到目标对象上，并触发Unity内部的更新机制
                serializedObject.ApplyModifiedProperties();
                // 标记指定的目标对象为已修改，确保其状态会被保存到场景文件或资源文件中
                EditorUtility.SetDirty(this.TargetObj);
            }
            if (GUILayout.Button("清除所有中间点"))
            {
                TargetObj.RemoveControlPoint(rope);
                // 通过序列化属性所做的修改应用到目标对象上，并触发Unity内部的更新机制
                serializedObject.ApplyModifiedProperties();
                // 标记指定的目标对象为已修改，确保其状态会被保存到场景文件或资源文件中
                EditorUtility.SetDirty(this.TargetObj);
            }
            EditorGUILayout.EndVertical();
        }

        return true;
    }
    #endregion
    #region 节点修改方法
    /// <summary> 节点修改方法 </summary>
    private bool RopePointModifyingMethod(ObiRopeBase rope)
    {
        showRopePointModifying = EditorGUILayout.Foldout(showRopePointModifying, "节点修改", true, EditorStyles.boldLabel);
        if (showRopePointModifying)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            if (rope == null)
                return false;

            var blueprint = rope.sourceBlueprint;
            if (blueprint != null)
            {
                //开始粒子组选择
                var rect1 = EditorGUILayout.GetControlRect();
                SerializedProperty startGroup = serializedObject.FindProperty("m_startGroup");
                var label1 = EditorGUI.BeginProperty(rect1, new GUIContent("Start ParticleGroup"), startGroup);
                rect1 = EditorGUI.PrefixLabel(rect1, label1);

                if (GUI.Button(rect1, TargetObj.m_startGroup != null ? TargetObj.m_startGroup.name : "None", EditorStyles.popup))
                {
                    // create the menu and add items to it
                    GenericMenu menu = new GenericMenu();
                    menu.allowDuplicateNames = true;

                    for (int i = 0; i < blueprint.groups.Count; ++i)
                    {
                        menu.AddItem(new GUIContent(blueprint.groups[i].name), blueprint.groups[i] == TargetObj.m_startGroup, OnStartParticleGroupSelected, blueprint.groups[i]);
                    }
                    // display the menu
                    menu.DropDown(rect1);
                }
                EditorGUI.EndProperty();

                //结束粒子组选择
                var rect2 = EditorGUILayout.GetControlRect();
                SerializedProperty endGroup = serializedObject.FindProperty("m_endGroup");
                var label2 = EditorGUI.BeginProperty(rect2, new GUIContent("End ParticleGroup"), endGroup);
                rect2 = EditorGUI.PrefixLabel(rect2, label2);

                if (GUI.Button(rect2, TargetObj.m_endGroup != null ? TargetObj.m_endGroup.name : "None", EditorStyles.popup))
                {
                    // create the menu and add items to it
                    GenericMenu menu2 = new GenericMenu();
                    menu2.allowDuplicateNames = true;

                    for (int i = 0; i < blueprint.groups.Count; ++i)
                    {
                        menu2.AddItem(new GUIContent(blueprint.groups[i].name), blueprint.groups[i] == TargetObj.m_endGroup, OnEndParticleGroupSelected, blueprint.groups[i]);
                    }
                    // display the menu
                    menu2.DropDown(rect2);
                }
                EditorGUI.EndProperty();
            }
            // 验证节点按钮
            // if (GUILayout.Button("确认索引"))
            // {
            //     TargetObj.GetIndexOfSelectPoint(rope, TargetObj.m_startGroup, TargetObj.m_endGroup, out int startIndex, out int endIndex);
            // }

            // 节点修改方法
            GUILayout.Space(6);
            EditorGUILayout.LabelField("增加节点", EditorStyles.label);
            TargetObj.m_middlePointCount = EditorGUILayout.IntField("Middle Point Count", TargetObj.m_middlePointCount);
            if (GUILayout.Button("增加中间节点"))
            {
                TargetObj.AddMiddleControlPoint(rope, TargetObj.m_middlePointCount);
                // 通过序列化属性所做的修改应用到目标对象上，并触发Unity内部的更新机制
                serializedObject.ApplyModifiedProperties();
                // 标记指定的目标对象为已修改，确保其状态会被保存到场景文件或资源文件中
                EditorUtility.SetDirty(this.TargetObj);
            }
            if (GUILayout.Button("删除中间节点"))
            {
                TargetObj.RemoveMiddleControlPoint(rope);
                // 通过序列化属性所做的修改应用到目标对象上，并触发Unity内部的更新机制
                serializedObject.ApplyModifiedProperties();
                // 标记指定的目标对象为已修改，确保其状态会被保存到场景文件或资源文件中
                EditorUtility.SetDirty(this.TargetObj);
            }

            GUILayout.Space(6);
            EditorGUILayout.LabelField("偏移节点", EditorStyles.label);
            TargetObj.m_pointOffset = EditorGUILayout.FloatField("Point Offset", TargetObj.m_pointOffset);
            if (GUILayout.Button("偏移节点"))
            {
                Undo.RecordObject(rope.sourceBlueprint, "Offset control point");
                TargetObj.OffsetControlPoint(rope);
                // 通过序列化属性所做的修改应用到目标对象上，并触发Unity内部的更新机制
                serializedObject.ApplyModifiedProperties();
                // 标记指定的目标对象为已修改，确保其状态会被保存到场景文件或资源文件中
                EditorUtility.SetDirty(this.TargetObj);
            }

            EditorGUILayout.EndVertical();
        }

        return true;
    }
    /// <summary>
    /// 开始粒子组选择回调
    /// </summary>
    /// <param name="index"></param>
    void OnStartParticleGroupSelected(object index)
    {
        Undo.RecordObject(TargetObj, "Set particle group");
        TargetObj.m_startGroup = index as ObiParticleGroup;
        PrefabUtility.RecordPrefabInstancePropertyModifications(TargetObj);
    }
    /// <summary>
    /// 结束粒子组选择回调
    /// </summary>
    /// <param name="index"></param>
    void OnEndParticleGroupSelected(object index)
    {
        Undo.RecordObject(TargetObj, "Set particle group");
        TargetObj.m_endGroup = index as ObiParticleGroup;
        PrefabUtility.RecordPrefabInstancePropertyModifications(TargetObj);
    }
    #endregion

}
