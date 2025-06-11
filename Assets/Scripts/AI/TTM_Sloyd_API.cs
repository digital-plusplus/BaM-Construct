using System;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Script that handles REST API calls to Sloyd.ai
/// </summary>

public class TTM_Sloyd_API : MonoBehaviour
{
    private enum SelectAIPromptModifiers
    {
        basic, recoloring, texturing
    }

    private string CLIENTID;       //Secure stuff entered in the Inspector
    [SerializeField] private string CLIENTSECRET;                               //Secure Stuff
    [Range(0f, 1f)] [SerializeField] float LevelofDetail = 0.5f;    //LOD
    [SerializeField] private SelectAIPromptModifiers selectAIPromptModifiers;   //texture
    [SerializeField] private Vector3 spawnPosition;     //Where do we spawn the object
    [SerializeField] private bool spokenFeedbackOnChanges;  //do we want the NPC to provide spoken feedback?

    const string TTM_API_URI = "https://api.sloyd.ai";
    public const string CREATE_ENDPOINT = "/create";
    public const string EDIT_ENDPOINT = "/edit";

    Animator avtAnimator;
    private string selectedPMod;
    private string lasterInteractionId;

    AI_Orchestrator aiO;
    API_Keys api_Keys;

    [SerializeField] bool debug;
    const string DEBUG_PREFIX = "LLM_GOOGLE: ";             //prefix we use for debugging

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Init()
    {
        //We first retrieve the API keys from the API Key component
        api_Keys = GetComponent<API_Keys>();
        if (!api_Keys)
            Debug.LogError(DEBUG_PREFIX + "Cannot find the API Keys component, please check the Inspector!");
        else
        {
            CLIENTID = api_Keys.GetAPIKey("Sloyd_CLIENTID");
            CLIENTSECRET = api_Keys.GetAPIKey("Sloyd_CLIENTSECRET");
        }

        if ((CLIENTID == null) || (CLIENTSECRET == null))
            Debug.LogWarning(DEBUG_PREFIX + "Warning: API key not found, check API Key File!");

        aiO = GetComponent<AI_Orchestrator>();
        if (!aiO)
        {
            Debug.LogError("AI Orchestrator component not found!");
            return;
        }

        avtAnimator = GetComponent<Animator>();
        selectedPMod = selectAIPromptModifiers.ToString();
        if (selectedPMod == "basic") selectedPMod = "";     //default
        if (spawnPosition == null) spawnPosition = transform.position;


        //DEBUG ONLY
        //Create("Fighter plane");
        //lasterInteractionId = "ef0b5518-2b9b-4158-8cf2-ae515c6ac41f";
        //Edit("Wing tapered");
    }


    //Public async call without return value - can be called from a non-async function if desired!
    public async void Create(string prompt)
    {
        await _Create(prompt);
    }

    //Public async call without return value - can be called from a non-async function if desired!
    public async void Edit(string prompt)
    {
        await _Edit(prompt);
    }


    //LOD manipulation
    public void IncreaseLOD()   //add 50%
    {
        LevelofDetail += (1 - LevelofDetail) / 2;
    }

    public void DecreaseLOD()   //reduce by 50%
    {
        LevelofDetail = LevelofDetail / 2; 
    }


    public async Task<GameObject> _Create(string prompt)        //CREATE endpoint!
    {
        TTMData ttmData = new TTMData();
        GameObject rtnObject = null;

        Debug.Log("TTM Create:" + prompt);

        //Pass on the AI parameters 
        ttmData.Prompt = prompt;
        ttmData.ClientId = CLIENTID;
        ttmData.ClientSecret = CLIENTSECRET;
        ttmData.LOD = LevelofDetail;
        ttmData.AiPromptModifiers = selectedPMod;

        string jsonRequestBody = JsonUtility.ToJson(ttmData);

        //Inform user 
        aiO.Say("Generating a 3D object for you!");
        avtAnimator.SetBool("isPainting", true);

        //Web stuff
        UnityWebRequest request = new UnityWebRequest(TTM_API_URI + CREATE_ENDPOINT, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

        //Sloyd.ai does NOT use HTTP headers

        await request.SendWebRequest(); //send the message to the API
        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;
            TTMResponse sloydResponse = JsonUtility.FromJson<TTMResponse>(responseText);
            lasterInteractionId = sloydResponse.InteractionId;
            rtnObject = await ImportGLTFModel(sloydResponse.ModelData, lasterInteractionId);
        }
        else Debug.Log("TTM API request failed: " + request.error);

        //Update animation - do later!
        avtAnimator.SetBool("isPainting", false);
        return (rtnObject);
    }


    public async Task<GameObject> _Edit(string prompt)      //EDIT endpoint!
    {
        TTMEDITData ttmData = new TTMEDITData();
        GameObject rtnObject = null;

        //check whether there actually is an InteractioID
        if (lasterInteractionId=="")
        {
            Debug.LogError("Called Sloyd-Edit endpoint without a InteractionID!");
            return null;
        }

        Debug.Log("TTM Edit:" + prompt);

        //Pass on the AI parameters 
        ttmData.Prompt = prompt;
        ttmData.InteractionId = lasterInteractionId;
        ttmData.ClientId = CLIENTID;
        ttmData.ClientSecret = CLIENTSECRET;

        string jsonRequestBody = JsonUtility.ToJson(ttmData);

        //Inform user - do later!
        aiO.Say("Updating 3D Object!");
        avtAnimator.SetBool("isPainting", true);

        UnityWebRequest request = new UnityWebRequest(TTM_API_URI + EDIT_ENDPOINT, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

        //Sloyd.ai does NOT use HTTP headers

        await request.SendWebRequest(); //send the message to the API
        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;
            TTMEDITResponse sloydResponse = JsonUtility.FromJson<TTMEDITResponse>(responseText);
            
            if (spokenFeedbackOnChanges) aiO.Say(sloydResponse.ResponseMessage);
            rtnObject = await ImportGLTFModel(sloydResponse.ModelData, sloydResponse.InteractionId);
        }
        else Debug.Log("TTM API request failed: " + request.error);

        //Update animation 
        avtAnimator.SetBool("isPainting", false);
        return (rtnObject);
    }


    //To delete a 3D generated object from the scene
    public bool Delete()
    {
        GameObject objectToDestroy = GameObject.Find(lasterInteractionId);
        if (objectToDestroy)
        {
            Destroy(objectToDestroy);
            aiO.Say("Deleted 3D object!");
            return true;
        }
        return false;
    }


    async Task<GameObject> ImportGLTFModel(string model, string id)
    {
        var gltf = new GltfImport();                            //GLTFast library object
        GameObject rtnObject = null;

        GameObject theObjectAlreadyExists = GameObject.Find(id);
        if (theObjectAlreadyExists) Destroy(theObjectAlreadyExists);

        bool success = await gltf.LoadGltfJson(model);
        if (success)
        {
            GameObject instantiatedModel = new GameObject(id);
            success = await gltf.InstantiateSceneAsync(instantiatedModel.transform);
            if (success)
            {
                Debug.Log("Object " + instantiatedModel.name + " generated successfully");
                instantiatedModel.transform.position = spawnPosition;
                return (instantiatedModel);
            }
            else
            {
                Destroy(instantiatedModel);
                Debug.Log("Cannot instantiate the GLTF object!");
            }
        }
        else Debug.Log("Cannot load GLTF from JSON!");
        return (rtnObject);
    }


    //JSON Support Classes
    [Serializable]
    public class TTMData                                        //Sloyd CREATE Input Schema
    {
        public string Prompt;
        public string ClientId;
        public string ClientSecret;
        public string ModelOutputType;                          //when empty, we use gltf
        public string ResponseEncoding;                         //when empty, we use json
        public string AiPromptModifiers;
        public float LOD;
    }

    [Serializable]
    public class TTMEDITData                                    //Sloyd EDIT Input Schema
    {
        public string Prompt;
        public string ClientId;
        public string ClientSecret;
        public string ModelOutputType;                          //when empty, we use gltf
        public string ResponseEncoding;                         //when empty, we use json
        public string InteractionId;
    }

    public class TTMResponse                                    //Sloyd CREATE Output Schema
    {
        public string InteractionId;
        public string Name;
        public string ConfidenceScore;
        public string ResponseEncoding;
        public string ModelOutputType;
        public string ModelData;
        public string ThumbnailPreviewExportType;
        public string ThumbnailPreview;
    }

    public class TTMEDITResponse                                //Sloyd EDIT Output Schema
    {
        public string InteractionId;
        public string Name;
        public string ResponseEncoding;
        public string ResponseType;
        public string ModelOutputType;
        public string ModelData;
        public string ResponseMessage;
    }
}
