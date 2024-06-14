using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class M4A4Script : MonoBehaviour
{

    public bool isFiring = false;
    public ParticleSystem MuzzleFlash;

    public void StartFire()
    {
        isFiring = true;
        MuzzleFlash.Emit(1);
    }
    public void StopFire()
    {
        isFiring = false;
    }
}
