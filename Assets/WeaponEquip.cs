using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class WeaponEquip : MonoBehaviour
{
    //private M4A4Script m4Script;
    private PlayerMovement pMovement;

    public GameObject WeaponHolder;
    private GameObject EquipedWeapon;
    private GameObject Weapon;

    public bool hasWeapon;
    private bool WeaponCheck = false;

    public RigBuilder rigBuilder;
    public Rig HandRigLayer;
    public TwoBoneIKConstraint LeftHand;
    public TwoBoneIKConstraint RightHand;

    public Animator Anim;

    public Transform ShootDirec;
    void Start()
    {
        pMovement = GetComponent<PlayerMovement>();

        if (WeaponHolder.transform.childCount == 0)
            hasWeapon = false;
        else
            hasWeapon = true;
        //if (WeaponHolder.transform.childCount == 1)
        //{
        //    EquipedWeapon = WeaponHolder.transform.GetChild(0).gameObject;
        //    EquipedWeapon.GetComponent<Rigidbody>().isKinematic = true;
        //    LeftHand.data.target = EquipedWeapon.transform.GetChild(0);
        //    RightHand.data.target = EquipedWeapon.transform.GetChild(1);
        //    EquipedWeapon.GetComponent<M4A4Script>().raycastTarget = ShootDirec;
        //    //Düzenlenecek
        //}
    }

    void Update()
    {
        if (WeaponCheck && !hasWeapon)
        {
            if (Input.GetButtonDown("Interact"))
            {
                EquipWeapon();
            }
        }
        if (hasWeapon && !pMovement.aiming)
        {
            if (Input.GetButtonDown("Drop"))
            {
                DropWeapon(EquipedWeapon);
            }
        }

        if (hasWeapon)
        {
            Anim.SetLayerWeight(2, 0);
            HandRigLayer.weight = 1;
        }
        else
        {
            Anim.SetLayerWeight(2, 1);
            HandRigLayer.weight = 0;
        }
    }
    private void EquipWeapon()
    {
        hasWeapon = true;
        EquipedWeapon = Instantiate(Weapon, WeaponHolder.transform.position, WeaponHolder.transform.rotation, WeaponHolder.transform);
        EquipedWeapon.GetComponent<M4A4Script>().enabled = true;
        LeftHand.data.target = EquipedWeapon.transform.GetChild(0);
        RightHand.data.target = EquipedWeapon.transform.GetChild(1);
        rigBuilder.Build();
        EquipedWeapon.GetComponent<Rigidbody>().isKinematic = true;
        EquipedWeapon.GetComponent<M4A4Script>().raycastTarget = ShootDirec;
        Destroy(Weapon);
    }
    private void DropWeapon(GameObject DroppedWeaponInput)
    {
        hasWeapon = false;
        GameObject DroppedWeapon = Instantiate(DroppedWeaponInput, this.transform.position, Quaternion.Euler(45,0,0));
        DroppedWeapon.GetComponent<M4A4Script>().enabled = false;
        DroppedWeapon.GetComponent<Rigidbody>().isKinematic = false;
        Destroy(DroppedWeaponInput);
    }
    private void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.CompareTag("Weapon"))
        {
            WeaponCheck = true;                                       
            Weapon = collision.gameObject;                                
        }
    }
    private void OnTriggerExit(Collider collision)
    {
        if (collision.gameObject.CompareTag("Weapon"))
        {
            Weapon = null;                       
            WeaponCheck = false;                
        }
    }
}
