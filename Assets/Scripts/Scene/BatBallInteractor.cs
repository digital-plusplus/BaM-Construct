using Unity.Netcode;
using UnityEngine;

public class BatBallInteractor : NetworkBehaviour
{
    [SerializeField] float hitForce=3f;                //hitForce to shoot off the ball
    [SerializeField] float velocityDetectionScaling = 50;              //velocityDetectionScalings the velocity output of the bat
    [Range(0f,1f)][SerializeField] float velocityDetectionSensitivity=0.05f;       //Noise Gate: value between 0 and 1 to detect velocity
    
    Rigidbody bRB;
    private Vector3 oldPos, dP;
    Transform rigPosition;
    Vector3 hit;
    AudioSource smash;
    ulong clientId;
    

    private void Start()
    {
        //Get links
        bRB = GetComponent<Rigidbody>();
        rigPosition = GameObject.Find("XR Origin (XR Rig)").transform;      //We need this to ensure the velocity vector is independent of avatar movements

        //Initialize
        oldPos = bRB.position;
        dP = Vector3.zero;

        smash = GetComponent<AudioSource>();    //Link to audio 

        //First lets see who is holding the bat, this should be my client ID, we will need that when we detect collisions
        clientId = GameObject.Find("Network Manager").GetComponent<NetworkManager>().LocalClientId;  //find out which client (locally) is smashing the ball
    }


    private void Update()
    {
        //Detect velocity
        dP = oldPos - (bRB.position-rigPosition.position);
        hit = GetDirection(dP);

        //End detect velocity
        oldPos = bRB.position-rigPosition.position;  //ensure we don't include the position and velocity of the XR Rig
    }


    //Make a delta (x,y,z) calculation == velocity vector
    public Vector3 GetDirection(Vector3 deltaP)
    {
        deltaP.x = -deltaP.x;       //Invert x
        deltaP.z = -deltaP.z;       //Invert z
        return (deltaP.magnitude>velocityDetectionSensitivity ? deltaP*velocityDetectionScaling : Vector3.zero);  //Only return a value>0 if we are above the velocityDetectionSensitivity gate level
    }


    //Detect collision between ball and the bat, we can't use OnTriggerEnter as that requires the Bat to be set with isTrigger
    //  and then we can't grab the bat anymore 
    private void OnCollisionEnter(Collision collision)
    {
        //only intercept for the bat that we hold (ie. we own) <= this is crucial otherwise all clients will execute this on any collision with any bat with the ball
        if (OwnerClientId != clientId) return;          
        
        //Check whether we hit the ball, that means the Ball must have a Tag set to "Ball"!
        if (collision.collider.CompareTag("Ball"))
        {
            smash.Play();       //play a smash-sound

            //We take ownership of the ball first and then we slam it !
            TakeBallOwnershipServerRpc(collision.collider.gameObject.GetComponent<NetworkObject>(), clientId);
            collision.collider.GetComponent<Rigidbody>().linearVelocity = (hit + new Vector3(0, 0.5f, 0)) * hitForce;    //it will go up when we hit it
        }
    }


    //Ask the server for ownership of the ball so we can change its direction and speed
    [ServerRpc(RequireOwnership = false)]
    public void TakeBallOwnershipServerRpc(NetworkObjectReference ball, ulong clientID)
    {
        if (!IsServer) return;

        if (ball.TryGet(out NetworkObject netObj))
        {
            Debug.Log(netObj.name +" ownership is now " + clientID);
            netObj.ChangeOwnership(clientID);
        }
    }

}
