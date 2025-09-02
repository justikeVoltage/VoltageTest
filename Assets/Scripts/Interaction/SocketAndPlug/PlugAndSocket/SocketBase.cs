/****************************************************
    功能：插座基类
    作者：ZZQ
    创建日期：#2025/02/20#
    // 修改内容：插座类的enable控制、跟随plug、公开部分属性、难度设置暂时禁用（已保留） ZZQ #2025/03/17#
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

namespace Voltage
{
    /// <summary>
    /// 注意：物理驱动的交互需要注意切换刚体运动学状态时会导致更新一次碰撞行为
    /// </summary>
    public class SocketBase : MonoBehaviour
    {
        [SerializeField] public bool m_enable = true;
        [Header("Match")]
        [SerializeField] public LockWithSocket m_lock;

        [Space(6)]
        [Header("Placement")]
        [SerializeField] public Transform m_targetTransform;

        [Space(6)]
        [Header("Connection processing")]
        [SerializeField] public bool m_fixedAfterConnection = true;
        [SerializeField] public bool m_FollowAfterConnect = false;


        [Foldout("Connection Events"), SerializeField] public UnityEvent m_beforeConnection;
        [Foldout("Connection Events"), SerializeField] public UnityEvent m_onConnectting;
        [Foldout("Connection Events"), SerializeField] public UnityEvent m_afterConnection;

        private bool _isConnected = false;
        private bool _allowConnect = true;
        public HashSet<PlugBase> _plugs = new HashSet<PlugBase>();
        private Rigidbody _rigidbody;
        public PlugBase _connectedPlug;
        private bool _initialKinematicState;
        private bool _currentKinematicState;
        public bool AllowConnect { get { return _allowConnect; } set { _allowConnect = value; } }
        public bool IsConnected { get { return _isConnected; } set { _isConnected = value; } }
        public LockBase Lock { get { return m_lock; } }
        public Rigidbody Rigidbody { get { return _rigidbody; } }
        public Transform TargetTransform { get { return m_targetTransform; } set { m_targetTransform = value; } }
        public bool InitialKinematicState { get { return _initialKinematicState; } }
        public bool CurrentKinematicState { get { return _currentKinematicState; } set { _currentKinematicState = value; } }

        #region life cycle  
        public virtual void Awake()
        {
            //InitialDifficulty();//难度设置

            //改变rigidbody的isKinematic属性会导致重新唤醒OnTriggerEnter,因此通过获取引用的方式避免
            _rigidbody = GetRigidbody(this.transform);

            _initialKinematicState = _rigidbody.isKinematic;
            _currentKinematicState = _initialKinematicState;
        }

        public virtual void Start()
        {
            if (m_targetTransform == null)
            {
                m_targetTransform = this.transform;
            }
        }

        public virtual void FixedUpdate()
        {
            if (!m_enable) return;
            if (!_isConnected) CheckPlug();
        }

        private void OnTriggerEnter(Collider other)
        {
            PlugBase plug = other.GetComponent<PlugBase>();
            if (plug == null) return;

            //物理交互连接时会改变刚体运动学状态，因此需要检测
            if (_isConnected)
            {
                //避免刚体运动学模式切换时连续触发碰撞器，从而导致错误连接
                if (_rigidbody.isKinematic != _currentKinematicState || plug.Rigidbody.isKinematic != plug.CurrentKinematicState)//运动状态主动改变
                {
                    //Debug.Log("检测运动学变化");
                    _currentKinematicState = _rigidbody.isKinematic;
                    plug.CurrentKinematicState = plug.Rigidbody.isKinematic;
                    return;
                }
                Disconnect();//清除状态并防止拿走立即检测
                return;
            }

            if (!_plugs.Contains(plug))
            {
                _plugs.Add(plug);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            PlugBase plug = other.GetComponent<PlugBase>();
            if (plug == null) return;

            if (_plugs.Contains(plug))
            {
                _plugs.Remove(plug);
            }
        }
        #endregion

        #region main logic
        //检查插头
        private void CheckPlug()
        {
            if (!CanConnection()) return;

            foreach (PlugBase plug in _plugs)
            {
                if (!_isConnected)
                {
                    if (MatchDetection(plug))
                    {
                        _connectedPlug = plug;
                        plugConnection(plug);
                        break;
                    }
                }
            }
            RemoveIncorrectPlugs();
        }
        public virtual bool CanConnection()
        {
            return _plugs.Count > 0;
        }

        //返回lockbase和keybase是否匹配的结果
        public bool MatchDetection(PlugBase plug)
        {
            //Debug.Log("CanConnection:" + LockBase.Match(m_lock, plug.Key));
            return LockBase.Match(m_lock, plug.Key);
        }

        //连接插头
        private void plugConnection(PlugBase plug)
        {
            plug.ConnectedSocket = this;
            BeforeConnection(plug);
            OnConnectting(plug);
            AfterConnection(plug);
        }

        //移除插头除了当前连接的
        private void RemoveIncorrectPlugs()
        {
            _plugs.Clear();
            if (_connectedPlug != null) _plugs.Add(_connectedPlug);
        }

        public virtual void BeforeConnection(PlugBase plug)
        {
            //TODO: 连接前的逻辑
            ReleaseSocket();

            m_beforeConnection?.Invoke();
            plug.BeforeConnection();
        }

        public virtual void OnConnectting(PlugBase plug)
        {
            //TODO: 连接中的逻辑
            PlugMoveToSocket(plug, m_targetTransform);

            _isConnected = true;
            plug.IsConnected = true;

            m_onConnectting?.Invoke();
            plug.OnConnectting();
        }

        public virtual void AfterConnection(PlugBase plug)
        {
            //TODO: 连接后的逻辑
            SetRigidbodyKinematicState(true);
            plug.SetRigidbodyKinematicState(true);

            m_afterConnection?.Invoke();
            plug.AfterConnection();
        }

        private void PlugMoveToSocket(PlugBase plug, Transform socketTarget)
        {
            plug.SetTransform(socketTarget);
        }

        //释放插座
        public virtual void ReleaseSocket()
        {
            //TODO: 释放插座的逻辑
        }
        private Rigidbody GetRigidbody(Transform _transform)
        {
            Rigidbody targetRigidbody;
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
                //通过默认更新碰撞行为更新刚体运动学状态-->OnTriggerEnter
                //Debug.Log($"{transform.name} SetRigidbodyIsKinematic:" + _rigidbody.isKinematic);
            }
        }
        public void Disconnect()
        {
            if (_isConnected)
            {
                _connectedPlug = null;
                Debug.Log("Disconnect",this);
                foreach (PlugBase plug in _plugs)
                {
                    plug.ConnectedSocket = null;
                    plug.IsConnected = false;
                }
                _plugs.Clear();
                _isConnected = false;
            }
        }
        #endregion

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
        //设置碰撞体大小
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
