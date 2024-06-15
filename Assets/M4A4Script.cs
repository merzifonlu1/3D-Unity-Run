using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class M4A4Script : MonoBehaviour
{

    public bool isFiring = false;
    public ParticleSystem MuzzleFlash;

    public Transform raycastOrigin;
    public Transform raycastTarget;

    public ParticleSystem[] hitEffect;
    public LayerMask[] MaterialLayers;
    private int MaterialIndex;

    Ray ray;
    RaycastHit hitInfo;
    public void StartFire()
    {
        isFiring = true;
        MuzzleFlash.Emit(1);

        ray.origin = raycastOrigin.position;
        ray.direction = raycastTarget.position - raycastOrigin.position;
      
        if (Physics.Raycast(ray, out hitInfo))
        {
            //Debug.DrawLine(ray.origin, hitInfo.point, Color.yellow, .5f);
            HýtEffects();
        }

    }
    public void StopFire()
    {
        isFiring = false;
    }
    private void HýtEffects()
    {
        for (int i = 0; i < MaterialLayers.Length; i++)
        {
            if (MaterialLayers[i] == (MaterialLayers[i] | (1 << hitInfo.collider.gameObject.layer)))
            {
                hitEffect[i].transform.position = hitInfo.point;
                hitEffect[i].transform.forward = hitInfo.normal;
                hitEffect[i].Emit(1);
                break;
            }
        }
    }
}
