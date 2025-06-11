using System.Collections;
using System.Collections.Generic;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.UI;

public class MessageObjectUI : MonoBehaviour
{

    public Text MessageText;
    public Button EditButton;
    public Button DeleteButton;
    public Button ConfirmDeleteButton;
    public Button ConfirmCancelButton;
    public Button UpdateButton;
    public InputField MessageInputField;
    public GameObject Controls;

    private Color m_defaultTextColor = new Color(0.1960784f, 0.1960784f, 0.1960784f, 1f);
    private Color m_dangerTextColor = new Color(0.6705883f, 0, 0, 1f);
    private VivoxMessage m_vivoxMessage;

    // Start is called before the first frame update
    void OnEnable()
    {
        EditButton.onClick.AddListener(OnEditButtonClick);
        DeleteButton.onClick.AddListener(OnDeleteButtonClick);
        ConfirmCancelButton.onClick.AddListener(() => EnableEditMode());
        ConfirmDeleteButton.onClick.AddListener(OnConfirmDeleteButtonClick);
        UpdateButton.onClick.AddListener(OnUpdateButtonClick);
        EnableEditMode(false);
    }

    void OnDestroy()
    {
        EditButton.onClick.RemoveAllListeners();
        DeleteButton.onClick.RemoveAllListeners();
        ConfirmCancelButton.onClick.RemoveAllListeners();
        ConfirmDeleteButton.onClick.RemoveAllListeners();
        UpdateButton.onClick.RemoveAllListeners();
    }

    private async void OnUpdateButtonClick()
    {
        var updatedMessage = MessageInputField.text;
        EnableEditMode(false);
        await VivoxService.Instance.EditChannelTextMessageAsync(VivoxVoiceManager.LobbyChannelName, m_vivoxMessage.MessageId, updatedMessage);
    }

    private void OnDeleteButtonClick()
    {
        EnableEditMode(true, true);
    }
    private async void OnConfirmDeleteButtonClick()
    {
        EnableEditMode(false);
        await VivoxService.Instance.DeleteChannelTextMessageAsync(VivoxVoiceManager.LobbyChannelName, m_vivoxMessage.MessageId);
    }

    private void OnEditButtonClick()
    {
        EnableEditMode(true);
    }

    public void SetTextMessage(VivoxMessage message, bool deleted = false)
    {
        var updatedStatusMessage = deleted ? string.Format($"(Deleted) ") : string.Format($"(Edited) ");
        var editedText = m_vivoxMessage != null ? updatedStatusMessage : null;

        m_vivoxMessage = message;
        if (string.IsNullOrEmpty(message.ChannelName))
        {
            EditButton.onClick.RemoveAllListeners();
            DeleteButton.onClick.RemoveAllListeners();
            ConfirmCancelButton.onClick.RemoveAllListeners();
            ConfirmDeleteButton.onClick.RemoveAllListeners();
            UpdateButton.onClick.RemoveAllListeners();
            EditButton.gameObject.SetActive(false);
            DeleteButton.gameObject.SetActive(false);
        }

        if (deleted)
        {
            MessageText.text = string.Format($"<color=#5A5A5A><size=8>{editedText}{message.ReceivedTime}</size></color>");
            Controls.SetActive(false);
            return;
        }

        if (message.FromSelf)
        {
            MessageText.alignment = TextAnchor.MiddleRight;
            MessageText.text = string.Format($"{message.MessageText} :<color=blue>{message.SenderDisplayName} </color>\n<color=#5A5A5A><size=8>{editedText}{message.ReceivedTime}</size></color>");
        }
        else
        {
            MessageText.alignment = TextAnchor.MiddleLeft;
            MessageText.text = string.IsNullOrEmpty(message.ChannelName)
                ? string.Format($"<color=purple>{message.SenderDisplayName} </color>: {message.MessageText}\n<color=#5A5A5A><size=8>{editedText}{message.ReceivedTime}</size></color>") // DM
                : string.Format($"<color=green>{message.SenderDisplayName} </color>: {message.MessageText}\n<color=#5A5A5A><size=8>{editedText}{message.ReceivedTime}</size></color>"); // Channel Message
        }

        // If it's your own message you can edit and delete them so lets show those controls
        Controls.SetActive(message.FromSelf);
    }

    private void EnableEditMode(bool isEditMode = false, bool isDelete = false)
    {
        // Update input field with text otherwise clear the field
        MessageInputField.text = isEditMode ? m_vivoxMessage.MessageText : null;

        // If isDelete means we are confirming delete lets manipulate the input field look
        MessageInputField.interactable = !isDelete;
        MessageInputField.textComponent.color = isDelete ? m_dangerTextColor : m_defaultTextColor;
        MessageInputField.textComponent.alignment = isDelete ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;


        // Update visibility
        MessageText.gameObject.SetActive(!isEditMode);
        EditButton.gameObject.SetActive(!isEditMode);
        DeleteButton.gameObject.SetActive(!isEditMode);
        ConfirmCancelButton.gameObject.SetActive(isEditMode);
        ConfirmDeleteButton.gameObject.SetActive(isEditMode && isDelete);
        UpdateButton.gameObject.SetActive(isEditMode && !isDelete);
        MessageInputField.gameObject.SetActive(isEditMode);
    }
}
