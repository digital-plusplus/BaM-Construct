using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

public class ControllerButtonReader : MonoBehaviour
{
    [SerializeField] XRInputButtonReader m_LeftControllerSelect = new XRInputButtonReader("Value");
    [SerializeField] XRInputButtonReader m_RightControllerSelect = new XRInputButtonReader("Value");
    [SerializeField] XRInputButtonReader m_LeftControllerActivate = new XRInputButtonReader("Value");
    [SerializeField] XRInputButtonReader m_RightControllerActivate = new XRInputButtonReader("Value");
    [Tooltip("Enter a value between 0 and 1")] public float treshold;
    [Tooltip("To test contoller buttons and hand pinch")] [SerializeField] bool debug;
   
    //Public access methods to retrieve whether a button is pressed: 0=no press, >0 <1=press
    public bool GetLeftControllerSelect()
    {
        return (m_LeftControllerSelect.ReadValue() > treshold);
    }

    public bool GetRightControllerSelect()
    {
        return (m_RightControllerSelect.ReadValue() > treshold);
    }

    public bool GetLeftControllerActivate()
    {
        return (m_LeftControllerActivate.ReadValue() > treshold);
    }
    
    public bool GetRightControllerActivate()
    {
        return (m_RightControllerActivate.ReadValue() > treshold);
    }

    private void Update()
    {
        if (debug) 
            Debug.Log(GetLeftControllerActivate() + " " + GetLeftControllerSelect()
            + " " + GetRightControllerActivate() + " " + GetRightControllerSelect());
    }

}
