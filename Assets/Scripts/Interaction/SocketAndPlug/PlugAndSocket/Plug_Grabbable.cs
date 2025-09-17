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

        #region Public API for AutoCircularPointTools
        
        /// <summary>
        /// 设置Plug的KeyID - 为AutoCircularPointTools提供的公共接口
        /// </summary>
        /// <param name="keyID">要设置的KeyID</param>
        /// <returns>是否设置成功</returns>
        public bool SetPlugKeyID(int keyID)
        {
            try
            {
                Debug.Log($"🔑 Plug_Grabbable.SetPlugKeyID: 开始设置KeyID为 {keyID}");
                
                // 如果没有Key，创建一个新的KeyWithPlug实例
                if (Key == null)
                {
                    Debug.Log($"🔧 创建新的KeyWithPlug实例");
                    // 通过反射设置父类的m_key字段
                    var keyField = this.GetType().BaseType.GetField("m_key", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (keyField != null)
                    {
                        KeyWithPlug newKey = new KeyWithPlug();
                        keyField.SetValue(this, newKey);
                        Debug.Log($"✅ 成功创建并设置新的KeyWithPlug实例");
                    }
                    else
                    {
                        Debug.LogError($"❌ 无法找到PlugBase的m_key字段");
                        return false;
                    }
                }
                
                // 使用KeyBase的公共方法设置KeyID
                if (Key != null)
                {
                    Key.SetKeyId(keyID);
                    
                    // 验证设置是否成功
                    if (Key.KeyId == keyID)
                    {
                        Debug.Log($"✅ 成功设置Plug KeyID为 {keyID}");
                        return true;
                    }
                    else
                    {
                        Debug.LogError($"❌ KeyID设置验证失败，期望: {keyID}, 实际: {Key.KeyId}");
                        return false;
                    }
                }
                else
                {
                    Debug.LogError($"❌ Key仍然为null，无法设置KeyID");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ 设置KeyID时发生异常: {ex.Message}");
                return false;
            }
        }
        
        #endregion
    }
}