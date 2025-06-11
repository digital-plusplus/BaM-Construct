using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class LLM_Google : MonoBehaviour
{
    private string apiKey;
    const string apiURI = "https://generativelanguage.googleapis.com/v1beta/models/";

    private enum LLMModel
    {
        gemini_2X0_flash, gemini_2X0_flash_lite, gemini_2X5_pro_preview_03_25, gemini_1X5_pro, gemini_1X5_flash, gemini_1X5_flash_8b, gemma_3_1b_it, gemma_3_4b_it, gemma_3_12b_it, gemma_3_27B_it, gemma2_2b_it, gemma_2_9b_it, gemma_2_27b_it
    }

    [SerializeField]
    private LLMModel selectedModel;
    string selectedLLMString;
    private string LLMresult = "Waiting";

    [SerializeField]
    private int maxNumberOfWords = 0;                     //0 means no limits to the length of the response

    //NEW!
    [SerializeField]
    private string whoAmI = "nobody";

    [SerializeField]
    private string context;

    [SerializeField]
    private bool closedContext;

    List<Content> messageHistory;
    AI_Orchestrator aiO;
    API_Keys api_Keys;

    [SerializeField] bool debug;
    const string DEBUG_PREFIX = "LLM_GOOGLE: ";             //prefix we use for debugging
    
    Content systemInstruction = new Content();              //Google does not have a system message but rather uses a system_instruction preceeding the regular contents array        


    public void Init()
    {
        string prompt;
        DateTime currentDate = DateTime.Now;

        //We first retrieve the API keys from the API Key component
        api_Keys = GetComponent<API_Keys>();
        if (!api_Keys)
            Debug.LogError(DEBUG_PREFIX + "Cannot find the API Keys component, please check the Inspector!");
        else apiKey = api_Keys.GetAPIKey("Google_API_Key");

        if (apiKey == null)
            Debug.LogWarning(DEBUG_PREFIX + "Warning: API key not found, check API Key File!");

        //Now find the orchestrator
        aiO = GetComponent<AI_Orchestrator>();
        if (!aiO)
        {
            Debug.LogError(DEBUG_PREFIX + "AI Orchestrator component not found!");
            return;
        }

        selectedLLMString = selectedModel.ToString().Replace('_', '-').Replace('X', '.');
        if (debug)
            Debug.Log(DEBUG_PREFIX + "You have selected LLM: " + selectedLLMString);

        //CONSTRUCT PROMPT - STEP 1: WHO IS THIS
        prompt = "You are " + whoAmI;

        //STEP 2: HOW LONG CAN THE RESPONSE BE
        if (maxNumberOfWords > 0)
            prompt += "\nAnswer all questions in maximum " + maxNumberOfWords + " words\n";

        //STEP 3: GIVE IT A NOTION OF TIME & AVOID IT REINTRODUCING ITSELF
        prompt += "\nToday is " + currentDate.ToShortDateString();
        prompt += "\nYou can only mention your name once in your anwsers, unless you are specifically asked for your name.\n";    //to avoid it keeps introducing itself

        //STEP4: NOW WE ADD THE CONTEXT
        prompt += CreatePromptContext(context);

        if (debug)
            Debug.Log(DEBUG_PREFIX + prompt);

        messageHistory = new List<Content>();

        //UPDATED: Initialize the conversation history with a system_message, different than Groq cloud!
        //AppendConversation(prompt, "system_message");
        systemInstruction.role = "model";
        systemInstruction.parts = new Part[]
        {
            new Part { text = prompt }
        };
    }


    //Public method accessed by the AI Orchestrator
    public void TextToLLM(string mesg, string context)       
    {
        StartCoroutine(TalkToLLM(mesg, context));
    }


    //Creates the context for the initial System message and for any consecutive RAG contexts if applicable
    private string CreatePromptContext(string input)
    {
        string prompt = "";
        if (input != "")
        {
            prompt += "\nAnswer the question based on the following context:\n===\n";
            prompt += input;
            prompt += "\n===";
            prompt += "\nYou can search the web if the user asks you to SEARCH THE WEB or SEARCH ONLINE!";  //ADDED FOR WEBSEARCH!
            if (closedContext)
                prompt += "\nIf you can't find the answer in the context then you respond with: 'I really have no idea' or 'I don't know, sorry!' or 'Uuuhm, dunno!'";

        }
        return prompt;
    }


    private IEnumerator TalkToLLM(string mesg, string context)
    {
        RequestData requestBody = new RequestData();

        //Now we check for context! Gemini has a 1M token window so we can safely amend all previous "user" and "model" messages
        string tmpContext = CreatePromptContext(context);
        string promptWithContext = mesg;

        if (debug) Debug.Log(DEBUG_PREFIX + "MESSAGE=" + mesg + "CONTEXT=" + tmpContext);

        if (tmpContext != "") promptWithContext += tmpContext;      //If there was context, add it to the prompt
        AppendConversation(promptWithContext, "user");              //Add the context to the prompt for RAG

        requestBody.system_instruction = systemInstruction;         //Initialize the conversation with a system_instruction which is similar to a system message in LLaMa models
        requestBody.contents = messageHistory.ToArray();            //Add the complete conversation history

        string jsonRequestBody = JsonUtility.ToJson(requestBody);
        LLMresult = "Waiting";
        if (debug)
            Debug.Log(DEBUG_PREFIX + jsonRequestBody);

        string toSend = apiURI + selectedLLMString + ":generateContent?key=" + apiKey;                      //Google sends the API key as a PUT parameter vs a http header!
        UnityWebRequest request = new UnityWebRequest(toSend, "POST");
        if (debug)
            Debug.Log(DEBUG_PREFIX + " using URI: " + toSend);

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");       

        //Ready to fire off the HTTP request to the API!
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;
            GeminiResponse geminiCS = JsonUtility.FromJson<GeminiResponse>(responseText);
            LLMresult = geminiCS.candidates[0].content.parts[0].text;                                       //Assuming there is 1 Candidate and 1 Part in the response!
            if (debug)
                Debug.Log(DEBUG_PREFIX + LLMresult);

            //now lets call TTS via a single call to the central AI Orchestrator!
            aiO.Say(LLMresult);
        }
        else Debug.LogError(DEBUG_PREFIX + "LLM API Request failed: " + request.error);

        //replace last message by removing the context and keep the prompt only - to avoid LLM prompt overload
        AppendConversation(LLMresult, "model");                                                             //In Gemini we have a 1M token window so we store both user and model history!
    }


    private void AppendConversation(string mesg, string myRole)
    {
        Content newMesg = new Content
        {
            role = myRole,
            parts = new Part[]
            {
                new Part { text = mesg }
            }
        };
        messageHistory.Add(newMesg);
    }


    /// ================================
    /// JSON structures for Google API
    /// ================================

    //REQUESTS
    [Serializable] // PARTS section
    public class Part
    {
        public string text;
    }

    [Serializable] // PARTS are encapsulated by multiple CONTENT
    public class Content
    {
        public string role;
        public Part[] parts; // Array to match JSON []
    }


    [Serializable] // array of CONTENT
    public class RequestData
    {
        public Content system_instruction;          // Single system_instruction field
        public Content[] contents;                  // Array of Parts and roles
    }

   
    //RESPONSES
    [Serializable]
    public class ResponsePart
    {
        public string text;
    }

    [Serializable]
    public class ResponseContent
    {
        public ResponsePart[] parts;
        public string role;
    }

    [Serializable]
    public class Candidate
    {
        public ResponseContent content;
        // Add other fields if needed: finishReason, index, safetyRatings, etc.
    }

    [Serializable]
    public class GeminiResponse
    {
        public Candidate[] candidates;
        // Add promptFeedback if needed
    }
}




