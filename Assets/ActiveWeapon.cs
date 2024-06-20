using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActiveWeapon : MonoBehaviour
{
    public Transform CrossshirTarget;
    private M4A4Script M4A4Script;
    private PlayerMovement PM;
    public Animator Anim;

    void Start()
    {
        M4A4Script existingWeapon = GetComponentInChildren<M4A4Script>();
        PM = GetComponentInChildren<PlayerMovement>();
        if (existingWeapon)
        {
            Equip(existingWeapon);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (PM.aiming)
        {
            if (Input.GetButtonDown("Fire1"))
            {
                M4A4Script.StartFire();
            }
            if (M4A4Script.isFiring)
            {
                M4A4Script.UpdateFiring(Time.deltaTime);
            }
            if (Input.GetButtonUp("Fire1"))
            {
                M4A4Script.StopFire();
            }
        }
        else
        {
            Anim.SetLayerWeight(2, 1);
        }
    }

    public void Equip(M4A4Script newWeapon)
    {
        M4A4Script = newWeapon;
        M4A4Script.raycastTarget = CrossshirTarget;
        Anim.SetLayerWeight(2, 0);
    }
}
