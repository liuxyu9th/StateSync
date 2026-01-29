using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public int id;
    public Vector3Data PositionData => transform.position.ConvertToGameStateData();
    public float velocity = 1;
    public Vector3 moveForward = Vector3.zero;
    private MainLogic mainLogic;

    private Vector3 syncPos;
    
    public void Init(int id,MainLogic logic)
    {
        this.id = id;
        mainLogic = logic;
    }

    public void SyncState(Vector3Data postion)
    {
        syncPos = postion.ToVector3();
    } public void SyncState(Vector3 postion)
    {
        syncPos = postion;
    }
    private void Update()
    {
        moveForward = Vector3.zero;
        PlayerInput();
        transform.position = Vector3.Lerp(transform.position, syncPos,0.5f);
    }
    private void PlayerInput()
    {
        if (mainLogic.MyPlayerId != id)
            return;
        if (Input.GetKey(KeyCode.W))
        {
            moveForward += Vector3.up;
        }
        if (Input.GetKey(KeyCode.S))
        {
            moveForward += Vector3.down;
        }
        if (Input.GetKey(KeyCode.A))
        {
            moveForward += Vector3.left;
        }
        if (Input.GetKey(KeyCode.D))
        {
            moveForward += Vector3.right;
        }
        moveForward.Normalize();
        transform.position += moveForward * velocity * Time.deltaTime;
        if (mainLogic.IsServer)
        {
            syncPos = transform.position;
        }
    }
}
