using UnityEngine;

public class SaveLoadPosition : MonoBehaviour
{
    public static Vector3 savePosition;
    public static Vector3 saveAngles;
    private CharacterController Jumper;
    private Camera Camera;

    private void Start()
    {
        // Get the CharacterController component attached to this game object
        Jumper = GetComponent<CharacterController>();
        Camera = Jumper.GetComponentInChildren<Camera>();

        savePosition = Jumper.transform.position;
    }

    private void Update()
    {
        SavePosition();
        LoadPosition();
    }

    private void SavePosition()
    {
        //Save Position
        if (Input.GetKeyDown(KeyCode.V) && JumperMovement.isOnGround)
        {
            savePosition = Jumper.transform.position;
            savePosition.y = savePosition.y + .5f;
            saveAngles = transform.rotation.eulerAngles;

            Debug.Log("saved: " + Jumper.transform.position);

        }
    }

    private void LoadPosition()
    {
        //Load Position
        if (Input.GetKeyDown(KeyCode.F))
        {
            Jumper.enabled = false;
            this.GetComponent<JumperMovement>().enabled = false;

            CPMPlayer.playerVelocity = Vector3.zero;
            Jumper.transform.position = savePosition;
            Camera.transform.rotation = Quaternion.Euler(saveAngles);

            Jumper.enabled = true;
            this.GetComponent<JumperMovement>().enabled = true;
            Debug.Log("loaded: " + Jumper.transform.position);
        }
    }
}
