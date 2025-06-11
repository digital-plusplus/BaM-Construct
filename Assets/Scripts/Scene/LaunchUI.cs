using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.Hands;

/// <summary>
/// Main scripts that handles the UI buttons and toggles during the various stages of signin (client and host)
///     Also detects Left Controller Select+Activate button to (un)hide the UI
///     Debounces UI (un)hiding as well
///     Updated: 20241101 - moved some UI code from ClientServerJoinProtocol.cs
/// </summary>
public class LaunchUI : MonoBehaviour
{
    //Initial signin buttons
    [SerializeField]
    private Button HostButton;
    [SerializeField]
    private Button ClientButton;
    [SerializeField]
    private Button GenderButton;
    [SerializeField]
    private Button BreedButton;
    [SerializeField]
    private Toggle VoiceToggle;

    [SerializeField] Vector3 ObjectSpawnerPosition; //GRAB
    [SerializeField] public bool isVisible;         //visible by default or not?
    [SerializeField] float debounceTime;            //debouncing the controller buttons to avoid flickering menu
    private ControllerButtonReader cBR;             //Detect controller buttons
    private float lastButtonPressTime;              //Helper for debounce

    private Vector3 vZero = new Vector3(0, 0, 0);    //use to show/hide the menu
    private Vector3 vOne = new Vector3(1, 1, 1);


    private void Awake()
    {
        HostButton.onClick.AddListener(() =>
        {
            //add code here
            NetworkManager.Singleton.StartHost(); //the server will now spawn the InitialNetworkPrefab

            //GRAB!
            GameObject spawner = Resources.Load("Table") as GameObject;
            GameObject go = Instantiate(spawner, ObjectSpawnerPosition, Quaternion.identity);
            go.GetComponent<NetworkObject>().Spawn();
            go.GetComponent<GrabbableCreator>().SpawnGrabbables();
        });


        ClientButton.onClick.AddListener(() =>      //the server will now spawn the InitialNetworkPrefab
        {
            NetworkManager.Singleton.StartClient();
        });


        //TEMPORARY AVATAR SELECTORS, WILL BE REPLACED WITH FUTURE PIN CODE MECHANISM
        GenderButton.onClick.AddListener(() =>
        {
            TMPro.TextMeshProUGUI t = GenderButton.transform.Find("Text (TMP)").GetComponent<TMPro.TextMeshProUGUI>();
            t.text = (t.text == "Male" ? "Female" : "Male");  //flip
        });


        //TEMPORARY AVATAR SELECTORS, WILL BE REPLACED WITH FUTURE PIN CODE MECHANISM
        BreedButton.onClick.AddListener(() =>
        {
            TMPro.TextMeshProUGUI t = BreedButton.transform.Find("Text (TMP)").GetComponent<TMPro.TextMeshProUGUI>();
            t.text = (t.text == "Human" ? "Bot" : "Human");  //flip
        });


        VoiceToggle.onValueChanged.AddListener(delegate
             { VivoxToggle(VoiceToggle); });

    }

    void VivoxToggle(Toggle voiceToggle)
    {
        Debug.Log("Voice " + voiceToggle.isOn);
    }


    //HOST SIGNIN ONLY: Once signed in as a Host, remove all these initial-connection-based UI items
    //Called by ClientServerJoinProtocol.cs
    public void DisableAllConnectionItems()
    {
        //Disable
        HostButton.gameObject.SetActive(false);
        ClientButton.gameObject.SetActive(false);
        BreedButton.gameObject.SetActive(false);
        GenderButton.gameObject.SetActive(false);
        VoiceToggle.gameObject.SetActive(false);
    }


    //CLIENT SIGNIN ONLY: In the Client 2-nd stage signin process, remove all but the Vivox voice button and client button
    //Called by ClientServerJoinProtocol.cs
    public void DisableConnectionItemsFor2ndStageClient(string clientButtonText)
    {
        //Disable
        HostButton.gameObject.SetActive(false);
        BreedButton.gameObject.SetActive(false);
        GenderButton.gameObject.SetActive(false);

        //Update
        ClientButton.gameObject.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = clientButtonText;
    }


    //CLIENT SIGNIN ONLY: After completion of 2nd stage, client needs to remove the Client/Join and Vivox Button
    //Called by ClientServerJoinProtocol.cs
    public void DisableRemainingConnectionItemsFor2ndStageClient()
    {
        //Disable
        GameObject.Find("Client").SetActive(false);
        GameObject.Find("Toggle").SetActive(false);
    }


    //Show/Hide the UI
    void TurnOnOffUI(bool value)
    {
        //The Gender/Breed/Host/Client/Vivox buttons are removed by the ClientServerJoinProtocol script when logged in (may want to pull that into this script!)
        GameObject.Find("Canvas").transform.Find("LaunchUI").transform.Find("CameraPosition").transform.localScale = (value ? vOne : vZero);    //Just hide, dont disable
        GameObject.Find("Canvas").transform.Find("IngameDebugConsole").gameObject.SetActive(value);                                             //Disable
    }


    void ToggleMenuVisibility()
    {
        isVisible = !isVisible;
        TurnOnOffUI(isVisible);
    }


    private void Start()
    {
        cBR = GameObject.Find("XR Origin (XR Rig)").GetComponent<ControllerButtonReader>();
        TurnOnOffUI(isVisible);         //Initial call to follow the UI setting
        lastButtonPressTime = -debounceTime;
    }


    private void Update()
    {
        if (!cBR) return;

        //When both buttons on left controller are pressed (with some debouncing) at the same time we open/close the menu 
        if (cBR.GetLeftControllerActivate() && cBR.GetLeftControllerSelect() && (Time.time - lastButtonPressTime > debounceTime))
        { 
            ToggleMenuVisibility();
            lastButtonPressTime = Time.time;
        }
    }
}
