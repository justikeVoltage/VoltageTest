/****************************************************
    功能：材质替换、闪烁效果开关
    作者：ZZQ
    创建日期：#2025/03/21#
    修改内容：1.代码优化 ZZQ #2025/04/01#
*****************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;
using Voltage;

public class MaterialFlicker : MonoBehaviour
{
    [SerializeField] private Renderer m_renderer = null;
    [SerializeField] private Material m_Material ;
    [SerializeField] private Color m_EmissionColor = new Color(0, 192, 255, 0.8f);
    [SerializeField] private float m_minimumStrength = 0;
    [SerializeField] private float m_maximumStrength = 0.8f;
    [SerializeField] private AnimationCurve m_IndensityCurve;
    private Material _material = null;
    private Color m_BaseColor = Color.black;//材质颜色，在Awake中自动获取原材质颜色
    private Texture m_BaseTexture = null;//材质贴图，在Awake中自动获取原材质贴图
    private String m_RenderType = "";//渲染类型，在Awake中自动获取渲染类型
    private float m_SmoothnessValue = 0;
    private Material _originalMaterial = null;
    private LocalKeyword _kwEmission;
    public bool IsFlicker = false;
    private float _startTime = 0;
    private void Awake()
    {
        InitMaterial();
    }
    //初始化
    private void InitMaterial()
    {
        //渲染器获取
        if (m_renderer == null) m_renderer = GetComponent<Renderer>();
        if (m_renderer == null) m_renderer = GetComponentInChildren<Renderer>();
        if (m_renderer == null) return;
        //参数自定义设置
        m_IndensityCurve = new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(0.5f, 0.5f, 2, 2), new Keyframe(1, 1, 0, 0));
        m_IndensityCurve.postWrapMode= WrapMode.PingPong;
        //原材质基础值获取
        m_RenderType = m_renderer.material.GetTag("RenderType", false, "Opaque");// 默认返回 "Opaque"
        m_BaseColor = m_renderer.material.GetColor("_Color");
        m_BaseTexture = m_renderer.material.GetTexture("_MainTex")? m_renderer.material.GetTexture("_MainTex"):null;
        m_SmoothnessValue = m_renderer.material.GetFloat("_Glossiness");
        //原材质记录
        _originalMaterial = m_renderer.material; 
        //发光材质获取和设置
        if (m_Material == null&& m_RenderType.Equals("Opaque")) m_Material = Resources.Load<Material>("Textures/Material/FlickerMaterial");//默认材质、黑色塑料
        if (m_Material == null&& m_RenderType.Equals("Transparent")) m_Material = Resources.Load<Material>("Textures/Material/FlickerMaterial_Transparent");//默认材质、黑色塑料
        _material = Instantiate(m_Material);
        _material.SetColor("_Color", m_BaseColor);
        _material.SetTexture("_MainTex", m_BaseTexture);
        _material.SetFloat("_Glossiness", m_SmoothnessValue);
        _material.SetColor("_EmissionColor", m_EmissionColor * 0);
        _kwEmission = new LocalKeyword(_material.shader, "_EMISSION");//获取发光关键字
    }
    /// <summary>
    /// 开始发光
    /// </summary>
    [Button("开始发光")]
    private void StartFlicker()
    {
        SetRendererActive(true);
        m_renderer.material = _material;
        StartCoroutine(Flicker());
    }
    /// <summary>
    /// 停止发光
    /// </summary>
    [Button("停止发光")]
    private void StopFlicker()
    {
        StopCoroutine(Flicker());
        _material.SetKeyword(_kwEmission, false);
        IsFlicker = false;
        m_renderer.material = _originalMaterial;
    }
    /// <summary>
    /// 设置发光状态
    /// </summary>
    /// <param name="state"></param>
    public void SetFlickerState(bool state)
    {
        if (state) StartFlicker();
        else StopFlicker();
    }
    /// <summary>
    /// 设置渲染器的激活状态
    /// </summary>
    /// <param name="active"></param>
    public void SetRendererActive(bool active)
    {
        if (m_renderer == null) 
        {
            SetChildActive(active);
            return;
        }
        if (!active) StopFlicker();
        m_renderer.enabled = active;
    }
    /// <summary>
    /// 设置所有子物体的激活状态
    /// </summary>
    /// <param name="active"></param>
    public void SetChildActive(bool active)
    {
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(active);
        }
    }
    /// <summary>
    /// 一直循环发光的协程
    /// </summary>
    /// <returns></returns>
    private IEnumerator Flicker()
    {
        yield return null;
        _material.SetKeyword(_kwEmission, true);
        IsFlicker = true;
        //开始发光
        _startTime = Time.time;
        while (IsFlicker)
        {
            float time = (Time.time - _startTime)*1;//1秒速度控制
            float value = m_IndensityCurve.Evaluate(time);
            float strength = Mathf.Lerp(m_minimumStrength, m_maximumStrength, value);
            _material.SetColor("_EmissionColor", m_EmissionColor * strength);
            yield return null;
        }
    }
}
