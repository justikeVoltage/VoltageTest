/****************************************************
    功能：通过Grabbable组件实现交互的对象附加的匹配插座类，修改匹配后的一些抓取行为
    作者：ZZQ
    创建日期：#2025/02/20#
    修改内容：1.为Socket添加跟随plug的功能，并修改了一些代码逻辑 ZZQ #2025/03/14#
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using Autohand;
using Unity.VisualScripting;
using UnityEngine;
using Voltage;

namespace Voltage
{
    public class Socket_Grabbable : SocketBase
    {
        [Space(6)]
        [Header("Processing Grabbable")]
        [SerializeField] private Grabbable m_grabbable;
        public override void Awake()
        {
            base.Awake();

            m_grabbable = GetGrabbable();
            if (m_grabbable != null)
            {
                m_grabbable.onGrab.AddListener(OnGrab);
                m_grabbable.onRelease.AddListener(OnRelease);
            }
        }

        public override void BeforeConnection(PlugBase plug)
        {
            //HandHaptics(plug);
            base.BeforeConnection(plug);
        }

        public override void OnConnectting(PlugBase plug)
        {
            base.OnConnectting(plug);
        }

        public override void AfterConnection(PlugBase plug)
        {
            base.AfterConnection(plug);
            //设置物理状态
            if (m_fixedAfterConnection)
            {
                foreach (Grabbable grabbable in GetAllGrabbables())
                {
                    if (grabbable is Grabbable_Voltage _grabbable)
                    {
                        _grabbable.isDisablePhysicsExceptGrabbing = false;
                        _grabbable.DisablePhysics();
                        _grabbable.SetGrabbableState(false);
                    }
                    else
                    {
                        grabbable.enabled = false;
                    }
                }
            }
            else
            {
                if (GetGrabbable() is Grabbable_Voltage _grabbable)
                {
                    _grabbable.EnablePhysics();
                    _grabbable.gameObject.layer = LayerMask.NameToLayer("OnlyHandInteractive");
                    plug.GetComponent<Grabbable_Voltage>().EnablePhysics();
                    if (plug is Plug_Grabbable _plug && !_plug.m_FollowAfterConnect && m_FollowAfterConnect)
                    {
                        _grabbable.gameObject.GetOrAddComponent<Follower>().Follow(plug.transform);
                        _grabbable.gameObject.GetOrAddComponent<SocketBase>().enabled = false;
                        plug.enabled = false;
                    }
                }
                else
                {
                    GetGrabbable().GetComponent<Rigidbody>().isKinematic = false;
                    GetGrabbable().gameObject.layer = LayerMask.NameToLayer("OnlyHandInteractive");
                    plug.gameObject.layer = LayerMask.NameToLayer("OnlyHandInteractive");
                }
            }
        }

        public void OnGrab(Hand hand, Grabbable grab)
        {
            SetRigidbodyKinematicState(false);
        }
        private void OnRelease(Hand hand, Grabbable grab)
        {
            //此处添加释放后执行的内容
        }

        public override bool CanConnection()
        {
            return base.CanConnection();
        }

        public override void ReleaseSocket()
        {
            base.ReleaseSocket();
            if (m_grabbable != null)
            {
                m_grabbable.ForceHandsRelease();
            }
            else
            {
                Debug.LogWarning("No grabbable found on " + name, this);
            }
        }
        private void HandHaptics(PlugBase plug)
        {

            if (m_grabbable == null) return;
            if (m_grabbable.IsHeld())
            {
                foreach (Hand hand in m_grabbable.heldBy)
                {
                    hand.PlayHapticVibration(0.2f);
                }
            }

            if (plug is Plug_Grabbable _plug)
            {
                if (_plug.grabbable == null) return;
                if (_plug.grabbable.IsHeld())
                {
                    foreach (Hand hand in _plug.grabbable.heldBy)
                    {
                        hand.PlayHapticVibration(0.2f);
                    }
                }
            }
        }
        private Grabbable[] GetAllGrabbables()
        {
            List<Grabbable> grabbables = new List<Grabbable>();
            if (m_grabbable != null)
            {
                grabbables.Add(m_grabbable);
            }
            if (_plugs != null)
            {
                foreach (PlugBase plug in _plugs)
                {
                    if (plug is Plug_Grabbable _plug)
                    {
                        grabbables.Add(_plug.grabbable);
                    }
                }
            }
            return grabbables.ToArray();
        }
        private Grabbable GetGrabbable()
        {
            Grabbable grabbable = GetComponent<Grabbable>();
            if (grabbable == null)
            {
                grabbable = GetComponentInParent<Grabbable>();
            }
            if (grabbable == null)
            {
                Debug.LogWarning("No grabbable found on " + name, this);
            }
            return grabbable;
        }
    }
}
