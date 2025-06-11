using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Main placeholder for all AI related components
/// </summary>
public class AI_Orchestrator : MonoBehaviour
{
    [Header("Speech to Text")]
    [SerializeField] public STT_Groq_OpenAI sttGroqOpenAI;
    [SerializeField] public STT_HF_OpenAI sttHFOpenAI;
    [SerializeField] public STT_11_Labs stt11Labs;

    [Header("LLM")]
    [SerializeField] public LLM_Groq llmGroq;
    [SerializeField] public LLM_Google llmGoogle;
    [SerializeField] public LLM_Ollama llmOllama;

    [Header("RAG")]
    [SerializeField] public RAG_MariaDB ragMariaDB;       //Future
    [SerializeField] public int maxResults;

    [Header("Text to Speech")]
    [SerializeField] public TTS_RA_OpenAI ttsRAOpenAI;
    [SerializeField] public TTS_RA_Speach ttsRASpeach;
    [SerializeField] public TTS_SF_Simba ttsSFSimba;
    [SerializeField] public TTS_11_Labs tts11Labs;

    [Header("Text to Image")]
    [SerializeField] public TTI_HF_SDXLB ttiHFSDXLB;

    //[Header("Text to Mesh")]
    //[SerializeField] public TTM_Sloyd_API ttmSloyd;       //Deprecated 


    //This initializes all AI components AFTER the API keys were read from the APIKeys file
    public void Init()
    {
        if (llmGoogle) llmGoogle.Init();
        if (llmGroq) llmGroq.Init();
        if (llmOllama) llmOllama.Init();

        if (ragMariaDB) ragMariaDB.Init();

        if (sttGroqOpenAI) sttGroqOpenAI.Init();
        if (sttHFOpenAI) sttHFOpenAI.Init();
        if (stt11Labs) stt11Labs.Init();

        if (ttiHFSDXLB) ttiHFSDXLB.Init();

        //if (ttmSloyd) ttmSloyd.Init();    Deprecated

        if (tts11Labs) tts11Labs.Init();
        if (ttsRAOpenAI) ttsRAOpenAI.Init();
        if (ttsRASpeach) ttsRASpeach.Init();
        if (ttsSFSimba) ttsSFSimba.Init();
    }


    //Generalized Say command - Expand here for new services!
    public void Say(string input)
    {
        if (ttsSFSimba)     ttsSFSimba.Say(input);
        if (ttsRAOpenAI)    ttsRAOpenAI.Say(input);
        if (ttsRASpeach)    ttsRASpeach.Say(input);
        if (tts11Labs)      tts11Labs.Say(input);
    }


    //Generalized TextToLLM command - Expand here for new services!
    public void TextToLLM(string input, string context)
    {
        if (llmGroq)        llmGroq.TextToLLM(input, context);
        if (llmGoogle)      llmGoogle.TextToLLM(input, context);
        if (llmOllama)      llmOllama.TextToLLM(input, context);
    }


    //Generalized TextToImage command - Expand here for new services!
    public void TextToImage(string input)
    {
        if (ttiHFSDXLB)     ttiHFSDXLB.GetImage(input);
    }


    //Generalized TextToMesh commands - Expand here for new services!
    public void TTMCreate(string input)
    {
        //if (ttmSloyd)       ttmSloyd.Create(input);   //deprecated
    }

    public void TTMEdit(string input)
    {
        //if (ttmSloyd)       ttmSloyd.Edit(input);     //deprecated
    }

    public void TTMDelete()
    {
        //if (ttmSloyd)       ttmSloyd.Delete();        //deprecated
    }

    //Non-async call ro retrieve Context from a RAG database
    // - all RAG systems must implement a .GetContext method
    // - add new services here to ensure consistent calls via aiO.RAGGetContext
    public async Task<string> RAGGetContext(string prompt, int numberOfResults)
    {
        if (ragMariaDB)
        {
            return await ragMariaDB.GetContext(prompt, numberOfResults);
        }
        else return null;
    }

    //Check whether to use RAG or not 
    public bool RAGConfigured()
    {
        return (ragMariaDB == null ? false : true);
    }


    //Event handlers for XR Interaction Toolkit Select Interactions
    // Moved from the individual STT components to the AI Orchestrator
    public void SelectEnterEventHandler(SelectEnterEventArgs eventArgs)
    {
        if (stt11Labs) stt11Labs.StartSpeaking();
        if (sttGroqOpenAI) sttGroqOpenAI.StartSpeaking();
        if (sttHFOpenAI) sttHFOpenAI.StartSpeaking();
    }

    public void SelectExitEventHandler(SelectExitEventArgs eventArgs)
    {
        Microphone.End(null);
    }

}
