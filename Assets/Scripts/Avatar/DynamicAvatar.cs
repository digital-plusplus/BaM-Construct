using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DynamicAvatar : NetworkBehaviour
{
    // Update is called once per frame
    void Update()
    {
        
    }

    //Clients are told to load the resource and make some changes after which the server instantiates and spawns 
    //This way we could theoretically make changes to resources before they are spawned.
    //JUST SOME TEST CODE THAT DOES A VERTEX DISPOSITION TO CHECK WHETHER CLIENT CAN ACTUALLY DO THAT PRE SPAWN!
    [ClientRpc]
    void SetCubeColorClientRpc(string objectToLoad, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("SetCubeColorClientRpc called to change " + objectToLoad);
        GameObject rTL = Resources.Load(objectToLoad) as GameObject;

        //TEST: CHECK WHETHER WE CAN CHANGE THE COLOR PRE-SPAWNING
        Mesh alphaS = new Mesh();
        Transform aS = rTL.transform.Find("Alpha_Surface");
        SkinnedMeshRenderer sMR = aS.GetComponent<SkinnedMeshRenderer>();

        alphaS = Instantiate(sMR.sharedMesh);
        if (!alphaS) Debug.LogError("No SharedMesh"); else Debug.Log(alphaS);

        Vector3[] newVerts = new Vector3[alphaS.vertexCount];
        newVerts = alphaS.vertices;             //make a copy, cant make changes to vertices directly!
        newVerts[10] = new Vector3(0, 0, 2);    //deform
        sMR.sharedMesh.vertices = newVerts;             //write back

        //sMR.sharedMaterial.SetColor("_Color", Color.green);    //WORKS BUT PERMANENTLY CHANGES THE OBJECT IN THE REPOSITORY!
    }
}
