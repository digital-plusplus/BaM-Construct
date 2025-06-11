using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class LLM_Groq : MonoBehaviour
{
    private string apiKey;
    const string apiURI = "https://api.groq.com/openai/v1/chat/completions";

    private enum LLMModel { gemma2_9b_it, deepseek_r1_distill_llama_70b, llama_3X1_8b_instant, llama_3X3_70b_versatile, mistral_saba_24b, allam_2_7b }


    [SerializeField]
    private LLMModel selectedModel;
    string selectedLLMString;
    private string LLMresult = "Waiting";

    [SerializeField]
    private bool shortResponse;

    [SerializeField]
    private int maxNumberOfWords = 0;  //Amount of words the LLM is allowed to respond in

    //NEW!
    [SerializeField]
    private string whoAmI = "Nobody";

    [SerializeField]            //Only used when we don't use RAG
    private string context;


    [SerializeField]
    private bool closedContext;

    List<Message> messageHistory;
    AI_Orchestrator aiO;
    API_Keys api_Keys;

    [SerializeField] bool debug;
    const string DEBUG_PREFIX = "LLM_GROQ: ";               //prefix we use for debugging


    // Start is called before the first frame update
    public void Init()
    {
        string prompt;
        DateTime currentDate = DateTime.Now;

        //We first retrieve the API keys from the API Key component
        api_Keys = GetComponent<API_Keys>();
        if (!api_Keys)
            Debug.LogError(DEBUG_PREFIX + "Cannot find the API Keys component, please check the Inspector!");
        else apiKey = api_Keys.GetAPIKey("Groq_API_Key");

        if (apiKey == null)
            Debug.LogWarning(DEBUG_PREFIX + "Warning: API key not found, check API Key File!");

        //Now find the AI Orchestrator
        aiO = GetComponent<AI_Orchestrator>();
        if (!aiO)
        {
            Debug.LogError("LLM: AI Orchestrator component not found!");
            return;
        }

        selectedLLMString = selectedModel.ToString().Replace('_', '-').Replace('X', '.').Replace('Y', '/');
        if (debug)
            Debug.Log("You have selected LLM: " + selectedLLMString);

        //NEW - generate a prompt!
        prompt = "You are " + whoAmI;
        
        if (maxNumberOfWords > 0) prompt += "\nRespond to all questions in a maximum of " + maxNumberOfWords + " words!\n";
        prompt += "\nToday is " + currentDate.ToShortDateString();
        prompt += "\n===";
        prompt += CreatePromptContext(context);

        if (debug)
            Debug.Log(prompt);

        //UPDATED: Initialize the conversation history
        messageHistory = new List<Message>();
        AppendConversation(prompt, "system");
    }


    private void AppendConversation(string mesg, string myRole)
    {
        Message newMesg = new Message
        {
            role = myRole,
            content = mesg                                          //UPDATED!
        };
        messageHistory.Add(newMesg);
    }


    //replace the content of the last message in the list by mesg (ie. without context)
    private void ReplaceLastMessageInConversation(string mesg)
    {
        messageHistory.FindLast(x => true).content = mesg;

    }


    //Main function that sends messages to the LLM, not a coroutine
    // prompt + context
    public void TextToLLM(string mesg, string context)              //UPDATED!
    {
        StartCoroutine(TalkToLLM(mesg, context));                   //NEW!
    }


    //Creates the context for the initial System message and for any consecutive RAG contexts if applicable
    private string CreatePromptContext(string input)
    {
        string prompt = "";
        if (input != "")
        {
            prompt += "\nAnswer the question using the following context:\n===\n";
            prompt += input;
            prompt += "\n===";

            if (closedContext) prompt += "\nIf the answer can't be found in the context then respond with: \"I don't know! \"";
        }
        return prompt;
    }


    private IEnumerator TalkToLLM(string mesg, string context)
    {
        RequestBody requestBody = new RequestBody();

        //Check for context!
        // Note that we can't concat the context to all messages as this would cause LLM prompt length limitations 
        // therefore we should only concat the context to the last message and remove it after the message is sent
        string tmpContext = CreatePromptContext(context);
        string promptWithContext = mesg;

        if (tmpContext != "") promptWithContext += tmpContext;      //If there was context, add it to the prompt
        AppendConversation(promptWithContext, "user");              //Add to the list but excluding the previous context - would run into limits of LLM quickly!
        requestBody.messages = messageHistory.ToArray();            //Add the complete conversation history

        if (debug)                                                  //Show the complete prompt with all old messages
        {
            int j = 0;
            foreach (var x in requestBody.messages)
            {
                Debug.Log("LLM-message " + j + ": " + x.content + " " + x.role);
                j++;
            }
        }

        requestBody.model = selectedLLMString;
        string jsonRequestBody = JsonUtility.ToJson(requestBody);
        LLMresult = "Ik wacht";

        //Prepare  REST API Request
        UnityWebRequest request = new UnityWebRequest(apiURI, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

        //headers
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        //Wait for the result without blocking the main thread
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;
            GroqCloudResponse groqCS = JsonUtility.FromJson<GroqCloudResponse>(responseText);
            LLMresult = groqCS.choices[0].message.content;  //here is the field where the actual response is!
            Debug.Log(LLMresult);

            //now lets call TTS in a single call via the AI Orchestrator!
            aiO.Say(LLMresult);
        }
        else Debug.Log("LLM API Request failed: " + request.error);

        //replace last message by removing the context and keep the prompt only - to avoid LLM prompt overload
        ReplaceLastMessageInConversation(mesg);
    }


    //=============================
    //Write JSON to LLM classes - generated with LLama!
    //=============================
    [System.Serializable]
    public class RequestBody
    {
        public Message[] messages;
        public string model;
    }


    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
    }


    //Read JSON response from LLM classes
    [System.Serializable]
    public class GroqCloudResponse
    {
        public string id;
        public string @object;
        public int created;
        public string model;
        public Choice[] choices;
        public Usage usage;
        public string system_fingerprint;
        public XGroq x_groq;
    }

    [System.Serializable]
    public class Choice
    {
        public int index;
        public ChoiceMessage message;
        public object logprobs;
        public string finish_reason;
    }

    [System.Serializable]
    public class ChoiceMessage
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    public class Usage
    {
        public int prompt_tokens;
        public float prompt_time;
        public int completion_tokens;
        public float completion_time;
        public int total_tokens;
        public float total_time;
    }

    [System.Serializable]
    public class XGroq
    {
        public string id;
    }
}
