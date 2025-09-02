/****************************************************
    功能：使插头跟随目标物体
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
    public class Follower : MonoBehaviour
    {
        private Transform _target = null;
        //public  Transform Target => _target;
        private bool _allowFollow = false;
        private void Update()
        {
            if (!_allowFollow) return;
            if (_target == null) return;

            transform.position = _target.position;
            transform.rotation = _target.rotation;
        }
        public void Follow(Transform target)
        {
            _target = target;
            _allowFollow = true;
        }
        public void StopFollow()
        {
            _target = null;
            _allowFollow = false;
        }
    }
}