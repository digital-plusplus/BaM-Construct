using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Code that detects client disconnect events and updates the server repository when that happens
/// </summary>
public class ConnectionNotificationManager : MonoBehaviour
{
    ServerRepository serverRepo;

    // Start is called before the first frame update
    void Start()
    {
        serverRepo = GameObject.Find("ServerRepository").GetComponent<ServerRepository>();
        if (!serverRepo)
        {
            Debug.LogError("ConnectworkNotificationManager FATAL, cannot find ServerRepository");
            return;
        }
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallBack;
    }


    private void OnClientDisconnectCallBack (ulong clientID)
    {
        if (serverRepo.IsServer())
        {
            Debug.Log("Client " + clientID + " was disconnected");
            serverRepo.SetDisconnected(clientID);
        }
    }
    
}
