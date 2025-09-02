/****************************************************
    功能：扎带Trigger检测器
    作者：LZL
    创建日期：#2025/08/06#
    修改内容：独立的Trigger检测组件，支持Tag过滤
*****************************************************/

using UnityEngine;
using System;

namespace Voltage
{
    /// <summary>
    /// 扎带Trigger检测器，用于检测碰撞触发
    /// </summary>
    public class CableTieTriggerDetector : MonoBehaviour
    {
        [Header("触发设置")]
        [SerializeField] public bool useTagFilter = true; // 是否使用Tag过滤
        [SerializeField] public string[] allowedTags = { "punchHole" }; // 允许触发的Tag列表
        [SerializeField] public bool triggerOnce = true; // 是否只触发一次
        
        
        // 事件回调
        private System.Action _onTriggerActivated;
        public System.Action OnTriggerActivated 
        { 
            get => _onTriggerActivated; 
            set 
            { 
                Debug.Log($"[CableTieTriggerDetector] {gameObject.name} 的OnTriggerActivated事件被设置: {(value != null ? "有监听器" : "null")}");
                _onTriggerActivated = value;
            } 
        }
        
        public System.Action<Collider> OnTriggerActivatedWithCollider; // 带碰撞体信息的回调
        
        private bool hasTriggered = false; // 是否已经触发过
        
        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[CableTieTriggerDetector] {gameObject.name} 检测到碰撞: {other.gameObject.name} (Tag: {other.tag})");
            Debug.Log($"[CableTieTriggerDetector] 当前对象ID: {GetInstanceID()}, GameObject: {gameObject.name}");
            
            // 如果设置了只触发一次且已经触发过，则返回
            if (triggerOnce && hasTriggered)
            {
                Debug.Log($"[CableTieTriggerDetector] {gameObject.name} 已触发过，忽略重复触发");
                return;
            }
            
            // Tag过滤检查
            if (useTagFilter)
            {
                Debug.Log($"[CableTieTriggerDetector] {gameObject.name} 启用Tag过滤，允许列表: [{string.Join(", ", allowedTags)}]");
                if (!IsTagAllowed(other.tag))
                {
                    Debug.Log($"[CableTieTriggerDetector] {gameObject.name} Tag过滤失败：{other.tag} 不在允许列表中，忽略触发");
                    return; // 重要：Tag不匹配时应该return，而不是继续执行
                }
                else
                {
                    Debug.Log($"[CableTieTriggerDetector] {gameObject.name} Tag过滤通过：{other.tag} 在允许列表中");
                }
            }
            else
            {
                Debug.Log($"[CableTieTriggerDetector] {gameObject.name} 未启用Tag过滤");
            }
            
            // 检查是否有事件监听器（详细调试）
            Debug.Log($"[CableTieTriggerDetector] 详细检查事件状态:");
            Debug.Log($"  - OnTriggerActivated是否为null: {OnTriggerActivated == null}");
            Debug.Log($"  - _onTriggerActivated是否为null: {_onTriggerActivated == null}");
            
            if (OnTriggerActivated == null)
            {
                Debug.LogWarning($"[CableTieTriggerDetector] 没有注册OnTriggerActivated事件监听器！");
            }
            else
            {
                var invocationList = OnTriggerActivated.GetInvocationList();
                Debug.Log($"[CableTieTriggerDetector] 找到 {invocationList.Length} 个事件监听器");
                for (int i = 0; i < invocationList.Length; i++)
                {
                    Debug.Log($"  监听器[{i}]: {invocationList[i].Method.Name} (目标: {invocationList[i].Target})");
                }
            }
            
            // 触发事件
            Debug.Log($"[CableTieTriggerDetector] ✅ Trigger触发成功: {other.gameObject.name} (Tag: {other.tag})");
            hasTriggered = true;
            
            // 调用回调事件
            Debug.Log($"[CableTieTriggerDetector] 准备调用OnTriggerActivated事件，监听器数量: {OnTriggerActivated?.GetInvocationList()?.Length ?? 0}");
            Debug.Log($"[CableTieTriggerDetector] 当前TriggerDetector对象ID: {GetInstanceID()}");
            Debug.Log($"[CableTieTriggerDetector] 当前GameObject: {gameObject.name}");
            
            OnTriggerActivated?.Invoke();
            Debug.Log($"[CableTieTriggerDetector] OnTriggerActivated事件调用完成");
            
            Debug.Log($"[CableTieTriggerDetector] 准备调用OnTriggerActivatedWithCollider事件");
            OnTriggerActivatedWithCollider?.Invoke(other);
            Debug.Log($"[CableTieTriggerDetector] OnTriggerActivatedWithCollider事件调用完成");
            
            Debug.Log($"[CableTieTriggerDetector] 所有事件回调执行完成");
        }
        
        /// <summary>
        /// 检查Tag是否在允许列表中
        /// </summary>
        private bool IsTagAllowed(string tag)
        {
            if (allowedTags == null || allowedTags.Length == 0)
            {
                return true; // 如果没有设置过滤列表，则允许所有Tag
            }
            
            foreach (string allowedTag in allowedTags)
            {
                if (tag.Equals(allowedTag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 重置触发状态（用于重复使用）
        /// </summary>
        public void ResetTrigger()
        {
            hasTriggered = false;
            Debug.Log("[CableTieTriggerDetector] Trigger状态已重置");
        }
        
        /// <summary>
        /// 手动触发事件（用于测试）
        /// </summary>
        [ContextMenu("手动触发事件")]
        public void ManualTrigger()
        {
            Debug.Log("[CableTieTriggerDetector] 手动触发事件");
            OnTriggerActivated?.Invoke();
        }
    }
}