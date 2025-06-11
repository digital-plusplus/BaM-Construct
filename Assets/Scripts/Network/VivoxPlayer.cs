using System.Collections;
using System.Collections.Generic;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Android;
using Unity.Services.Vivox;         //new API
using Unity.Services.Authentication;


//This is derived mostly from the Samples -> LoginScreenUI.cs script provided with the package
//20240830 - upgraded to Package version 16.5.0, as per 16.0.0 there was a major API update
//           so the old VivoxVoiceManager script did not work anymore
public class VivoxPlayer : MonoBehaviour
{
    private int m_PermissionAskedCount;
    float _nextUpdate = 0;
    private bool connected = false;
    [SerializeField] string voiceChannel = "BaMChannel";

    private Transform xrCam; //position of our Main Camera


    async void InitializeAsync()
    {
        Debug.Log("Vivox InitializeAsync called");
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        await VivoxService.Instance.InitializeAsync();
    }


    // Start is called before the first frame update
    void Start()
    {
        VivoxService.Instance.LoggedIn += OnUserLoggedIn;
        VivoxService.Instance.LoggedOut += OnUserLoggedOut;

        xrCam = GameObject.Find("Main Camera").transform;
    }


    //Cleanup elegantly
    void OnDestroy()
    {
        VivoxService.Instance.LoggedIn -= OnUserLoggedIn;
        VivoxService.Instance.LoggedOut -= OnUserLoggedOut;
    }


    // ===============================================
    // Android approval helpers to use the microphone
    // ===============================================
#if (UNITY_ANDROID && !UNITY_EDITOR) || __ANDROID__
        bool IsAndroid12AndUp()
        {
            // android12VersionCode is hardcoded because it might not be available in all versions of Android SDK
            const int android12VersionCode = 31;
            AndroidJavaClass buildVersionClass = new AndroidJavaClass("android.os.Build$VERSION");
            int buildSdkVersion = buildVersionClass.GetStatic<int>("SDK_INT");

            return buildSdkVersion >= android12VersionCode;
        }

        string GetBluetoothConnectPermissionCode()
        {
            if (IsAndroid12AndUp())
            {
                // UnityEngine.Android.Permission does not contain the BLUETOOTH_CONNECT permission, fetch it from Android
                AndroidJavaClass manifestPermissionClass = new AndroidJavaClass("android.Manifest$permission");
                string permissionCode = manifestPermissionClass.GetStatic<string>("BLUETOOTH_CONNECT");

                return permissionCode;
            }

            return "";
        }
#endif


    bool IsMicPermissionGranted()
    {
        bool isGranted = Permission.HasUserAuthorizedPermission(Permission.Microphone);
        #if (UNITY_ANDROID && !UNITY_EDITOR) || __ANDROID__
            if (IsAndroid12AndUp())
            {
                // On Android 12 and up, we also need to ask for the BLUETOOTH_CONNECT permission for all features to work
                isGranted &= Permission.HasUserAuthorizedPermission(GetBluetoothConnectPermissionCode());
            }
        #endif
        return isGranted;
    }

    void AskForPermissions()
    {
        string permissionCode = Permission.Microphone;

        #if (UNITY_ANDROID && !UNITY_EDITOR) || __ANDROID__
            if (m_PermissionAskedCount == 1 && IsAndroid12AndUp())
            {
                permissionCode = GetBluetoothConnectPermissionCode();
            }
        #endif
        m_PermissionAskedCount++;
        Permission.RequestUserPermission(permissionCode);
    }

    bool IsPermissionsDenied()
    {
        #if (UNITY_ANDROID && !UNITY_EDITOR) || __ANDROID__
            // On Android 12 and up, we also need to ask for the BLUETOOTH_CONNECT permission
            if (IsAndroid12AndUp())
            {
                return m_PermissionAskedCount == 2;
            }
        #endif
        return m_PermissionAskedCount == 1;
    }


    //============================================
    //This is where we actually sign into Vivox
    //============================================
    public void SignIntoVivox ()     //in the new API this is now an Async function
    {
        //Actual code runs from here
        if (IsMicPermissionGranted())
            LoginToVivox();
        
        else
        {
            if (IsPermissionsDenied())
            {
                m_PermissionAskedCount = 0;
                LoginToVivox();
            }
            else
                //AskForPermissions();
                LoginToVivox();
        }
    }


    async void LoginToVivox()
    {
        await VivoxVoiceManager.Instance.InitializeAsync(transform.name.ToString());
        var loginOptions = new LoginOptions()
        {
            ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.FivePerSecond,
            DisplayName = transform.name.ToString(),
            EnableTTS = false
        };
        await VivoxService.Instance.LoginAsync(loginOptions);
        await VivoxService.Instance.JoinGroupChannelAsync(voiceChannel, ChatCapability.AudioOnly);
    }


    void OnUserLoggedIn ()
    {
        Debug.Log("Logged into Vivox");
        connected = true;
    }

    void OnUserLoggedOut()
    {
        Debug.Log("Logged out from Vivox");
        connected = false;
    }


    // Update is called once per frame
    void Update()
    {
        if (!connected) return;

        if (Time.time > _nextUpdate)
        {
            //VivoxService.Instance.Set3DPosition(xrCam.gameObject, voiceChannel); //new in API>1.6.0
            _nextUpdate += 0.5f;
        }        
    }
}
