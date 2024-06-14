using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.UIElements;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float BaseSpeed;
    [SerializeField] private float SprintCarpaný;
    private float TotalSpeed;
    private float SprintValue;

    private Rigidbody rb;
    private Animator anim;
    private Vector3 Direc;

    public GameObject LookAtObject;
    public GameObject RigLayerWeaponAim;
    public GameObject RigLayerBodyAim;

    public float ADSSpeed;
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = transform.GetChild(0).GetComponent<Animator>();
    }
    private void Update()
    {
        if (Input.GetMouseButton(1))
            LookAtMouse();
        else
        {
            aiming = false;
        }
     
        
        Sprint();
        Walk();
    }

    private void FixedUpdate()
    {
        if (aiming)
        {
            anim.SetBool("Running", false);
            anim.SetBool("Walking", false);
            OnAimAnimations();
        }
        else
        {
            if (rb.velocity.magnitude > 3)
            {
                anim.SetBool("Walking", false);
                anim.SetBool("Running", true);
            }
            else if (rb.velocity.magnitude <= 3.02 && rb.velocity.magnitude >= .02f)
            {
                anim.SetBool("Running", false);
                anim.SetBool("Walking", true);
            }
            else
            {
                anim.SetBool("Running", false);
                anim.SetBool("Walking", false);
            }
        }

        if (RigLayerWeaponAim.GetComponent<Rig>().weight == 1 && anim.GetLayerWeight(1) == 1 && !aiming)
        {
            StartCoroutine(ChangeAnimWeight(ADSSpeed));
            StartCoroutine(ChangeRigLayerAimWeight(ADSSpeed, RigLayerWeaponAim));
            StartCoroutine(ChangeRigLayerAimWeight(ADSSpeed, RigLayerBodyAim));
        }
    }

    private void OnAimAnimations()
    {
        if (RigLayerWeaponAim.GetComponent<Rig>().weight == 0 && anim.GetLayerWeight(1) == 0)
        {
            StartCoroutine(ChangeAnimWeight(ADSSpeed));
            StartCoroutine(ChangeRigLayerAimWeight(ADSSpeed, RigLayerWeaponAim));
            StartCoroutine(ChangeRigLayerAimWeight(ADSSpeed, RigLayerBodyAim));
        }

        var (success, position) = GetMousePosition();

        if (success)
        {
            float MouseDirecX = Mathf.Clamp(position.x - transform.position.x, -1f, 1f);

            float MouseDirecZ = Mathf.Clamp(position.z - transform.position.z, -1f, 1f);

            anim.SetFloat("MouseDirecx", MouseDirecX);
            anim.SetFloat("MouseDirecz", MouseDirecZ);

            anim.SetFloat("Velocityx", rb.velocity.x);
            anim.SetFloat("Velocityz", rb.velocity.z);
        }
    }

    private void Walk()
    {
        TotalSpeed = BaseSpeed * SprintValue ;
        Direc.x = Input.GetAxisRaw("Horizontal");
        Direc.z = Input.GetAxisRaw("Vertical");
        Direc.Normalize();
        rb.velocity = new Vector3(Direc.x * TotalSpeed, rb.velocity.y, Direc.z * TotalSpeed);
        if (!aiming)
            Rotation();

    }

    private void Rotation()
    {
        if (Direc.x == 0 && Direc.z > 0) { transform.rotation = Quaternion.Euler(0, 0, 0); }
        if (Direc.x > 0 && Direc.z > 0) { transform.rotation = Quaternion.Euler(0, 45, 0); }
        if (Direc.x > 0 && Direc.z == 0) { transform.rotation = Quaternion.Euler(0, 90, 0); }
        if (Direc.x > 0 && Direc.z < 0) { transform.rotation = Quaternion.Euler(0, 135, 0); }
        if (Direc.x == 0 && Direc.z < 0) { transform.rotation = Quaternion.Euler(0, 180, 0); }
        if (Direc.x < 0 && Direc.z < 0) { transform.rotation = Quaternion.Euler(0, -135, 0); }
        if (Direc.x < 0 && Direc.z == 0) { transform.rotation = Quaternion.Euler(0, -90, 0); }
        if (Direc.x < 0 && Direc.z > 0) { transform.rotation = Quaternion.Euler(0, -45, 0); }
    }

    private void Sprint()
    {
        if (Input.GetKey(KeyCode.LeftShift))
            SprintValue = SprintCarpaný;
        else
            SprintValue = 1;
    }

    //------------------------------------- Look At Mouse Start -------------------------------------   
    private bool aiming = false;
    private (bool success, Vector3 position) GetMousePosition()
    {
        Ray mousepos = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(mousepos, out var hitInfo, Mathf.Infinity))
        {
            return (success: true, position: hitInfo.point);
        }
        else
        {
            return (success: false, position: Vector3.zero);
        }
    }
    private void LookAtMouse()
    {
        aiming = true;
        var (success, position) = GetMousePosition();
        if (success)
        {
            LookAtObject.transform.position = position;
            var Direction = position - transform.position;
            Direction.y = 0;
            transform.forward = Direction;
            transform.Rotate(new Vector3(0, 31.2f, 0));
        }
    }
    //----------------------------------------------------------------------------------------------

    //------------------------------------- Layer Weights ------------------------------------------
    private float endValue;
    private IEnumerator ChangeRigLayerAimWeight(float time, GameObject weightedLayer)
    {
        float startValue = weightedLayer.GetComponent<Rig>().weight;

        if (startValue == 0)
            endValue = 1f;
        else if (startValue == 1)
            endValue = 0f;

        float elapsedTime = 0f;

        while (elapsedTime < time)
        {
            elapsedTime += Time.deltaTime;
            weightedLayer.GetComponent<Rig>().weight = Mathf.Lerp(startValue, endValue, Mathf.SmoothStep(0, 1, elapsedTime / time));
            yield return null;
        }
        //if (endValue == 0f)
        //{
        //    LookAtObject.transform.localPosition = new Vector3(0, 0, 13);
        //}
        weightedLayer.GetComponent<Rig>().weight = endValue;
    }
    private IEnumerator ChangeAnimWeight(float time)
    {
        float startValue = anim.GetLayerWeight(1);

        if (startValue == 0)
            endValue = 1f;
        else if (startValue == 1)
            endValue = 0f;

        float elapsedTime = 0f;

        while (elapsedTime < time)
        {
            elapsedTime += Time.deltaTime;            
            anim.SetLayerWeight(1, Mathf.Lerp(startValue, endValue, Mathf.SmoothStep(0, 1, elapsedTime / time)));
            yield return null;
        }
        anim.SetLayerWeight(1, endValue);
    }
    //----------------------------------------------------------------------------------------------
}
