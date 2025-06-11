using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Unity.Networking.Transport;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Client/Server protocol that amends Netcode for GameObjects prefab loading system
///     Requires the ServerRepository component to store prefab information
///     Updated: 20241101 - moved UI code to LaunchUI.cs
/// </summary>
public class ClientServerJoinProtocol : NetworkBehaviour
{
    ulong networkID;                                    //client and network ID, note these are not the same
    ServerRepository serverRepo;                        //which clientID uses what prefab
    [SerializeField] bool useWebRepository;             //false: resources are loaded from local Resources, true: resources loaded from AssetBundles in assetBundleUrl
    [SerializeField] string assetBundleUrl;             //the http address of the asset bundle repository. MUST contain /windows and /android folders with assets!
    [SerializeField] int retryTime = 5;                 //time client waits until it tries to sign in again when server is busy
    [SerializeField] byte pinSize = 5;                  //Size of the PIN code
    public bool protocolDebug = false;                  //Showing detailed join debug messages

    //============
    //Generic client and server code
    //============

    public override void OnNetworkSpawn()
    {
        if (protocolDebug) 
            Debug.Log(Application.platform.ToString());

        //Unfortunately AssetBundles are platform-dependent! This implies you must store an asset bundle for each platform on your webserver in different folders
        switch (Application.platform) {
            case RuntimePlatform.WindowsPlayer: 
            case RuntimePlatform.WindowsEditor:
                assetBundleUrl += "/windows";
                break;
            case RuntimePlatform.Android:
                assetBundleUrl += "/android";
                break;
            default:
                Debug.LogError("FATAL: The Assetbundles are not supported on your platform");
                break;
        }

        networkID = GetComponent<NetworkObject>().NetworkObjectId;
        serverRepo = GameObject.Find("ServerRepository").GetComponent<ServerRepository>();
        string playerPrefab=null;

        serverRepo.SetServer(IsServer);                         //Needed for Connection Manager to detect whether we're on the server or not - WEIRD!
             
        switch (serverRepo.GetPIN())
        {
            case "":                                            //Stage 1: announce new player and let player load all already signed in prefabs
                if (IsOwner)                                    //only the owner needs to do something in the first step
                {
                    playerPrefab = GeneratePrefabName();
                    if (protocolDebug)
                        Debug.Log("CLIENT:" + OwnerClientId + " Stage1: SIGN IN - Telling server I want to use prefab, " + playerPrefab);

                    //Preps for Stage 2 
                    serverRepo.SetPIN(serverRepo.GeneratePin(pinSize));     
                    serverRepo.SetPREFAB(playerPrefab);         //In the next stage we know what the prefab name is we selected in the first stage
                    serverRepo.setClientID(OwnerClientId);      //This way in the next stage we known where to find the resources in the dictionary
                    //serverRepo.Dump("STAGE1:");
                    IWantToSignInServerRpc(playerPrefab);
                }
                break;

            default:                                            //Stage 2: resources are loaded, ready to join
                if (IsOwner)
                {
                    //Now ask the server to spawn my avatar
                    playerPrefab = serverRepo.getPREFAB();

                    if (protocolDebug)
                        Debug.Log("CLIENT:" + networkID + " I am now JOINING with prefab:, " + playerPrefab);
                    IWantToJoinServerRpc(playerPrefab);

                    //Cleanup the UI when we're done, this runs on the client ie. not a server/clientRPC
                    UpdateRemainingUIClient();
                }
                break;
        }
    }


    //==========
    //Stage1 Functions
    //==========

    [ServerRpc(RequireOwnership = false)]
    void IWantToSignInServerRpc(string playerPrefab, ServerRpcParams serverRpcParams = default)
    {
        var clientID = serverRpcParams.Receive.SenderClientId;

        //First check if someone else isn't completing its join process yet
        if (!serverRepo.IsBlockedForNewJoins())
        {
            //Initialization of the Server Repository
            serverRepo.SetBlockedForNewJoins(true);                         //prevent new players to join until this one is ready 
            serverRepo.ResetPlayersLoadedResource();                        //clear the array that checks whether all connected clients have loaded the new prefab
            serverRepo.InitPrefab(playerPrefab);                            //make sure there is an entry, fill that when the resource is loaded successfully
            serverRepo.AddSetConnected(clientID);                           //Mark it as connected so we include it in our waiting list for clients to load new resources
        
           
            //=================================================
            //Clients to load prefabs (horizontal and vertical)
            //=================================================
            ClientRpcParams clientRpcParams = new ClientRpcParams
            { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientID } } };
            if (protocolDebug)
                Debug.Log("SERVER: StartCoroutine(LoadNetworkPrefab(" + playerPrefab + ", 0, 0)");

            if (!useWebRepository) LoadLocalNetworkPrefab(playerPrefab, 0, 0, clientID);        //Mode = 0 - local asset Repo
            else StartCoroutine(LoadNetworkPrefab(playerPrefab, 0, 0, clientID));               //Mode = 0 - remote Asset Bundle


            //=================================
            //Wait until all are done
            //=================================
            if (protocolDebug)
                Debug.Log("SERVER: StartCoroutine(WaitUntilAllClientsReadyLoadingPrefab("+clientID+","+ playerPrefab +")");
            StartCoroutine(WaitUntilAllClientsReadyLoadingPrefab(clientID, playerPrefab));
        }

        else  //someone else is already in the process of joining to ask new joiner to wait a while
        {
            ClientRpcParams clientRpcParams = new ClientRpcParams
            { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientID } } };
                
            Debug.Log("Telling client " + clientID + " to wait because another player has not completed sign in process!");
            WaitBecauseServerIsBlockedClientRpc(playerPrefab, clientRpcParams);
        }
    }


    //This coroutine tells specific client to wait for 5 seconds and then try again
    [ClientRpc] void WaitBecauseServerIsBlockedClientRpc(string playerPrefab, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Server told me to wait because someone else is logging in currently, waiting for " + retryTime + " seconds...");
        StartCoroutine(ClientWait(retryTime, playerPrefab));
    }


    //Runs on client only
    IEnumerator ClientWait(int duration, string playerPrefab)
    {
        yield return new WaitForSeconds(duration);

        //Now we try again
        networkID = GetComponent<NetworkObject>().NetworkObjectId;
        serverRepo = GameObject.Find("ServerRepository").GetComponent<ServerRepository>();

        Debug.Log("CLIENT: Trying to sign in again!");
        IWantToSignInServerRpc(playerPrefab);
    }


    //This coroutine waits until all remote clients have loaded the NEW player prefab (if needed)
    //It also waits until the new player loaded all existing prefabs
    //Runs on Server only
    IEnumerator WaitUntilAllClientsReadyLoadingPrefab(ulong clientID, string playerPrefab)
    {
        if (protocolDebug)
            serverRepo.DumpServerRepo("WaitUntilAllClientsReadyLoadingPrefab");

        //We wait until all clients have replied they are ready loading the resources (incl this client itself)
        while (!serverRepo.AllResourcesLoadedForPlayer())         //inverse logic: BOTH must be true to proceed
        {
            //Debug.Log("serverRepo.AllResourcesLoadedForPlayer()=" + serverRepo.AllResourcesLoadedForPlayer() + " serverRepo.PlayerLoadedAllResources()=" + serverRepo.PlayerLoadedAllResources());
            yield return null;
        }
        if (protocolDebug)
            Debug.Log("SERVER: serverRepo.AllResourcesLoadedForPlayer()=" + serverRepo.AllResourcesLoadedForPlayer());

        while (!serverRepo.PlayerLoadedAllResources())         //inverse logic: BOTH must be true to proceed
        {
            //Debug.Log("serverRepo.AllResourcesLoadedForPlayer()=" + serverRepo.AllResourcesLoadedForPlayer() + " serverRepo.PlayerLoadedAllResources()=" + serverRepo.PlayerLoadedAllResources());
            yield return null;
        }

        if (protocolDebug)
        {
            Debug.Log("SERVER: serverRepo.PlayerLoadedAllResources()=" + serverRepo.PlayerLoadedAllResources());
            serverRepo.DumpServerRepo("WaitUntilAllClientsReadyLoadingPrefab");
            Debug.Log("serverRepo.AllResourcesLoadedForPlayer()=" + serverRepo.AllResourcesLoadedForPlayer() + " serverRepo.PlayerLoadedAllResources()=" + serverRepo.PlayerLoadedAllResources());
        }

        //code below waits until all resources are loaded - FUTURE: need timeout to avoid it can hang if a client disconnects 
        Debug.Log("SERVER: New player with prefab " + playerPrefab + " was loaded by all clients and new loaded all prefabs");

        //Update Client UI so the player can reconnect
        ClientRpcParams clientRpcParams = new ClientRpcParams
        { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientID } } };
        if (clientID != 0)  //not for the Host!
        {
            PrepareToReconnectClientRpc(clientRpcParams);

            //Disconnect the client so client needs to reconnect and it can load all the resources before the StartClient() call
            GameObject.Find("Network Manager").GetComponent<NetworkManager>().DisconnectClient(clientID);
            Debug.Log("SERVER: Completed sign-in process for client with ID " + clientID);
        }
        else //Host spawns immediately
        {
            SpawnPlayer(playerPrefab, clientID);
            UpdateHostUIClientRpc();
            NetworkManager.Destroy(transform.gameObject);   //remove temporary prefab
        }
    }


    //Just some UI code to inform the player it can now join
   [ClientRpc]
    void PrepareToReconnectClientRpc(ClientRpcParams clientRpcParams = default)
    {
        GameObject.Find("LaunchUI").GetComponent<LaunchUI>().DisableConnectionItemsFor2ndStageClient("Join");
    }


    //Remove all connection related items in the UI, no longer needed
    [ClientRpc]
    void UpdateHostUIClientRpc(ClientRpcParams clientRpcParams = default)
    {
        GameObject.Find("LaunchUI").GetComponent<LaunchUI>().DisableAllConnectionItems();
    }


    //Remove Remaining items after 2nd stage signin completed, not a Client nor ServerRPC!
    void UpdateRemainingUIClient()
    {
        GameObject.Find("LaunchUI").GetComponent<LaunchUI>().DisableRemainingConnectionItemsFor2ndStageClient();
    }


    //This only runs on ONE client, the new joiner!
    [ClientRpc]
    void InformNewClientToLoadExistingPrefabsClientRpc(ulong num, string prefabList, string prefabHashList, ClientRpcParams clientRpcParams = default)
    {
        string[] pLArray = prefabList.Split(',');                                           //find all prefabs in the CSV
        string[] iDArray = prefabHashList.Split(',');                                       //find all hashes in the CSV

        if (protocolDebug)
            Debug.Log("CLIENT: Hashlist " + prefabHashList);
        for (int i = 0; i < pLArray.Length; i++)
        {
            if (protocolDebug)
                Debug.Log("CLIENT: I need to also load Resource " + pLArray[i] + " with hash " + uint.Parse(iDArray[i]));
            if (!useWebRepository) LoadLocalNetworkPrefab(pLArray[i], uint.Parse(iDArray[i]), 2, 0);   //mode=2 - local repo
            else StartCoroutine(LoadNetworkPrefab(pLArray[i], uint.Parse(iDArray[i]), 2, 0));           //mode=2 - remote assetbundle
        }
    }


    [ClientRpc]
    void InformAllClientsClientRpc(string playerPrefab, uint hash, ClientRpcParams clientRpcParams = default)
    {
        if (protocolDebug)
            Debug.Log("CLIENT: The server told me to load " + playerPrefab + " with hash " + hash + " for new joiner");
        if (!useWebRepository) LoadLocalNetworkPrefab(playerPrefab, hash, 1, 0);                       //mode=1 - local repo
        else StartCoroutine(LoadNetworkPrefab(playerPrefab, hash, 1, 0));                               //mode=1 - remote assetbundle
    }


    //==================================================================================================================
    // Open AssetBundle from webserver using https, check whether the prefab already exists and if not load it
    // then confirm to server you're done via an RPC
    // Multiple operate modes as depending on where this function is used, it has to wait until the resource is loaded
    // mode=0: server, mode=1: existing clients load new prefab, mode=2: new client loads existing prefabs
    //==================================================================================================================
    IEnumerator LoadNetworkPrefab(string playerPrefab, uint hash, int mode, ulong clientID)             
    {
        //Find the local network manager
        GameObject loadedPrefab;

        if (protocolDebug)
            Debug.Log("LoadNetworkPrefab(" + playerPrefab + "," + hash + "," + mode + "," + clientID + ")");

        //Try to load the resource
        if (assetBundleUrl[assetBundleUrl.Length - 1] != '/') assetBundleUrl += "/";
        string resourceURL = assetBundleUrl + playerPrefab.ToLower();                  
        var request = UnityWebRequestAssetBundle.GetAssetBundle(resourceURL, 0);
        yield return request.SendWebRequest();

        if (protocolDebug)
            Debug.Log("LoadNetworkPrefab(" + playerPrefab + "," + hash + "," + mode + "," + clientID + ") request state:" + request.ToString());

        //Code below continues when webrequest is completed 
        if (request.result != UnityWebRequest.Result.Success) Debug.LogError("FATAL: webrequest insuccessful, could not load " + resourceURL);
        else
        {
            if (protocolDebug)
                Debug.Log("Found the assetbundle " + resourceURL);
            if (serverRepo.GetHash(playerPrefab)==0)        //not loaded yet
            {
                //try to load the Asset Bundle
                AssetBundle bundle = UnityEngine.Networking.DownloadHandlerAssetBundle.GetContent(request);
                if (bundle == null)                                                                     //FAILED
                {
                    Debug.LogError("FATAL: Could not load AssetBundle" + resourceURL);
                }
                else
                {
                    Debug.Log("Loaded Resource Bundle " + resourceURL);

                    //Try to load the Resource from the Asset Bundle
                    loadedPrefab = bundle.LoadAsset<GameObject>(playerPrefab);
                    if (!loadedPrefab) Debug.LogError("FATAL: cannot load resource:" + playerPrefab + " from assetbundle");              //handle fails
                    else
                    {
                        //Call function to manage the GlobalHash value and then add it the NetworkManager prefab list IFF you are the server or client in stage2
                        if (protocolDebug)
                            Debug.Log("Loaded Resource " + playerPrefab);

                        if (IsServer)
                        {
                            hash = AddNetworkPrefabHashed(loadedPrefab, 0);                                 //Server generates a new hash
                            serverRepo.DictUpdate(playerPrefab, loadedPrefab, hash);                        //add it to the resources
                            if (protocolDebug)
                                Debug.Log("SERVER: Added " + playerPrefab + " to dictionary and generated hash " + hash);
                        }
                        else
                        {
                            AddNetworkPrefabHashed(loadedPrefab, hash);                                     //Client receives the hash from the server
                            serverRepo.DictUpdate(playerPrefab, loadedPrefab, hash);                        //add it to the resources, needed for stage 2
                            if (protocolDebug)
                                Debug.Log("CLIENT: Added " + playerPrefab + " to dictionary with hash " + hash);
                        }
                    }
                }
            }
            else
                if (protocolDebug)
                Debug.Log("No need to add " + playerPrefab + " to dictionary as it already exists");    //duplicate network prefabs results in exception error

            ExecuteWaitCode(playerPrefab, clientID, mode);
        }
    }

    //==================================================================================================================
    // Load resource from local repository-FOR DEV PURPOSES! FASTER TESTING PREFAB CHANGES W/O NEED TO CREATE ASSTBUNDLES
    // Multiple operate modes as depending on where this function is used, it has to wait until the resource is loaded
    // mode=0: server, mode=1: existing clients load new prefab, mode=2: new client loads existing prefabs
    //==================================================================================================================
    void LoadLocalNetworkPrefab(string playerPrefab, uint hash, int mode, ulong clientID)
    {
        //Find the local network manager
        GameObject loadedPrefab;

        if (protocolDebug)
            Debug.Log("LoadNetworkPrefab(" + playerPrefab + "," + hash + "," + mode + "," + clientID + ")");

        if (serverRepo.GetHash(playerPrefab) == 0)        //not loaded yet
        {
            //Try to load the Resource from the Asset Bundle
            loadedPrefab = Resources.Load(playerPrefab) as GameObject;
            if (!loadedPrefab) Debug.LogError("FATAL: cannot load resource:" + playerPrefab + " from local Resources folder");              //handle fails
            else
            {
                //Call function to manage the GlobalHash value and then add it the NetworkManager prefab list IFF you are the server or client in stage2
                Debug.Log("Loaded Resource " + playerPrefab);

                if (IsServer)
                {
                    hash = AddNetworkPrefabHashed(loadedPrefab, 0);                                 //Server generates a new hash
                    serverRepo.DictUpdate(playerPrefab, loadedPrefab, hash);                        //add it to the resources
                    if (protocolDebug)
                        Debug.Log("SERVER: Added " + playerPrefab + " to dictionary and generated hash " + hash);
                }
                else
                {
                    AddNetworkPrefabHashed(loadedPrefab, hash);                                     //Client receives the hash from the server
                    serverRepo.DictUpdate(playerPrefab, loadedPrefab, hash);                        //add it to the resources, needed for stage 2
                    if (protocolDebug)
                        Debug.Log("CLIENT: Added " + playerPrefab + " to dictionary with hash " + hash);
                }
            }
        } 
        else if (protocolDebug)
            Debug.Log("No need to add " + playerPrefab + " to dictionary as it already exists");    //duplicate network prefabs results in exception error

        ExecuteWaitCode(playerPrefab, clientID, mode);
    }


    void ExecuteWaitCode(string playerPrefab, ulong clientID, int mode)
    {
        //Depending on who calls this function the below executes code that has to wait for the resource load to be finished
        switch (mode)
        {
            case 0:                                                                                 //mode=0: server
                if (protocolDebug)
                    Debug.Log("SERVER: Informing all clients to load prefab " + playerPrefab + " with hash " + serverRepo.GetHash(playerPrefab));
                InformAllClientsClientRpc(playerPrefab, serverRepo.GetHash(playerPrefab));

                if (protocolDebug)
                    Debug.Log("SERVER: InformNewClientToLoadExistingPrefabsClientRpc(" + clientID + "," + serverRepo.ReturnOnlinePrefabs() + "," + serverRepo.ReturnOnlineHashes() + ")");
                ClientRpcParams clientRpcParams = new ClientRpcParams
                { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientID } } };
                serverRepo.ResetNewPlayerLoadedResource();
                InformNewClientToLoadExistingPrefabsClientRpc(clientID, serverRepo.ReturnOnlinePrefabs(), serverRepo.ReturnOnlineHashes(), clientRpcParams);
                break;

            case 1:                                                                                 //mode=1: existing clients load new prefab
                ILoadedResourceForNewJoinerServerRpc(playerPrefab);
                break;

            case 2:                                                                                 //mode=2: new client loads existing prefabs
                IloadedResourceForRemoteClientServerRpc(playerPrefab);
                break;

            default:
                Debug.LogError("LoadNetworkPrefab called with wrong mode argument");
                break;
        }
    }


    //Called by remote clients to confirm they loaded the new joiner's prefab
    [ServerRpc(RequireOwnership = false)]
    void ILoadedResourceForNewJoinerServerRpc(string playerPrefab, ServerRpcParams serverRpcParams = default)
    {
        var clientID = serverRpcParams.Receive.SenderClientId;

        Debug.Log("SERVER: Client " + clientID + " reported it has loaded prefab " + playerPrefab);
        serverRepo.SetResourceLoadedForPlayer(clientID);                                                //store that client clientID is ready in server Repo
    }


    //Called by new joiner to confirm it loaded a resource from the playerDictionary
    [ServerRpc(RequireOwnership = false)]
    void IloadedResourceForRemoteClientServerRpc(string playerPrefab, ServerRpcParams serverRpcParams = default)
    {
        var clientID = serverRpcParams.Receive.SenderClientId;

        Debug.Log("SERVER: New joiner " + clientID +" reported it has loaded prefab " + playerPrefab);
        serverRepo.SetLoadedResource(playerPrefab);
    }


   //=============
   //Stage2 functions
   //=============

   //Only called when SIGN-IN process has been completed (ie. PIN is set)
   [ServerRpc(RequireOwnership = false)]
    void IWantToJoinServerRpc(string playerPrefab, ServerRpcParams serverRpcParams = default)
    {
        var clientID = serverRpcParams.Receive.SenderClientId;

        Debug.Log("SERVER: new client " + clientID + " joining with prefab " + playerPrefab);

        //Spawn
        SpawnPlayer(playerPrefab, clientID);
        serverRepo.AddSetConnected(clientID);                                   //Mark as connected!!
    }


    void SpawnPlayer(string playerPrefab, ulong clientID)
    {
        GameObject tmp = serverRepo.ReturnGameObject(playerPrefab);
        if (tmp)
        {
            GameObject go = Instantiate(tmp, Vector3.zero, Quaternion.identity);
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientID);         //spawn the new prefab as player prefab for clientID
                                                                                    
            Debug.Log("SERVER: SPAWNED " + playerPrefab);
        }
        else
        {
            Debug.LogError("SERVER: FATAL CANNOT LOAD RESOURCE FOR NEW PLAYER " + playerPrefab);
        }

        //enable new players to join until again - NOT HERE YET NEW PLAYER STILL NEEDS TO ACTUALLY JOIN!
        serverRepo.SetBlockedForNewJoins(false);     
    }


    //============
    //Helper functions
    //============
    string GeneratePrefabName()
    {
        //Get the selected playerPrefab (currently from the UI)
        string gender = GameObject.Find("Gender").GetComponentInChildren<TMPro.TextMeshProUGUI>().text;
        string breed = GameObject.Find("Breed").GetComponentInChildren<TMPro.TextMeshProUGUI>().text;

        return gender + breed; 
    }


    uint AddNetworkPrefabHashed(GameObject loadedPrefab, uint hash)
    {
        if (loadedPrefab)   //in case we provided a non-existing prefab we will return 0
        {
            NetworkManager nm = GameObject.Find("Network Manager").GetComponent<NetworkManager>();
            Type type = typeof(NetworkObject);
            FieldInfo fieldInfo = type.GetField("GlobalObjectIdHash", BindingFlags.NonPublic | BindingFlags.Instance);

            if (IsServer) hash = (uint)loadedPrefab.GetHashCode();                      //The server just generates one, could be any uint value as long as its unique

            fieldInfo.SetValue(loadedPrefab.GetComponent<NetworkObject>(), hash);       //Set the hash value into the GameObject
            nm.AddNetworkPrefab(loadedPrefab);                                          //Add the prefab to the Network Prefab List on the server!
        }
        return hash;                                                                    //for the server it will return a new value, the clients will ignore the return value
    }
}
