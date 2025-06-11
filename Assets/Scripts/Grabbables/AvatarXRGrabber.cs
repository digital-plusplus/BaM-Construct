using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.Hands;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// Script that handles THIRD PERSON Grabbed Objects only!
/// Ensures the grabbed object follows the grabbing avatar hand over the network, not the XR Hand
/// Unity 6: The ActionBasedController component is deprecated so we need another way to detect grab poses!
///     This also uses a class from the XR Interaction Toolkit samples so RISK FOR FUTURE INCOMPATIBILITY!!!!
/// </summary>

public class AvatarXRGrabber : NetworkBehaviour
{
    ControllerButtonReader cBR;                     //Components that allows reading of the button/pinch pose
    bool grabbing;                                  //grabbing state
    bool isLeft = false;
    bool isRight = false;                           //To determine which controller we're using

    NetworkObject grabbedObject;
    Transform attachPoint;
    Toggle thirdPToggle;
    

    void Start()
    {
        if (!IsOwner) return;
        
        //Find a link to the Controller Buttons and/or the Hand Pinch Pose
        cBR = GameObject.Find("XR Origin (XR Rig)").GetComponent<ControllerButtonReader>();

        if (transform.name == "Left Arm IK_target")     isLeft = true; 
        if (transform.name == "Right Arm IK_target")    isRight = true;

        //Check whether the ControllerButtonReader component was added
        if (cBR) attachPoint = transform.Find("attachPoint");
        else Debug.LogError("Can't find the ControllerButtonReader component, so will not be able to detect 3P GRAB!");

        //Check the value of the FP/3P toggle on the UI
        thirdPToggle = GameObject.Find("LaunchUI").transform.Find("CameraPosition").GetComponent<Toggle>();
        if (!thirdPToggle) Debug.LogError("Can't find the FP/3P Toggle object!");
    }


    void Update()
    {
        if (!IsOwner) return;
        if (!thirdPToggle.isOn) return;     //only in 3P mode
        if (!grabbing) return;              //only update serverRPC if grabbing

        bool grabVal = false;

        //Which controller is this?
        if (isLeft)  grabVal = cBR.GetLeftControllerSelect();
        if (isRight) grabVal = cBR.GetRightControllerSelect();

        if (!grabVal)                       //Not grabbing anymore
        {
            grabbing = false;
            setIsKinematicServerRpc(grabbedObject, false);  //re-enable IsKinematic
            Debug.Log("3P Release " + grabbedObject.NetworkObjectId);
            grabbedObject = null;           //Just to be sure
        } 
        else moveMyGrabbedObjectServerRpc(grabbedObject, attachPoint.position, attachPoint.rotation); //Grabbing so tell server to move the object
    }


    //OnTriggerStay is called when an isTrigger collider (hand) collides with a non-isTrigger collider (grabbable)
    //if we use an OnCollisionStay both colliders must be non-isTrigger, that would bump the Grabbable when we want to grab!
    //Updated for Unity6
    private void OnTriggerStay(Collider other)
    {
        if (!IsOwner) return;           //Only execute this if this trigger is called on the person that actually tries to grab something
        if (grabbing) return;           //grabbing so no need to do this again and just wait until grab is released 
        if (!thirdPToggle.isOn) return; //FP mode

        bool grabVal=false;

        //Debug.Log("Touching object -> Trigger Stay!");

        //Get trigger/pinch input = for some !@*&#!@& reason these values don't revert to 0 when released
        //so we need to test on whether the value is 2.0 or <2.0
        if (isLeft) grabVal = cBR.GetLeftControllerSelect();
        if (isRight) grabVal = cBR.GetRightControllerSelect();
        if (grabVal)
        {
            grabbedObject = other.gameObject.GetComponent<NetworkObject>();
            
            setIsKinematicServerRpc(grabbedObject, true);           //grabbing so lets turn on IsKinematic to avoid bouncing objects 
            Debug.Log("3P grabbed " + grabbedObject.NetworkObjectId);
            grabbing = true;
        }
    }


    //============
    //Server Side - For 3P mode we let the server move the object, ie. THE SERVER MUST BE THE OWNER!
    //============
    [ServerRpc(RequireOwnership = false)] public void moveMyGrabbedObjectServerRpc(NetworkObjectReference grabbedObj, Vector3 position, Quaternion rotation, ServerRpcParams serverRpcParams = default)
    {
        if (!IsServer) return;
        if (grabbedObj.TryGet(out NetworkObject netObj))
        {
            
            netObj.ChangeOwnership(NetworkManager.ServerClientId);   //Note that the below only works when the server is the owner of the grabbable so lets grab ownership first
            netObj.transform.position = position;
            netObj.transform.rotation = rotation;
            //We don't implement 2-handed SCALING here as opposed to the FP grab alternative 

            //Debug.Log("ClientID " + serverRpcParams.Receive.SenderClientId+ " moved object " + netObj.NetworkObjectId + " to position " + netObj.transform.position);
        }
    }


    [ServerRpc] public void setIsKinematicServerRpc(NetworkObjectReference grabbedObj, bool value, ServerRpcParams serverRpcParams = default)
    {
        if (!IsServer) return;
        if (grabbedObj.TryGet(out NetworkObject netObj))
        {
            netObj.GetComponent<Rigidbody>().isKinematic = value;
        }
    }
}
