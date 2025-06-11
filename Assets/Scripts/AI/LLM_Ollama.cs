using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public class LLM_Ollama : MonoBehaviour
{
    [SerializeField]
    const string apiURI = "http://127.0.0.1:11434/api/chat";        //we use the chat API endpoint vs generate
    private enum LLMModel { deepseek_r1V1X5b, codellamaVlatest, nomic_embed_textVlatest, llama3X1Vlatest }

    [SerializeField]
    private LLMModel selectedModel;
    string selectedLLMString;

    [SerializeField] private bool shortResponse;
    [SerializeField] private bool excludeReasoning = true;
    [SerializeField] private string whoAmI = "nobody";
    [SerializeField] private string context;
    [SerializeField] private bool closedContext;
    [SerializeField] private bool debug = false;

    AI_Orchestrator aiO;

    List<Message> messageHistory;


    // Start is called before the first frame update
    public void Init()
    {
        string prompt;
        DateTime currentDate = DateTime.Now;

        aiO = GetComponent<AI_Orchestrator>();
        if (!aiO)
        {
            Debug.LogError("LLM: AI Orchestrator component not found!");
            return;
        }

        selectedLLMString = selectedModel.ToString().Replace('_', '-').Replace('X', '.').Replace("V", ":");
        Debug.Log("You have selected Model: " + selectedLLMString);

        //NEW - generate a prompt!
        prompt = "You are " + whoAmI;
        if (shortResponse)
            prompt += "\nAnswer all questions concise and brief!\n";
        prompt += "\nToday is " + currentDate.ToShortDateString() + "\n";
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
            content = mesg
        };
        messageHistory.Add(newMesg);
    }


    //replace the content of the last message in the list by mesg (ie. without context)
    private void ReplaceLastMessageInConversation(string mesg)
    {
        messageHistory.FindLast(x => true).content = mesg;

    }


    public void TextToLLM(string mesg, string context)
    {
        StartCoroutine(TalkToLLM(mesg, context));          //NEW!
    }


    //Creates the context for the initial System message and for any consecutive RAG contexts if applicable
    private string CreatePromptContext(string input)
    {
        string prompt = "";
        if (input != "")
        {
            prompt += "\nAnswer the question using following context:\n===\n";
            prompt += input;
            prompt += "\n===";

            if (closedContext) prompt += "\nIf the answer can't be found in the previous messages nor in the context, then respond with: \"I don't know! \"";
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
        AppendConversation(mesg, "user");                           //Add to the memory

        requestBody.model = selectedLLMString;                      //Required in each Ollama API call
        requestBody.messages = messageHistory.ToArray();            //Add the complete conversation history
        requestBody.stream = false;                                 //all responses in 1 output string

        if (debug)                                                  //Show the complete prompt with all old messages
        {
            int j = 0;
            foreach (var x in requestBody.messages)
            {
                Debug.Log("LLM-message " + j + ": " + x.content + " " + x.role);
                j++;
            }
        }

        string jsonRequestBody = JsonUtility.ToJson(requestBody);
        UnityWebRequest request = new UnityWebRequest(apiURI, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

        //NO headers

        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;
            OllamaResponse response = JsonUtility.FromJson<OllamaResponse>(responseText);
            if (debug) Debug.Log("RAW+" + responseText);
            if (debug) Debug.Log("Content:" + response.message.content);

            string trimmedResponse = (excludeReasoning ? RemoveReasoning(response.message.content) : SkipThinkTags(response.message.content));

            //now lets call TTS via the central AI Orchestrator!
            aiO.Say(trimmedResponse);

            if (debug) Debug.Log("OLLAMA: TRIMMED:" + trimmedResponse);
        }
        else Debug.Log("Ollama API Request failed: " + request.error);

        //replace last message by removing the context and keep the prompt only - to avoid LLM prompt overload
        ReplaceLastMessageInConversation(mesg);

    }


    //Readout the reasoning but don't speak the think tags themselves. Only effective with Deepseek models
    string SkipThinkTags(string input)
    {
        return input.Replace("<think>", "").Replace("</think>", "");
    }


    //Skip all the blabla and just return the response. Only effective with Deepseek models
    string RemoveReasoning(string input)
    {
        int position1 = input.IndexOf("<think>");
        int position2 = input.IndexOf("</think>");
        Debug.Log(position1 + "," + position2);
        string result = "";

        //Not found, just return input
        if (position2 == -1)
            return input;

        //Remove the last think tag
        for (int i = 0; i < input.Length; i++)
        {
            if ((i < position1) || (i > (position2 + 8))) result += input[i];
        }

        return result;
    }


    //=============================
    //Write JSON to LLM classes - generated with LLama!
    //=============================
    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
        //public string images;  //not used
    }

    [System.Serializable]
    public class RequestBody
    {
        public string model;
        public Message[] messages;
        public bool stream;             //we want to set this to false to avoid we get chunked responses
    }

    //Read JSON response from LLM classes
    [System.Serializable]
    public class OllamaResponse
    {
        public string model;
        public string created_at;
        public Message message;
        public bool done;
        /* not used
        public string total_duration;
        public string load_duration;
        public string prompt_eval_count;
        public string eval_count;
        public string eval_duration;
        */
    }
}
