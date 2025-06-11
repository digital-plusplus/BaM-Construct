using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class SyncAllBlendShapes : MonoBehaviour
{
    //Ensure to drag/drop the Gen9.shape GameObject and Gen9 Mouth GameObject in Inspector
    [SerializeField]
    GameObject          gen9Shape, gen9Mouth, gen9Brow, gen9Lash, gen9Tear, gen9Eyes;
    SkinnedMeshRenderer gen9Shape_SMR, gen9Mouth_SMR, gen9Brow_SMR, gen9Lash_SMR, gen9Tear_SMR, gen9Eyes_SMR;
    Mesh                gen9Shape_Mesh;
    Transform           xrCamPos;       //link to the position of the XR camera (ie. the eyes of the player)

    Animator avtAnimator;

    //Blink timer
    [SerializeField]
    float blinkInterval = 5;        //5 seconds default

    [SerializeField]
    float blinkDuration = 1f;
    float timeRemaining, timeHold; 

    [SerializeField]
    bool enableBlink = false;       //Enable/Disable
    bool isBlinking;

    [SerializeField]
    bool lookAtMe = false;          //eyes of NPC try to follow you and look at you

    //Definitions of some states
    const int BLINK = 10;
    const int SMILE = 12;
    const int SMILE2 = 14;
    const int SERIOUS = 15;
    const int LOOKH = 9;
    const int LOOKV = 8;
    const int ON = 100;
    const int OFF = 0;
    const bool HOR = true;
    const bool VERT = false;
    

    [SerializeField]
    GameObject npcLEye, npcREye;

   

    // Start is called before the first frame update
    void Start()
    {
        avtAnimator = GetComponent<Animator>();

        gen9Shape_SMR = gen9Shape.GetComponent<SkinnedMeshRenderer>();
        gen9Mouth_SMR = gen9Mouth.GetComponent<SkinnedMeshRenderer>();
        gen9Brow_SMR = gen9Brow.GetComponent<SkinnedMeshRenderer>();
        gen9Lash_SMR = gen9Lash.GetComponent<SkinnedMeshRenderer>();
        gen9Tear_SMR = gen9Tear.GetComponent<SkinnedMeshRenderer>();
        gen9Eyes_SMR = gen9Eyes.GetComponent<SkinnedMeshRenderer>();
        gen9Shape_Mesh = gen9Shape_SMR.sharedMesh;

        //Blink timer
        enableBlink = true;
        isBlinking = false;
        timeRemaining = blinkInterval;
        timeHold = blinkDuration;

        //Eye tracking
        //xrCamPos = GameObject.Find("Camera Offset").transform;      //player eyes
        xrCamPos = GameObject.Find("Main Camera").transform;      //player eyes
    }


    // Update is called once per frame
    void Update()
    {
        //Synchronize all blendshapes at each cycle
        if (avtAnimator.GetBool("isTalking") )   //add other activities where the blendshape must be updated
            SyncBlendShapesCycle();

        //Update blinking
        if (enableBlink) BlinkManager();

        //Update Eye tracking
        if (lookAtMe)
        {
            EyeToXRTrackingUpdate();         
            //EyeToXRTrackingUpdate(VERT);        //up/down
        }

    } 


    //run a cycle to sync all BSs
    private void SyncBlendShapesCycle()
    {
        for (int i = 0; i < gen9Shape_Mesh.blendShapeCount; i++)    //for each blendshape
        {
            float bsVal = gen9Shape_SMR.GetBlendShapeWeight(i);
            gen9Mouth_SMR.SetBlendShapeWeight(i, bsVal);
            gen9Brow_SMR.SetBlendShapeWeight(i, bsVal);
            gen9Lash_SMR.SetBlendShapeWeight(i, bsVal);
            gen9Tear_SMR.SetBlendShapeWeight(i, bsVal);
            gen9Eyes_SMR.SetBlendShapeWeight(i, bsVal); 
        }
    }


    //Manages blinking of the NPC
    private void BlinkManager()
    {
        //Blink timer
        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
        }
        else
        {
            if (!isBlinking) BlendFace(BLINK,ON);      //blink on
            
            isBlinking = true;                                      //avoid constant setting the weight at each cycle
            timeHold -= Time.deltaTime;

            if (timeHold < 0)
            {
                if (isBlinking) BlendFace(BLINK, OFF);    //blink off
                
                isBlinking = false;
                timeHold = blinkDuration;
                timeRemaining = blinkInterval * UnityEngine.Random.Range(-blinkInterval / 4, blinkInterval / 4);      //add some random noise
            }
        }
    }


    //NPC eyes follow the player
    //
    private void EyeToXRTrackingUpdate()
    {
        float bsoH, bsoV;
        Transform lE = npcLEye.transform;
        
        Vector3 delta = Quaternion.Inverse(lE.rotation) * (xrCamPos.position - lE.position);     //Compensate for NPC rotation
        Vector3 deltaH, deltaV;

        deltaH = delta; deltaH.y = 0;        //XZ plane for horizontal tracking
        deltaV = delta; deltaV.x = 0;        //YZ plane for vertical tracking

        bsoH = Mathf.Min(Mathf.Max(100 * Mathf.Asin(deltaH.x) / delta.magnitude, -100), 100);
        bsoV = Mathf.Min(Mathf.Max(200 * Mathf.Asin(deltaV.y) / delta.magnitude, -100), 100);
        
        //Debug.Log(bsoH + " " + bsoV);

        BlendFace(LOOKH, (int)bsoH);
        BlendFace(LOOKV, (int)bsoV);
    }



    public void BlendFace(int what, int value)
    {
        gen9Shape_SMR.SetBlendShapeWeight(what, value);
        SyncBlendShapesCycle();
    }
}
