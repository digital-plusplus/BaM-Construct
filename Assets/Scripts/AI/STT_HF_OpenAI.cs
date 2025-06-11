using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using UnityEngine.XR.Interaction.Toolkit;


public class STT_HF_OpenAI : MonoBehaviour
{
    private string HF_INF_API_KEY;
    const string STT_API_URI_t = "https://api-inference.huggingface.co/models/openai/whisper-tiny";             //POST URI
    const string STT_API_URI_L = "https://api-inference.huggingface.co/models/openai/whisper-large-v3";         //another POST URI
    const string STT_API_URI_T = "https://api-inference.huggingface.co/models/openai/whisper-large-v3-turbo";   //another POST URI

    private enum STTModel { tiny, large, turbo };

    [SerializeField] private STTModel selectedModel;
    string selectedSTTString;

    AI_WAV wavObject;                                   //Object that holds stream and methods for WAV
    AI_STT_Text_Filter aiSTTTextFilter;
    API_Keys api_Keys;

    [SerializeField] bool debug;
    const string DEBUG_PREFIX = "STT_HF: ";

    public void Init()
    {
        //We first retrieve the API keys from the API Key component
        api_Keys = GetComponent<API_Keys>();
        if (!api_Keys)
            Debug.LogError(DEBUG_PREFIX + "Cannot find the API Keys component, please check the Inspector!");
        else HF_INF_API_KEY = api_Keys.GetAPIKey("HF_API_Key");

        if (HF_INF_API_KEY == null)
            Debug.LogWarning(DEBUG_PREFIX + "Warning: STT API key is empty, check Inspector!");

        switch (selectedModel)
        {
            case STTModel.tiny:
                selectedSTTString = STT_API_URI_t;
                break;
            case STTModel.large:
                selectedSTTString = STT_API_URI_L;
                break;
            case STTModel.turbo:
                selectedSTTString = STT_API_URI_T;
                break;

            default:
                Debug.LogError("STT: ILLEGAL STT MODEL SELECTED!");
                break;
        }

        //Note: you can't use new to allocate memory for MonoBehavior objects
        wavObject = GetComponent<AI_WAV>();                      //Start with a clean stream
        aiSTTTextFilter = GetComponent<AI_STT_Text_Filter>();    //Connect with Text Filter
    }


    //=========================================================================
    //Event handlers initiate the AI Conversation
    //  Enter these in an XR Grab Interactable component -> Interactable Events
    //  of the GameObject you want to have a conversation with
    // 20250611: MOVED TO AI Orchestrator component, so we can use the same handlers for all AI components
    //=========================================================================
    /*public void SelectEnterEventHandler(SelectEnterEventArgs eventArgs)
    {
        StartSpeaking();
    }


    public void SelectExitEventHandler(SelectExitEventArgs eventArgs)
    {
        Microphone.End(null);
    }
    */

    public void StartSpeaking()
    {
        //Setup the AudioSource for reading
        AudioSource aud = GetComponent<AudioSource>();

        //listen to the mic for 5 sec, change to start/end click event! Non-blocking so use Coroutine!
        if (debug) 
            Debug.Log(DEBUG_PREFIX+"Start recording");
        aud.clip = Microphone.Start(null, false, 30, 11025);        //use default mic
        
        StartCoroutine(RecordAudio(aud.clip));
    }


    //Coroutine to wait until recording is finished
    IEnumerator RecordAudio(AudioClip clip)
    {
        while (Microphone.IsRecording(null))
        {
            yield return null;
        }

        if (debug) 
            Debug.Log(DEBUG_PREFIX+"Done Recording!");
        AudioSource aud = GetComponent<AudioSource>();
        wavObject.ConvertClipToWav(aud.clip);       //wavObject now holds the WAV stream data

        StartCoroutine(STT());                  //Call STT cloudsvc   
    }


    //REST API Call using the converted WAV stream buffer
    IEnumerator STT()
    {
        //JSON
        SpeechToTextData sttData = new SpeechToTextData();

        //Set up the UnityWebRequest
        UnityWebRequest request = new UnityWebRequest(selectedSTTString, "POST");

        //Audio must be converted to WAV before this is called!
        request.uploadHandler = new UploadHandlerRaw(wavObject.stream.GetBuffer());
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

        //Headers
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer "+ HF_INF_API_KEY);


        // Send the request and decompress the multimedia response
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;
            SpeechToTextData sttResponse = JsonUtility.FromJson<SpeechToTextData>(responseText);

            // Extract the "Content" section, text
            if (debug)
                Debug.Log("STT service responded with: " + sttResponse.text);

            //Now analyze the text and direct to LLM or TTI or....
            aiSTTTextFilter.DirectToCloudProviders(sttResponse.text); 
        }
        else Debug.LogError("API request failed: " + request.error);
    }


    //JSON Output Class representation
    [Serializable]
    public class SpeechToTextData
    {
        public string text;
    }

    //Input data is MP3, FLAC, WAV etc, no JSON wrapper required

}
