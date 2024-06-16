using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class M4A4Script : MonoBehaviour
{
    [SerializeField] private AudioSource myAudio;

    public bool isFiring = false;
    public int fireRate = 25;
    float accumulatedTime;

    public Transform raycastOrigin;
    public Transform raycastTarget;

    public ParticleSystem MuzzleFlash;
    public ParticleSystem[] hitEffect;
    public LayerMask[] MaterialLayers;
    private int MaterialIndex;

    public TrailRenderer BulletTracer;

    Ray ray;
    RaycastHit hitInfo;
    public void StartFire()
    {
        isFiring = true;
        //FireBullet();
    }

    public void UpdateFiring(float deltaTime)
    {
        accumulatedTime += deltaTime;
        float fireInterval = 1.0f / fireRate;
        while (accumulatedTime >= .0f) 
        {
            FireBullet();
            accumulatedTime -= fireInterval;
        }
    }
    private void FireBullet()
    {
        MuzzleFlash.Emit(1);
        myAudio.Play();

        ray.origin = raycastOrigin.position;
        ray.direction = raycastTarget.position - raycastOrigin.position;

        var tracer = Instantiate(BulletTracer, ray.origin, Quaternion.identity);
        tracer.AddPosition(ray.origin);

        if (Physics.Raycast(ray, out hitInfo))
        {
            //Debug.DrawLine(ray.origin, hitInfo.point, Color.yellow, .5f);
            HýtEffects();
            tracer.transform.position = hitInfo.point;
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
