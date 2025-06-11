using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

/// <summary>
/// Text To Speech REST API interface for Text to Speach on RapidAPI
/// Returns MP3 audio stream
/// Precoded voice but 120 reqs/day rate limit. Default English, slow
/// </summary>


public class TTS_RA_Speach : MonoBehaviour
{
    private string RAPID_API_KEY;
   
    const string TTS_API_URI = "https://text-to-speach-api.p.rapidapi.com/text-to-speech";      //POST URI
    const string TTS_API_HOST = "text-to-speach-api.p.rapidapi.com";                            //HTTP Header
    Animator avtAnimator;
    API_Keys api_Keys;

    [SerializeField] private bool debug;
    const string DEBUG_PREFIX = "TTS_RA_SPEACH: ";


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
        ttsData.text = mesg;
        ttsData.lang = "en";    //English
        ttsData.speed = "fast";
        string jsonPrompt = JsonUtility.ToJson(ttsData);


        //Set up the UnityWebRequest
        UnityWebRequest request = new UnityWebRequest(TTS_API_URI, "POST");
        request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonPrompt));
        request.downloadHandler = new DownloadHandlerAudioClip(TTS_API_URI, AudioType.MPEG);


        //Headers
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("x-rapidapi-host", TTS_API_HOST);
        request.SetRequestHeader("x-rapidapi-key", RAPID_API_KEY);


        // Send the request and decompress the multimedia response, start talking animation
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
        public string text;
        public string lang;
        public string speed;
    }

    //Output data is MP3, no JSON wrapper required
}
