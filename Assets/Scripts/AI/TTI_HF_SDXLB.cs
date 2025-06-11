using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using UnityEngine.UI;

public class TTI_HF_SDXLB : MonoBehaviour
{
    private string HF_INF_API_KEY;
    const string TTI_API_URI = "https://api-inference.huggingface.co/models/stabilityai/";      //POST URI
    
    private enum SDModel { stable_diffusion_3X5_large_turbo, stable_diffusion_3X5_large , stable_diffusion_xl_base_1X0}
    [SerializeField] SDModel selectedModel;
    string selectedSDString;

    Animator avtAnimator;
    AI_Orchestrator aiO;
    API_Keys api_Keys;

    const string DEBUG_PREFIX = "TTI_HF: ";

    public void Init()
    {
        //We first retrieve the API keys from the API Key component
        api_Keys = GetComponent<API_Keys>();
        if (!api_Keys)
            Debug.LogError(DEBUG_PREFIX + "Cannot find the API Keys component, please check the Inspector!");
        else HF_INF_API_KEY = api_Keys.GetAPIKey("HF_API_Key");

        if (HF_INF_API_KEY == null)
            Debug.LogWarning(DEBUG_PREFIX + "Warning: API key is empty, check Inspector!");


        aiO = GetComponent<AI_Orchestrator>();
        if (!aiO)
        {
            Debug.LogError("AI Orchestrator component not found!");
            return;
        }

        selectedSDString = selectedModel.ToString().Replace('_', '-').Replace('X', '.');
        Debug.Log("You have selected SD model: " + selectedSDString);

        //Debug only
        //GetImage("Image of a dog wearing a helmet");
    }


    public void GetImage(string prompt)
    {
        avtAnimator = GetComponent<Animator>();

        StartCoroutine(SD(prompt));
        Debug.Log("TTI: " + prompt);
    }


    IEnumerator SD(string prompt)
    {
        TTIData ttiData = new TTIData();
        ttiData.inputs = prompt;
        string jsonPrompt = JsonUtility.ToJson(ttiData);

        //Set up the UnityWebRequest
        UnityWebRequest request = new UnityWebRequest(TTI_API_URI+selectedSDString, "POST");
        request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonPrompt));
        request.downloadHandler = new DownloadHandlerTexture();

        //Headers
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + HF_INF_API_KEY);

        //Tell user something....
        aiO.Say("Generating image");
        
        avtAnimator.SetBool("isPainting", true);     //pretend you're working hard :)
        // Send the request and decompress the multimedia response
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D img = DownloadHandlerTexture.GetContent(request);
            
            GameObject genImg;
            genImg = Resources.Load<GameObject>("ImageFrame");
            if (!genImg) Debug.Log("Can't load the ImageFrame for the TTI output");
            else
            {
                //generate the image prefab
                GameObject g2 = Instantiate(genImg, transform.position+new Vector3(-1,1.5f,0), Quaternion.identity);    //to the left
                
                //set texture
                Material myNewMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                myNewMaterial.SetTexture("_BaseMap", img);
                g2.GetComponent<MeshRenderer>().material = myNewMaterial;

                avtAnimator.SetBool("isPainting", false);        //done working
            }
        }
        else Debug.LogError("TTI API request failed: " + request.error);

    }

    //JSON Input Class representation
    [Serializable]
    public class TTIData
    {
        public string inputs;        //core only, need to expand with addl. parameters
    }

    //Output data is JPG, no JSON wrapper required
}
