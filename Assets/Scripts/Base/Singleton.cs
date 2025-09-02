/****************************************************
    功能：普通的单例模式
    作者：ZH
    创建日期：#2025/01/07#
    修改人：ZH
    修改日期：#2025/01/10#
    修改内容：
*****************************************************/

namespace Voltage
{
    public abstract class Singleton<T> 
        where T : new()
    {
        /// <summary>
        /// 保证我们的单例，是线程安全的
        /// </summary>
        private static object mutex = new object();

        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (mutex)
                    {
                        if (_instance == null)
                        {
                            _instance = new T();
                        }
                    }
                }

                return _instance;
            }
        }
    }
}