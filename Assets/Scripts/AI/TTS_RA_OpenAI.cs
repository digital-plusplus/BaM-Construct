using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

/// <summary>
/// Text To Speech REST API interface for OpenAPI via RapidAPI
/// Returns MP3 audio stream
/// Select Voice from Unity UI or hardcoding default in line 19
/// </summary>

public class TTS_RA_OpenAI : MonoBehaviour
{
    private string RAPID_API_KEY;   //configure in Unity Inspector! 
        
    const string TTS_API_URI = "https://open-ai-text-to-speech1.p.rapidapi.com/";       //POST URI
    const string TTS_API_HOST = "https://open-ai-text-to-speech1.p.rapidapi.com";       //HTTP Header

    //Options: Alloy, Echo, Fable, Onyx, Nova, Shimmer
    [SerializeField] public string voice = "nova";                                      //default voice

    Animator avtAnimator;
    API_Keys api_Keys;

    private bool debug;
    const string DEBUG_PREFIX = "TTS_RAPID_OPENAI: ";


    public void Init()
    {
        //We first retrieve the API keys from the API Key component
        api_Keys = GetComponent<API_Keys>();
        if (!api_Keys)
            Debug.LogError(DEBUG_PREFIX + "Cannot find the API Keys component, please check the Inspector!");
        else RAPID_API_KEY = api_Keys.GetAPIKey("Rapid_API_Key");

        if (RAPID_API_KEY == null)
            Debug.LogWarning(DEBUG_PREFIX + "Warning: TTS API key is empty, check Inspector!");
    }


    /// <summary>
    /// Public wrapper for Text To Speech, no logic layer implemented yet to filter Llama responses before its sent to TTS
    /// </summary>
    /// <param name="textInput">Text to send to Llama</param>
    public void Say(string textInput)
    {
        avtAnimator = GetComponent<Animator>();
        if (!avtAnimator) Debug.LogError("Can't animate the NFC, check the Animator!");

        StartCoroutine(PlayTTS(textInput));
    }


    /// <summary> Internal
    /// Connect to OpenAI TTS via RapidAPI, requires a subscription/API KEY but provides a limited Free subscription
    /// call using StartCoroutine(PlayTTS())
    /// </summary>
    /// <param name="mesg">The message to talk over the AudioSource component </param>
    /// <returns>Spoken audio</returns>
    IEnumerator PlayTTS(string mesg)
    {
        //JSON
        TextToSpeechData ttsData = new TextToSpeechData();
        ttsData.model = "tts-1";
        ttsData.input = mesg;
        ttsData.voice = voice;
        string jsonPrompt = JsonUtility.ToJson(ttsData);


        //Set up the UnityWebRequest
        UnityWebRequest request = new UnityWebRequest(TTS_API_URI, "POST");
        request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonPrompt));

        //=======================
        //THE BELOW IS CRUCIAL!!! using a standard DownloadHandlerBuffer will give access to the RAW MP3 data which Unity can't natively use!
        //=======================
        request.downloadHandler = new DownloadHandlerAudioClip(TTS_API_URI, AudioType.MPEG);


        //Headers
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("x-rapidapi-host", TTS_API_HOST);
        request.SetRequestHeader("x-rapidapi-key", RAPID_API_KEY);


        // Send the request and decompress the multimedia response
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            avtAnimator.SetBool("isTalking", true);
            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            GetComponent<AudioSource>().PlayOneShot(clip);

            StartCoroutine(WaitForTalkingFinished());       //wait for talking to finish
        }
        else Debug.LogError("API request failed: " + request.error);
    }


    //Wait for talking to be complete and then return to idle animation
    IEnumerator WaitForTalkingFinished()
    {
        Animator avtAnimator = GetComponent<Animator>();
        if (!avtAnimator) Debug.LogError("Can't animate the NFC, check the Animator!");

        while (GetComponent<AudioSource>().isPlaying)
        {
            yield return null;
        }
        avtAnimator.SetBool("isTalking", false);
    }


    //JSON Input Class representation
    [Serializable]
    public class TextToSpeechData
    {
        public string model;
        public string input;
        public string voice;
    }

    //Output data is MP3, no JSON wrapper required
}
