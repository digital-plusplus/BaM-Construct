using System.Linq;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.UI;

public class AudioDeviceSettings : MonoBehaviour
{
    public Dropdown InputDeviceDropdown;
    public Dropdown OutputDeviceDropdown;

    public Slider InputDeviceVolume;
    public Slider OutputDeviceVolume;

    public Image DeviceEnergyMask;

    public Text EffectiveInputDeviceText;
    public Text EffectiveOutputDeviceText;

    // Setting based on the min and max acceptable values for Vivox but the range can be adjusted.
    const float k_minSliderVolume = -50;
    const float k_maxSliderVolume = 50;
    const float k_voiceMeterSpeed = 3;
    const string k_effectiveDevicePrefix = "Effective Device: ";

    private void Start()
    {
        VivoxService.Instance.AvailableInputDevicesChanged += RefreshInputDeviceList;
        VivoxService.Instance.AvailableOutputDevicesChanged += RefreshOutputDeviceList;
        VivoxService.Instance.EffectiveInputDeviceChanged += EffectiveInputDeviceChanged;
        VivoxService.Instance.EffectiveOutputDeviceChanged += EffectiveOutputDeviceChanged;


        InputDeviceDropdown.onValueChanged.AddListener((i) =>
        {
            InputDeviceValueChanged(i);
        });
        InputDeviceVolume.onValueChanged.AddListener((val) =>
        {
            OnInputVolumeChanged(val);
        });
        InputDeviceVolume.minValue = k_minSliderVolume;
        InputDeviceVolume.maxValue = k_maxSliderVolume;

        OutputDeviceDropdown.onValueChanged.AddListener((i) =>
        {
            OutputDeviceValueChanged(i);
        });
        OutputDeviceVolume.onValueChanged.AddListener((val) =>
        {
            OnOutputVolumeChanged(val);
        });
        OutputDeviceVolume.minValue = k_minSliderVolume;
        OutputDeviceVolume.maxValue = k_maxSliderVolume;
    }

    // Start is called before the first frame update
    void OnEnable()
    {
        DeviceEnergyMask.fillAmount = 0;

        RefreshInputDeviceList();
        RefreshOutputDeviceList();

        InputDeviceVolume.value = VivoxService.Instance.InputDeviceVolume;
        OutputDeviceVolume.value = VivoxService.Instance.OutputDeviceVolume;
        EffectiveInputDeviceText.text = $"{k_effectiveDevicePrefix} {VivoxService.Instance.EffectiveInputDevice.DeviceName}";
        EffectiveOutputDeviceText.text = $"{k_effectiveDevicePrefix} {VivoxService.Instance.EffectiveOutputDevice.DeviceName}";
    }

    void OnDestroy()
    {
        // Unbind all UI actions
        InputDeviceDropdown.onValueChanged.RemoveAllListeners();
        InputDeviceVolume.onValueChanged.RemoveAllListeners();
        OutputDeviceDropdown.onValueChanged.RemoveAllListeners();
        OutputDeviceDropdown.onValueChanged.RemoveAllListeners();

        VivoxService.Instance.AvailableInputDevicesChanged -= RefreshInputDeviceList;
        VivoxService.Instance.AvailableOutputDevicesChanged -= RefreshOutputDeviceList;
    }

    private void Update()
    {
        if (VivoxService.Instance.ActiveChannels.Count > 0)
        {
            var channel = VivoxService.Instance.ActiveChannels.FirstOrDefault();
            var localParticipant = channel.Value.FirstOrDefault(p => p.IsSelf);
            DeviceEnergyMask.fillAmount = Mathf.Lerp(DeviceEnergyMask.fillAmount, (float)localParticipant.AudioEnergy, Time.deltaTime * k_voiceMeterSpeed);
        }
    }

    private void RefreshInputDeviceList()
    {
        InputDeviceDropdown.Hide();
        InputDeviceDropdown.ClearOptions();

        InputDeviceDropdown.options.AddRange(VivoxService.Instance.AvailableInputDevices.Select(v => new Dropdown.OptionData() { text = v.DeviceName }));
        InputDeviceDropdown.SetValueWithoutNotify(InputDeviceDropdown.options.FindIndex(option => option.text == VivoxService.Instance.ActiveInputDevice.DeviceName));
        InputDeviceDropdown.RefreshShownValue();
    }

    private void RefreshOutputDeviceList()
    {
        OutputDeviceDropdown.Hide();
        OutputDeviceDropdown.ClearOptions();

        OutputDeviceDropdown.options.AddRange(VivoxService.Instance.AvailableOutputDevices.Select(v => new Dropdown.OptionData() { text = v.DeviceName }));
        OutputDeviceDropdown.SetValueWithoutNotify(OutputDeviceDropdown.options.FindIndex(option => option.text == VivoxService.Instance.ActiveOutputDevice.DeviceName));
        OutputDeviceDropdown.RefreshShownValue();
    }

    void InputDeviceValueChanged(int index)
    {
        VivoxService.Instance.SetActiveInputDeviceAsync(VivoxService.Instance.AvailableInputDevices.First(device => device.DeviceName == InputDeviceDropdown.options[index].text));
    }

    void EffectiveInputDeviceChanged()
    {
        EffectiveInputDeviceText.text = $"{k_effectiveDevicePrefix} {VivoxService.Instance.EffectiveInputDevice.DeviceName}";
    }

    void OutputDeviceValueChanged(int index)
    {
        VivoxService.Instance.SetActiveOutputDeviceAsync(VivoxService.Instance.AvailableOutputDevices.First(device => device.DeviceName == OutputDeviceDropdown.options[index].text));
    }

    void EffectiveOutputDeviceChanged()
    {
        EffectiveOutputDeviceText.text = $"{k_effectiveDevicePrefix} {VivoxService.Instance.EffectiveOutputDevice.DeviceName}";
    }

    private void OnInputVolumeChanged(float val)
    {
        VivoxService.Instance.SetInputDeviceVolume((int)val);
    }

    private void OnOutputVolumeChanged(float val)
    {
        VivoxService.Instance.SetOutputDeviceVolume((int)val);
    }
}
