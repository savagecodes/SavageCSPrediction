using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleCubeMovement : MonoBehaviour
{

    public void Move(Vector2 input)
    {
        transform.position += transform.forward * 100 * Time.fixedDeltaTime * input.y;
        transform.position += transform.right * 100 * Time.fixedDeltaTime * input.x;
    }

    public void Rotate(float input)
    {
        transform.eulerAngles += input * 70 * Time.fixedDeltaTime * Vector3.up;
    }
}
