using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Updated for Unity 6
/// This component defines the Grab Enter and Grab Exit event handlers
/// It checks whether the grabbed object "wants" to  be grabbed and if so, calls the methods in GrabbedObjectController.cs
/// </summary>
public class XRGrabEventHandler : MonoBehaviour
{
    public ulong avatarNetworkObjectId; //This is a pointer to the avatar grabs the object
    public GameObject avatarObject;
    

    public void SelectLeftGrabEnterEventController(SelectEnterEventArgs eventArgs)
    {
        if (!avatarObject) return;  //Unity 6
        xrGrabEnabler xrGE;

        var grabbedObj = eventArgs.interactableObject.transform.GetComponent<NetworkObject>();
        xrGE = grabbedObj.GetComponent<xrGrabEnabler>();
        if (xrGE)
        {
            if (xrGE.xrGrabEnabled)
            {
                avatarObject.GetComponent<GrabbedObjectController>().AvatarSelectGrabEnterEventHub(grabbedObj, true);
                Debug.Log("LEFT GRAB!");
            }
            else Debug.Log("Object is disabled for grabbing!");
        }
    }


    public void SelectLeftGrabExitEventController(SelectExitEventArgs eventArgs)
    {
        if (!avatarObject) return;  //Unity 6
        xrGrabEnabler xrGE;

        var grabbedObj = eventArgs.interactableObject.transform.GetComponent<NetworkObject>();
        xrGE = grabbedObj.GetComponent<xrGrabEnabler>();
        if (xrGE)
        {
            if (xrGE.xrGrabEnabled)
            {
                avatarObject.GetComponent<GrabbedObjectController>().AvatarSelectGrabExitEventHub(grabbedObj, true);
            }
            else Debug.Log("Object is disabled for grabbing!");
        }
    }


    public void SelectRightGrabEnterEventController(SelectEnterEventArgs eventArgs)
    {
        if (!avatarObject) return;  //Unity 6
        xrGrabEnabler xrGE;

        var grabbedObj = eventArgs.interactableObject.transform.GetComponent<NetworkObject>();
        xrGE = grabbedObj.GetComponent<xrGrabEnabler>();
        if (xrGE)
        {
            if (xrGE.xrGrabEnabled)
            {
                avatarObject.GetComponent<GrabbedObjectController>().AvatarSelectGrabEnterEventHub(grabbedObj, false);
                Debug.Log("RIGHT GRAB!");
            }
        }
    }

    public void SelectRightGrabExitEventController(SelectExitEventArgs eventArgs)
    {   
        if (!avatarObject) return;  //Unity 6
        xrGrabEnabler xrGE;

        var grabbedObj = eventArgs.interactableObject.transform.GetComponent<NetworkObject>();
        xrGE = grabbedObj.GetComponent<xrGrabEnabler>();
        if (xrGE)
        {
            if (xrGE.xrGrabEnabled)
            {
                avatarObject.GetComponent<GrabbedObjectController>().AvatarSelectGrabExitEventHub(grabbedObj, false);
            }
        }
    }
}
