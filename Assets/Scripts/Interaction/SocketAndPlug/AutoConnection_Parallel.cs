/****************************************************
    功能：自动连接脚本_多个并行
    作者：ZZQ
    创建日期：#2025/03/17#
    修改内容：1.代码优化 ZZQ #2025/04/01#
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using Voltage;
using UnityEngine.Events;

public class AutoConnection_Parallel : MonoBehaviour
{
    [Header("Setting")]
    public Transform socketparent;
    public Transform plugparent;
    public bool fixedSocket = true;
    public bool EnableTurn = false;
    public int parallelNum = 1;//并行连接数

    [Header("Motion Attribute"), Space(6)]
    public float StartDelayTime = 0.01f;//开始延迟时间
    public float MaxRotateDuration = 0.8f;//最大旋转时间
    public float MaxConnectDuration = 2f;//最大运动时间
    public float intervalTime = 0f;//间隔时间
    public AnimationCurve curve = new AnimationCurve(new Keyframe(0, 0, 3, 3), new Keyframe(0.5f, 0.7f, 2f, 1.8f), new Keyframe(1, 1, 0, 0));

    [Header("On Connect Complete"), Space(6)]
    public bool HideObject = false;
    public UnityEvent OnConnectComplete;
    // 对象引用获取、连接参数计算
    private List<SocketBase> sockets;
    private List<PlugBase> plugs;
    private Dictionary<SocketBase, PlugBase> matchs;
    private float rotationDuration;
    private float connectDuration;
    private bool _allowConnect = false;
    // 协程开启判断
    private int index = 0;
    private Coroutine[] connects = null;
    private bool Connecting = false;

    void Start()
    {
        if (socketparent == null) socketparent = transform;
        if (plugparent == null) plugparent = transform;
    }

    [Button("Auto Connect")]
    public void AutoConnect()
    {
        _allowConnect = true;
        connects = new Coroutine[parallelNum];
        TeamUp();
    }
    void TeamUp()
    {
        sockets = new List<SocketBase>();
        plugs = new List<PlugBase>();
        foreach (SocketBase socket in socketparent.GetComponentsInChildren<SocketBase>(false))
        {
            if (socket.IsConnected) continue;
            sockets.Add(socket);
        }
        foreach (PlugBase plug in plugparent.GetComponentsInChildren<PlugBase>(false))
        {
            if (plug.IsConnected) continue;
            plugs.Add(plug);
        }

        matchs = new Dictionary<SocketBase, PlugBase>();
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i].m_lock.VerifyComponent != null)
            {
                matchs.Add(sockets[i], sockets[i].m_lock.VerifyComponent);
            }
            else
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
    }
    void Update()
    {
        if (!_allowConnect) return;
        foreach (Coroutine connect in connects)
        {
            if (connect == null) Connecting = false;
        }

        if (Connecting) return;
        for (int i = 0; i < connects.Length; i++)
        {
            if (connects[i] != null) continue;

            connects[i] = StartCoroutine(ConnectOnlyMove(sockets[index++], i));

            if (index >= sockets.Count)
            {
                _allowConnect = false;
                OnConnectComplete?.Invoke();
                break;
            }
        }
    }
    /// <summary>
    /// 对某个Socket进行移动计算，配合update实现多个连接并行计算
    /// </summary>
    /// <param name="socket"></param>
    /// <returns></returns>
    IEnumerator ConnectOnlyMove(SocketBase socket, int coroutinesIndex)
    {
        yield return null;
        //处理物理状态
        if (socket.GetComponent<ObiControl_Grabbable>() != null)
        {
            socket.GetComponent<Rigidbody>().isKinematic = true;
            socket.GetComponent<ObiControl_Grabbable>().ChangeAllObiAttachmentType(AttachmentType.Static);
        }
        if (matchs[socket].GetComponent<ObiControl_Grabbable>() != null)
        {
            matchs[socket].GetComponent<Rigidbody>().isKinematic = true;
            matchs[socket].GetComponent<ObiControl_Grabbable>().ChangeAllObiAttachmentType(AttachmentType.Static);
        }
        yield return new WaitForSeconds(StartDelayTime);//开始延迟时间

        float elapsedTime;
        if (EnableTurn)//旋转计算
        {
            #region 旋转计算
            Quaternion socketStartRotation = socket.transform.rotation;
            Quaternion plugStartRotation = matchs[socket].transform.rotation;
            Vector3 targetPosition, targetPositionInFixedSocket;//目标点
            Vector3 socketDirection, plugDirection, plugDirectionInFixedSocket;//指向
            Quaternion socketRotation01, plugRotation01, plugRotationInFixedSocket01;//1.水平面对准
            Quaternion socketRotation02, plugRotation02, plugRotationInFixedSocket02;//2.最终对准

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

        float distance = Vector3.Distance(socketStartPosition, socketTargetPosition) / 0.8f;//0.8米为最大时间对应的节点距离，更远的距离按1.5米计算
        float multiple = distance > 1 ? 1 : distance < 0.2f ? 0.2f : distance;//0.2为最小计算时间系数
        connectDuration = MaxConnectDuration * multiple;

        //开始移动
        elapsedTime = 0f;
        while (elapsedTime < connectDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / connectDuration;
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

        #region 某对连接结束
        if (HideObject)
        {
            socket.gameObject.SetActive(false);
            matchs[socket].gameObject.SetActive(false);
        }
        #endregion
        yield return new WaitForSeconds(intervalTime);//间隔时间

        connects[coroutinesIndex] = null;
    }
}
