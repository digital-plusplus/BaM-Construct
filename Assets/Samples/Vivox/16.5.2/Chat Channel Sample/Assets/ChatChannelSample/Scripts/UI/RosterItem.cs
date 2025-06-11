using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Vivox;
using System.Linq;

public class RosterItem : MonoBehaviour
{
    // Player specific items.
    public VivoxParticipant Participant;
    public Text PlayerNameText;

    public Image ChatStateImage;
    public Sprite MutedImage;
    public Sprite SpeakingImage;
    public Sprite NotSpeakingImage;
    public Slider ParticipantVolumeSlider;
    public Button MuteButton;
    public Dropdown EffectDropdown;

    const float k_minSliderVolume = -50;
    const float k_maxSliderVolume = 7;
    readonly Color k_MutedColor = new Color(1, 0.624f, 0.624f, 1);

    private void UpdateChatStateImage()
    {
        if (Participant.IsMuted)
        {
            ChatStateImage.sprite = MutedImage;
            ChatStateImage.gameObject.transform.localScale = Vector3.one;
        }
        else
        {
            if (Participant.SpeechDetected)
            {
                ChatStateImage.sprite = SpeakingImage;
                ChatStateImage.gameObject.transform.localScale = Vector3.one;
            }
            else
            {
                ChatStateImage.sprite = NotSpeakingImage;
            }
        }
    }

    public void SetupRosterItem(VivoxParticipant participant)
    {
        Participant = participant;
        PlayerNameText.text = Participant.DisplayName;
        UpdateChatStateImage();
        Participant.ParticipantMuteStateChanged += UpdateChatStateImage;
        Participant.ParticipantSpeechDetected += UpdateChatStateImage;

        MuteButton.onClick.AddListener(() =>
        {
            // If already muted, unmute, and vice versa.
            if (Participant.IsMuted)
            {
                participant.UnmutePlayerLocally();
                MuteButton.image.color = Color.white;
            }
            else
            {
                participant.MutePlayerLocally();
                MuteButton.image.color = k_MutedColor;
            }
        });

        if (participant.IsSelf)
        {
            // Can't change our own participant volume, so turn off the slider
            ParticipantVolumeSlider.gameObject.SetActive(false);
        }
        else
        {
            ParticipantVolumeSlider.minValue = k_minSliderVolume;
            ParticipantVolumeSlider.maxValue = k_maxSliderVolume;
            ParticipantVolumeSlider.value = participant.LocalVolume;
            ParticipantVolumeSlider.onValueChanged.AddListener((val) =>
            {
                OnParticipantVolumeChanged(val);
            });
        }

        EffectDropdown.gameObject.SetActive(AudioTapsManager.Instance.IsFeatureEnabled);

        EffectDropdown.onValueChanged.AddListener(delegate
        {
            EffectChanged(EffectDropdown);
        });
        AudioTapsManager.Instance.OnTapsFeatureChanged += OnAudioTapsManagerFeatureChanged;
    }

    private void OnAudioTapsManagerFeatureChanged(bool enabled)
    {
        EffectDropdown.gameObject.SetActive(enabled);
    }

    private void EffectChanged(Dropdown effectDropDown)
    {
        var effect = (AudioTapsManager.Effects)effectDropDown.value;

        if (Participant.IsSelf)
        {
            AudioTapsManager.Instance.AddSelfCaptureEffect(effect); // TODO: Re-enable transmit effects when CaptureSink becomes supported
        }
        else
        {
            AudioTapsManager.Instance.AddParticipantEffect(Participant, effect);
        }
    }

    void OnDestroy()
    {
        Participant.ParticipantMuteStateChanged -= UpdateChatStateImage;
        Participant.ParticipantSpeechDetected -= UpdateChatStateImage;
        MuteButton.onClick.RemoveAllListeners();
        ParticipantVolumeSlider.onValueChanged.RemoveAllListeners();
    }

    void OnParticipantVolumeChanged(float volume)
    {
        if (!Participant.IsSelf)
        {
            Participant.SetLocalVolume((int)volume);
        }
    }
}
