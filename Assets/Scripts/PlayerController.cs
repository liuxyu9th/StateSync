using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    
    private Vector2 moveInput;
    

    private void Awake()
    {
    }

    private void Update()
    {
        if (!IsOwner) return;
        
        // 获取输入
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;
        
        MoveServerRpc(moveInput);
        
        // 客户端预测：立即应用移动
        transform.position += (Vector3)moveInput * moveSpeed * Time.deltaTime;
        
    }

    private void FixedUpdate()
    {
    }

    [ServerRpc]
    private void MoveServerRpc(Vector2 input)
    {
        moveInput = input;
    }
}