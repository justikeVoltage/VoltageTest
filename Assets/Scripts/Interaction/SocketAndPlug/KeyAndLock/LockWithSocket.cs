/****************************************************
    功能：插座的锁，继承自LockBase
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
using Voltage.AttributeTools;


namespace Voltage
{
    [System.Serializable]
    public class LockWithSocket : LockBase
    {
        [SerializeField, ShowIf("m_matchMode", MatchMode.Component)]
        private  PlugBase m_verifyComponent;
        public PlugBase VerifyComponent { get { return m_verifyComponent; }set { m_verifyComponent = value; } }
        public override bool LockMatch(KeyBase _key)
        {
            if (MatchMode == MatchMode.Component)
            {
                return LockMatchByObject(_key);
            }
            else
            {
                return LockMatchById(_key);
            }

        }
        public virtual bool LockMatchByObject(KeyBase _key)
        {
            if (m_verifyComponent == null) 
            {
                m_matchState = false;
                return false;
            }
            if (m_verifyComponent.Key == _key)
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
