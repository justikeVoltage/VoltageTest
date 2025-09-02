/****************************************************
    功能：触发器事件
    作者：ZH
    创建日期：#2025/03/03#
    修改内容：
        1.通用触发事件类拆分    2025/03/03 ZH
        2.增加退出触发事件    2025/03/20 ZH
*****************************************************/

using UnityEngine;
using UnityEngine.Events;

namespace Voltage
{
    public class TriggerEvent : MonoBehaviour
    {
        public UnityEvent enterTriggerEvent = new UnityEvent();
        public UnityEvent exitTriggerEvent = new UnityEvent();

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("HandPlayer"))
            {
                enterTriggerEvent?.Invoke();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("HandPlayer"))
            {
                exitTriggerEvent?.Invoke();
            }
        }
    }
}