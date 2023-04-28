using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class SaveLoadPosition : MonoBehaviour
{
    private Vector3 savePosition;
    private Vector3 saveAngles;
    private CharacterController Jumper;
    private Camera CameraMain;
    public Transform playerView;

    private void Start()
    {
        // Get the CharacterController component attached to this game object
        Jumper = GetComponent<CharacterController>();
        CameraMain = GetComponent<Camera>();

    }

    private void Update()
    {
        SavePosition();
        LoadPosition();
    }

    private void SavePosition()
    {
        //Save Position
        if (Input.GetKeyDown(KeyCode.V) && Jumper.isGrounded)
        {
            savePosition = Jumper.transform.position;
            saveAngles = transform.rotation.eulerAngles;

            Debug.Log("saved at position: " + Jumper.transform.position);

        }
    }

    private void LoadPosition()
    {
        //Load Position
        if (Input.GetKeyDown(KeyCode.F))
        {

      
            Jumper.enabled = false;
            this.GetComponent<CPMPlayer>().enabled = false;
            {
                Jumper.transform.position = savePosition;

            }
            
            Jumper.enabled = true;
            this.GetComponent<CPMPlayer>().enabled = true;

            Debug.Log("loaded to position: " + Jumper.transform.position);
        }
    }
}
