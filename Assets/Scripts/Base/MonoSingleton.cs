/****************************************************
    功能：继承Mono的单例
    作者：ZH
    创建日期：#2025/01/07#
    修改内容：
*****************************************************/

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Voltage
{
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _isQuitting;

        public static T Instance
        {
            get
            {
                if (_isQuitting) return null;

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // 优先查找场景中现有实例
                        _instance = FindObjectOfType<T>();

                        if (_instance == null)
                        {
                            // 创建专用容器对象
                            var singletonObject = new GameObject($"{typeof(T).Name}_Singleton");
                            _instance = singletonObject.AddComponent<T>();
                            DontDestroyOnLoad(singletonObject);
                            // singletonObject.hideFlags = HideFlags.HideInHierarchy;
                        }
                    }
                    return _instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError($"尝试创建多个实例的单例类 {typeof(T).Name}");
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            DontDestroyOnLoad(gameObject);
            // gameObject.hideFlags = HideFlags.HideInHierarchy;
        }

        protected virtual void OnApplicationQuit() => _isQuitting = true;
    }
}