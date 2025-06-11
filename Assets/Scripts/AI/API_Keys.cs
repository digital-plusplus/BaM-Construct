using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class API_Keys : MonoBehaviour
{
    //*************IMPORTANT***************************************************************
    //!! The API Key variables are loaded from a file which is EXCLUDED by .gitignore !!!
    //*************IMPORTANT***************************************************************
    //  Note: ReadOnly attribute is defined in /Scripts/System/ReadOnlyAttributes script!
    
    [SerializeField] public string filePath;              //file + path
    [SerializeField] bool debug;

    AI_Orchestrator aiO;

    private Dictionary<string, string> apiKeys = new Dictionary<string, string>();
    const string DEBUG_PREFIX="API_Keys: ";


    private void Start()
    {
        //Now find the orchestrator
        aiO = GetComponent<AI_Orchestrator>();
        if (!aiO)
        {
            Debug.LogError(DEBUG_PREFIX + "AI Orchestrator component not found!");
            return;
        }


        ReadAPIKeys();

        if (debug) 
            DumpAPIKeys();

        //Now initialize all AI components, AFTER all keys were read so we do have all keys loaded
        aiO.Init();     //will on its turn Init all registered AI components
    }


    //Public method to request the API key for a service, API keys are all in a simple text file 
    public string GetAPIKey(string serviceName)
    {
        return (apiKeys.ContainsKey(serviceName) ? apiKeys[serviceName] : null);
    }


    //Read API keys from a simple text file, this file must NEVER be synchronized by GitHub!
    private void ReadAPIKeys()
    {
        TextAsset textFile = Resources.Load<TextAsset>(filePath);

        if (textFile!=null)
        {
            string[] lines = textFile.text.Split('\n');
            foreach (string line in lines)
            {
                string[] parts = line.Split(':');
                if (parts.Length == 2)
                    apiKeys[parts[0].Trim()] = parts[1].Trim();
                else Debug.LogError(DEBUG_PREFIX + "illegal API key line found, check your API keys file!");
            }
        }
        else Debug.LogError(DEBUG_PREFIX + "API keys file not found!");
    }

    
    //Debug only
    private void DumpAPIKeys()
    {
        foreach (KeyValuePair<string, string> entry in apiKeys)
        {
            Debug.Log(DEBUG_PREFIX+$"Service: {entry.Key}, API Key: {entry.Value}");
        }
    }
}
