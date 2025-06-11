using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.Interaction.Toolkit;

public class STT_11_Labs : MonoBehaviour
{
    private string ELEVENLABS_API_KEY;
    const string STT_API_URI = "https://api.elevenlabs.io/v1/speech-to-text";       //POST URI

    private enum STTModel { scribe_v1 }                                             //Just one model available
    [SerializeField] private STTModel selectedModel;                                //What model did we select
    string selectedSTTString;
    private enum STTLang { en, nl };
    [SerializeField] private STTLang selectedLanguage;
    string selectedSTTLang;

    [SerializeField] bool debug;                                                    //Debug information
    const string DEBUG_PREFIX = "STT_11LABS: ";

    AI_WAV wavObject;                                                               //Object that holds stream and methods for WAV
    AI_STT_Text_Filter aiSTTTextFilter;
    API_Keys api_Keys;

    public void Init()
    {
        //We first retrieve the API keys from the API Key component
        api_Keys = GetComponent<API_Keys>();
        if (!api_Keys)
            Debug.LogError(DEBUG_PREFIX + "Cannot find the API Keys component, please check the Inspector!");
        else ELEVENLABS_API_KEY = api_Keys.GetAPIKey("ElevenLabs_API_Key");

        if (ELEVENLABS_API_KEY == null)
            Debug.LogWarning(DEBUG_PREFIX + "Warning: STT API key not found, check API Key File!");

        //Reinstate original non-alphanumerical characters
        selectedSTTString = selectedModel.ToString();
        selectedSTTLang = selectedLanguage.ToString();

        //Link to the WAV output source & Text Filter
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
    }*/

    public void StartSpeaking()
    {
        wavObject = new AI_WAV();               //Start with a clean stream

        //Setup the AudioSource for reading
        AudioSource aud = GetComponent<AudioSource>();
        if (debug)
            for (int i = 0; i < Microphone.devices.Length; i++)
                Debug.Log(Microphone.devices[i]);   //need to auto select the VR system if available?

        //listen to the mic for 30 sec, change to start/end click event! Non-blocking so use Coroutine!
        if (debug)
            Debug.Log("Start recording");
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
            Debug.Log("Done Recording!");
        AudioSource aud = GetComponent<AudioSource>();

        wavObject.ConvertClipToWav(aud.clip);       //wavObject now holds the WAV stream data
        StartCoroutine(STT());                      //Call STT cloudsvc   
    }


    //REST API Call using the converted WAV stream buffer
    IEnumerator STT()
    {
        //11LABS STT doesnt use JSON but http forms
        WWWForm form = new WWWForm();
        form.AddField("model_id", selectedSTTString);
        form.AddBinaryData("file", wavObject.stream.GetBuffer(), "audio.wav", "audio/wav");     //push the data into a http form field
        form.AddField("language", selectedSTTLang);                                             //Language, default is English
        UnityWebRequest request = UnityWebRequest.Post(STT_API_URI, form);                      //slightly different, not using JSON but Form to send parameters
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

        //Headers
        request.SetRequestHeader("xi-api-key", ELEVENLABS_API_KEY);

        // Send the request and decompress the multimedia response
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;
            SpeechToTextData sttResponse = JsonUtility.FromJson<SpeechToTextData>(responseText);

            // Extract the "Content" section, text
            if (debug)
                Debug.Log(DEBUG_PREFIX + "STT service responded with: " + sttResponse.text
                    + "[Language:" + sttResponse.language_code + ", probability " + sttResponse.language_probability + "]");

            //Now analyze the text and direct to LLM or TTI or....
            aiSTTTextFilter.DirectToCloudProviders(sttResponse.text);
        }
        else Debug.LogError(DEBUG_PREFIX + "API request failed: " + request.error);
    }


    //JSON Output Class representation
    [Serializable]
    public class SpeechToTextData
    {
        public string text;
        public string language_code;
        public string language_probability;
    }
}
