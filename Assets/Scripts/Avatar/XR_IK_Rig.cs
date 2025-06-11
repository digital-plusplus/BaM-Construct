using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class XR_IK_Rig : NetworkBehaviour
{
    // Note that this is called only when the HumanoidAvatar prefab is spawned by the server
    // This class identifies the actual bones using the HumanDescription bones and:
    // 1. Sets all IK Constraints objects fields (bones) in the XR IK Rig
    // 2. Sets the IK_Target positions/rotations in the XR IK Rig 

    GameObject Dynamic_FBX_Object;                                                      //Automatically filled in InitializeIK()
    Avatar Dynamic_FBX_Avatar;                                                          //Automatically filled in InitializeIK()

    Animator HumanoidAvatarAnimator;
    Dictionary<string, GameObject> BoneDict = new Dictionary<string, GameObject>();
    string[] humanBones = { "LeftUpperArm",  "LeftLowerArm", "LeftHand",                //list of all the bones we need to identfy
                            "RightUpperArm", "RightLowerArm", "RightHand",
                            "Head",
                            "Left Index Proximal", "Left Index Distal",                 //Note! If the distal has a child, then we must use that, the Humanoid model has only 3 finger digits!
                            "Right Index Proximal", "Right Index Distal",               //idem & for all the below...
                            "Left Middle Proximal", "Left Middle Distal",
                            "Right Middle Proximal", "Right Middle Distal",
                            "Left Ring Proximal", "Left Ring Distal",
                            "Right Ring Proximal", "Right Ring Distal",
                            "Left Thumb Proximal", "Left Thumb Distal",
                            "Right Thumb Proximal", "Right Thumb Distal",
                            "Left Little Proximal", "Left Little Distal",
                            "Right Little Proximal", "Right Little Distal"
    };

    //Introducing some variables to determine what kind of model is being loaded
    // => unfortunately not all models behave well when we animate them...
    bool isGen9 = false;
    bool isMixamo = false;

    //Find a child object by its name but search 2 levels deep
    GameObject FindInChildren(GameObject from, string name)
    {
        //simplest, direct child
        if (from.transform.Find(name)) return from; 

        //One level deeper, child of a child
        for (int i=0; i<from.transform.childCount; i++)
        {
            if (from.transform.GetChild(i).Find(name)) return from.transform.GetChild(i).Find(name).gameObject;
        }

        //any more levels deeper is not supported
        return null;
    }


    /// <summary>
    /// Main function to initialize the Inverse Kinematics for head, arms and fingers
    /// Called by HNP in the OnNetworkSpawn() phase
    /// </summary>
    /// <returns>false when something failed </returns>
    public bool InitializeIK()
    {
        GameObject rootBone;

        Debug.Log("Initialize IK");

        //Auto fill
        Dynamic_FBX_Object = transform.parent.gameObject;                          //XR_IK_Rig MUST always be a component of the main object.
        if (Dynamic_FBX_Object.GetComponent<Animator>()) 
            Dynamic_FBX_Avatar = Dynamic_FBX_Object.GetComponent<Animator>().avatar;   
        else
        {
            Debug.LogError("Fatal, cannot find animator for " + Dynamic_FBX_Object.name);
            return false;
        }

        //Check for known FBX model standards
        if (transform.parent.Find("Genesis9"))          { isGen9 = true; Debug.Log("Genesis 9"); }
        if (transform.parent.Find("Genesis8Female"))    { isGen9 = true; Debug.Log("Genesis 8 Female"); }       //use same logic as Gen 9
        if (transform.parent.Find("Genesis8Male"))      { isGen9 = true; Debug.Log("Genesis 8 Female"); }       //use same logic as Gen 9
        if (transform.parent.Find("mixamorig:Hips"))    { isMixamo = true; Debug.Log("Mixamo"); }
        if (!(isGen9 || isMixamo)) Debug.LogWarning("Non-standard FBX model, the avatar may not render/animate well!");

        //IK Must be initialized for ALL avatars, not just the local avatar
        HumanoidAvatarAnimator = transform.parent.GetComponent<Animator>();
        var hD = HumanoidAvatarAnimator.avatar.humanDescription;        //helper to avoid unnecessary lengthy code

        HumanoidAvatarAnimator.avatar = Dynamic_FBX_Avatar;
            
        //Some testing
        if (!HumanoidAvatarAnimator) Debug.LogError("XR IK Rig Initialization: cannot find the Animator object");
        if (!HumanoidAvatarAnimator.avatar.isHuman)
        {
            Debug.LogError("XR IK Rig Initialization: your avatar object is not configured with a human bone skeleton");
            return false;       //stop trying and return failure
        }

        //Some FBX models have the root bone under another child object, so need to search 2 levels deep, any more is not supported at this time 
        FindInChildren(Dynamic_FBX_Object, hD.human[0].boneName);

        //Fill Bone mapping lookup Dictionary
        Debug.Log("Trying to find " + hD.human[0].boneName + " in " + Dynamic_FBX_Object.name);
        
        rootBone = FindInChildren(Dynamic_FBX_Object, hD.human[0].boneName);
        if (!rootBone) Debug.LogError("FATAL: cannot find bone structure");

        for (int i=0;i<hD.human.Length; i++)
        {
            Transform targetBone = RealBoneFind(rootBone, hD.human[i].boneName).transform;
            if (targetBone)
            {
                BoneDict.Add(hD.human[i].humanName, targetBone.gameObject);
                //Debug.Log("Bone: " + hD.human[i].humanName + " mapped to: " + targetBone.name);
            }
        }

        DisableEnableRigBuilder(false);  //turn rigbuilder OFF => THIS IS NECESSARY OTHERWISE THE ANIMATIONS WON"T WORK AFTER THE CHANGES!

        //Now we're relinking our IK Constraint Objects
        InitTwoBoneConstraintObject(transform.Find("Left Arm IK").gameObject, 0);
        InitTwoBoneConstraintObject(transform.Find("Right Arm IK").gameObject, 3);
        InitMPConstraintObject     (transform.Find("Head IK").gameObject, 6);
        InitChainIKConstraintObject(transform.Find("Left Index Finger IK").gameObject, 7);
        InitChainIKConstraintObject(transform.Find("Right Index Finger IK").gameObject, 9);
        InitChainIKConstraintObject(transform.Find("Left Middle Finger IK").gameObject, 11);
        InitChainIKConstraintObject(transform.Find("Right Middle Finger IK").gameObject, 13);
        InitChainIKConstraintObject(transform.Find("Left Ring Finger IK").gameObject, 15);
        InitChainIKConstraintObject(transform.Find("Right Ring Finger IK").gameObject, 17);
        InitChainIKConstraintObject(transform.Find("Left Thumb IK").gameObject, 19);
        InitChainIKConstraintObject(transform.Find("Right Thumb IK").gameObject, 21);
        InitChainIKConstraintObject(transform.Find("Left Pinky Finger IK").gameObject, 23);
        InitChainIKConstraintObject(transform.Find("Right Pinky Finger IK").gameObject, 25);

        DisableEnableRigBuilder(true);  //turn rigbuilder ON => THIS IS NECESSARY OTHERWISE THE ANIMATIONS WON"T WORK AFTER THE CHANGES!

        Debug.Log("XR_IK Constraints initialized");

        return (true);
    }


    //Helper function to find the GameObject for the bone with name, start searching from bone
    //Recursive so we initiate the search with searching at the root bone level and then do a recursive DFS
    GameObject RealBoneFind(GameObject bone, string name)       //recursive search
    {
        GameObject tmp = null;

        if (bone.name == name) return (bone);                   //match!
        else
            for (int i = 0; i < bone.transform.childCount; i++) //search all children
            {
                tmp = RealBoneFind(bone.transform.GetChild(i).gameObject, name);
                if (tmp) break;                                 //if something comes back, push it upward
            }
        return (tmp);
    }


    void InitTwoBoneConstraintObject(GameObject XRIK, int LR)  //VRIK=IK object link, LR=0==left, LR=3==right
    {
        TwoBoneIKConstraint tbc;

        //Hands
        tbc = XRIK.GetComponent<TwoBoneIKConstraint>();
        tbc.data.targetPositionWeight = 1;
        tbc.data.targetRotationWeight = 1;
        tbc.data.hintWeight = 1;
        tbc.weight = 1;

        //Debug.Log(XRIK.name);
        tbc.data.root = BoneDict[humanBones[LR]].transform;
        tbc.data.mid = BoneDict[humanBones[LR + 1]].transform;
        tbc.data.tip = BoneDict[humanBones[LR + 2]].transform;

        //Now we set the IK Target transforms
        //XRIK.transform.GetChild(0).position = XRIK.GetComponent<TwoBoneIKConstraint>().data.tip.position;
        //XRIK.transform.GetChild(0).rotation = XRIK.GetComponent<TwoBoneIKConstraint>().data.tip.rotation;
        
    }


    void InitMPConstraintObject(GameObject XRIK, int hBindex)  //VRIK=IK object link
    {
        //Head
        MultiParentConstraint mpc = XRIK.GetComponent<MultiParentConstraint>();

        mpc.weight = 1;
        mpc.data.constrainedObject = BoneDict[humanBones[hBindex]].transform;

        //Head IK_Target 
        XRIK.transform.GetChild(0).position = XRIK.GetComponent<MultiParentConstraint>().data.constrainedObject.position;
        XRIK.transform.GetChild(0).rotation = XRIK.GetComponent<MultiParentConstraint>().data.constrainedObject.rotation;
    }


    void InitChainIKConstraintObject(GameObject XRIK, int hBindex)  //VRIK=IK object link, hBindex=root, hBindex+1=Tip
    {
        //Fingers
        //Debug.Log("XR_IK_Rig: Fingers");
        ChainIKConstraint cikc = XRIK.GetComponent<ChainIKConstraint>();

        //Explicitly set the values 
        cikc.data.chainRotationWeight = 1;
        cikc.data.maxIterations = 15;
        cikc.data.tolerance = 0.0001f;
        cikc.weight = 1;

        //check if the distal has another digit connected to it, in that case we need to use that child as the tip!!!
        //cikc.data.tip = (BoneDict[humanBones[hBindex + 1]].transform.childCount > 0 ?
        if (BoneDict[humanBones[hBindex + 1]].transform.childCount > 0)
            cikc.data.tip = BoneDict[humanBones[hBindex + 1]].transform.GetChild(0).transform; //Set the tip bone to the farest child
        else
            cikc.data.tip = BoneDict[humanBones[hBindex + 1]].transform;                  //Set the tip bone


        //==========================================================================================
        //For standard FBX models, we use 3 bones for IK in the fingers, for Genesis 9 we use 2 bones
        //because the Genesis 9 hand bone structure is very different which causes major finger deformation
        //Add addl exceptions here for models that have deformed finger movement!
        //==========================================================================================
        if (isGen9)                                                                 //Ensure DAZ models have Genesis9 as an object
        {
            cikc.data.root = cikc.data.tip.parent.parent;                           //only two bones up the chain
            cikc.data.tipRotationWeight = 0;                                        //With the G9 model, the bones are ending at the anchorpoint => avoid dangling fingertips!
        }
        else
        {
            cikc.data.root = BoneDict[humanBones[hBindex]].transform;               //all finger bones
            cikc.data.tipRotationWeight = 1;                                        //With the Mixamo model, the bones start at the anchorpoint
        }
        
        //Now we set the IK_Target transforms
        if (XRIK.transform.childCount == 0) Debug.LogError("Can't find the IK_Target childobject for " + XRIK.name);
        else
        {
            XRIK.transform.GetChild(0).transform.position = XRIK.GetComponent<ChainIKConstraint>().data.tip.transform.position;
            XRIK.transform.GetChild(0).transform.rotation = XRIK.GetComponent<ChainIKConstraint>().data.tip.transform.rotation;
        }
    }


    //Helper function to turn ON/OFF the RigBuilder component 
    //Apparently you can't make runtime changes to IK Constraints without turning off RigBuilder and then turning it on again when you're done!
    public void DisableEnableRigBuilder(bool state)
    {
        Debug.Log((state ? "En" : "Dis") + "able RigBuilder");

        RigBuilder rB = transform.parent.GetComponent<RigBuilder>();
        if (!rB)
            Debug.LogError("RigBuilder not found!");
        else
        {
            foreach (RigLayer rl in rB.layers) rl.active = state;
            rB.enabled = state;
        }
    }
}
