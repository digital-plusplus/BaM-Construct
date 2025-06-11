using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AvatarHasController : MonoBehaviour
{
    int oldHCValue;         //0: controllers, 1: hands

    // Start is called before the first frame update
    void Start()
    {
        oldHCValue = -1;    //illegal start
    }

 
    public void set(int value)
    {
        transform.localScale = new Vector3(value, 0, 0);
    }

    public int get()
    {
        return (int)transform.localScale.x;
    }

    public bool HasChanged()
    {
        return (oldHCValue != (int)transform.localScale.x);
    }

    public void Reset()
    {
        oldHCValue = (int)transform.localScale.x;
    }
}
