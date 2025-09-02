/****************************************************
    功能：插座的钥匙，继承自KeyBase
    作者：ZZQ
    创建日期：#2025/02/20#
    修改人：ZZQ
    修改日期：#2025/02/20#
    修改内容：
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Voltage
{
    [System.Serializable]
    public class KeyWithPlug : KeyBase
    {
        public static bool Match(KeyBase _key, LockWithSocket _lock)
        {
            return _key.KeyMatch(_lock);
        }
        public bool KeyMatch(LockWithSocket _lock)
        {
            if (_lock.MatchMode == MatchMode.LockId)
            {
                return KeyMatchById(_lock);
            }
            else
            {
                return KeyMatchByObject(_lock);
            }
        }
        private bool KeyMatchByObject(LockWithSocket _lock)
        {
            if (_lock.VerifyComponent == null || _lock.VerifyComponent.Key == this)
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