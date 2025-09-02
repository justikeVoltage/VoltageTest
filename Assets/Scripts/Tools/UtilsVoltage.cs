/****************************************************
    功能：项目工具函数
    作者：ZH
    创建日期：#2025/01/08#
    修改内容：
        1.增加Catmull-Rom曲线平滑方法    2025/03/14 ZH
*****************************************************/

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Voltage
{
    public class UtilsVoltage
    {
        /// <summary>
        /// 打印颜色日志
        /// </summary>
        /// <param name="color"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static string DebugLog(Color color, string log)
        {
            string hexColor = ColorUtility.ToHtmlStringRGBA(color);
            string str = $"<color=#{hexColor}>{log}</color>";
            Debug.Log(str);
            return str;
        }
        /// <summary>
        /// 打印颜色日志
        /// </summary>
        /// <param name="color">打印颜色</param>
        /// <param name="log">打印信息</param>
        /// <param name="component">编辑器中标记组件</param>
        /// <returns></returns>
        public static string DebugLog(Color color, string log, Component component)
        {
            string hexColor = ColorUtility.ToHtmlStringRGBA(color);
            string str = $"<color=#{hexColor}>{log}</color>";
            Debug.Log(str, component);
            return str;
        }

        /// <summary>
        /// 递归查找子物体
        /// </summary>
        public static Transform FindChildInTransform(Transform parent, string childName)
        {
            Transform childTrans = parent.Find(childName);

            if (childTrans != null)
            {
                return childTrans;
            }
            for (int i = 0; i < parent.childCount; i++)
            {
                childTrans = FindChildInTransform(parent.GetChild(i), childName);
                if (childTrans != null)
                {
                    return childTrans;
                }
            }
            return null;
        }

        public static void Unload_Collect()
        {
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// 计算路径长度
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static float CalculatePathLength(List<Vector3> path)
        {
            float length = 0;
            for (int i = 1; i < path.Count; i++)
                length += Vector3.Distance(path[i], path[i - 1]);
            return length;
        }

        /// <summary>
        /// 生成Catmull-Rom控制点
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static Vector3[] GenerateCatmullRomControlPoints(Vector3[] path)
        {
            if (path == null || path.Length < 2)
            {
                Debug.LogError("路径数据无效，无法生成曲线控制点！");
                return null;
            }

            Vector3[] points = new Vector3[path.Length + 2];
            Array.Copy(path, 0, points, 1, path.Length);

            // 首尾控制点计算
            points[0] = points[1] + (points[1] - points[2]);
            points[points.Length - 1] = points[points.Length - 2] + (points[points.Length - 2] - points[points.Length - 3]);

            // 首位点重合时，形成闭合的Catmull-Rom曲线
            if (points[1] == points[points.Length - 2])
            {
                points[0] = points[points.Length - 3];
                points[points.Length - 1] = points[2];
            }

            return points;
        }

        /// <summary>
        /// Catmull-Rom插值计算
        /// </summary>
        /// <param name="pts"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public static Vector3 InterpolateCatmullRom(Vector3[] points, float t)
        {
            int numSections = points.Length - 3;
            int currIdx = Mathf.Min(Mathf.FloorToInt(t * numSections), numSections - 1);
            float u = t * numSections - currIdx;

            Vector3 a = points[currIdx];
            Vector3 b = points[currIdx + 1];
            Vector3 c = points[currIdx + 2];
            Vector3 d = points[currIdx + 3];

            return 0.5f * (
                (-a + 3f * b - 3f * c + d) * (u * u * u) +
                (2f * a - 5f * b + 4f * c - d) * (u * u) +
                (-a + c) * u +
                2f * b
            );
        }

        /// <summary>
        /// 获取本地IP地址
        /// </summary>
        /// <returns></returns>
        public static string GetLocalIPAddress()
        {
            string ip = string.Empty;
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var address in host.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    ip = address.ToString();
                    break;
                }
            }
            return ip;
        }
        
        /// <summary>
        /// 将路径转换为Unity路径风格
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string ToUnityPathStyle(string path) => path.Replace('\\', '/');
        
        /// <summary>
        /// 将路径转换为Windows路径风格
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string ToWindowsPathStyle(string path) => path.Replace('/', '\\');
    }
}