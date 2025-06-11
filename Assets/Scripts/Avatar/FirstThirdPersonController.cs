using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class FirstThirdPersonController : MonoBehaviour
{
    public bool FP3PToggle;         //Need this bool to detect state changes in thirdPToggle !
    public Toggle thirdPToggle;

    public Vector3 thirdPOffset = new Vector3(0, 0, 1.5f);
    public Vector3 firstPOffset = new Vector3(0, 0, 0.0f);   //BUGFIX - INIT
    Dictionary<string, Mesh> FPDict = new Dictionary<string, Mesh>();        //store meshes for all avatar objects in FP mode
    Transform myCameraOffset;
    [SerializeField] bool debug=true;                            //Debug messages


    //Note we're not using Start() here as we can only call this once the network object is being spawned
    public void Initialize()
    {
        //Link to the Actual Camera Offset of the XR Origin
        //IMPORTANT: in the Unity UI the transform position shows a value of 0,0,0 but during runtime its actually the XR camera position world coordinates! 
        myCameraOffset = GameObject.Find("Camera Offset").transform;

        //Link to UI Toggle component
        //thirdPToggle = GameObject.Find("CameraPosition").GetComponent<Toggle>();    //can read IsOn state here
        //This will also find the 3P toggle when the UI is hidden!
        thirdPToggle = GameObject.Find("Canvas").transform.Find("LaunchUI").transform.Find("CameraPosition").GetComponent<Toggle>();

        //Value of the UI Toggle component, just for code simplification
        FP3PToggle  = (thirdPToggle.isOn ? false : true);
    }

    public Vector3 FP3P_Offset()
    {
        return (thirdPToggle.isOn ? thirdPOffset : firstPOffset);
    }

    public bool switchFP3P() //for demo purposes, allows switching between first and 3rd person
    {
        if (thirdPToggle.isOn != FP3PToggle)            //state change!
        {
            SwitchMeshRenderers(thirdPToggle.isOn);     //turn on/off
            SwitchAvatarXRGrabbers(thirdPToggle.isOn);  //turn on/off
            SwitchXRDirectInteractors(FP3PToggle);      //turn off/on
            FP3PToggle = thirdPToggle.isOn;             //detect new state change
            return true;                                //yes something changed 
        }
        return false;                                   //no state change
    }

    void SwitchMeshRenderers(bool value)
    {
        foreach (var x in transform.GetComponentsInChildren<SkinnedMeshRenderer>())     //for all SkinnedMesh Renders, remove the mesh instead of disabling the SMR!
        {
            //when turning off the SMRs - the palmbone of the hand is no longer updated causing WFS - found out during testing
            if (!value)                     //switching to FP mode, now try to hide the avatar
            {
                FPDict.Add(x.transform.name.ToString(), x.sharedMesh);
                if (debug) Debug.Log("Storing mesh for " + x.transform.name);
                x.sharedMesh = null;
            }
            else                            //3P mode
            {
                if (FPDict.Count > 0)       //in case we spawn in 3P mode, FPDict will still be empty so nothing to restore
                {
                    Mesh y = FPDict[x.transform.name];
                    if (y)                  //avoid errors if a Mesh can't be found
                    {
                        x.sharedMesh = y;
                        FPDict.Remove(x.transform.name);
                        if (debug) Debug.Log("Removing mesh for " + x.transform.name);
                    }
                    else Debug.LogError("ERROR: no mesh found for " + x.transform.name);
                }
            }
        }
    }


    //Turn the 3P Grab component ON or OFF
    void SwitchAvatarXRGrabbers(bool value)
    { 
        foreach (var x in transform.GetComponentsInChildren<AvatarXRGrabber>()) x.enabled = value; 
    }


    //Unity 6: Turn NearCasting in the NFInteractor ON (FP mode) or OFF (3P mode)
    //  in UNity 2022 this turned the Direct Interactors ON or OFF
    void SwitchXRDirectInteractors(bool value)
    {
        //The below no longer works in Unity 6 as the new XR Origin uses NearFar Interactors
        //foreach (var x in myCameraOffset.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor>()) x.enabled = value; 
        foreach (var x in myCameraOffset.GetComponentsInChildren<NearFarInteractor>()) x.enableNearCasting = value; //turn off new "Direct" interactors
    }
}
