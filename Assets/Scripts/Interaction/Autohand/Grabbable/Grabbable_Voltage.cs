
/****************************************************
    功能：定制化抓取类，继承自Grabbable类，提供自定义初始抓取状态、物理效果禁用等功能
    作者：ZZQ
    创建日期：#2025/02/20#
    修改人：ZZQ
    修改日期：#2025/02/20#
    修改内容：
*****************************************************/using System;
using System.Collections;
using System.Collections.Generic;
using Autohand;
using UnityEngine;
using Voltage;
using NaughtyAttributes;
using Unity.VisualScripting;

namespace Voltage
{
    public class Grabbable_Voltage : Grabbable
    {
        [AutoToggleHeader("Custom")]
        public bool custom = true;
        [ShowIf("custom")]
        public bool m_StartGrabState = true;//初始抓取状态
        [ShowIf("custom")]
        public bool m_disablePhysicsExceptGrabbing = false;//抓取外禁用物理效果
        [ShowIf("custom")]
        public bool m_disablePhysicsBeforeGrabbing = false;//抓取前禁用物理效果

        public bool isDisablePhysicsExceptGrabbing { get => m_disablePhysicsExceptGrabbing; set => m_disablePhysicsExceptGrabbing = value; }
        
        public event Action onDisable;
        public event Action onEnable;
        [HideInInspector]
        private CollisionDetectionMode m_rigidbodyDetectionMode;
        #region lifeCycle
        protected override void OnDisable()
        {
            base.OnDisable();
            onDisable?.Invoke();
        }
        protected void OnEnable()
        {
            onEnable?.Invoke();
            m_rigidbodyDetectionMode = body.collisionDetectionMode;//记录刚体的碰撞检测模式
            if (m_disablePhysicsExceptGrabbing || m_disablePhysicsBeforeGrabbing) DisablePhysics();//初始启用运动学刚体

            if (m_StartGrabState) SetGrabbableState(true);
            else SetGrabbableState(false);
        }
        #endregion

        #region custom Add
        public override void HeldFixedUpdate()
        {
            base.HeldFixedUpdate();
        }
        
        //当前玩家持有时，其他持有者强制松开，通信行为通过方法名调用
        public void OnHeldFixedUpdate()
        {
            ForceHandsRelease();
        }


        protected override void OnGrab(Hand hand)
        {
            EnablePhysics();//抓取时禁用运动学刚体
            base.OnGrab(hand);
        }

        protected override void OnRelease(Hand hand)
        {
            base.OnRelease(hand);
            if(m_disablePhysicsExceptGrabbing)DisablePhysics();//释放时启用运动学刚体
        }
        #endregion
        
        //设置抓取状态
        public void SetGrabbableState(bool _isgrabbed)
        {
            if (_isgrabbed) SetGrabType(HandType.both);
            else SetGrabType(HandType.none);

            Debug.Log($"设置 {gameObject.name} 抓取状态为 {_isgrabbed}".FontColoring("yellow"), gameObject);
        }
        private void SetGrabType(HandType _handType)
        {
            if (handType == _handType) return;
            handType = _handType;
        }
        //禁用物理特性，可在Onrelease\onenable调用
        public void DisablePhysics()
        {
            if (body != null && !body.isKinematic)
            {
                //body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.isKinematic = true;
            }
        }
        //启用物理特性,可在Ongrab调用
        public void EnablePhysics()
        {
            if (body != null && body.isKinematic)
            {
                body.isKinematic = false;
                body.collisionDetectionMode = m_rigidbodyDetectionMode;
            }
        }
    }
}

