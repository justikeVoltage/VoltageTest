/****************************************************
    功能：按照顺序自动连接匹配的Socket和Plug
    作者：ZZQ
    创建日期：#2025/03/05#
    修改内容：1.线性运动改为非线性运动、增加非线性旋转时间  #2025/03/10#  ZZQ
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using NaughtyAttributes;
using UnityEngine;
using Voltage;
using UnityEngine.Events;

public class AutoConnection : MonoBehaviour
{
    [Header("Socket Attribute")]
    public bool fixedSocket = false;

    [Header("Motion Attribute"), Space(6)]
    public bool EnableTurn = true;
    public float StartDelayTime = 0.2f;//开始延迟时间
    public float MaxRotateDuration = 1f;//旋转时间
    public float ConnectDuration = 1f;//运动时间
    public float intervalTime = 0f;//间隔时间
    public AnimationCurve curve = new AnimationCurve(new Keyframe(0, 0, 1, 1), new Keyframe(0.5f, 0.7f, 2f, 1.8f), new Keyframe(1, 1, 0, 0));

    [Header("On Connect Complete"), Space(6)]
    public bool HideObject = true;
    public UnityEvent OnConnectComplete;
    private List<SocketBase> sockets;
    private List<PlugBase> plugs;
    private Dictionary<SocketBase, PlugBase> matchs;
    private float rotationDuration;
    void Start()
    {
        TeamUp();
    }

    [Button("Auto Connect")]
    public void AutoConnect()
    {
        StartCoroutine(Connect());
    }
    void TeamUp()
    {
        sockets = new List<SocketBase>();
        plugs = new List<PlugBase>();
        foreach (SocketBase socket in GetComponentsInChildren<SocketBase>())
        {
            if (socket.IsConnected) continue;
            socket.GetComponent<Rigidbody>().isKinematic = true;
            sockets.Add(socket);
        }
        Debug.Log("sockets count:" + sockets.Count);
        foreach (PlugBase plug in GetComponentsInChildren<PlugBase>())
        {
            if (plug.IsConnected) continue;
            plug.GetComponent<Rigidbody>().isKinematic = true;
            plugs.Add(plug);
        }

        matchs = new Dictionary<SocketBase, PlugBase>();
        for (int i = 0; i < sockets.Count; i++)
        {
            for (int j = 0; j < plugs.Count; j++)
            {
                if (sockets[i].MatchDetection(plugs[j]))
                {
                    matchs.Add(sockets[i], plugs[j]);
                    plugs.Remove(plugs[j]);
                    break;
                }
            }
        }
    }
    /// <summary>
    /// 连续_单个连接计算
    /// </summary>
    /// <returns></returns>
    IEnumerator Connect()
    {
        yield return new WaitForSeconds(StartDelayTime);

        foreach (var socket in matchs.Keys)
        {
            float elapsedTime;
            if (EnableTurn)//旋转计算
            {
                Quaternion socketStartRotation = socket.transform.rotation;
                Quaternion plugStartRotation = matchs[socket].transform.rotation;
                Vector3 targetPosition, targetPositionInFixedSocket;//目标点
                Vector3 socketDirection, plugDirection, plugDirectionInFixedSocket;//指向
                Quaternion socketRotation01, plugRotation01, plugRotationInFixedSocket01;//1.水平面对准
                Quaternion socketRotation02, plugRotation02, plugRotationInFixedSocket02;//2.最终对准

                #region 水平旋转计算
                //计算水平旋转
                targetPosition = (socket.transform.position + matchs[socket].transform.position) / 2;
                targetPositionInFixedSocket = socket.m_targetTransform.position;

                socketDirection = (targetPosition - socket.transform.position).normalized;
                plugDirection = (targetPosition - matchs[socket].transform.position).normalized;
                plugDirectionInFixedSocket = (targetPositionInFixedSocket - matchs[socket].transform.position).normalized;

                socketRotation01 = Quaternion.LookRotation(socketDirection);
                plugRotation01 = Quaternion.LookRotation(plugDirection);
                plugRotationInFixedSocket01 = Quaternion.LookRotation(plugDirectionInFixedSocket);
                socketRotation01 = Quaternion.Euler(0, socketRotation01.eulerAngles.y - 90, 0);
                plugRotation01 = Quaternion.Euler(0, plugRotation01.eulerAngles.y + 90, 0);
                plugRotationInFixedSocket01 = Quaternion.Euler(0, plugRotationInFixedSocket01.eulerAngles.y + 90, 0);

                float angle1 = Quaternion.Angle(socket.transform.rotation, socketRotation01);
                float angle2 = Quaternion.Angle(matchs[socket].transform.rotation, plugRotation01);
                float angle = angle1 >= angle2 ? angle1 : angle2;
                rotationDuration = MaxRotateDuration * (angle / 180);

                elapsedTime = 0f;
                while (elapsedTime < rotationDuration)
                {
                    elapsedTime += Time.deltaTime;
                    float t = elapsedTime / rotationDuration;
                    float bezierT = curve.Evaluate(t);

                    if (!fixedSocket && (socket is Socket_Grabbable))
                    {
                        socket.transform.rotation = Quaternion.Slerp(socketStartRotation, socketRotation01, bezierT);//x轴朝外
                        matchs[socket].transform.rotation = Quaternion.Slerp(plugStartRotation, plugRotation01, bezierT);//x轴朝内
                    }
                    else
                    {
                        matchs[socket].transform.rotation = Quaternion.Slerp(plugStartRotation, plugRotationInFixedSocket01, bezierT);//x轴朝内
                    }
                    yield return null;
                }
                #endregion

                #region 最终旋转计算
                //水平旋转基础上计算最终旋转
                socketStartRotation = socket.transform.rotation;
                plugStartRotation = matchs[socket].transform.rotation;
                socketRotation02 = Quaternion.LookRotation(socketDirection, socket.transform.forward);
                plugRotation02 = Quaternion.LookRotation(plugDirection, matchs[socket].transform.forward);
                plugRotationInFixedSocket02 = Quaternion.LookRotation(plugDirectionInFixedSocket, matchs[socket].transform.forward);
                socketRotation02 = Quaternion.Euler(socket.transform.rotation.eulerAngles + new Vector3(0, 0, -socketRotation02.eulerAngles.x)); ;
                plugRotation02 = Quaternion.Euler(matchs[socket].transform.rotation.eulerAngles + new Vector3(0, 0, plugRotation02.eulerAngles.x));
                plugRotationInFixedSocket02 = Quaternion.Euler(matchs[socket].transform.rotation.eulerAngles + new Vector3(0, 0, plugRotationInFixedSocket02.eulerAngles.x));
                float angle3 = Quaternion.Angle(socket.transform.rotation, socketRotation02);
                float angle4 = Quaternion.Angle(matchs[socket].transform.rotation, plugRotation02);
                angle = angle3 >= angle4 ? angle3 : angle4;
                rotationDuration = MaxRotateDuration * (angle / 180);

                elapsedTime = 0f;
                while (elapsedTime < rotationDuration)
                {
                    elapsedTime += Time.deltaTime;
                    float t = elapsedTime / rotationDuration;
                    float bezierT = curve.Evaluate(t);

                    if (!fixedSocket && (socket is Socket_Grabbable))
                    {
                        socket.transform.rotation = Quaternion.Slerp(socketStartRotation, socketRotation02, bezierT);//x轴朝外
                        matchs[socket].transform.rotation = Quaternion.Slerp(plugStartRotation, plugRotation02, bezierT);//x轴朝内
                    }
                    else
                    {
                        matchs[socket].transform.rotation = Quaternion.Slerp(plugStartRotation, plugRotationInFixedSocket02, bezierT);//x轴朝内
                    }
                    yield return null;
                }
                #endregion
            }

            #region 移动计算
            //移动
            Vector3 socketStartPosition = socket.transform.position;//socket startPoint
            Vector3 plugStartPosition = matchs[socket].transform.position;//plug startPoint
            Vector3 socketTargetPosition;
            Vector3 plugTargetPosition;

            //计算Socket的移动结束点
            Vector3 midPoint = (socket.m_targetTransform.position + matchs[socket].transform.position) / 2;//更新midPoint
            Transform midTransform = new GameObject().transform;
            midTransform.position = midPoint;
            midTransform.rotation = socket.transform.rotation;

            Vector3 socketRelativePosition = socket.m_targetTransform.InverseTransformPoint(socket.transform.position);
            socketRelativePosition = Vector3.Scale(socketRelativePosition, socket.transform.localScale);
            socketTargetPosition = midTransform.TransformPoint(socketRelativePosition);//socket endPoint
            Destroy(midTransform.gameObject);

            //开始移动
            elapsedTime = 0f;
            while (elapsedTime < ConnectDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / ConnectDuration;
                float bezierT = curve.Evaluate(t);

                if (!fixedSocket && (socket is Socket_Grabbable))
                {
                    plugTargetPosition = midPoint;//plug endPoint
                    socket.transform.position = Vector3.Lerp(socketStartPosition, socketTargetPosition, bezierT);
                    matchs[socket].transform.position = Vector3.Lerp(plugStartPosition, plugTargetPosition, bezierT);
                }
                else
                {
                    plugTargetPosition = socket.m_targetTransform.position;//plug endPoint
                    matchs[socket].transform.position = Vector3.Lerp(plugStartPosition, plugTargetPosition, bezierT);
                }
                yield return null;
            }
            #endregion

            #region 一对连接结束
            if (HideObject)
            {
                socket.gameObject.SetActive(false);
                matchs[socket].gameObject.SetActive(false);
            }
            Debug.Log($"\"{matchs[socket].name}\" 连接", matchs[socket]);
            Debug.Log($"连接 \"{socket.name}\" 成功", socket);
            #endregion
            yield return new WaitForSeconds(intervalTime);
        }
        OnConnectComplete?.Invoke();
    }
}
    