using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

public class TTS_SF_Simba : MonoBehaviour
{
    //Variables
    private string SPEECHIFY_API_KEY;

    private enum SelectVoice
    {
        NL_Daan, NL_Lotte, US_Henry, US_Carly, US_Kyle, US_Kristy, US_Oliver, US_Tasha, US_Joe, US_Lisa,
        US_George, US_Emily, US_Rob, GB_Russell, GB_Benjamin, GB_Michael, AU_KIM, IN_Ankit, IN_Arun,
        GB_Carol, GB_Helen, US_Julie, AU_Linda, US_Mark, US_Nick, NG_Elijah, GB_Beverly, GB_Collin,
        US_Erin, US_Jack, US_Jesse, US_Keenan, US_Lindsey, US_Monica, GB_Phil, GB_Declan, US_Stacy,
        GB_Archie, US_Evelyn, GB_Freddy, GB_Harper, US_Jacob, US_James, US_Mason, US_Victoria
    }

    private enum SelectModel
    {
        _base, _english, _multilingual, _turbo
    }

    [SerializeField]
    private SelectVoice selectVoice;

    [SerializeField]
    private SelectModel selectModel;

    const string TTS_API_URI = "https://api.sws.speechify.com/v1/audio/stream";      //POST URI, streaming API
    private string sfVoice;
    private string sfModel;
    Animator avtAnimator;
    API_Keys api_Keys;

    [SerializeField] private bool debug;
    const string DEBUG_PREFIX = "TTS_SF_SIMBA: ";


    // Start is called before the first frame update
    public void Init()
    {
        //We first retrieve the API keys from the API Key component
        api_Keys = GetComponent<API_Keys>();
        if (!api_Keys)
            Debug.LogError(DEBUG_PREFIX + "Cannot find the API Keys component, please check the Inspector!");
        else SPEECHIFY_API_KEY = api_Keys.GetAPIKey("Speechify_API_Key");

        if (SPEECHIFY_API_KEY == null)
            Debug.LogWarning(DEBUG_PREFIX + "Warning: TTS API key is empty, check Inspector!");

        avtAnimator = GetComponent<Animator>();

        sfVoice = selectVoice.ToString().Substring(3).ToLower();                    //20250403 - fix as API is now case-sensitive!
        sfModel = "simba-"+selectModel.ToString().Substring(1);
        Debug.Log("You have selected voice " + sfVoice + " and model "+sfModel);

        //DEBUG
        //Say("Hallo ik ben de groenteboer");
    }


    public void Say(string textInput)
    {
        StartCoroutine(PlayTTS(textInput));
    }


    IEnumerator PlayTTS(string mesg)
    {
        //JSON
        TextToSpeechData ttsData = new TextToSpeechData();
        ttsData.input = SimpleCleanText(mesg);
        ttsData.voice_id = sfVoice;
        ttsData.model = sfModel;
        string jsonPrompt = JsonUtility.ToJson(ttsData);

        //WebRequest
        UnityWebRequest request = new UnityWebRequest(TTS_API_URI, "POST");
        request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonPrompt));
        request.downloadHandler = new DownloadHandlerAudioClip(TTS_API_URI, AudioType.MPEG);

        //Headers
        request.SetRequestHeader("content-type", "application/json");
        request.SetRequestHeader("accept", "audio/mpeg");
        request.SetRequestHeader("Authorization", SPEECHIFY_API_KEY);

        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            avtAnimator.SetBool("isTalking", true);
            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            GetComponent<AudioSource>().PlayOneShot(clip);
            StartCoroutine(WaitForTalkingFinished());
        }
        else Debug.Log("TTS API Request failed: " + request.error);
    }


    IEnumerator WaitForTalkingFinished()
    {
        while (GetComponent<AudioSource>().isPlaying)
        {
            yield return null;
        }
        avtAnimator.SetBool("isTalking", false);
        //Add any code here that has to be sure the speech is completed, eg. animations
    }


    //JSON Support Classes
    [Serializable]
    public class TextToSpeechData
    {
        public string input;
        public string voice_id;
        public string model;
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
                case 'I':
                    if ((i < msg.Length + 2) && (msg[i + 1] == '\'') && msg[i + 2] == 'm')
                    {
                        result += "I am";
                        i += 2;
                    }
                    else result += 'I';
                    break;
                default:
                    result += msg[i];       //simply pass on everything else
                    break;
            }
        }
        return result;
    }
}
