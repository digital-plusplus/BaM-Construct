using System.Linq;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuOptions : MonoBehaviour
{
    public Button SaveButton;
    public Button QuitButton;
    public Dropdown TTSVoiceDropdown;
    public GameObject ConfirmationMenu;
    public Button ConfirmYesButton;
    public Button ConfirmNoButton;
    public Toggle EnableAudioTapsCheckbox;
    public Button toResetCursorTo;

    const string k_TTSVoiceSetting = "VivoxTTSVoice";

    GameObject m_optionsMenuPanel => gameObject;
    EventSystem m_EventSystem;
    int m_SelectedTTSVoiceIndex;

    void Start()
    {
        // Setup menu objects on awake
        m_optionsMenuPanel.SetActive(false);
        // Fill the TTS dropdown with all possible options
#if !UNITY_WEBGL
        PopulateTextToSpeechDropdown();
#else
        //The only settings are related to TTS so hide the button completely if this is WebGL
        GameObject.Find("Settings").SetActive(false);
#endif
        // Fetch the current EventSystem
        m_EventSystem = EventSystem.current;
        // Bind all the ui actions
        TTSVoiceDropdown.onValueChanged.AddListener(delegate
        {
            TTSDropdownValueChanged(TTSVoiceDropdown);
        });
        EnableAudioTapsCheckbox.isOn = AudioTapsManager.Instance.IsFeatureEnabled;
        SaveButton.onClick.AddListener(SaveAction);
        QuitButton.onClick.AddListener(QuitButtonAction);
        ConfirmYesButton.onClick.AddListener(ConfirmYesButtonAction);
        ConfirmNoButton.onClick.AddListener(ConfirmNoAction);
    }

    void OnDestroy()
    {
        // Unbind all the ui actions
        TTSVoiceDropdown.onValueChanged.RemoveAllListeners();
        SaveButton.onClick.RemoveListener(SaveAction);
        QuitButton.onClick.RemoveListener(QuitButtonAction);
        ConfirmYesButton.onClick.RemoveListener(ConfirmYesButtonAction);
        ConfirmNoButton.onClick.RemoveListener(ConfirmNoAction);
    }


    // Detects if the keyboard key or console button was pressed
    void Update()
    {
        // While on standalone or editor the escape key will open and close the menu
#if UNITY_STANDALONE || UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            MenuInputAction();
        }
#endif
        // While on consoles the start button will open and close the menu
#if UNITY_XBOXONE || UNITY_PS4 || UNITY_SWITCH
        if (Input.GetButtonDown("Options"))
        {
            MenuInputAction();
        }
#endif
    }

    bool IsDirty => TTSVoiceDropdown.options[m_SelectedTTSVoiceIndex].text != PlayerPrefs.GetString(k_TTSVoiceSetting, VivoxService.Instance.TextToSpeechCurrentVoice);

    private void PopulateTextToSpeechDropdown()
    {
        // Clear/remove all option item
        TTSVoiceDropdown.options.Clear();
        TTSVoiceDropdown.options.AddRange(
            VivoxService.Instance.TextToSpeechAvailableVoices
            .Select(v => new Dropdown.OptionData() { text = v })
            );
        SelectOptionFromSavedSettings();
    }

    private void SelectOptionFromSavedSettings()
    {
        var currentVoice = PlayerPrefs.GetString(k_TTSVoiceSetting, VivoxService.Instance.TextToSpeechCurrentVoice);
        VivoxService.Instance.TextToSpeechSetVoice(currentVoice);
        var voiceToSelect = TTSVoiceDropdown.options
            .FindIndex((i) =>
            {
                return i.text.Equals(currentVoice);
            });
        TTSVoiceDropdown.value = voiceToSelect;
        TTSVoiceDropdown.captionText.text = currentVoice;
        m_SelectedTTSVoiceIndex = voiceToSelect;
    }

    public void ShowOptionsMenu(bool showMenu = false)
    {
        if (showMenu == false && IsDirty)
        {
            ShowConfirmMenu(true);
        }
        else
        {
            ShowConfirmMenu(false);
            m_optionsMenuPanel.SetActive(showMenu);
        }
    }

    private void ShowConfirmMenu(bool showMenu = false)
    {
        ConfirmationMenu.SetActive(showMenu);
    }

    // When Menu Input has fired
    private void MenuInputAction()
    {
        if (m_optionsMenuPanel.activeInHierarchy)
        {
            ShowOptionsMenu(false);
        }
        else
        {
            ShowOptionsMenu(true);
        }
    }

    // Resume button on the InGameMenu
    private void SaveAction()
    {
        PlayerPrefs.SetString(k_TTSVoiceSetting, TTSVoiceDropdown.options[m_SelectedTTSVoiceIndex].text);
        // Set tts voice
        VivoxService.Instance.TextToSpeechSetVoice(TTSVoiceDropdown.options[m_SelectedTTSVoiceIndex].text);
        ShowOptionsMenu(false);
        AudioTapsManager.Instance.TapsFeatureSettingChanged(EnableAudioTapsCheckbox.isOn);

        // Remove focus on ui elements
        m_EventSystem.SetSelectedGameObject(toResetCursorTo.gameObject);
    }

    // Quit button on the InGameMenu
    private void QuitButtonAction()
    {
        ShowOptionsMenu(false);
    }

    private void ConfirmYesButtonAction()
    {
        //Save settings and close
        SaveAction();
        ShowConfirmMenu(false);
        ShowOptionsMenu(false);
    }

    private void ConfirmNoAction()
    {
        ShowConfirmMenu(false);
        ShowConfirmMenu(false);
        m_optionsMenuPanel.SetActive(false);
        SelectOptionFromSavedSettings();
    }

    void TTSDropdownValueChanged(Dropdown target)
    {
        VivoxService.Instance.TextToSpeechCancelAllMessages();
        m_SelectedTTSVoiceIndex = target.value;
    }
}
