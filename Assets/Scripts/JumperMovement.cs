using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using TreeEditor;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.UIElements;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;

struct cmd
{
    public float forwardMove;
    public float rightMove;
    public float upMove;
}

public class JumperMovement : MonoBehaviour
{
    //Player controller
    public Transform playerView;     // Camera
    public float playerViewYOffset = 0.6f; // The height at which the camera is bound to
    public float xMouseSensitivity = 30.0f;
    public float yMouseSensitivity = 30.0f;

    private CharacterController _controller;

    // Camera rotations
    private float rotX = 0.0f;
    private float rotY = 0.0f;

    // Player commands, stores wish commands that the player asks for (Forward, back, jump, etc)
    private cmd cmd;

    //Player Movement
    Vector3 wishDirNorm = Vector3.zero;                     //WishDir normalized
    public static Vector3 playerVelocity = Vector3.zero;    //Velocity of our player
    public static bool isOnGround;

    //Player Movement Physics
    public float g_gravity = 800f;                           //Gravity
    public float friction = 5.5f;                            //Ground friction
    public float g_speed = 190f;                             //Player speed
    public float sprintSpeedScale = 1.5f;                    //Player sprint speed scale (multiplier to g_speed, ex 285 when sprinting)
    public float stopspeed = 100f;                           //Player stop/deceleration speed on ground
    public float groundAccel = 9f;                           //Player acceleration on ground
    public float jump_height = 39f;                          //Players Jump Height


    //ShowFPS variables
    private float fpsDisplayRate = 4.0f; // 4 updates per sec
    private int frameCount = 0;
    private float dt = 0.0f;
    private float fps = 0.0f;

    //PM_ProjectVelocity variables
    float distanceToGround;
    Vector3 surfaceNormal;

    //Debug variables
    public GUIStyle style;                                   //GUI Style for debugging
    int bounceCounter = 0;                                   //Count bounces from PM_ProjectVelocity

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
    }

    private void Update()
    {
        //Lock camera to player and misc stuff like clamp
        SetCameraToPlayer();

        //Set FPS
        {
            QualitySettings.vSyncCount = 0;

            if (Input.GetKeyDown(KeyCode.E))
            {
                Application.targetFrameRate = 125;
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                Application.targetFrameRate = 250;
            }
            if (Input.GetKeyDown(KeyCode.T))
            {
                Application.targetFrameRate = 333;
            }
            if (Input.GetKeyDown(KeyCode.Y))
            {
                Application.targetFrameRate = 500;
            }
        }

        //Main Calculations go here
        {
            //Check if on ground
            if (PM_CheckJump())
            {
                isOnGround = true;
            }
            else isOnGround = false;

            //Run movement according to ground state
            if (isOnGround)
                PM_WalkMove();
            else
                PM_AirMove();

            // Move the controller & convert back to unity units
            // This should be the last movement call
            playerVelocity.x = MathF.Round(playerVelocity.x);
            playerVelocity.z = MathF.Round(playerVelocity.z);
            playerVelocity.y = MathF.Round(playerVelocity.y);
            _controller.Move(toUnityUnitsVec3(playerVelocity) * Time.deltaTime);
        }

        //Update camera after all calculations to avoid it being 1 frame behind
        UpdateCameraAfterCalculations();

        //Debug drawing wishdir/wishspeed etc
        DebugDrawing();
        ShowFPS();
    }

    private void SetCameraToPlayer()
    {
        /* Ensure that the cursor is locked into the screen */
        if (UnityEngine.Cursor.lockState != CursorLockMode.Locked)
        {
            if (Input.GetButtonDown("Fire1"))
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        }

        /* Camera rotation stuff, mouse controls this shit */
        rotX -= Input.GetAxisRaw("Mouse Y") * xMouseSensitivity * 0.02f;
        rotY += Input.GetAxisRaw("Mouse X") * yMouseSensitivity * 0.02f;

        // Clamp the X rotation
        if (rotX < -85)
            rotX = -85;
        else if (rotX > 85)
            rotX = 85;

        this.transform.rotation = Quaternion.Euler(0, rotY, 0); // Rotates the collider
        playerView.rotation = Quaternion.Euler(rotX, rotY, 0); // Rotates the camera
    }
    private void UpdateCameraAfterCalculations()
    {
        //Need to move the camera after the player has been moved because otherwise the camera will clip the player if going fast enough and will always be 1 frame behind.
        // Set the camera's position to the transform
        playerView.position = new Vector3(
            transform.position.x,
            transform.position.y + playerViewYOffset,
            transform.position.z);
    }


    //Movement
    private void PM_SetMovementDir()
    {
        cmd.forwardMove = Input.GetAxisRaw("Vertical");
        cmd.rightMove = Input.GetAxisRaw("Horizontal");
    }
    private void PM_Accelerate(Vector3 wishdir, float wishspeed, float accel)
    {
        Vector3 velocityLocal = playerVelocity;
        float value;

        float currentspeed = Vector3.Dot(velocityLocal, wishdir);
        float addspeed = wishspeed - currentspeed; // 190 - current speed

        if (addspeed > 0.0)
        {
            if (wishspeed >= stopspeed) // if (wishspeed >= 100)
                value = wishspeed;
            else
                value = 100;

            float accelspeed = accel * Time.deltaTime * value;
            if (accelspeed > addspeed)
                accelspeed = addspeed;

            velocityLocal.x = (accelspeed * wishdir.x) + velocityLocal.x;
            velocityLocal.z = (accelspeed * wishdir.z) + velocityLocal.z;
            velocityLocal.y = (accelspeed * wishdir.y) + velocityLocal.y;

        }

        playerVelocity.x = Mathf.Round(velocityLocal.x);
        playerVelocity.y = Mathf.Round(velocityLocal.y);
        playerVelocity.z = Mathf.Round(velocityLocal.z);

    }
    private bool PM_CheckJump()
    {
        Vector3 raycastOrigin = transform.position + Vector3.up * 0.1f; // Move the raycast origin up slightly
        Vector3 raycastDirection = -Vector3.up; // Cast the raycast straight down

        Debug.DrawLine(raycastOrigin, raycastOrigin + raycastDirection * (_controller.height / 2 + 0.0f), Color.black);

        if ( _controller.isGrounded)
        {
            //Debug.DrawLine(raycastOrigin, hit.point, Color.yellow);
            return true;
        }
        else
        {
            return false;
        }
    }
    private void PM_WalkMove()
    {
        //Apply friction first
        PM_Friction();

        //Create wishdir
        PM_SetMovementDir();
        Vector3 wishdir;
        wishdir = new Vector3(cmd.rightMove, 0, cmd.forwardMove);

        //Vector normalize
        wishdir = transform.TransformDirection(wishdir);
        wishdir.Normalize();

        wishDirNorm = wishdir; //wishDirNorm = wishvel in cod4/quake

        //If player is sprinting scale g_speed
        if (Input.GetButton("Sprint") && cmd.forwardMove > 0)
        {
            g_speed = 190 * sprintSpeedScale; //Scale by 1.5
        }
        else g_speed = 190; //Set back to 190 if not sprinting

        //Wishspeed
        float wishspeed = wishdir.magnitude;
        wishspeed = wishspeed * g_speed;

        PM_ProjectVelocity(surfaceNormal, ref wishdir, 10);
        PM_Accelerate(wishdir, wishspeed, groundAccel);

        //Missing PM_StepSlide here, will add if it is needed.

        PM_ProjectVelocity(surfaceNormal, ref playerVelocity, 10);

        // Reset the gravity velocity
        playerVelocity.y = -g_gravity * Time.deltaTime;


        //Jumping, this MAY NOT BE RIGHT!!! PORTED FROM WIGGLE WIZARD AAAAAAAHHHHHH
        if (Input.GetButtonDown("Jump") && isOnGround)
        {

            playerVelocity.y = jump_height;

        }


    }
    private void PM_AirMove()
    {
        PM_SetMovementDir();
        PM_Friction();

        float cmdScale = g_speed;

        //Create wishdir and normalize
        Vector3 wishdir;
        wishdir = new Vector3(cmd.rightMove, 0, cmd.forwardMove);
        //Vector normalize
        wishdir = transform.TransformDirection(wishdir);
        wishdir.Normalize();

        //for debug drawing
        wishDirNorm = wishdir;

        // normalize returns length before normalizing
        float wishSpeed = wishdir.magnitude * cmdScale;
        PM_Accelerate(wishdir, wishSpeed, 1.0f);
        if (isOnGround)
            PM_ClipVelocity(ref playerVelocity, surfaceNormal, ref playerVelocity);



        //Gravity, this MAY NOT BE RIGHT!!! PORTED FROM WIGGLE WIZARD AAAAAAAHHHHHH
        float resultingGravity = MathF.Abs(g_gravity * Time.deltaTime);
        // Apply gravity
        playerVelocity.y -= resultingGravity;
    }
    private void PM_Friction()
    {
        
        //COD4
        float localVelocityY = playerVelocity.y;
        if (isOnGround)
        {
            localVelocityY = 0;
        }
        
        float pmldMagnitude = playerVelocity.x * playerVelocity.x + playerVelocity.z * playerVelocity.z + localVelocityY * localVelocityY;
        float pmldLength = MathF.Sqrt(pmldMagnitude);
        float pmldLengthCopy = pmldLength;
        if (pmldLength < 1.0f)
        {
            playerVelocity.x = 0f;
            playerVelocity.z = 0f;
            playerVelocity.y = 0f;
            return;
        }
        float v5 = 0f;
        float deceleration;
        float pmla = 0;

        if (!isOnGround)
            goto LABEL_19;
        
        if (stopspeed <= pmldLengthCopy)
            deceleration = pmldLength;
        else
            deceleration = stopspeed;
        if (isOnGround)
        {  
            //Dont want jump slowdoown from jumporiginz
            //deceleration = JumpOriginZZeroSomething(playerState) * deceleration;
            deceleration = 1 * deceleration;
            pmldLengthCopy = pmldLength;
             v5 = 0.0f;
            
        }
        else
        {
            deceleration = deceleration * 0.300000011920929f;
        }
        float v7 = 5.5f * deceleration * Time.deltaTime;
        
        pmla = v7;

        LABEL_19:
        float pmlc = pmldLengthCopy - pmla;
        if (v5 > pmlc)
            pmlc = v5;
        float pmle = pmlc / pmldLengthCopy;
        playerVelocity.x = playerVelocity.x * pmle;
        playerVelocity.z = playerVelocity.z * pmle;
        playerVelocity.y = pmle * playerVelocity.y;
    }

    private void PM_ProjectVelocity(Vector3 normalVector, ref Vector3 currentVelocity, int framesAhead)
    {

        float lengthSquared2D = currentVelocity.z * currentVelocity.z + currentVelocity.x * currentVelocity.x;
        float lengthSquared2DCopy = lengthSquared2D;
        bool isNormalHorizontal = Math.Abs(normalVector.z) < 0.001;

        // Copy over input velocity to output
        if (isNormalHorizontal || lengthSquared2D == 0.0)
        {
            return;
        }

        // Player is actually moving and its not a usual wall
        //else
        //{
        //    float unknownDot = normalVector.z * currentVelocity.z + currentVelocity.x * normalVector.x;
        //
        //    // Black ops 1 pdb calls it that, not sure yet
        //    float newZ = -unknownDot / normalVector.y;
        //
        //    float speedSquared = currentVelocity.z * currentVelocity.z + currentVelocity.x * currentVelocity.x + currentVelocity.y + currentVelocity.y;
        //    float newSpeedSquared = speed2DSquared + newZ * newZ;
        //
        //    float lengthScale = Mathf.Sqrt((currentVelocity.y * currentVelocity.y + speedSquared) / (speedSquared + newZ * newZ));
        //
        //    //Raycast prediction
        //    Vector3 futurePos = transform.position + currentVelocity * Time.fixedDeltaTime * framesAhead;
        //    RaycastHit hit;
        //    if (Physics.Raycast(futurePos, Vector3.down, out hit, distanceToGround + 8 * Time.fixedDeltaTime * framesAhead))
        //    {
        //        if ((float)lengthScale < 1.0 || newZ < 0.0 || currentVelocity.z > 0.0)
        //        {
        //            currentVelocity.x = (float)lengthScale * currentVelocity.x;
        //            currentVelocity.z = (float)lengthScale * currentVelocity.z;
        //            currentVelocity.y = (float)lengthScale * newZ;
        //
        //            bounceCounter++;
        //
        //        }
        //        Debug.Log("lengthScale: " + (float)lengthScale + " | newZ: " + newZ + " | curVelocity: " + currentVelocity.z);
        //    }
        //    distanceToGround = hit.distance;
        //
        //}

        else
        {
            float unknownDot = normalVector.z * currentVelocity.z + currentVelocity.x * normalVector.x;
            float newZ = -unknownDot / normalVector.y;
            float currentX = currentVelocity.x;
            float currentZ = currentVelocity.z; //NOTE this is cod4's Y axis but we use z in unity
            float lengthSquared = currentVelocity.y * currentVelocity.y + lengthSquared2DCopy;
            float newLengthSquared = lengthSquared2DCopy + newZ * newZ;
            float lengthSquaredCopy = lengthSquared;
            float newLengthSquaredCopy = newLengthSquared;
            float v12 = lengthSquaredCopy / newLengthSquaredCopy;
            float v13 = MathF.Sqrt(v12);
            float v6 = v13;
            if (v13 < 1.0 || newZ < 0.0 || currentVelocity.y > 0.0)
            {
                currentVelocity.x = currentX * v6;
                currentVelocity.z = currentZ * v6;
                currentVelocity.y = v6 * newZ;
            }
        }
    }
    private void PM_ClipVelocity(ref Vector3 inVec, Vector3 normal, ref Vector3 overbounce)
    {
        float backoff; // [esp+14h] [ebp+4h]
        float overbounceParama; // [esp+14h] [ebp+4h]
        float overbounceParamb; // [esp+14h] [ebp+4h]

        backoff = Vector3.Dot(inVec, normal);
        overbounceParama = backoff - MathF.Abs(backoff) * 0.001000000047497451f;
        overbounceParamb = -overbounceParama;
        overbounce = normal * overbounceParamb + inVec;
        overbounce.z = overbounceParamb * normal.z + inVec.z;
        overbounce.y = overbounceParamb * normal.y + inVec.y;
    }

    //Conversions
    private float toQuakeUnits(float UnityVal)
    {
        return UnityVal / .0265625f;
    }
    private float toUnityUnits(float QuakeVal)
    {
        return QuakeVal * .0265625f;
    }
    private Vector3 toUnityUnitsVec3(Vector3 QuakeVal)
    {
        return QuakeVal * .0265625f;
    }

    //Debugging
    private void ShowFPS()
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
    }
    private void DebugDrawing()
    {
        //Public vars for read only purposes (external use)
        Debug.DrawLine(_controller.transform.position, transform.position + transform.forward, Color.red);
        Debug.DrawLine(_controller.transform.position, transform.position + playerVelocity, Color.blue);
        Debug.DrawLine(_controller.transform.position, transform.position + wishDirNorm, Color.green);
    }
    private void OnGUI()
    {
        style.fontSize = 28;
        GUI.Label(new Rect(0, 0, 400, 100), "FPS: " + fps, style);
        var ups = _controller.velocity;
        ups.y = 0;
        GUI.Label(new Rect(0, 25, 400, 100), "Speed: " + Mathf.Round(ups.magnitude * 100) / 100 + "ups", style);
        GUI.Label(new Rect(0, 50, 400, 100), "PM_CheckJump: " + isOnGround, style);
        GUI.Label(new Rect(0, 75, 400, 100), "_c.isGrounded: " + _controller.isGrounded, style);

        GUI.Label(new Rect(0, 100, 400, 100), "Velocity X: " + playerVelocity.x, style);
        GUI.Label(new Rect(0, 125, 400, 100), "Velocity Z: " + playerVelocity.z, style);
        GUI.Label(new Rect(0, 150, 400, 100), "Velocity Y: " + playerVelocity.y, style);

    }
}

