using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// This implements the server repository object 
/// It should ONLY run on the server and stores network status info for clients
/// Called by: OnNetworkSpawn of the initial network prefab object for the server only
/// </summary>
public class ServerRepository : MonoBehaviour
{
    Dictionary<string, uint>        playerDict              = new Dictionary<string, uint>();       //stores player prefab string with unique hash
    Dictionary<string, GameObject>  GODict                  = new Dictionary<string, GameObject>(); //stores prefab gameobject, needed for stage2
    Dictionary<ulong, bool>         playerDisconnectedDict  = new Dictionary<ulong, bool>();        //stores whether a player was marked as disconnected 
    Dictionary<string, bool>        newPlayerLoadedResource = new Dictionary<string, bool>();       //Used by new joiner to administer what prefabs have been loaded

    ulong clientID;                                                                                 //Used in stage 2, this was the clientID when I signed in the first time
    string PIN="";                                                                                  //Used to detect whether you completed phase 1 
    string PREFAB = "";                                                                             //Used to store the GENDER+BREED string

    bool blockedForNewJoins = false;                                                                //Used to ensure the server only handles one join process at once 
    [SerializeField] const uint maxPlayers = 40;
    bool[] playersLoadedResource = new bool[maxPlayers];                                            //Used by server Coroutines to wait for readiness of all clients
    bool isServer;                                                                                  //Used by ConnectionManager to check whether this runs on the server
    [SerializeField] bool protocolDebug = false;

    //==========
    //Methods for Connection Manager
    //==========
    public void SetServer(bool value)
    {
        isServer = value;
    }

    public bool IsServer()
    {
        return isServer;
    }


    //==========
    //Methods for block/unblocking of new joins
    //==========

    public void SetBlockedForNewJoins(bool value)
    {
        blockedForNewJoins = value;
    }


    public bool IsBlockedForNewJoins()
    {
        return blockedForNewJoins;
    }


    //==========
    //Methods for playerDict, GODict and GHashDict
    //==========

    //Set initialized entry
    public void DictUpdate(string prefabKey, GameObject goValue, uint hashValue)
    {
        int i = playerDict.Count;

        if (!playerDict.ContainsKey(prefabKey)) InitPrefab(prefabKey);

        playerDict[prefabKey]               = hashValue;
        GODict[prefabKey]                   = goValue;
        newPlayerLoadedResource[prefabKey]  = false;
        
    }


    //Initialize with dummy values
    public void InitPrefab(string prefabKey)
    {
        if (!playerDict.ContainsKey(prefabKey)) {
            playerDict.Add(prefabKey, 0);
            GODict.Add(prefabKey, null);
            newPlayerLoadedResource.Add(prefabKey, false);
        }
        else Debug.LogWarning("InitPrefab(): Key " + prefabKey + "already exists => there will be duplicate prefabs in the scene");
    }


    public void ResetNewPlayerLoadedResource()
    {
        foreach (var i in playerDict)
        {
            if (!newPlayerLoadedResource.ContainsKey(i.Key))
                newPlayerLoadedResource.Add(i.Key, false);
            else
                newPlayerLoadedResource[i.Key] = false;
        }
    }   


    //Get the value from the GlobalObjectIdHash
    public uint GetGOIdHash(GameObject value)
    {
        if (GODict.ContainsValue(value))
        {
            foreach (var i in GODict)
            {
                if (i.Value == value)               //As soon as we find it we get the hash(key) and return
                {
                    return (playerDict[i.Key]);     //get the hashcode from the key that also holds the GameObject 
                }
            }
            return 0;
        }
        else
        {
            Debug.LogError("SERVER REPO: Can't find hash for " + value.name);            
            return 0;
        }
    }


    void DumpConnectedState(string prefix)
    {
        string tmp = prefix + " PLAYERDISCONNECTEDDICT:";
        foreach (var i in playerDisconnectedDict)
            tmp += "(" + i.Key + "," + i.Value + ")";

        Debug.Log(tmp);     
    }

    //Set status to disconnected - need that to ensure server isn't waiting for clients that are no longer there to load prefabs
    public void SetDisconnected(ulong key)
    {
        if (playerDisconnectedDict.ContainsKey(key))
            playerDisconnectedDict[key] = true;
        else Debug.LogError("ServerRepository error: can't mark client " + key + " as disconnected as it does not exist");
        
        if (protocolDebug)
            DumpConnectedState("SetDisconnected");
    }


    //Set connected state and if not exist create dictionary entry
    public void AddSetConnected(ulong key)
    {
        if (!playerDisconnectedDict.ContainsKey(key))       //client already is registered
            playerDisconnectedDict[key] = false;
        else playerDisconnectedDict.Add(key, false);        //new client, register and set connected state 

        if (protocolDebug)
            Debug.Log("Client " + key + " now marked as connected");

        if (protocolDebug)
            DumpConnectedState("AddSetConnected");
    }


    //simple function to return the content of the dictionary in a string
    public void Dump(string prefix)
    {
        string outP = prefix + " PLAYERDICT={";
        foreach (var i in playerDict)
            outP += "(" + i.Key + "," + i.Value + "),";
        outP += "}";

        Debug.Log(outP);
        Debug.Log(prefix + " PIN=" + GetPIN());
        Debug.Log(prefix + " PREFAB=" + getPREFAB());
        Debug.Log(prefix + " PREFABLIST=" + ReturnOnlinePrefabs());
        Debug.Log(prefix + " HASHLIST=" + ReturnOnlineHashes());
        DumpConnectedState(prefix);
    }


    public uint GetHash(string key)
    {
        if (playerDict.ContainsKey(key))
            return playerDict[key];
        else return 0;
    }


    //Get the GameObject from the dictionary given the prefabname
    public GameObject ReturnGameObject(string key)
    {
        if (GODict.ContainsKey(key)) return GODict[key];
        else return null;
    }


    //returns an array of prefabs separated by comma's for all prefabs in the dictionary
    public string ReturnOnlinePrefabs()
    {
        string outP = "";
        foreach (var i in playerDict) 
        {   
            outP += i.Key + ",";
        }

        //there must be an easier way to cut off the last empty , field... ?
        if (outP.Length>0)                                      //its not empty (in which case we get an exception for [Length-1]
            if (outP[outP.Length-1]==',')                       //terminates with a comma!
                    outP = outP.Substring(0, outP.Length - 1);  //cut off the last , 

        return outP;
    }


    //returns an array of PrefabHashes separated by comma's for all clients with ID < from
    //->prefer to send in CSV rather than multiple C/S Rpc calls causing chatty network traffic
    public string ReturnOnlineHashes()
    {
        string outP = "";
        foreach (var i in playerDict)
        { 
            outP += i.Value.ToString() + ",";
        }

        //there must be an easier way to cut off the last empty , field... ?
        if (outP.Length > 0)                                        //its not empty (in which case we get an exception for [Length-1]
            if (outP[outP.Length - 1] == ',')                       //terminates with a comma!
                outP = outP.Substring(0, outP.Length - 1);          //cut off the last 

        return outP;
    }


    //=====
    //Methods for PIN & PREFAB
    //=====
    public string GetPIN()
    {
        return PIN;
    }


    public void SetPIN(string pin)
    {
        PIN = pin;
    }


    public string getPREFAB()
    {
        return PREFAB;
    }


    public void SetPREFAB(string prefab)
    {
        PREFAB = prefab;
    }


    public void setClientID(ulong cID)
    {
        clientID = cID; 
    }


    /*public ulong GetClientID()
    {
        return clientID;
    }*/


    public string GeneratePin(byte length)
    {   
        string result="";

        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        for (int i=0; i<length; i++)
        {
            result += chars[(int)Random.Range(0, chars.Length - 1)];
        }

        //both set PIN code and return PIN code
        PIN = result;   
        return result;
    }


    //======
    //Methods for playersLoadedResource
    //======

    //For each new client the playersLoadedResource array must be cleaned and reset
    public void ResetPlayersLoadedResource()
    {
        //Array for the connected remote clients
        for (uint i = 0; i < maxPlayers; i++) 
            playersLoadedResource[i] = false;

        //For new player
        newPlayerLoadedResource.Clear();
        foreach (var i in newPlayerLoadedResource) newPlayerLoadedResource[i.Key] = false;
    }


    //Return true if all resources are loaded, "Horizontal" and "Vertical"
    public bool AllResourcesLoadedForPlayer()   //check whether all remote clients loaded the new prefab and whether the new player loaded all remote client's prefab
    {
        bool result = true;

        //All remote clients ready loading new player prefab? "Horizontal"
        foreach (var i in playerDisconnectedDict)
            if (i.Value == false)                                               //player exists and is connected (not disconnected)
                if (playersLoadedResource[i.Key] == false) return (false);      //there is one that is not ready yet

        

        return result;
    }


    //Return true if the new player loaded all resources - player loads all and then sends RPC instead of one RPC per loaded resource 
    public bool PlayerLoadedAllResources()
    {
        bool result = true;

        foreach (var i in newPlayerLoadedResource)
            result=result && i.Value;                                           //if there is one resource not ready yet then the new player is not ready for joining

        return result;
    }


    //Mark combination of resources loaded for clientA by clientB as completed
    public void SetResourceLoadedForPlayer(ulong byClient)
    {
        playersLoadedResource[byClient] = true;
    }


    public void SetLoadedResource(string playerPrefab)
    {
        newPlayerLoadedResource[playerPrefab] = true;
    }


    //Debug function only
    public void DumpServerRepo(string prefix) 
    {
        string tmp;

        tmp = prefix+" playerDict:";
        foreach (var i in playerDict)
            tmp += "(" + i.Key + "," + i.Value + ")";
        Debug.Log(tmp);

        tmp = prefix + " playerDisconnectedDict:";
        foreach (var i in playerDisconnectedDict)
            tmp += "(" + i.Key + "," + i.Value + ")";
        Debug.Log(tmp);

        tmp = prefix + " GODict:";
        foreach (var i in GODict)
            tmp += "(" + i.Key + "," + i.Value + ")";
        Debug.Log(tmp);

        tmp = prefix + " newPlayerLoadedResource:";
        foreach (var i in newPlayerLoadedResource)
            tmp += "(" + i.Key + "," + i.Value + ")";
        Debug.Log(tmp);

        tmp = prefix + " playersLoadedResource:";
        for (uint i= 0; i< maxPlayers; i++)
            tmp += "(" + i + "," + playersLoadedResource[i] + ")";
        Debug.Log(tmp);

    }
}
