using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameSpeedController : MonoBehaviour
{
    [SerializeField]
    [Range(0.01f, 2f)]
    float speed = 1f;
    
    void Update()
    {
        Time.timeScale = speed;
    }
}
