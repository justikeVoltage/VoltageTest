using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.Text;
using System;

public static class Extension
{
    #region 说明
    /*
        扩展脚本
        输出一律使用#eb6ea5这个色号以便区分
    */
    #endregion

    #region GameObject

    /// <summary>
    /// 获取身上的脚本，没有就添加
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="gameObject"></param>
    /// <returns></returns>
    public static T WH_GetOrAddComponent<T>(this GameObject gameObject) where T : Component
    {
        // ?? 空合并运算符 左值不为空返回左值，否则返回右值
        // return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();

        if (gameObject.GetComponent<T>() != null)
        {
            return gameObject.GetComponent<T>();
        }
        else
        {
            return gameObject.AddComponent<T>();
        }
    }

    /// <summary>
    /// 根据名字和挂在的组件查找子物体，同名的返回第一个
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="gameObject"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static T SearchComponent<T>(this GameObject gameObject, string name) where T : Component
    {
        List<T> ts = gameObject.GetComponentsInChildren<T>().ToList();
        if(ts!=null && ts.Count > 0)
        {
            for (int i = 0; i < ts.Count; i++)
            {
                if (ts[i].name.Equals(name))
                {
                    return ts[i];
                }
            }
        }
        else
        {
            Debug.Log($"<color=#eb6ea5>{gameObject.name}物体下没有找到对应名字{name}拥有{typeof(T)}的物体</color>");
        }

        return null;
    }

    /// <summary>
    /// 获取这个物体在Hierarchy中的路径
    /// </summary>
    /// <param name="gameObject"></param>
    /// <returns></returns>
    public static string GameObjectPath(this GameObject gameObject)
    {
        var path = "/" + gameObject.name;
        while (gameObject.transform.parent != null)
        {
            gameObject = gameObject.transform.parent.gameObject;
            path = "/" + gameObject.name + path;
        }

        Debug.Log($"<color=#eb6ea5>{gameObject.name}物体的路径：{path}</color>");

        return path;
    }

    /// <summary>
    /// EventTrigger，对应拖拽、点击、鼠标在物体上等回调，用起来比较方便，
    /// 注意：场景中要有EventSystem，如果是3D物体，则还需要给Camera加上Physics Raycaster组件
    /// </summary>
    public static void AddEventTrigger(this GameObject obj, EventTriggerType eventType, UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = eventType;
        entry.callback.AddListener(callback);
        EventTrigger trigger = obj.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = obj.AddComponent<EventTrigger>();
        }
        trigger.triggers.Add(entry);
    }

    #endregion

    #region Transform

    public static void SetRotationY(this Transform transform, float y)
    {
        transform.eulerAngles = new Vector3(transform.eulerAngles.x, y, transform.eulerAngles.z);
    }

    /// <summary>
    /// 设置Transform.Position的x坐标
    /// </summary>
    /// <param name="transform"></param>
    /// <param name="x"></param>
    public static void SetPositionX(this Transform transform,float x)
    {
        transform.position = new Vector3(x, transform.position.y, transform.position.z);
    }

    /// <summary>
    /// 设置Transform.Position的y坐标
    /// </summary>
    /// <param name="transform"></param>
    /// <param name="y"></param>
    public static void SetPositionY(this Transform transform, float y)
    {
        transform.position = new Vector3(transform.position.x, y, transform.position.z);
    }

    /// <summary>
    /// 设置Transform.Position的z坐标
    /// </summary>
    /// <param name="transform"></param>
    /// <param name="z"></param>
    public static void SetPositionZ(this Transform transform, float z)
    {
        transform.position = new Vector3(transform.position.x, transform.position.y, z);
    }

    /// <summary>
    /// 设置Transform.localPosition的x坐标
    /// </summary>
    /// <param name="transform"></param>
    /// <param name="x"></param>
    public static void SetLocalPositionX(this Transform transform, float x)
    {
        transform.localPosition = new Vector3(x, transform.position.y, transform.position.z);
    }

    /// <summary>
    /// 设置Transform.localPosition的y坐标
    /// </summary>
    /// <param name="transform"></param>
    /// <param name="y"></param>
    public static void SetLocalPositionY(this Transform transform, float y)
    {
        transform.localPosition = new Vector3(transform.position.x, y, transform.position.z);
    }

    /// <summary>
    /// 设置Transform.localPosition的z坐标
    /// </summary>
    /// <param name="transform"></param>
    /// <param name="z"></param>
    public static void SetLocalPositionZ(this Transform transform, float z)
    {
        transform.localPosition = new Vector3(transform.position.x, transform.position.y, z);
    }

    /// <summary>
    /// 将物体设置到目标物体的位置和方向
    /// </summary>
    /// <param name="transform"></param>
    /// <param name="target"></param>
    public static void SetPosAndRot(this Transform transform,Transform target)
    {
        transform.SetPositionAndRotation(target.position, target.rotation);
    }

    /// <summary>
    /// 根据名字查找子物体
    /// </summary>
    /// <param name="transform"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static Transform FindChildByName(this Transform transform,string name)
    {
        List<Transform> list = transform.GetComponentsInChildren<Transform>(true).ToList();
        if (list != null && list.Count > 0)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].name.Equals(name))
                {
                    return list[i];
                }
            }
        }
        else
        {
            Debug.Log($"<color=#eb6ea5>{transform.name}没有子物体</color>");
        }

        return null;
    }

    public static List<Transform> FindChildrenByName(this Transform transform, string name)
    {
        List<Transform> temp = new List<Transform>();
        List<Transform> list = transform.GetComponentsInChildren<Transform>(true).ToList();
        if (list != null && list.Count > 0)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].name.Contains(name))
                {
                    temp.Add(list[i]);
                }
            }

            return temp;
        }
        else
        {
            Debug.Log($"<color=#eb6ea5>{transform.name}没有子物体</color>");
        }

        return null;
    }

    public static Transform FindByPath(this Transform target, string path)
    {
        return target.Find(path);
    }

    public static List<T> FindChilds<T>(this Transform origin, bool includeInactive = true) where T : Component
    {
        List<T> list = new List<T>();
        origin.GetComponentsInChildren<T>(includeInactive, list);
        return list;
    }


    /// <summary>
    /// 清除子物体
    /// </summary>
    /// <param name="transform"></param>
    public static void ClearChild(this Transform transform)
    {
        int count = transform.childCount;
        if (count > 0)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject.Destroy(transform.GetChild(i).gameObject);
            }
        }
        else
        {
            Debug.Log($"<color=#eb6ea5>{transform.name}没有子物体</color>");
        }
    }

    public static void ActiveMeshAndChilds(this Transform target, bool enable)
    {
        List<MeshRenderer> list = new List<MeshRenderer>();
        target.GetComponentsInChildren<MeshRenderer>(true, list);
        for (int i = 0; i < list.Count; i++)
        {
            list[i].enabled = enable;
        }
    }

    #endregion

    #region string

    /// <summary>
    /// 添加首行缩进
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public static string TextIndent(this string info)
    {
        return "\u3000\u3000" + info;
    }

    /// <summary>
    /// 字体上色
    /// </summary>
    /// <param name="info"></param>
    /// <param name="colorNumber"></param>
    /// <returns></returns>
    public static string FontColoring(this string info, string colorNumber = "")
    {
        return $"<color={colorNumber}>{info}</color>";
    }

    /// <summary>
    /// 字符串组合，多用于类的ToString
    /// </summary>
    /// <param name="info"></param>
    /// <param name="textContent"></param>
    /// <returns></returns>
    public static string TextMerge(this string info,params string[] textContent)
    {
        if (textContent.Length > 0)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < textContent.Length; i++)
            {
                sb.AppendLine(textContent[i]);
            }

            info = sb.ToString();
        }
        else
        {
            Debug.Log($"<color=#eb6ea5>需要添加输出的字符串为空</color>");
        }

        return info;
    }

    public static string ListTextShow(this List<string> list, string separator = "")
    {
        if (list != null && list.Count > 0)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (!string.IsNullOrEmpty(separator))
                {
                    sb.AppendLine(list[i] + separator);
                }
                else
                {
                    sb.AppendLine(list[i]);
                }
            }
            Debug.Log($"<color=#eb6ea5>字符串列表最终生成的是{sb.ToString()}</color>");
            return sb.ToString();
        }
        else
        {
            Debug.Log($"<color=#eb6ea5>需要输出的字符串列表为空</color>");
        }

        return string.Empty;
    }

    /// <summary>
    /// 服务端日志
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public static string ServerLog(this string info)
    {
        return $"服务端：{info}";
    }

    /// <summary>
    /// 客户端日志
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public static string ClientLog(this string info)
    {
        return $"客户端：{info}";
    }

    #endregion

    #region Coroutine

    /// <summary>
    /// 延时回调
    /// </summary>
    /// <param name="behaviour"></param>
    /// <param name="time"></param>
    /// <param name="callBack"></param>
    /// <returns></returns>
    public static Coroutine Wait(this MonoBehaviour behaviour,float time,Action callBack)
    {
        return behaviour.StartCoroutine(Wait(time, callBack));
    }

    /// <summary>
    /// 循环调用Func<bool>这个回调，知道Func<bool>返回值为true时停止
    /// </summary>
    /// <param name="behaviour"></param>
    /// <param name="callBack"></param>
    /// <returns></returns>
    public static Coroutine Until(this MonoBehaviour behaviour,Func<bool> callBack)
    {
        return behaviour.StartCoroutine(Until(callBack));
    }

    public static Coroutine WaitAndUntil(this MonoBehaviour behaviour, bool isShow, float time, Action callBack)
    {
        return behaviour.StartCoroutine(WaitAndUntil(isShow, time, callBack));
    }

    private static IEnumerator Wait(float time,Action callBack)
    {
        yield return new WaitForSeconds(time);

        if (callBack != null)
            callBack.Invoke();
    }

    private static IEnumerator Until(Func<bool> callBack)
    {
        if(callBack!=null)
        {
            yield return new WaitUntil(callBack);
        }
    }

    private static IEnumerator WaitAndUntil(bool isShow, float time,Action callBack)
    {
        while (isShow)
        {
            yield return new WaitForSeconds(time);

            if (callBack != null)
                callBack.Invoke();
        }
    }

    #endregion


}
