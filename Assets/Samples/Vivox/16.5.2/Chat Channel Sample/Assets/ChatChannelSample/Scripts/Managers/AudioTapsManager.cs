using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Services.Vivox;
using Unity.Services.Vivox.AudioTaps;
using UnityEngine;
using UnityEngine.Audio;

public class AudioTapsManager : MonoBehaviour
{
    private bool _featureFeatureEnabled;
    public bool IsFeatureEnabled => _featureFeatureEnabled;
    public Action<bool> OnTapsFeatureChanged;
    public AudioMixer mixer;

    private GameObject _channelAudioGameObject;
    private GameObject _captureGameObject;

    static object m_Lock = new object();
    static AudioTapsManager m_Instance;

    public enum Effects
    {
        None,
        Echo,
        Evil
    }

    /// <summary>
    /// Access singleton instance through this propriety.
    /// </summary>
    public static AudioTapsManager Instance
    {
        get
        {
            lock (m_Lock)
            {
                if (m_Instance == null)
                {
                    // Search for existing instance.
                    m_Instance = (AudioTapsManager)FindObjectOfType(typeof(AudioTapsManager));

                    // Create new instance if one doesn't already exist.
                    if (m_Instance == null)
                    {
                        // Need to create a new GameObject to attach the singleton to.
                        var singletonObject = new GameObject();
                        m_Instance = singletonObject.AddComponent<AudioTapsManager>();
                        singletonObject.name = typeof(AudioTapsManager).ToString() + " (Singleton)";
                    }
                }
                // Make instance persistent even if its already in the scene
                DontDestroyOnLoad(m_Instance.gameObject);
                return m_Instance;
            }
        }
    }

    void Start()
    {
        StartCoroutine(Setup());
    }

    IEnumerator Setup()
    {
        yield return new WaitUntil(() => VivoxService.Instance != null);
        VivoxService.Instance.LoggedOut += DestroyAllEffectGameObjects;
    }

    private void OnDestroy()
    {
        VivoxService.Instance.LoggedOut -= DestroyAllEffectGameObjects;
        DestroyAllEffectGameObjects();
    }

    public void TapsFeatureSettingChanged(bool value)
    {
        if (value != IsFeatureEnabled)
        {
            _featureFeatureEnabled = value;
            OnTapsFeatureChanged?.Invoke(_featureFeatureEnabled);
        }

        if (!IsFeatureEnabled)
        {
            DestroyAllEffectGameObjects();
        }
    }

    private void AddEffect(Effects effect, GameObject obj)
    {
        switch (effect)
        {
            case Effects.Echo:
                obj.AddComponent<AudioEchoFilter>();
                break;
            case Effects.Evil:
                AudioSource audioSource = obj.GetComponent<AudioSource>();
                if (audioSource != null)
                {
                    audioSource.outputAudioMixerGroup = mixer.FindMatchingGroups("Master")[0];
                }

                break;
            default:
                Debug.LogError($"Unhandled Effect type: {effect}");
                break;
        }
    }

    private void DestroyEffectGameObject(GameObject obj)
    {
        if (obj != null)
        {
            Destroy(obj);
        }
    }

    private void DestroyAllEffectGameObjects()
    {
        DestroyEffectGameObject(_channelAudioGameObject);
        DestroyEffectGameObject(_captureGameObject);
        // VivoxParticipantLifetimes are handled by the VivoxParticipant themselves, no need to delete them
    }

    public void AddParticipantEffect(VivoxParticipant participant, Effects effect)
    {
        // Note: Here we delete because we want to not have any tap anymore, but it would be possible to handle the GameObject to remove the current effect without deleting the tap
        // Doesn't do anything if there is no current Tap for this participant
        participant.DestroyVivoxParticipantTap();

        if (effect == Effects.None)
        {
            return;
        }

        var go = participant.CreateVivoxParticipantTap();
        AddEffect(effect, go);
    }

    public void AddSelfCaptureEffect(Effects effect)
    {
        DestroyEffectGameObject(_captureGameObject);
        if (effect == Effects.None)
        {
            return;
        }

        _captureGameObject = new GameObject("CaptureGameObject");
        _captureGameObject.AddComponent<VivoxCaptureSourceTap>();
        AddEffect(effect, _captureGameObject);
#if VIVOX_ENABLE_CAPTURE_SINK_TAP
        _captureGameObject.AddComponent<VivoxCaptureSinkTap>();
#endif
    }

    public void AddChannelAudioEffect(Effects effect)
    {
        DestroyEffectGameObject(_channelAudioGameObject);
        if (effect == Effects.None)
        {
            return;
        }

        _channelAudioGameObject = new GameObject("ChannelAudioGameObject");
        _channelAudioGameObject.AddComponent<VivoxChannelAudioTap>();
        AddEffect(effect, _channelAudioGameObject);
    }
}
