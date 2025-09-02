/****************************************************
    功能：根物体
    作者：ZH
    创建日期：#2025/06/03#
    修改内容：
*****************************************************/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Voltage
{
    [DisallowMultipleComponent]
    public class Root : MonoSingleton<Root>
    {

        [HideInInspector]
        public GameObject VRObj;

        /// <summary>
        /// 服务与系统集合
        /// </summary>       
        [HideInInspector]

        /// <summary>
        /// 是否是多人游戏
        /// </summary>
        public bool isMulti;

        #region 单机

        /// <summary>
        /// 加载物体管理类
        /// </summary>
        
        /// <summary>
        /// 场景切换管理类
        /// </summary>

        /// <summary>
        /// 单机全局方法类
        /// </summary>

        /// <summary>
        /// 单机数据管理类
        /// </summary>
        

        #endregion

        #region 网络

        /// <summary>
        /// 加载网络物体管理类
        /// </summary>

        /// <summary>
        /// 网络全局方法类
        /// </summary>

        #endregion

        protected override void Awake()
        {
            base.Awake();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Init()
        {


        }

        /// <summary>
        /// 初始化多人相关
        /// </summary>
        public void InitMulti()
        {

        }

        private void Update()
        {
         
        }

        private void OnDestroy()
        {
           
        }

        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();

            UtilsVoltage.Unload_Collect();
        }

        /// <summary>
        /// 添加服务与系统列表
        /// </summary>
        /// <param name="base_SS">服务系统类</param>
     
    }
}