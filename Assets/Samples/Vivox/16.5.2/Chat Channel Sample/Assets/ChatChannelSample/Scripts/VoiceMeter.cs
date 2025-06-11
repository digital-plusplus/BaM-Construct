using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VoiceMeter : MonoBehaviour
{
    public List<Image> statusBars;
    public Color defaultColor;
    public Color lowColor;
    public Color medColor;
    public Color highColor;

    private void Awake()
    {
#if !UNITY_WEBGL
        gameObject.SetActive(false);
#else
        // If this is WebGL we don't support TTS so hide references to it.
        GameObject.Find("ToggleTTS").SetActive(false);
#endif
    }

    // Start is called before the first frame update
    void Start()
    {
        foreach (Image img in statusBars)
        {
            img.color = defaultColor;
        }
    }

    public void UpdateVoiceMeter(double voiceLevel)
    {
        foreach (Image img in statusBars)
        {
            img.color = defaultColor;
        }

        if (voiceLevel > 0.03f)
        {
            statusBars[0].color = lowColor;
        }

        if (voiceLevel > 0.07f)
        {
            statusBars[1].color = lowColor;
        }

        if (voiceLevel > 0.12f)
        {
            statusBars[2].color = medColor;
        }

        if (voiceLevel > 0.2f)
        {
            statusBars[3].color = medColor;
        }

        if (voiceLevel > 0.3f)
        {
            statusBars[4].color = highColor;
        }

        if (voiceLevel > 0.35f)
        {
            statusBars[5].color = highColor;
        }
    }
}
