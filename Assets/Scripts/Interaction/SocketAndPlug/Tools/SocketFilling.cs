/****************************************************
    功能：插座自动填充工具
    作者：ZZQ
    创建日期：#2025/03/28#
    修改内容：#2025/03/28#
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using Voltage;

public class SocketFilling : MonoBehaviour
{
    public Transform SocketParent;
    public Transform PlugParent;
    [ReadOnly] public List<SocketBase> sockets = new List<SocketBase>();
    [ReadOnly] public List<PlugBase> plugs = new List<PlugBase>();
    [Button("Fill Socket")]
    public void FillSocket()
    {
        sockets = new List<SocketBase>(SocketParent.childCount);
        plugs = new List<PlugBase>(PlugParent.childCount);

        foreach (Transform child in PlugParent)
        {
            plugs.Add(child.GetComponent<PlugBase>());
        }
        foreach (Transform child in SocketParent)
        {
            sockets.Add(child.GetComponent<SocketBase>());
        }
        Debug.Log("Plugs count: " + plugs.Count);
        Debug.Log("Sockets count: " + sockets.Count);
        for(int i = 0; i < sockets.Count; i++)
        {
            for(int j = 0; j < plugs.Count; j++)
            {
                if (sockets[i]!= null && plugs[j]!= null&&sockets[i].name == plugs[j].name)
                {
                    sockets[i].m_lock.VerifyComponent = plugs[j];
                }
            }
        }
    }
    [Button("Clear Socket")]
    public void ClearSocket()
    {
        foreach (SocketBase socket in sockets)
        {
            socket.m_lock.VerifyComponent = null;
        }
        sockets = new List<SocketBase>();
        plugs = new List<PlugBase>();
        
    }
}
