/****************************************************
    功能：插头基类
    作者：ZZQ
    创建日期：#2025/02/20#
    修改人：ZZQ
    修改日期：#2025/02/20#
    修改内容：
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

namespace Voltage
{
    public class PlugBase : MonoBehaviour
    {
        [Header("Match")]
        [SerializeField] private KeyWithPlug m_key;

        [Space(6)]
        [Header("Placement")]
        [SerializeField] public Transform m_rootTransform;

        [Foldout("Connection Events"), SerializeField] public UnityEvent m_beforeConnection;
        [Foldout("Connection Events"), SerializeField] public UnityEvent m_onConnectting;
        [Foldout("Connection Events"), SerializeField] public UnityEvent m_afterConnection;

        private Rigidbody _rigidbody;
        private bool _initialKinematicState;
        private bool _currentKinematicState;
        private SocketBase m_ConnectedSocket;

        private bool _isConnected = false;
        public bool IsConnected { get { return _isConnected; } set { _isConnected = value; }}
        public Rigidbody Rigidbody{ get { return _rigidbody; } }
        public KeyBase Key { get { return m_key; } }
        public SocketBase ConnectedSocket { get { return m_ConnectedSocket; } set { m_ConnectedSocket = value; } }
        public Transform RootTransform { get { return m_rootTransform; } set { m_rootTransform = value; } }
        public bool InitialKinematicState{ get { return _initialKinematicState; } }
        public bool CurrentKinematicState{ get { return _currentKinematicState; } set { _currentKinematicState = value; }}
        public virtual void Awake()
        {
            //InitialDifficulty();//难度设置

            if (m_rootTransform == null)
            {
                m_rootTransform = transform;
            }
            
            _rigidbody = GetRigidbody(m_rootTransform);
            _initialKinematicState = _rigidbody.isKinematic;
            _currentKinematicState = _initialKinematicState;

        }
        public virtual void BeforeConnection()
        {
            m_beforeConnection.Invoke();
            
            ReleasePlug();
        }
        public virtual void OnConnectting()
        {
            m_onConnectting.Invoke();
        }
        public virtual void AfterConnection()
        {
            m_afterConnection.Invoke();
        }
        public virtual void ReleasePlug()
        {
            //TODO: 释放插件的逻辑
        }

        public void SetTransform(Transform transform)
        {
            //Debug.Log(this.name + "SetTransform");
            RootTransform.position = transform.position;
            RootTransform.rotation = transform.rotation;
        }
        private Rigidbody GetRigidbody(Transform _transform)
        {
            Rigidbody targetRigidbody ;
            targetRigidbody = _transform.GetComponent<Rigidbody>();
            if (targetRigidbody == null) targetRigidbody = _transform.GetComponentInParent<Rigidbody>();
            return targetRigidbody;
        }
        public void SetRigidbodyKinematicState(bool _isKinematic)
        {
            Rigidbody _rigidbody = GetRigidbody(this.transform);

            if (_rigidbody != null && _rigidbody.isKinematic != _isKinematic)
            {
                _rigidbody.isKinematic = _isKinematic;
                //Debug.Log($"{transform.name} SetRigidbodyIsKinematic:" + _rigidbody.isKinematic);
            }
        }
        #region Difficulty related
        /// <summary>
        /// 碰撞体大小设置，保留功能，暂时不用
        /// </summary>
        public virtual void InitialDifficulty()
        {
            Collider _collider = GetComponent<Collider>();
            float sizeRatio = 1f;
            
            SetColliderSize(sizeRatio, _collider);
        }
        private void SetColliderSize(float sizeRatio, Collider _collider)
        {
            if (_collider is BoxCollider box)
            {
                box.size *= sizeRatio;
            }
            else if (_collider is SphereCollider sphere)
            {
                sphere.radius *= sizeRatio;
            }
            else if (_collider is CapsuleCollider capsule)
            {
                capsule.radius *= sizeRatio;
                capsule.height *= sizeRatio;
            }
        }
        #endregion
    }
}
