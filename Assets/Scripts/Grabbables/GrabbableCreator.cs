using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GrabbableCreator : NetworkBehaviour
{
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private int maxObjectsToSpawn = 5;
    [SerializeField] private GameObject bat;
    [SerializeField] private Vector3 batPosition;
    [SerializeField] private float numberOfBats = 2;
    [SerializeField] private GameObject ball;
    [SerializeField] private Vector3 ballPosition;
    

    public void SpawnGrabbables ()
    {
        //spawn the objects , only valid for host/server
        if (IsServer)
        {
            Debug.Log("Spawning " + maxObjectsToSpawn + " grabbables ...");
            Vector2 placementAreaMin, placementAreaMax;
            placementAreaMin.x = transform.position.x - (transform.localScale.x) / 2;
            placementAreaMax.x = transform.position.x + (transform.localScale.x) / 2;
            placementAreaMin.y = transform.position.z + (transform.localScale.z) / 2;
            placementAreaMax.y = transform.position.z - (transform.localScale.z) / 2;

            //Instantiate and Spawn resources
            for (int i=0;i<maxObjectsToSpawn; i++)
            {
                GameObject go = Instantiate(prefabs[Random.Range(0, prefabs.Length)], Vector3.zero, Quaternion.identity);
                go.transform.position = new Vector3(
                                                Random.Range(placementAreaMin.x, placementAreaMax.x),
                                                transform.position.y + (transform.localScale.y / 2) + go.transform.localScale.y/2,
                                                Random.Range(placementAreaMin.y, placementAreaMax.y));

                go.GetComponent<NetworkObject>().Spawn();
            }

            //Spawn a bat if needed
            if (bat)
            {
                for (int i = 0; i < numberOfBats; i++)
                {
                    batPosition.z += i * 0.2f;  //shift all bats a little away from eachother
                    GameObject batN = Instantiate(bat, batPosition, Quaternion.Euler(90, 0, 0));
                    batN.GetComponent<NetworkObject>().Spawn();
                }
            }

            //Spawn a ball if needed
            if (ball)
            {
                GameObject balN = Instantiate(ball, ballPosition, Quaternion.identity);
                balN.GetComponent<NetworkObject>().Spawn();
            }
        }

    }
}
