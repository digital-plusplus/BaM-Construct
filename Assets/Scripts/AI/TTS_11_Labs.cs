using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

public class TTS_11_Labs : MonoBehaviour
{
    private string ELEVENLABS_API_KEY;

    private string[,] voices = new string[,]
    {
        {"Aria",    "9BWtsMINqrJLrRacOk9x"},
        {"Roger",   "CwhRBWXzGAHq8TQ4Fs17"},
        {"Sarah",   "EXAVITQu4vr4xnSDxMaL"},
        {"Laura",   "FGY2WhTYpPnrIDTdsKH5"},
        {"Charlie", "IKne3meq5aSn9XLyUdCD"},
        {"George",  "JBFqnCBsd6RMkjVDRZzb"},
        {"Callum",  "N2lVS1w4EtoT3dr4eOWO"},
        {"River",   "SAz9YHcvj6GT2YYXdXww"},
        {"Liam",    "TX3LPaxmHKxFdv7VOQHJ"},
        {"Charlot", "XB0fDUnXU5powFXDhCwa"},
        {"Alice",   "Xb7hH8MSUJpSbSDYk0k2"},
        {"Matilda", "XrExE9yKIg1WjnnlVkGX"},
        {"Will",    "bIHbv24MWmeRgasZH58o"},
        {"Jessica", "cgSgspJ2msm6clMCkdW9"},
        {"Eric",    "cjVigY5qzO86Huf0OWal"},
        {"Chris",   "iP95p4xoKVk53GoZ742B"},
        {"Brian",   "nPczCjzI2devNBz1zQrb"},
        {"Daniel",  "onwK4e9ZLuTAKqWW03F9"},
        {"Lily",    "pFZP5JQG7iQjIQuC4Bku"},
        {"Bill",    "pqHfZKP75CvOlQylNhV4" }
    };

    private enum SelectVoices { Aria, Roger, Sarah, Laura, Charlie, George, Callum, River, Liam, Charlot, Alice, Matilda, Will, Jessica, Eric, Chris, Brian, Daniel, Lily, Bill }
    private enum SelectOutputFormat { mp3_22050_32, mp3_44100_128, mp3_44100_192, mp3_44100_32, mp3_44100_64 }

    private enum SelectModel
    {
        eleven_multilingual_v2, eleven_flash_v2_5
    }

    [SerializeField]
    private SelectVoices selectVoice;

    [SerializeField]
    private SelectModel selectModel;

    [SerializeField]
    private SelectOutputFormat selectOutputFormat;

    const string TTS_API_URI = "https://api.elevenlabs.io/v1/text-to-speech/";      //POST URI, streaming API
    private string sfVoice;
    private string sfModel;
    private string sfOutput;
    Animator avtAnimator;
    int selectedVoiceIndex;

    API_Keys api_Keys;

    [SerializeField] private bool debug = false;
    const string DEBUG_PREFIX = "TTS_11LABS: ";


    // Start is called before the first frame update
    public void Init()
    {
        //We first retrieve the API keys from the API Key component
        api_Keys = GetComponent<API_Keys>();
        if (!api_Keys)
            Debug.LogError(DEBUG_PREFIX + "Cannot find the API Keys component, please check the Inspector!");
        else ELEVENLABS_API_KEY = api_Keys.GetAPIKey("ElevenLabs_API_Key");

        if (ELEVENLABS_API_KEY == null)
            Debug.LogWarning(DEBUG_PREFIX + "Warning: TTS API key is empty, check Inspector!");


        avtAnimator = GetComponent<Animator>();

        selectedVoiceIndex = GetEnumIndex(selectVoice);
        sfVoice = voices[selectedVoiceIndex, 0];
        sfModel = selectModel.ToString();
        sfOutput = selectOutputFormat.ToString();

        if (debug) Debug.Log(DEBUG_PREFIX+"You have selected voice " + sfVoice + " with index " + selectedVoiceIndex + " and model " + sfModel + " and output format "+sfOutput);

    }

    int GetEnumIndex(SelectVoices voice)
    {
        SelectVoices[] voicesArray = (SelectVoices[])System.Enum.GetValues(typeof(SelectVoices));
        return System.Array.IndexOf(voicesArray, voice);

    }

    public void Say(string textInput)
    {
        StartCoroutine(PlayTTS(textInput));
    }

    IEnumerator PlayTTS(string mesg)
    {
        //JSON
        TextToSpeechData ttsData = new TextToSpeechData();
        ttsData.text = SimpleCleanText(mesg);
        ttsData.model_id = sfModel;
        //ttsData.language_code = "";     //for now we don't enforce yet
        string jsonPrompt = JsonUtility.ToJson(ttsData);

        //Construct the URI: API_URI/<voiceid>?output_format=<output_format>
        string elevenLabsTTSURI = TTS_API_URI + voices[selectedVoiceIndex, 1] + "?output_format=" + sfOutput;
        if (debug) 
        { 
            Debug.Log(elevenLabsTTSURI);
            Debug.Log(jsonPrompt);
        }

        //WebRequest
        UnityWebRequest request = new UnityWebRequest(elevenLabsTTSURI, "POST");
        request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonPrompt));
        request.downloadHandler = new DownloadHandlerAudioClip(TTS_API_URI, AudioType.MPEG);

        //Headers
        request.SetRequestHeader("Content-Type", "application/json");
        //request.SetRequestHeader("accept", "audio/mpeg");
        request.SetRequestHeader("xi-api-key", ELEVENLABS_API_KEY);

        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            avtAnimator.SetBool("isTalking", true);

            //================================================
            //Some manual simple facial expression changes => move to separate component later!
            //================================================

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);

            //The below replaces PlayOneShot(), used for WebGL compatibility
            GetComponent<AudioSource>().clip = clip;
            GetComponent<AudioSource>().loop = false;
            GetComponent<AudioSource>().Play();

            StartCoroutine(WaitForTalkingFinished());
        }
        else Debug.LogError(DEBUG_PREFIX+"API Request failed: " + request.error);
    }


    IEnumerator WaitForTalkingFinished()
    {
        while (GetComponent<AudioSource>().isPlaying)
        {
            yield return null;
        }

        //The below replaces PlayOneShot(), used for WebGL compatibility
        GetComponent<AudioSource>().clip = null;
        GetComponent<AudioSource>().Stop();

        avtAnimator.SetBool("isTalking", false);
        
        //Add any code here that has to be sure the speech is completed, eg. animations
    }


    //JSON Support Classes
    [Serializable]
    public class TextToSpeechData
    {
        public string text;
        public string model_id;
        //public string language_code;
    }


    string SimpleCleanText(string msg)     //just a barebone filter 
    {
        string result = "";

        for (int i = 0; i < msg.Length; i++)
        {
            switch (msg[i])
            {
                case '+':
                    result += " plus ";
                    break;
                case ':':
                    result += ", ";
                    break;
                case '*':
                    result += ", ";
                    break;
                case '=':
                    result += " equals ";
                    break;
                case '-':
                    result += " ";
                    break;
                case '#':
                    result += " hash ";
                    break;
                case '&':
                    result += " and ";
                    break;
                case '\n':
                    result += "       ";
                    break;
                default:
                    result += msg[i];       //simply pass on everything else
                    break;
            }
        }
        return result;
    }
}