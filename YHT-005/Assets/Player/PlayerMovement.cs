using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
            aiming = false;
        
        Sprint();
        Walk();
    }

    private void FixedUpdate()
    {
        anim.SetFloat("Velocityx", rb.velocity.x);
        anim.SetFloat("Velocityz", rb.velocity.z);

        if (rb.velocity.magnitude > 3)
        {
            anim.SetBool("Walking", false);
            anim.SetBool("Running", true);
        }
        else if (rb.velocity.magnitude <= 3.1 && rb.velocity.magnitude >= .1f)
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
    [SerializeField] private LayerMask Ground;
    private bool aiming = false;
    private (bool success, Vector3 position) GetMousePosition()
    {
        Ray mousepos = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(mousepos, out var hitInfo, Mathf.Infinity, Ground))
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
            var Direction = position - transform.position;
            Direction.y = 0;
            transform.forward = Direction;
        }
        //------------------------------------- Bu da bir yol ama yukarýdaki daha düzenli hali -------------------------------------
        //Ray mousepos = Camera.main.ScreenPointToRay(Input.mousePosition);
        //Physics.Raycast(mousepos, out var hitInfo, Mathf.Infinity, Ground);
        //if (hitInfo.collider != null)
        //{
        //    Vector3 Direction = hitInfo.point - transform.position;
        //    Direction.y = 0;
        //    transform.forward = Direction;
        //}
        //--------------------------------------------------------------------------------------------------------------------------
    }
    //------------------------------------- Look At Mouse End -------------------------------------
}
