/*
 * - Edited by PrzemyslawNowaczyk (11.10.17)
 *   -----------------------------
 *   Deleting unused variables
 *   Changing obsolete methods
 *   Changing used input methods for consistency
 *   -----------------------------
 *
 * - Edited by NovaSurfer (31.01.17).
 *   -----------------------------
 *   Rewriting from JS to C#
 *   Deleting "Spawn" and "Explode" methods, deleting unused varibles
 *   -----------------------------
 * Just some side notes here.
 *
 * - Should keep in mind that idTech's cartisian plane is different to Unity's:
 *    Z axis in idTech is "up/down" but in Unity Z is the local equivalent to
 *    "forward/backward" and Y in Unity is considered "up/down".
 *
 * - Code's mostly ported on a 1 to 1 basis, so some naming convensions are a
 *   bit fucked up right now.
 *
 * - UPS is measured in Unity units, the idTech units DO NOT scale right now.
 *
 * - Default values are accurate and emulates Quake 3's feel with CPM(A) physics.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using TreeEditor;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

//Contains the command the user wishes upon the character
struct Cmd
{
    public float forwardMove;
    public float rightMove;
    public float upMove;
}

public class CPMPlayer : MonoBehaviour
{
    public Transform playerView;     // Camera
    public float playerViewYOffset = 0.6f; // The height at which the camera is bound to
    public float xMouseSensitivity = 30.0f;
    public float yMouseSensitivity = 30.0f;
//
    /*Frame occuring factors*/
    public float gravity = 21.25f;

    public float friction = 5.5f; //Ground friction

    /* Movement stuff */
    public float moveSpeed = 5.046875f;            // Ground move speed
    public float sprintSpeed = 7.5703125f;          //Ground sprint speed
    public float runAcceleration = 14.0f;         // Ground accel
    public float runDeacceleration = 10.0f;       // Deacceleration that occurs when running on the ground
    public float airAcceleration = 1.0f;          // Air accel
    public float airDecceleration = 1.0f;         // Deacceleration experienced when ooposite strafing
    public float airControl = 0.0f;               // How precise air control is
    public float sideStrafeAcceleration = 1.0f;  // How fast acceleration occurs to get up to sideStrafeSpeed when
    public float sideStrafeSpeed = 1.0f;          // What the max speed to generate when side strafing
    public float jumpSpeed = 8.0f;                // The speed at which the character's up axis gains when hitting jump
    public bool holdJumpToBhop = false;           // When enabled allows player to just hold jump button to keep on bhopping perfectly. Beware: smells like casual.

    /*print() style */
    public GUIStyle style;

    /*FPS Stuff for GUI*/
    public float fpsDisplayRate = 4.0f; // 4 updates per sec

    private int frameCount = 0;
    private float dt = 0.0f;
    private float fps = 0.0f;

    private CharacterController _controller;

    private LineRenderer lineRenderer;

    private Rigidbody rb;

    // Camera rotations
    private float rotX = 0.0f;
    private float rotY = 0.0f;

    private Vector3 moveDirectionNorm = Vector3.zero;
    public static Vector3 playerVelocity = Vector3.zero;
    private float playerTopVelocity = 0.0f;

    // Q3: players can queue the next jump just before he hits the ground
    private bool wishJump = false;

    // Used to display real time fricton values
    private float playerFriction = 0.0f;

    // Player commands, stores wish commands that the player asks for (Forward, back, jump, etc)
    private Cmd _cmd;



    //Sliding stuff
    Vector3 surfaceNormal;
    float slopeAngle;
    public static bool isGrounded;
    bool isSliding;


    //Bounce stuff (prediction)
    float distanceToGround;
    public int framesAhead = 10;
    bool b_HasBounced;
    bool b_hasJumped;
    bool b_ShouldIBounce;
    int bounceCounter = 0;

    //Strafing stuff
    float fpsCJ = 125;

    //Load & Save position vars
    Vector3 saveAngles;
    Vector3 savePosition;


    private void Start()
    {
        // Hide the cursor
        UnityEngine.Cursor.visible = false;
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;

        if (playerView == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                playerView = mainCamera.gameObject.transform;
        }

        // Put the camera inside the capsule collider
        playerView.position = new Vector3(
            transform.position.x,
            transform.position.y + playerViewYOffset,
            transform.position.z);

        _controller = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        //Apply sliding if neccessary
        if (Physics.Raycast(transform.position, -Vector3.up, out RaycastHit hit, _controller.height / 2 + 0.1f))
        {
            isGrounded = true;
        }
        else isGrounded = false;

       


        // Do FPS calculation
        frameCount++;
        dt += Time.deltaTime;
        if (dt > 1.0 / fpsDisplayRate)
        {
            fps = Mathf.Round(frameCount / dt);
            frameCount = 0;
            dt -= 1.0f / fpsDisplayRate;
        }


        /* Ensure that the cursor is locked into the screen */
        if (UnityEngine.Cursor.lockState != CursorLockMode.Locked) {
            if (Input.GetButtonDown("Fire1"))
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        }

        /* Camera rotation stuff, mouse controls this shit */
        rotX -= Input.GetAxisRaw("Mouse Y") * xMouseSensitivity * 0.02f;
        rotY += Input.GetAxisRaw("Mouse X") * yMouseSensitivity * 0.02f;
      


        // Clamp the X rotation
        if(rotX < -90)
            rotX = -90;
        else if(rotX > 90)
            rotX = 90;

        this.transform.rotation = Quaternion.Euler(0, rotY, 0); // Rotates the collider
        playerView.rotation     = Quaternion.Euler(rotX, rotY, 0); // Rotates the camera

        

        /* Movement, here's the important part */
        QueueJump();
        if(_controller.isGrounded)
            GroundMove();
        else if(!_controller.isGrounded)
            AirMove();

        // Move the controller
        _controller.Move(playerVelocity * Time.deltaTime);

        /* Calculate top velocity */
        Vector3 udp = playerVelocity;
        udp.y = 0.0f;
        if(udp.magnitude > playerTopVelocity)
            playerTopVelocity = udp.magnitude;

        //Need to move the camera after the player has been moved because otherwise the camera will clip the player if going fast enough and will always be 1 frame behind.
        // Set the camera's position to the transform
        playerView.position = new Vector3(
            transform.position.x,
            transform.position.y + playerViewYOffset,
            transform.position.z);

        //Should call these first since they will want to override our normal movements
        Sliding();


        HasBounced(ref isGrounded, ref b_hasJumped, ref b_HasBounced, ref b_ShouldIBounce);
        if (slopeAngle > _controller.slopeLimit && surfaceNormal.y > 0.30000001) PM_ProjectVelocity(surfaceNormal, ref playerVelocity, framesAhead);

        DebugDrawing();
    }

     /*******************************************************************************************************\
    |* MOVEMENT
    \*******************************************************************************************************/

    /**
     * Sets the movement direction based on player input
     */
    private void SetMovementDir()
    {
        _cmd.forwardMove = Input.GetAxisRaw("Vertical");
        _cmd.rightMove   = Input.GetAxisRaw("Horizontal");
    }

    /**
     * Queues the next jump just like in Q3
     */
    private void QueueJump()
    {
        if(holdJumpToBhop)
        {
            wishJump = Input.GetButton("Jump");
            return;
        }
    
        if(Input.GetButtonDown("Jump") && !wishJump)
            wishJump = true;
        if(Input.GetButtonUp("Jump"))
            wishJump = false;
    }

    /**
     * Execs when the player is in the air
    */
    private void AirMove()
    {
        Vector3 wishdir;
        float wishvel = airAcceleration;
        
        SetMovementDir();

        wishdir =  new Vector3(_cmd.rightMove, 0, _cmd.forwardMove);
        wishdir = transform.TransformDirection(wishdir);

        float wishspeed = wishdir.magnitude;
        wishspeed *= moveSpeed;

        wishdir.Normalize();
        moveDirectionNorm = wishdir;


        playerVelocity = Accelerate(wishdir, wishspeed, 1) * 0.0265625f;

        // Apply gravity
        playerVelocity.y -= gravity * Time.deltaTime;
    }

    /**
     * Called every frame when the engine detects that the player is on the ground
     */
    private void GroundMove()
    {
        Vector3 wishdir;

        // Apply friction if the player is queueing up the next jump (bhopping)
        if (!wishJump)
            ApplyFriction(1.0f);
        else
            ApplyFriction(0f);

        SetMovementDir();

        wishdir = new Vector3(_cmd.rightMove, 0, _cmd.forwardMove);
        wishdir = transform.TransformDirection(wishdir);
        wishdir.Normalize();
        moveDirectionNorm = wishdir;

        var wishspeed = wishdir.magnitude;

        //Sprinting
        if (Input.GetButton("Sprint") && _cmd.forwardMove > 0)
        {
            moveSpeed = sprintSpeed;
        }
        else moveSpeed = 5.046875f;

        wishspeed *= moveSpeed;

        playerVelocity = Accelerate(wishdir, wishspeed, runAcceleration) * 0.0265625f;

        // Reset the gravity velocity
        playerVelocity.y = -gravity * Time.deltaTime;

        if(wishJump)
        {
            playerVelocity.y = jumpSpeed;
            wishJump = false;
        }
    }

    /**
     * Applies friction to the player, called in both the air and on the ground
     */
    private void ApplyFriction(float t)
    {
        Vector3 vec = playerVelocity; // Equivalent to: VectorCopy();
        float speed;
        float newspeed;
        float control;
        float drop;

        vec.y = 0.0f;
        speed = vec.magnitude;
        drop = 0.0f;

        /* Only if the player is on the ground then apply friction */
        if(_controller.isGrounded)
        {
            control = speed < runDeacceleration ? runDeacceleration : speed;
            drop = control * friction * Time.deltaTime * t;
        }

        newspeed = speed - drop;
        playerFriction = newspeed;
        if(newspeed < 0)
            newspeed = 0;
        if(speed > 0)
            newspeed /= speed;

        playerVelocity.x *= newspeed;
        playerVelocity.z *= newspeed;
    }

    private Vector3 Accelerate(Vector3 wishdir, float wishspeed, float accel)
    {
        float addspeedQ;
        float accelspeedQ;
        float currentspeedQ;
        float valueQ;

        //Quake specific values
        float stopspeedQ = 100f;
        Vector3 playerVelocityQ = playerVelocity / 0.0265625f;
        Vector3 wishdirQ = wishdir / 0.0265625f;
        float wishspeedQ = wishspeed / 0.0265625f;
        float accelQ = accel;


        if (Input.GetKeyDown(KeyCode.Z))
        {
            fpsCJ = 125;
        }
        else if (Input.GetKeyDown(KeyCode.X))
        {
            fpsCJ = 250;
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            fpsCJ = 333;
        }
        else if (Input.GetKeyDown(KeyCode.V))
        {
            fpsCJ = 500;
        }

        float frametimeQ = 1 / fpsCJ;


        currentspeedQ = Vector3.Dot(playerVelocityQ, wishdir);
        addspeedQ = wishspeedQ - currentspeedQ;

        if (addspeedQ > 0)
        {
            //From BO1 stopspeed, gradually slows down player until they reach a threshold
            {
                if (stopspeedQ <= wishspeedQ)
                    valueQ = wishspeedQ;
                else
                    valueQ = stopspeedQ;
            }

            accelspeedQ = accelQ * frametimeQ * wishspeedQ;
            if (accelspeedQ > addspeedQ)
                accelspeedQ = addspeedQ;

            playerVelocityQ.x += accelspeedQ * wishdir.x;
            playerVelocityQ.z += accelspeedQ * wishdir.z;
            playerVelocity.y += accelspeedQ * playerVelocity.y * Time.deltaTime;
            //Debug.Log("FrameTime: " + 1.0 / 333 + " | Accelspeed: " + accelspeedQ + " Accel: " + accel + "WishSpeed: " + wishspeedQ);
        }
        

        playerVelocityQ.x = Mathf.Round(playerVelocityQ.x);
        playerVelocityQ.y = Mathf.Round(playerVelocityQ.y);
        playerVelocityQ.z = Mathf.Round(playerVelocityQ.z);
        //Debug.Log(frametimeQ);
        //
        //
        //Debug.Log(playerVelocity.y);

        return playerVelocityQ;
    }

    private void PM_ClipVelocity(ref Vector3 inVec, Vector3 normal, float overbounce)
    {
        //Ported from Q3 pretty buggy atm
        float backoff;
        float change;
        int i;
        
        backoff = Vector3.Dot(inVec, normal);
        
        if (backoff < 0)
        {
            backoff *= overbounce;
        }
        else
        {
            backoff /= overbounce;
        }
        
        for (i = 0; i < 3; i++)
        {
            change = normal[i] * backoff;
            inVec[i] -= change;
        }
    }

    private void Sliding()
    {
        ////Raycast for slopes
        RaycastHit hit;
        
        // Cast a ray from the character's position downwards
        if (Physics.Raycast(transform.position, -Vector3.up, out hit))
        {
            // Get the normal vector of the surface the ray hit
            surfaceNormal = hit.normal;
        }
        
        slopeAngle = Vector3.Angle(Vector3.up, surfaceNormal);
        
        if (slopeAngle > _controller.slopeLimit && Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit1, _controller.height / 2 + .5f))
        {
            
            isSliding = true;
            PM_ClipVelocity(ref playerVelocity, surfaceNormal, 1.001f);
        }
        else
        {
            isSliding = false;
        }
        
    }

    private void DebugDrawing()
    {

        //Public vars for read only purposes (external use)
        Debug.DrawLine(_controller.transform.position, transform.position + transform.forward , Color.red);
        Debug.DrawLine(_controller.transform.position, transform.position + playerVelocity , Color.blue);
        Debug.DrawLine(_controller.transform.position, transform.position + moveDirectionNorm, Color.green);

    }

    // x = 0
    // z = 1
    // y = 2
    private void PM_ProjectVelocity(Vector3 normalVector, ref Vector3 currentVelocity, int framesAhead)
    {

        float speed2DSquared = currentVelocity.z * currentVelocity.z + currentVelocity.x * currentVelocity.x;
        bool isNormalHorizontal = Math.Abs(normalVector.z) < 0.001;

        // Copy over input velocity to output
        if (isNormalHorizontal || speed2DSquared == 0.0)
        {
            return;
        }

        // Player is actually moving and its not a usual wall
        else
        {
            float unknownDot = normalVector.z * currentVelocity.z + currentVelocity.x * normalVector.x;

            // Black ops 1 pdb calls it that, not sure yet
            float newZ = -unknownDot / normalVector.y;

            float speedSquared = currentVelocity.z * currentVelocity.z + currentVelocity.x * currentVelocity.x + currentVelocity.y + currentVelocity.y;
            float newSpeedSquared = speed2DSquared + newZ * newZ;

            float lengthScale = Mathf.Sqrt((currentVelocity.y * currentVelocity.y + speedSquared) / (speedSquared + newZ * newZ));

            //Raycast prediction
            Vector3 futurePos = transform.position + currentVelocity * Time.fixedDeltaTime * framesAhead;
             RaycastHit hit;
             if (Physics.Raycast(futurePos, Vector3.down, out hit, distanceToGround + 8 * Time.fixedDeltaTime * framesAhead))
             {
                if ((float)lengthScale < 1.0 || newZ < 0.0 || currentVelocity.z > 0.0)
                {
                    currentVelocity.x = (float)lengthScale * currentVelocity.x;
                    currentVelocity.z = (float)lengthScale * currentVelocity.z;
                    currentVelocity.y = (float)lengthScale * newZ;

                    bounceCounter++;

                    if (!isGrounded)
                    {
                        //notificationManager.notify("Doing bounce!");
                        Debug.Log("Doing bounce!");

                    }
                }
                Debug.Log("lengthScale: " + (float)lengthScale + " | newZ: " + newZ + " | curVelocity: " + currentVelocity.z);
            }
             //distanceToGround = hit.distance;

            

        }
    }

    //private void PM_ProjectVelocity(Vector3 normal, ref Vector3 velIn, int framesAhead)
    //{
    //    float speedSqrd = velIn.z * velIn.z + velIn.x * velIn.x;
    //    
    //    if (normal.y < 0.001f || speedSqrd == 0.0f)
    //    {
    //        return;
    //    }
    //
    //    else
    //    {
    //        float normalized = -(normal.z * velIn.z + velIn.x * normal.x) / normal.y;
    //        float projection = Mathf.Sqrt((velIn.y * velIn.y + speedSqrd) / (speedSqrd + normalized * normalized));
    //
    //        //Raycast prediction
    //        Vector3 futurePos = transform.position + velIn * Time.fixedDeltaTime * framesAhead;
    //        RaycastHit hit;
    //        if (Physics.Raycast(futurePos, Vector3.down, out hit, distanceToGround + 8 * Time.fixedDeltaTime * framesAhead))
    //        {
    //            if (projection < 1f || normalized < 0f || velIn.y > 0f)
    //            {
    //                velIn.x = projection * velIn.x;
    //                velIn.z = projection * velIn.z;
    //                velIn.y = projection * normalized;
    //                b_HasBounced = true;
    //                bounceCounter++;
    //            }
    //            Debug.Log("Projection: " + projection + " | Normalized: " + normalized + " | velIn.y: " + velIn.y);
    //        }
    //        //distanceToGround = hit.distance;
    //    }
    //}

    private void HasBounced(ref bool isGrounded, ref bool b_hasJumped, ref bool b_HasBounced, ref bool b_ShouldIBounce)
    {
        //Requesting bounce
        if (isGrounded)
        {
            b_hasJumped = false;
            b_HasBounced = false;
            b_ShouldIBounce = true;
        }
        //else if (b_HasBounced && !isGrounded) //If we haven't bounced we need to reset our states so we can bounce in the future.
        //{
        //    b_HasBounced = false;
        //    b_hasJumped = true;
        //    b_ShouldIBounce = false;
        //}
        b_ShouldIBounce = true;
    }

    private void OnGUI()
    {
        style.fontSize = 28;
        GUI.Label(new Rect(0, 0, 400, 100), "FPS: " + fps, style);
        var ups = _controller.velocity;
        ups.y = 0;
        GUI.Label(new Rect(0, 25, 400, 100), "Speed: " + Mathf.Round(ups.magnitude * 100) / 100 + "ups", style);
        GUI.Label(new Rect(0, 50, 400, 100), "Top Speed: " + Mathf.Round(playerTopVelocity * 100) / 100 + "ups", style);
        GUI.Label(new Rect(0, 75, 400, 100), "Slope Angle: " + Mathf.Round(slopeAngle * 100) / 100, style);

        GUI.Label(new Rect(0, 125, 400, 100), "b_HasBounced: " + b_HasBounced, style);
        GUI.Label(new Rect(0, 150, 400, 100), "b_hasJumped: " + b_hasJumped, style);
        GUI.Label(new Rect(0, 175, 400, 100), "b_ShouldIBounce: " + b_ShouldIBounce, style);
        GUI.Label(new Rect(0, 200, 400, 100), "isGrounded: " + isGrounded, style);

        GUI.Label(new Rect(0, 225, 400, 100), "bounce counter: " + bounceCounter, style);
        GUI.Label(new Rect(0, 250, 400, 100), "fpsCJ: " + fpsCJ, style);
    }
}


