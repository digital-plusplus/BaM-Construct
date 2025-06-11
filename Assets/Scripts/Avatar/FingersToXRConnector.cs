using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// IMPORTANT: Finger order MUST be identical for your imported FBX model:
///     1. Index Finger
///     2. Middle Finger
///     3. Ring Finger
///     4. Little Finger
///     5. Thumb
///     MAKE SURE YOU CHECK AND UPDATE THEN WHEN IMPORTING AN FBX HUMANOID MODEL!!!!
/// </summary>
public class FingersToXRConnector : MonoBehaviour
{
    //20240608 fix
    [SerializeField] float fScale = 1;          //Factor to tune stretching of fingers, TUNE THIS FOR EACH AVATAR TO AVOID OVERSTRETCHING OF FINGERDIGITS
    [SerializeField] bool debugBones = false;   //Detailed Debug messages when connecting the bones

    //Containers for all 10 Fingers Transforms for both Avatar and XR Hands
    public Transform[] XRigLeftHandFingers, XRigRightHandFingers;
    public Transform[] AvatLeftHandFingers, AvatRightHandFingers;
    float[] AvtFingerScale;                     //Scalefactor for each finger, assumes left and right hands are identical
    float[] XRLen, AVTLen;                      //scaling variables
    bool fingersAutotune = false;               //We scale the fingers once

    Transform leftPalmBone, rightPalmBone;      //Need the palm bone to refer finger tip positions to
    Transform avLeft, avRight, myXRLH, myXRRH;  //pointers to avatar and XR hands objects

    public GameObject handVisualizer;

  
    //Note we're not using Start() here as we can only call this once the network object is being spawned
    public void Initialize()
    {
        //Fingers - UPDATED!
        AvatLeftHandFingers = new Transform[5];
        AvatRightHandFingers = new Transform[5];
        XRigLeftHandFingers = new Transform[5];
        XRigRightHandFingers = new Transform[5];
        AvtFingerScale = new float[5];

        avLeft = transform.Find("XR IK Rig").Find("Left Arm IK").Find("Left Arm IK_target");
        avRight = transform.Find("XR IK Rig").Find("Right Arm IK").Find("Right Arm IK_target");

        handVisualizer = GameObject.Find("LeftHandDebugDrawJoints");
        if (!handVisualizer) 
            Debug.Log("ERROR: your XR system has no controllers nor hands active!");
    }


    /// <summary>
    /// Determines XR and avatar palm and fingertip transforms so LinkAvtFingersToXROrigin can connect these each Update() cycle
    /// Also determines scale factor for each finger, assuming left and right finger sizes are identical!
    /// </summary>
    public void MapHands()
    {
        XRLen = new float[5];
        AVTLen = new float[5];

        //XR Origin - LEFT
        XRigLeftHandFingers[0] = GameObject.Find("LeftHandDebugDrawJoints").transform.Find("IndexTip").transform;
        XRigLeftHandFingers[1] = GameObject.Find("LeftHandDebugDrawJoints").transform.Find("MiddleTip").transform;
        XRigLeftHandFingers[2] = GameObject.Find("LeftHandDebugDrawJoints").transform.Find("RingTip").transform;
        XRigLeftHandFingers[3] = GameObject.Find("LeftHandDebugDrawJoints").transform.Find("LittleTip").transform;
        XRigLeftHandFingers[4] = GameObject.Find("LeftHandDebugDrawJoints").transform.Find("ThumbTip").transform;


        //XR Origin - RIGHT
        XRigRightHandFingers[0] = GameObject.Find("RightHandDebugDrawJoints").transform.Find("IndexTip").transform;
        XRigRightHandFingers[1] = GameObject.Find("RightHandDebugDrawJoints").transform.Find("MiddleTip").transform;
        XRigRightHandFingers[2] = GameObject.Find("RightHandDebugDrawJoints").transform.Find("RingTip").transform;
        XRigRightHandFingers[3] = GameObject.Find("RightHandDebugDrawJoints").transform.Find("LittleTip").transform;
        XRigRightHandFingers[4] = GameObject.Find("RightHandDebugDrawJoints").transform.Find("ThumbTip").transform;
        
        //Palms of XR Origin
        myXRLH = GameObject.Find("LeftHandDebugDrawJoints").transform.Find("Palm").transform;
        myXRRH = GameObject.Find("RightHandDebugDrawJoints").transform.Find("Palm").transform;

        //Avatar - LEFT
        AvatLeftHandFingers[0] = transform.Find("XR IK Rig").Find("Left Index Finger IK").Find("Left Index Finger IK_target").transform;
        AvatLeftHandFingers[1] = transform.Find("XR IK Rig").Find("Left Middle Finger IK").Find("Left Middle Finger IK_target").transform;
        AvatLeftHandFingers[2] = transform.Find("XR IK Rig").Find("Left Ring Finger IK").Find("Left Ring Finger IK_target").transform;
        AvatLeftHandFingers[3] = transform.Find("XR IK Rig").Find("Left Pinky Finger IK").Find("Left Pinky Finger IK_target").transform;
        AvatLeftHandFingers[4] = transform.Find("XR IK Rig").Find("Left Thumb IK").Find("Left Thumb IK_target").transform;

        //Avatar - RIGHT
        AvatRightHandFingers[0] = transform.Find("XR IK Rig").Find("Right Index Finger IK").Find("Right Index Finger IK_target").transform;
        AvatRightHandFingers[1] = transform.Find("XR IK Rig").Find("Right Middle Finger IK").Find("Right Middle Finger IK_target").transform;
        AvatRightHandFingers[2] = transform.Find("XR IK Rig").Find("Right Ring Finger IK").Find("Right Ring Finger IK_target").transform;
        AvatRightHandFingers[3] = transform.Find("XR IK Rig").Find("Right Pinky Finger IK").Find("Right Pinky Finger IK_target").transform;
        AvatRightHandFingers[4] = transform.Find("XR IK Rig").Find("Right Thumb IK").Find("Right Thumb IK_target").transform;

        //20240607 fix - Palms of hands
        leftPalmBone = transform.Find("XR IK Rig").Find("Left Arm IK").GetComponent<TwoBoneIKConstraint>().data.tip;
        rightPalmBone = transform.Find("XR IK Rig").Find("Right Arm IK").GetComponent<TwoBoneIKConstraint>().data.tip;


        //20240607 - Scaling
        uint fDigit = 0;     //actual digits
        if (debugBones) Debug.Log("Palmbone "+leftPalmBone.name.ToString()+" has " +leftPalmBone.childCount + " children:");
        
        //Some FBX models have palm bones with more than 5 children (eg. anchors etc)
        for (uint i=0; i<leftPalmBone.childCount; i++)
        {
            AVTLen[fDigit] = 0;
            if (leftPalmBone.GetChild((int)i).childCount != 0)    //skip any fingerbones with no children (anchor objects etc)
            {
                //Avatar finger length calculation
                FingerDigit(leftPalmBone, leftPalmBone.GetChild((int)i), fDigit);

                //XR rig finger length calculation
                XRLen[fDigit] = XRFingerDigit(fDigit);
                if (XRLen[fDigit]==0)
                {
                    if (debugBones) Debug.LogError("XR fingerlength returned 0");
                    XRLen[fDigit] = AVTLen[fDigit];                 //avoid division by 0 and assume a default value of 1 and
                                                                    //enforce scaling of 1 - best guess
                }
                
                //Determine scale for each finger
                AvtFingerScale[fDigit] = XRLen[fDigit] / AVTLen[fDigit];
                if (debugBones) Debug.Log("AVT: finger " + fDigit + " len=" + AVTLen[fDigit] + ", XR len=" + XRLen[fDigit] + ", scale="+AvtFingerScale[fDigit]);

                fDigit++;
            }
            else 
                if (debugBones) Debug.Log("Skipping " + leftPalmBone.GetChild((int)i).name);
        }

        //We should have exactly 5 fingers
        if (fDigit != 5) Debug.LogError("Not all fingers were scaled properly! Incompatible bonestructure!");
    }


    /// <summary>
    /// Code to determine length of XR rig fingers
    /// </summary>
    /// <param name="i">Index of the finger </param>
    /// <returns>length of finger indexed by i</returns>
    float XRFingerDigit(uint i)
    {
        Vector3 p, l0, l1, l2, l3, l4;
        string[] fingers;
        fingers = new string[5];

        //ORDER IS CRUCIAL!
        fingers[0] = "Index";
        fingers[1] = "Middle";
        fingers[2] = "Ring";
        fingers[3] = "Little";
        fingers[4] = "Thumb";

        //Get the position of each of the finger digits
        p = myXRLH.position;
        l0 = GameObject.Find(fingers[i]+"Metacarpal").transform.position;
        l1 = GameObject.Find(fingers[i]+"Proximal").transform.position;
        l2 = (i==4 ? new Vector3(0, 0, 0) : GameObject.Find(fingers[i] + "Intermediate").transform.position);   //Thumb has no intermediate digit
        l3 = GameObject.Find(fingers[i]+"Distal").transform.position;
        l4 = GameObject.Find(fingers[i]+"Tip").transform.position;

        //Add up the length for each finger digit
        if (i!=4)
            return ( Size(l0 - p) + Size(l1 - l0) + Size(l2 - l1) + Size(l3 - l2) + Size(l4 - l3) );
        else
            return ( Size(l0 - p) + Size(l1 - l0) + Size(l3 - l1) + Size(l4 - l3) );    //skip Intermediate

    }


    /// <summary>
    /// Code to determine length of finger in order to determine scale factor XR vs Avatar for each finger
    /// This should result in an accurate finger tracking in XR when using hands vs controllers
    /// </summary>
    /// <param name="p">parent</param>
    /// <param name="o">child</param>
    /// <param name="finger">index of finger used</param>
    /// <param name="sel">Selector - true: XR, false: avatar </param>
    void FingerDigit(Transform p, Transform o, uint finger )
    {
        if (debugBones) Debug.Log(o.name);

        if (o.childCount != 0) 
            FingerDigit(o, o.GetChild(0), finger);      //Recursive DFS, assumes each digit has only 0 or 1 children -> Humanoid!
        
        AVTLen[finger] += Size(p.position - o.position);
    }


    //Helper to determine length of a Vector3, magnitude doesnt always provide the right result
    float Size(Vector3 vin)
    {
        return (Mathf.Sqrt(vin.x * vin.x + vin.y * vin.y + vin.z * vin.z));
    }


    //Main function to update finger movement, called EACH cycle!
    public void LinkAvtFingersToXROrigin(Transform avatarHand, float scale)
    {
        if (!fingersAutotune)           //20240607 fix - We do this only once and only when we use the hands
        {
            MapHands();
            fingersAutotune = true;
        }

        for (int i = 0; i < 5; i++)     //for each finger
        {
            if (avatarHand == avLeft)
            {
                AvatLeftHandFingers[i].position = leftPalmBone.position + fScale*(XRigLeftHandFingers[i].position - myXRLH.position) / AvtFingerScale[i];
                //AvatLeftHandFingers[i].rotation = avatarHand.rotation; //20240607 fix - not needed
            }
           
            if (avatarHand == avRight)
            {
                AvatRightHandFingers[i].position = rightPalmBone.position + fScale*(XRigRightHandFingers[i].position - myXRRH.position) / AvtFingerScale[i];
                //AvatRightHandFingers[i].rotation = avatarHand.rotation; //20240607 fix - not needed
            }
        }
        
    }
}
