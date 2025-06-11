using UnityEngine;

/// <summary>
/// Just a very basic public variable to enable/disable the object for grabbing
/// When DISABLED, this will allow the player to interact with the object but not move it
/// When ENABLED, this will allow the player to interact and move it
/// </summary>

public class xrGrabEnabler : MonoBehaviour
{
    [SerializeField] public bool xrGrabEnabled = true;
}
