/****************************************************
    功能：UI始终看向摄像机
    作者：ZH
    创建日期：#2025/03/03#
    修改人：ZH
    修改日期：#2025/03/07#
    修改内容：
        1.功能拆分    2025/03/05 ZH
        2.增加Y轴旋转控制    2025/03/07 ZH
*****************************************************/

using UnityEngine;

namespace Voltage
{
    public class LookCamera : MonoBehaviour
    {
        private Camera targetCamera;

        public bool isApplyY = false;

        private void Start()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    Debug.LogError("No main camera found!");
                    return;
                }
            }
        }

        private void Update()
        {
            Vector3 direction = (targetCamera.transform.position - transform.position).normalized;
            if (!isApplyY) direction = new Vector3(direction.x, 0, direction.z).normalized;
            if (direction != Vector3.zero) transform.forward = direction;
        }
    }
}