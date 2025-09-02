/****************************************************
    功能：钥匙基类
    作者：ZZQ
    创建日期：#2025/02/20#
    修改人：ZZQ
    修改日期：#2025/02/20#
    修改内容：
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voltage
{
    [System.Serializable]
    /// <summary>
    /// Return verification results, if the key matches the lock,or LockBase.LockId is 0, it will always return true.
    /// Or LockBase.m_verifyComponent is this KeyBase, it will return true.
    /// </summary>
    public class KeyBase
    {
        [SerializeField] protected bool m_matchState = false;
        [SerializeField] private int m_keyId;
        
        public int KeyId { get { return m_keyId; } }

        public static bool Match(KeyBase _key, LockBase _lock)
        {
            return _key.KeyMatchById(_lock);
        }
        public virtual bool KeyMatch(LockBase _lock)
        {
            return KeyMatchById(_lock);
        }

        protected  bool KeyMatchById(LockBase _lock)
        {
            if (_lock.LockId == 0 || m_keyId == _lock.LockId)
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