/****************************************************
    功能：远程抓取输入控制
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
using Autohand.Demo;
using Autohand;

namespace Voltage{
    public class OpenXRHandPointGrabLink_Voltage : MonoBehaviour{
        public HandDistanceGrabber pointGrab;
        public InputActionProperty pointAction;
        public InputActionProperty stopPointAction;
        public InputActionProperty selectAction;
        public InputActionProperty stopSelectAction;

        void OnEnable(){
            if(pointAction.action != null) pointAction.action.Enable();
            if (pointAction.action != null) pointAction.action.performed += OnPoint;
            if (stopPointAction.action != null) stopPointAction.action.Enable();
            if (stopPointAction.action != null) stopPointAction.action.performed += OnStopPoint;

            if (selectAction.action != null) selectAction.action.Enable();
            if (selectAction.action != null) selectAction.action.performed += OnSelect;
            if (stopSelectAction.action != null) stopSelectAction.action.Enable();
            if (stopSelectAction.action != null) stopSelectAction.action.performed += OnDeselect;
        }
        
        private void OnDisable() {
            if (pointAction.action != null) pointAction.action.performed -= OnPoint;
            if (stopPointAction.action != null) stopPointAction.action.performed -= OnStopPoint;

            if (selectAction.action != null) selectAction.action.performed -= OnSelect;
            if (stopSelectAction.action != null) stopSelectAction.action.performed -= OnDeselect;
            
        }

        void OnPoint(InputAction.CallbackContext e) {
            pointGrab.StartPointing();
        }

        void OnStopPoint(InputAction.CallbackContext e) {
            pointGrab.StopPointing();
        }

        private void OnSelect(InputAction.CallbackContext e) {
            pointGrab.SelectTarget();
        }

        void OnDeselect(InputAction.CallbackContext e) {
            pointGrab.CancelSelect();
        }
        
        public void InitWithStartPointing() 
        {
            pointGrab.StartPointing();
        }

    }
}
