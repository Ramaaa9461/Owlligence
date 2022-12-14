using System.Collections;

using UnityEngine;
using UnityEngine.UI;


public class PlayerMovement : MonoBehaviour
{
    [Header("Gravity Values")]
    [SerializeField] float jumpForce = 15.0f;
    [SerializeField] float fallGravity = -18.0f;
    [SerializeField] float flightGravity = -9.0f;
    [SerializeField] float verticalSpeed = 0.0f;
    [SerializeField] float duration = 0;
    [SerializeField] float _dashVelocity = 0;
    [SerializeField] float rotationSpeed = 0;
     float gravity = -9.81f;

    [Header("Speed Values")]
    [SerializeField] float maxSpeed = 15.0f;
    [SerializeField] float velocity = 3.0f;
    float currentSpeed = 0.0f;


    [Header("References")]
    [SerializeField] InputManagerReferences inputManagerReferences = null;
    [SerializeField] Transform characterBase;
    [SerializeField] AudioSource dashSound = null;
    [SerializeField] StepsSounds stepsSounds = null;
    [SerializeField] JumpSounds jumpSounds = null;

    CharacterController characterController;
    Camera cam;
    bool stayInWater;
    bool isGrounded;
    bool useDash = true;
    bool toLandSound = true;
    bool hasJustJumped = false;

    Coroutine startDash;
    Coroutine breaking;

    Vector3 movement = Vector3.zero;
    Vector3 dashMovement = Vector3.zero;
    Vector3 direction;


    //Animation Variables
    Animator animatorController;
    [SerializeField] LayerMask mapLayer;

    //Variables para modo Debug
    [SerializeField] Toggle debugModeUI;
    public bool debugMode = false;



    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        cam = Camera.main;
        animatorController = GetComponentInChildren<Animator>();

        debugModeUI.isOn = debugMode;
    }

    void Update()
    {
        MovePlayer();

        if (Input.GetKeyDown(KeyCode.T))
        {
            debugMode = !debugMode;
            debugModeUI.isOn = debugMode;
        }
    }



    void MovePlayer()
    {
        float hor = Input.GetAxis(inputManagerReferences.GetHorizontalMovementName());
        float ver = Input.GetAxis(inputManagerReferences.GetVerticalMovementName());

        movement = dashMovement * Time.deltaTime;

        if (hor != 0 || ver != 0)
        {
            if (breaking != null)
            {
                StopCoroutine(breaking);
                breaking = null;
            }

            Vector3 forward = cam.transform.forward;
            forward.y = 0;
            forward.Normalize();

            Vector3 right = cam.transform.right;
            right.y = 0;
            right.Normalize();

            direction = forward * ver + right * hor;
            direction.Normalize();


            currentSpeed += velocity;
            if (currentSpeed >= maxSpeed)
            {
                currentSpeed = maxSpeed;
            }

            if (debugMode)
            {
                currentSpeed = 25;
            }

            animatorController.SetFloat("PlayerHorizontalVelocity", currentSpeed / maxSpeed);

            movement += direction * currentSpeed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * Time.deltaTime);

            if (isGrounded)
            {
                if (stayInWater)
                {
                    stepsSounds.randomSoundStepInWater();
                }
                else
                {
                    stepsSounds.randomSoundStepOnLand();
                }
            }
        }
        else
        {
            if (currentSpeed / maxSpeed > 0.2f)
            {
                if (breaking == null)
                {
                    breaking = StartCoroutine(Breaking());
                }
            }
            else if (breaking == null)
            {
                currentSpeed = 0;
                animatorController.SetFloat("PlayerHorizontalVelocity", currentSpeed);
            }
        }

        CheckJump();

        movement.y = verticalSpeed * Time.deltaTime;
        characterController.Move(movement);

        calculateDistanceToFloor();
    }
    void CheckJump()
    {
        isGrounded = IsGrounded();

        if (!isGrounded || hasJustJumped)
        {
            verticalSpeed += gravity * Time.deltaTime;
        }
        else
        {
            gravity = fallGravity;
            verticalSpeed = 0;
            useDash = true;

            if (toLandSound)
            {
                if (stayInWater)
                {
                    jumpSounds.randomSoundJumpInWater();
                }
                else
                {
                    jumpSounds.randomSoundJumpOnLand();
                }

                toLandSound = false;
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isGrounded)
            {
                hasJustJumped = true;
                StartCoroutine(waitHasJustJumped());

                verticalSpeed = jumpForce;

                if (stayInWater)
                {
                    jumpSounds.randomSoundJumpInWater();
                }
                else
                {
                    jumpSounds.randomSoundJumpOnLand();
                }

                toLandSound = true;

                animatorController.SetTrigger("Jumped");
            }
            else if (useDash && startDash == null)
            {
                AnimatorStateInfo stateInfo = animatorController.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName("Base Layer.Jumped") || stateInfo.IsName("Base Layer.Falled") &&
                    animatorController.GetFloat("DistanceToFloor") > 0.04f)
                {
                    animatorController.SetTrigger("Dashed");
                    startDash = StartCoroutine(StartDash());
                    verticalSpeed = 0;
                    dashSound.Play();

                    gravity = flightGravity;
                    useDash = false;
                }
            }
        }

        animatorController.SetBool("IsGrounded", isGrounded);

        if (debugMode)
        {
            if (Input.GetKey(KeyCode.M))
            {
                verticalSpeed += 80 * Time.deltaTime;
            }
        }
    }



    void calculateDistanceToFloor()
    {
        RaycastHit hit;

        if (Physics.Raycast(characterBase.position + Vector3.up / 10, -Vector3.up, out hit, 150.0f))
        {
            float distanceToFloor = 0;
            distanceToFloor = Vector3.Distance(characterBase.position, hit.point);

            animatorController.SetFloat("DistanceToFloor", distanceToFloor / 100);
        }
    }

    bool IsGrounded()
    {
        RaycastHit hit;
        int divisions = 10;
        Vector3 offSet;

        if (Physics.Raycast(characterBase.position + Vector3.up * 0.1f, Vector3.down, out hit, 0.2f, mapLayer, QueryTriggerInteraction.Ignore))
        {
            return true;
            //return !hit.collider.isTrigger;
        }
        else
        {

            float angle = (2 * Mathf.PI) / divisions;
            float x, z;

            for (int i = 0; i < divisions; i++)
            {
                x = Mathf.Cos(angle * i);
                z = Mathf.Sin(angle * i);



                offSet = new Vector3(x / 3f, 0.0f, z / 3f); //Lo divido para achicar el cirulo
                if (Physics.Raycast(characterBase.position + offSet + Vector3.up * 0.1f, Vector3.down, out hit,
                    0.2f, mapLayer, QueryTriggerInteraction.Ignore))
                {   
                    return true;
                    //return !hit.collider.isTrigger;
                }
            }
        }

        return false;
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Water"))
        {
            stayInWater = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Water"))
        {
            stayInWater = false;
        }
    }

    IEnumerator Breaking()
    {
        while (currentSpeed != 0)
        {
            //currentSpeed = currentSpeed <= 0 ? 0 : currentSpeed - velocity * 1.5f; //Lo multiplico para que coincida con la animacion
            if (currentSpeed < 0)
            {
                currentSpeed = 0;
            }
            else
            {
                currentSpeed -= velocity * 1.5f;
            }

            animatorController.SetFloat("PlayerHorizontalVelocity", currentSpeed);
            movement += direction * currentSpeed * Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        breaking = null;

    }

    IEnumerator waitHasJustJumped()
    {
        yield return new WaitForSeconds(1);
        hasJustJumped = false;
    }

    IEnumerator StartDash()
    {
        float timer = 0;

        while (timer <= duration)
        {
            float interpolationValue = 1 - timer / duration;

            //dashVelocity = Vector3.Lerp(transform.position, endPosition, interpolationValue);
            dashMovement = transform.forward * _dashVelocity * interpolationValue;


            timer += Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        dashMovement = Vector3.zero;// = endPosition;
        startDash = null;
    }
}



//        Debug.DrawRay(characterBase.position, Vector3.down / 10, Color.red, 100);
//Coseno X
//seno Y

