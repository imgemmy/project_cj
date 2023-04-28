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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    public float moveSpeed = 5.046875f;                // Ground move speed
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

    // Camera rotations
    private float rotX = 0.0f;
    private float rotY = 0.0f;

    private Vector3 moveDirectionNorm = Vector3.zero;
    private Vector3 playerVelocity = Vector3.zero;
    private float playerTopVelocity = 0.0f;

    // Q3: players can queue the next jump just before he hits the ground
    private bool wishJump = false;

    // Used to display real time fricton values
    private float playerFriction = 0.0f;

    // Player commands, stores wish commands that the player asks for (Forward, back, jump, etc)
    private Cmd _cmd;

    private void Start()
    {
        // Hide the cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

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
    }

    private void Update()
    {

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
        if (Cursor.lockState != CursorLockMode.Locked) {
            if (Input.GetButtonDown("Fire1"))
                Cursor.lockState = CursorLockMode.Locked;
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


        Accelerate(wishdir, wishspeed, 1);

        // Apply gravity
        playerVelocity.y -= gravity * Time.deltaTime;
    }

    /**
     * Called every frame when the engine detects that the player is on the ground
     */
    private void GroundMove()
    {
        Vector3 wishdir;

        // Do not apply friction if the player is queueing up the next jump
        if (!wishJump)
            ApplyFriction(1.0f);
        else
            ApplyFriction(0);

        SetMovementDir();

        wishdir = new Vector3(_cmd.rightMove, 0, _cmd.forwardMove);
        wishdir = transform.TransformDirection(wishdir);
        wishdir.Normalize();
        moveDirectionNorm = wishdir;

        var wishspeed = wishdir.magnitude;
        wishspeed *= moveSpeed;

        Accelerate(wishdir, wishspeed, runAcceleration);

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

    private void Accelerate(Vector3 wishdir, float wishspeed, float accel)
    {
        float addspeed;
        float accelspeed;
        float currentspeed;
        float value;
        float stopspeed = 2;

        currentspeed = Vector3.Dot(playerVelocity, wishdir);
        addspeed = wishspeed - currentspeed;

        if (addspeed > 0)
        {
            //From BO1 stopspeed, gradually slows down player until they reach a threshold
            {
                if (stopspeed <= wishspeed)
                    value = wishspeed;
                else
                    value = stopspeed;
            }

            accelspeed = accel * Time.deltaTime * value;
            if (accelspeed > addspeed)
                accelspeed = addspeed;

            playerVelocity.x += accelspeed * wishdir.x;
            playerVelocity.z += accelspeed * wishdir.z;

        }

        //playerVelocity.x = Mathf.Round(playerVelocity.x) * Time.deltaTime;
        //playerVelocity.y = Mathf.Round(playerVelocity.y) * Time.deltaTime;
        //playerVelocity.z = Mathf.Round(playerVelocity.z) * Time.deltaTime;
    }

   //private void PM_ClipVelocity(Vector3 inVec, Vector3 normal, Vector3 outVec, float overbounce)
   //{
   //    float backoff;
   //    float change;
   //    int i;
   //
   //    backoff = DotProduct(in, normal);
   //
   //    if (backoff < 0)
   //    {
   //        backoff *= overbounce;
   //    }
   //    else
   //    {
   //        backoff /= overbounce;
   //    }
   //
   //    for (i = 0; i < 3; i++)
   //    {
   //        change = normal[i] * backoff;
	//	out[i] = in[i] -change;
   //    }
   //}

    private void OnGUI()
    {
        GUI.Label(new Rect(0, 0, 400, 100), "FPS: " + fps, style);
        var ups = _controller.velocity;
        ups.y = 0;
        GUI.Label(new Rect(0, 15, 400, 100), "Speed: " + Mathf.Round(ups.magnitude * 100) / 100 + "ups", style);
        GUI.Label(new Rect(0, 30, 400, 100), "Top Speed: " + Mathf.Round(playerTopVelocity * 100) / 100 + "ups", style);
    }
}


