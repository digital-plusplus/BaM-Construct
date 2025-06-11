using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class AI_STT_Text_Filter : MonoBehaviour
{
    //Classes of content for selecting either SD, LLM etc..
    public enum PreFilterClass { isImage, isSpeech, is3DEdit, is3DCreate, is3DDelete, is3DLOD, is3DSpecifier, isCode  };      //add more when needed

    string[] keyWords = new string[] { "image", "picture", "photo", "create", "generate", "edit", "change","update","delete", 
                                     "clear", "mesh", "model", "detail", "increase", "decrease", 
                                     "what", "who", "how", "when", "why", "where", "see", "look", "view", "switch", "search", "find", "web", "online"};

    int NUMKEYWORDS;
    bool[] containsKeyWord;                             //KeyWord detection matrix
    [SerializeField] bool debug;                        //Debug this component by setting this bool to true in Inspector
    const string DEBUG_PREFIX = "AI_STT_Text_Filter: "; //prefix we use for debugging

    AI_Orchestrator aiO;


    private void Start()
    {
        aiO = GetComponent<AI_Orchestrator>();
        if (!aiO)
        {
            Debug.LogError("AI Orchestrator component not found!");
            return;
        }

        NUMKEYWORDS = keyWords.Length;
        containsKeyWord = new bool[NUMKEYWORDS];        //Array to check whether a keyword was identified in the prompt
    }


    private PreFilterClass Analyze(string input)
    {
        PreFilterClass rtn = PreFilterClass.isSpeech;       //default

        //Lexical scan for keywords
        for (int i=0; i<keyWords.Length;i++) {
            containsKeyWord[i] = false;
            if (input.ToLower().Contains(keyWords[i]))
                containsKeyWord[i] = true;
        }

        //Now we know whether any keywords were found and now we use some simple regex
        if (containsKeyWord[15] || containsKeyWord[16] || containsKeyWord[17] ||
            containsKeyWord[18] || containsKeyWord[19] || containsKeyWord[20])
            return PreFilterClass.isSpeech;                                         //RAG questions have top prio->speech

        if (containsKeyWord[0] || containsKeyWord[1] || containsKeyWord[2])
            return PreFilterClass.isImage;
        if (containsKeyWord[3] || containsKeyWord[4])
            return PreFilterClass.is3DCreate;
        if (containsKeyWord[5] || containsKeyWord[6] || containsKeyWord[7])
            return PreFilterClass.is3DEdit;
        if (containsKeyWord[8] || containsKeyWord[9])
            return PreFilterClass.is3DDelete;
        if (containsKeyWord[10] || containsKeyWord[11])
            return PreFilterClass.is3DSpecifier;
        if (containsKeyWord[12] || containsKeyWord[13] || containsKeyWord[14])
            return PreFilterClass.is3DLOD;

        return rtn;
    }


    public async void DirectToCloudProviders(string sttResponseText)
    {
        string trimmed;

        switch (Analyze(sttResponseText))
        {
            
            case PreFilterClass.isSpeech:

                string context = "";                    //Will hold the context retrieved from RAG

                //Now send the text to LLM
                if (debug)
                    Debug.Log(DEBUG_PREFIX + "IS_SPEECH");

                //Now check whether RAG is enabled, if so concat the context from the RAG database!
                if (aiO.RAGConfigured())
                    context = await aiO.RAGGetContext(sttResponseText, aiO.maxResults);

                if (debug)
                    Debug.Log(DEBUG_PREFIX + "Prompt:" + sttResponseText + ", Context:" + context);

                //Now send the text to LLM via the AI Orchestrator
                aiO.TextToLLM(sttResponseText, context);
                break;

            case PreFilterClass.isImage:
                //Apparently we're asking for an image so send to StableDiffusion
                aiO.TextToImage(sttResponseText);
                break;

            case PreFilterClass.is3DCreate:
                trimmed = sttResponseText;
                trimmed = Trim(trimmed, keyWords[3]);
                trimmed = Trim(trimmed, keyWords[4]);
                if (trimmed!=sttResponseText)   //Found keyword, response was trimmed!
                {
                    //Remove the words Mesh or Model
                    trimmed = Trim(trimmed, keyWords[10]);
                    trimmed = Trim(trimmed, keyWords[11]);
                    trimmed = Regex.Replace(trimmed, "[^a-zA-Z0-9 ]", "");  //remove non-alpa
                    trimmed = Regex.Replace(trimmed, "of a", "");           //remove unneccesary words 
                    aiO.TTMCreate(trimmed);
                }
                else Debug.LogError("TTM - No CREATE keyword found!");
                break;

            case PreFilterClass.is3DEdit:
                trimmed = sttResponseText;
                trimmed = Trim(trimmed, keyWords[5]);
                trimmed = Trim(trimmed, keyWords[6]);
                trimmed = Trim(trimmed, keyWords[7]);
                if (trimmed != sttResponseText)     //Found keyword, remove 3D specifier keywords and send to Sloyd
                {
                    trimmed = Trim(trimmed, keyWords[10]);
                    trimmed = Trim(trimmed, keyWords[11]);
                    trimmed = Regex.Replace(trimmed, "[^a-zA-Z0-9 ]", "");   //remove non-alpa
                    aiO.TTMEdit(trimmed);
                }
                else Debug.LogError("TTM - No EDIT keyword found");
                break;

            case PreFilterClass.is3DDelete:
                aiO.TTMDelete();
                break;

            /*case PreFilterClass.is3DLOD:      //Deprecated
                if (aiO.ttmSloyd)
                {
                    if (containsKeyWord[13]) aiO.ttmSloyd.IncreaseLOD();
                    if (containsKeyWord[14]) aiO.ttmSloyd.DecreaseLOD();
                }
                else Debug.LogError("TTM_Sloyd not found, please configure in Inspector!");
                break;
            */

            default:
                Debug.LogWarning("Don't know what to do with this request!");
                break;
        }
    }


    //Trims the string and returns the part after the keyword
    private string Trim(string input, string keyword)
    {
        int idx;
        string rtn;

        rtn = input.ToLower();
        idx = rtn.LastIndexOf(keyword);             //last keyword

        if (idx != -1)
            rtn = rtn.Substring(idx + keyword.Length).Trim();   //Only part of string after the keyword                                                                                                    
        else rtn = input;                            //not found - returns original string

        return rtn;
    }
}
