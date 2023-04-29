// Rotate the normals by speed every frame

using UnityEngine;

public class NormalVectors : MonoBehaviour
{
    public Mesh mesh;
    public static Vector3 normals;
    public Collider collision;
    public static bool hasCollided;

    // Update is called once per frame
    void Update()
    {
        // obtain the normals from the Mesh


    }

    void OnCollisionEnter(Collision collision)
    {
        hasCollided = true;
        mesh = GetComponent<MeshFilter>().mesh;
        normals = collision.contacts[0].normal;

    }
}