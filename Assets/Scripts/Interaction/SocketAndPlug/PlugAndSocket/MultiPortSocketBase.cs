/****************************************************
    功能：交互对象附加的匹配插座类，可连接多个插头，但去除了断开连接及重新连接的功能
    作者：ZZQ
    创建日期：#2025/02/20#
    修改人：ZZQ
    修改日期：#2025/02/20#
    修改内容：
*****************************************************/
using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using Voltage.AttributeTools;
using UnityEngine.Events;

namespace Voltage
{
    public class MultiPortSocketBase : MonoBehaviour
    {

        [Header("Match")]
        [SerializeField] private List<PlugBase> m_reactPlugs;
        private PlugBase[] _ConnectedPlugs;
        [Header("Connection Events")] [Space(6)]
        [SerializeField] public bool HideAfterConnect = true;
        [Space(6)]
        [SerializeField] private UnityEvent m_allPlugsConnected;

        [Space(6)]
        [Header("Placement")]
        [SerializeField] private Transform m_targetTransform;
        [SerializeField] private bool m_goTargetPos = true;

        public HashSet<PlugBase> _plugs = new HashSet<PlugBase>();
        private bool _isConnected = false;
        public virtual void Awake()
        {
            if (m_targetTransform == null) m_targetTransform = this.transform;
            _ConnectedPlugs = new PlugBase[m_reactPlugs.Count];
        }
        public virtual void FixedUpdate()
        {
            if (!_isConnected) CheckPlug();
        }

        private void OnTriggerEnter(Collider other)
        {
            PlugBase plug = other.GetComponent<PlugBase>();
            if (plug == null)
            {
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
        private void CheckPlug()
        {
            if (!CanConnection()) return;

            foreach (PlugBase plug in _plugs)
            {
                if (!_isConnected)
                {
                    if (MatchDetection(plug))
                    {
                        plugConnection(plug);
                        break;
                    }
                }
            }
            _plugs.Clear();
        }
        public virtual bool CanConnection()
        {
            return _plugs.Count > 0;
        }
        private bool MatchDetection(PlugBase plug)
        {
            foreach (PlugBase reactPlug in m_reactPlugs)
            {
                if (reactPlug == plug) return true;
            }
            return false;
        }

        private void plugConnection(PlugBase plug)
        {
            if (_ConnectedPlugs.Length > 0)
            {
                for (int i = 0; i < _ConnectedPlugs.Length; i++)
                {
                    if (_ConnectedPlugs[i] == plug) return;

                    if (_ConnectedPlugs[i] == null)
                    {
                        _ConnectedPlugs[i] = plug;
                        break;
                    }
                }
            }
            BeforeConnection(plug);
            OnConnectting(plug);
            AfterConnection(plug);
        }
        public virtual void BeforeConnection(PlugBase plug)
        {
            _isConnected = true;
            plug.BeforeConnection();
            plug.IsConnected = true;
        }

        public virtual void OnConnectting(PlugBase plug)
        {
            if (m_goTargetPos) PlugMoveToSocket(plug, m_targetTransform);

            plug.OnConnectting();
        }

        public virtual void AfterConnection(PlugBase plug)
        {
            //TODO: 连接后的逻辑
            plug.SetRigidbodyKinematicState(false);
            plug.AfterConnection();

            CheckAllPlugsConnected();
        }
        private void PlugMoveToSocket(PlugBase plug, Transform socketTarget)
        {
            plug.SetTransform(socketTarget);
        }

        private void CheckAllPlugsConnected()
        {
            foreach (PlugBase plug in _ConnectedPlugs)
            {
                if (plug == null)
                {
                    _isConnected = false;
                    return;
                }
            }
            _isConnected = true;
            m_allPlugsConnected.Invoke();
        }

    }
}