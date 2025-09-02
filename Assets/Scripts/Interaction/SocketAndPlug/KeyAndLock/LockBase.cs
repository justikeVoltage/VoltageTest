/****************************************************
    功能：锁的基类
    作者：ZZQ
    创建日期：#2025/02/20#
    修改人：ZZQ
    修改日期：#2025/02/20#
    修改内容：
*****************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using Voltage.AttributeTools;

namespace Voltage
{
    public enum MatchMode
    {
        Component,
        LockId,
    }
    [System.Serializable]
    public class LockBase
    {
        [SerializeField] public bool m_matchState = false;
        [SerializeField] public MatchMode m_matchMode = MatchMode.LockId;

        [SerializeField,ShowIf ("m_matchMode", MatchMode.LockId)]
        public int m_lockId;
        public int LockId { get { return m_lockId; } }
        public MatchMode MatchMode { get { return m_matchMode; } }
        

        public static bool Match(LockBase _lock, KeyBase _key)
        {
            return _lock.LockMatch(_key);
        }

        public virtual bool LockMatch(KeyBase _key)
        {
            return LockMatchById(_key);
        }

        public virtual bool LockMatchById(KeyBase key)
        {
            if (m_lockId == key.KeyId)
            {
                m_matchState = true;
                return true;
            }
            else
            {
                m_matchState = false;
                return false;
            }
        }


    }
}