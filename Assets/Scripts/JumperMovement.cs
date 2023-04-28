using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



struct usercmd_t
{
    public float forwardMove;
    public float rightMove;
    public float upMove;

}



public class JumperMovement : MonoBehaviour
{
    private CharacterController Jumper;
    private Vector3 playerVelocity = Vector3.zero;

    // Player commands, stores wish commands that the player asks for (Forward, back, jump, etc)
    private usercmd_t cmd;


    // Start is called before the first frame update
    void Start()
    {
        Jumper = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void PM_SetMovementDir()
    {
        cmd.forwardMove = Input.GetAxisRaw("Vertical");
        cmd.rightMove = Input.GetAxisRaw("Horizontal");
    }

    private void VectorCopy(Vector3 a, Vector3 b)
    {
        b.x = a.x;
        b.y = a.y;
        b.z = a.z;
    }

    private float VectorLength(Vector3 v)
    {
        int i;
        float length;

        length = 0.0f;
        for (i = 0; i < 3; i++)
            length += v[i] * v[i];
        length = (float)Math.Sqrt(length);

        return length;
    }

    private void PM_Friction()
    {
        Vector3 vec = new Vector3(0,0,0);
        Vector3 vel = new Vector3(0,0,0);
        float speed, newspeed;
        float drop;

        vel = playerVelocity;
    
        //Vector copy Q3
        vec = vel;

        if (Jumper.isGrounded)
        {
            vec[2] = 0; // ignore slope movement
        }

        speed = VectorLength(vec);
        if (speed < 1)
        {
            vel[0] = 0;
            vel[1] = 0;     // allow sinking underwater
                            // FIXME: still have z friction underwater?
            return;
        }

        drop = 0;

        // scale the velocity
        newspeed = speed - drop;
        if (newspeed < 0)
        {
            newspeed = 0;
        }
        newspeed /= speed;

        vel[0] = vel[0] * newspeed;
        vel[1] = vel[1] * newspeed;
        vel[2] = vel[2] * newspeed;
    }
  //
  //private float PM_CmdScale(usercmd_t cmd)
  //{
  //    int max;
  //    float total;
  //    float scale;
  //    int speed;
  //
  //    max = Math.Abs((int)cmd.forwardMove);
  //    if (Math.Abs(cmd.rightMove) > max)
  //    {
  //        max = Math.Abs((int)cmd.rightMove);
  //    }
  //    if (Math.Abs((int)cmd.upMove) > max)
  //    {
  //        max = Math.Abs((int)cmd.upMove);
  //    }
  //    if (!max)
  //    {
  //        return 0;
  //    }
  //
  //    total = MathF.Sqrt(cmd.forwardMove * cmd.forwardMove
  //        + cmd.rightMove * cmd.rightMove + cmd.upMove * cmd.upMove);
  //    scale = (float)((float)speed * max / (127.0 * total));
  //
  //    return scale;
  //}
  //
  //
  //private void PM_AirMove()
  //{
  //    int i;
  //    Vector3 wishvel;
  //    float fmove, smove;
  //    Vector3 wishdir;
  //    float wishspeed;
  //    float scale;
  //    usercmd_t cmd;
  //
  //    PM_Friction();
  //
  //    fmove = cmd.forwardMove;
  //    smove = cmd.rightmove;
  //
  //    scale = PM_CmdScale(cmd);
  //
  //    // set the movementDir so clients can rotate the legs for strafing
  //    PM_SetMovementDir();
  //
  //    // project moves down to flat plane
  //    pml.forward[2] = 0;
  //    pml.right[2] = 0;
  //    VectorNormalize(pml.forward);
  //    VectorNormalize(pml.right);
  //
  //    for (i = 0; i < 2; i++)
  //    {
  //        wishvel[i] = pml.forward[i] * fmove + pml.right[i] * smove;
  //    }
  //    wishvel[2] = 0;
  //
  //    VectorCopy(wishvel, wishdir);
  //    wishspeed = VectorNormalize(wishdir);
  //    wishspeed *= scale;
  //
  //    // not on ground, so little effect on velocity
  //    PM_Accelerate(wishdir, wishspeed, pm_airaccelerate);
  //
  //    // we may have a ground plane that is very steep, even
  //    // though we don't have a groundentity
  //    // slide along the steep plane
  //    if (pml.groundPlane)
  //    {
  //        PM_ClipVelocity(pm->ps->velocity, pml.groundTrace.plane.normal,
  //            pm->ps->velocity, OVERCLIP);
  //    }
  //}
}
