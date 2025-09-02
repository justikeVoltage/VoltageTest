/****************************************************
    功能：通过Grabbable组件实现交互的对象附加的匹配插头类，修改匹配后的一些抓取行为
    作者：ZZQ
    创建日期：#2025/02/20#
    修改人：ZZQ
    修改日期：#2025/02/20#
    修改内容：
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voltage;
using Autohand;
using Unity.VisualScripting;
namespace Voltage
{
    public class Plug_Grabbable : PlugBase
    {
        [Space(6)]
        [Header("Processing Grabbable")]
        [SerializeField]public bool m_FollowAfterConnect=false;
        [SerializeField]private bool m_UnableToGrabAfterConnection = false;

        private Grabbable m_grabbable;
        public Grabbable grabbable{ get { return m_grabbable; } }
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
        public override void BeforeConnection()
        {
            base.BeforeConnection();
        }
        public override void OnConnectting()
        {
            base.OnConnectting();
        }
        public override void AfterConnection()
        {
            base.AfterConnection();
            if (m_FollowAfterConnect)
            {
              Follower follower = RootTransform.GetOrAddComponent<Follower>();
              follower.Follow(ConnectedSocket.transform);
            }
            if (m_UnableToGrabAfterConnection)
            {
                UnableToGrabAfterConnection();
            }
        }
        public void OnGrab(Hand hand, Grabbable grab)
        {
            SetRigidbodyKinematicState(false);

            //防止再次检测触发
            CurrentKinematicState = Rigidbody.isKinematic;
        }
        private void OnRelease(Hand hand, Grabbable grab)
        {
            //此处添加释放后执行的内容
        }
        public override void ReleasePlug()
        {
            base.ReleasePlug();
            if (m_grabbable != null) m_grabbable.ForceHandsRelease();
            else  Debug.LogError("No grabbable found on " + name,this);
        }
        private void UnableToGrabAfterConnection()
        {
            if (m_grabbable==null) m_grabbable = GetGrabbable();
            if (m_grabbable != null)
            {
                if(m_grabbable is Grabbable_Voltage _grabbable)
                {
                    _grabbable.SetGrabbableState(false);
                }
                else
                {
                    m_grabbable.handType=HandType.none;
                }
            }
        }
        private Grabbable GetGrabbable()
        {
            Grabbable _grabbable = GetComponent<Grabbable>();
            if (_grabbable == null) _grabbable = GetComponentInParent<Grabbable>();
            return _grabbable;
        }
    }
}