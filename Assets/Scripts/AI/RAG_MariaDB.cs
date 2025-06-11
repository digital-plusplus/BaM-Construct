using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System;


/// <summary>
/// Connect to MariaDB RAG database via a REST API PHP script
/// 20250101 DigitalPlusPlus
/// IMPORTANT: The embedding service is called in the Python script and the get_similarX PHP scripts
///             so there are NO API KEYS used in this script!
/// </summary>
/// 

public class ForceAcceptAllCertificates : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true; // Accept all certificates
    }
}

public class RAG_MariaDB : MonoBehaviour
{   
    [SerializeField] string apiURI;
    [Range(0, 1f)] [SerializeField] float similarityTreshold = 0.47f;       //below results will not be used
    [SerializeField] bool debug = false;

    AI_Orchestrator aiO;

    public void Init()
    {
        //First get a link to the AI Orchestrator
        aiO = GetComponent<AI_Orchestrator>();
        if (!aiO)
        {
            Debug.LogError("Fatal: RAG_MariaDB component cannot find the AI Orchestrator!");
            return;
        }

        //Debug purposes - example call
        //Debug.Log(await GetContext("OpenXR", 2));
    }


    //====================================
    //Simplified call to GetTopChunks == USE THE AI ORCHESTRATOR METHOD TO CALL THIS FUNCTION VS DIRECTLY CALLING THIS!
    //  this method just retrieves all content-fields from the RAGResponses and concates them in a single string
    //  this method is called by the LLM components but should be called via the AI Orchestrator which generalizes GetContext in case there will be more 
    //  RAG services in the future.
    //====================================
    public async Task<string> GetContext(string prompt, int topn)
    {
        string rtnvalue="";
        RAGResponse[] rr = await GetTopChunks(prompt, topn);

        if (rr == null) return rtnvalue;

        if (rr.Length == 0) return "";  //enforce empty string just to be sure
        int cnt = 0;
        for (int i=0;i<rr.Length;i++)
        {
            //Only include the results above the treshold, if the treshold==0 we include all results
            if ((rr[i].similarity >= similarityTreshold) || (similarityTreshold == 0))
            {
                rtnvalue += rr[i].content + "\n";
                cnt++;
            }
        }
        if (debug)
            Debug.Log("RAG:" + rr.Length + "chunks received, "+cnt+" chunks similarity are above treshold value");
        return rtnvalue;
    }


    //Retrieves all chunks - calls the GAIAPI REST API service and tells the API how many chunks max to return.
    // - chunks are returned as an array of output messages
    public async Task<RAGResponse[]> GetTopChunks(string prompt, int topn)
    {
        RAGRequest requestBody = new RAGRequest();

        requestBody.prompt = prompt;
        requestBody.topn = topn;
        
        string jsonRAGRequest = JsonUtility.ToJson(requestBody);
        if (debug) 
            Debug.Log(jsonRAGRequest);
        
        //For DEVELOPMENT PURPOSES ONLY! ACCEPT INVALID CERTIFICATES WHEN TESTING A LOCAL HTTPS SERVER WITHOUT OFFICIAL CERT
        UnityWebRequest request = new UnityWebRequest(apiURI, "POST");
        request.certificateHandler = new ForceAcceptAllCertificates();
        //For DEVELOPMENT PURPOSES ONLY! ACCEPT INVALID CERTIFICATES WHEN TESTING A LOCAL HTTPS SERVER WITHOUT OFFICIAL CERT

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRAGRequest);

        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        await request.SendWebRequest();

        //Now we process the responses from RAG
        if (request.result == UnityWebRequest.Result.Success)
        {
            if (debug) 
                Debug.Log("RAG: SUCCESS!");
            string responseText = request.downloadHandler.text;
            responseText = "{\"responses\":" + responseText + "}";
            if (debug) 
                Debug.Log("RAG:" + responseText);

            RAGResponseArray responsesWrapper = JsonUtility.FromJson<RAGResponseArray>(responseText);
            return responsesWrapper.responses;
        }
        else
        {
            Debug.LogError("RAG API Request failed: " + request.error);
            aiO.Say("Ik can't access the RAG database so my answers will be incomplete");
        }

        return null;
    }


    //GAIAPI Input and Output schema classes - JSON formatted
    public class RAGRequest         //Input schema
    {
        public string prompt;       //String with the question for the RAG database
        public int topn;            //Max # chunks to retrieve from the database
    }

    [Serializable]
    public class RAGResponse        //Output schema for 1 chunk
    {
        public int id;
        public string content;
        public float similarity;
    }

    [Serializable]
    public class RAGResponseArray   //Chunks are returned by the API as an array of output messages
    {
        public RAGResponse[] responses;
    }
}
