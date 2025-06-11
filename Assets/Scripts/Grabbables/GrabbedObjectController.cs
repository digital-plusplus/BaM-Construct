using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


/// <summary>
/// Script that handles FIRST PERSON Grabbed Objects only!
/// It launches ServerRPCs to tell the server to move the object 
/// The FP3P field must not be set in the Unity UI, this will be automatically set by the HNP script
/// Called by: HNP and XRGrabEventHandler
/// </summary>

public class GrabbedObjectController : NetworkBehaviour
{
    NetworkObject avtLeftGrabbedObject, avtRightGrabbedObject;
    [SerializeField] FirstThirdPersonController fP3P;

    Transform avLeft, avRight;  //passed on via Initialize()


    //Note we're not using Start() here as we can only call this once the network object is being spawned
    public void Initialize(Transform left, Transform right)
    {
        fP3P = GetComponent<FirstThirdPersonController>();

        //These we need to have to move the grabbed object to the attachpoint
        avLeft = left.Find("attachPoint");
        avRight = right.Find("attachPoint"); 
    }


   //This code is called outside the HNP Update() function!
    void Update()
    {
        //Return when not ready
        if (!IsOwner) return;
        if (!fP3P) return;
        if (!fP3P.thirdPToggle) return;         //If the Object was not defined!

        //We only send serverRPC calls to move the object in First Person Mode
        if (!fP3P.thirdPToggle.isOn)  
        {
            //send the grabbed object(s) to the XR IK_target transforms. Add the scale
            if (avtLeftGrabbedObject) 
                moveMyGrabbedObjectServerRpc(avtLeftGrabbedObject, avLeft.position, avLeft.rotation, avtLeftGrabbedObject.transform.localScale);
            
            if (avtRightGrabbedObject) 
                moveMyGrabbedObjectServerRpc(avtRightGrabbedObject, avRight.position, avRight.rotation, avtRightGrabbedObject.transform.localScale);
        }
    }


    //===============================================
    //CALLED BY XRGrabEventHandler
    //===============================================
    public void AvatarSelectGrabEnterEventHub(NetworkObject netObj, bool whichHand) //true=left, false=right
    {
        if (fP3P.thirdPToggle.isOn) return;     //We only execute this in FP mode

        //tell update() to align the grabbed object to the left/right hand position
        if (whichHand) avtLeftGrabbedObject = netObj;
        else avtRightGrabbedObject = netObj;

        Debug.Log("FP grab object " + netObj.NetworkObjectId + ", I am clientID "+ NetworkManager.Singleton.LocalClientId);
        //setIsKinematicServerRpc(netObj, true);            //DISABLED: turn on IsKinematic - as per Unity6 this is a setting in NetworkRigidBody!
        SetGrabbableOwnershipServerRpc(netObj, true);       //Enable this to allow throwing!
    }


    public void AvatarSelectGrabExitEventHub(NetworkObject netObj, bool whichHand)
    {
        if (fP3P.thirdPToggle.isOn) return; //We only execute this in FP mode

        Debug.Log("FP Release");
        if (whichHand) avtLeftGrabbedObject = null;
        else avtRightGrabbedObject = null;
        //setIsKinematicServerRpc(netObj, false);           //DISABLED:turn off IsKinematic - as per Unity6 this is a setting in NetworkRigidBody!
        //SetGrabbableOwnershipServerRpc(netObj, false);    //DISABLED:On grab release, the owner stays as-is until the next one grabs the object
    }


    //============================
    //SERVER SIDE ONLY
    //============================
    [ServerRpc(RequireOwnership = false)]
    public void moveMyGrabbedObjectServerRpc(NetworkObjectReference grabbedObj, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (!IsServer) return;
        if (grabbedObj.TryGet(out NetworkObject netObj))
        {
            netObj.transform.position = position;
            netObj.transform.rotation = rotation;
            netObj.transform.localScale = scale;

            //IMPORTANT: AS THE SERVER IS NOT THE OWNER, THE SERVER SHOULD NOW SEND A CLIENTRPC TO THE CLIENTS TO MOVE THE OBJECT AS WELL!
            //FOR SOME REASON THAT'S NOT NEEDED NOW BUT CONSIDER THIS IN CASE IT STOPS WORKING!!!!!
        }
    }


    //NO LONGER USED! REMOVE IN FUTURE
    [ServerRpc(RequireOwnership = false)]     
    public void setIsKinematicServerRpc(NetworkObjectReference grabbedObj, bool value)
    {
        if (!IsServer) return;
        if (grabbedObj.TryGet(out NetworkObject netObj))
        {
            netObj.GetComponent<Rigidbody>().isKinematic = value;
        }
    }


    //NEW: Give ownership to person that grabs the object
    //if value=true, assigns ownership of the grabbed Object to the player, if value=false, server takes ownership again
    [ServerRpc(RequireOwnership = false)]
    private void SetGrabbableOwnershipServerRpc(NetworkObjectReference grabbedObj, bool value,ServerRpcParams serverRpcParams = default)
    {
        if (!IsServer) return;
        if (grabbedObj.TryGet(out NetworkObject netObj))
        {
            ulong newOwner = (value ? serverRpcParams.Receive.SenderClientId : NetworkManager.ServerClientId);
            netObj.ChangeOwnership(newOwner);
            Debug.Log("FP Grab - Changing ownership of object " + netObj.NetworkObjectId + " to " + newOwner);
        }
        
    }
}
