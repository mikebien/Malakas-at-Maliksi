using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerInput))]
public class MalakasPlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float pushSpeedMultiplier = 0.5f;
    [SerializeField] private float jumpForce = 12f;

    [Header("Combat / Punch Settings")]
    [SerializeField] private Vector2 punchOffset = new Vector2(0.8f, 0f);
    [SerializeField] private Vector2 punchSize = new Vector2(0.8f, 0.8f);
    [SerializeField] private LayerMask destroyableLayer;

    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 moveInput;
    private bool isGrounded;
    private bool isHoldingObject = false;

    private Transform grabbedObject;
    private Rigidbody2D grabbedBoxRb;
    private Transform nearbyPushable;

    // Networked Facing Direction variable (Owner can write, everyone can read)
    private NetworkVariable<bool> isFacingRight = new NetworkVariable<bool>(
        true, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner
    );

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = IsOwner;
            if (IsOwner)
            {
                playerInput.ActivateInput();
            }
        }

        // Listen for changes to facing direction across the network
        isFacingRight.OnValueChanged += OnFacingDirectionChanged;
        
        // Apply initial visual state on spawn
        ApplyVisualFlip(isFacingRight.Value);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        isFacingRight.OnValueChanged -= OnFacingDirectionChanged;
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        if (!isHoldingObject && moveInput.x != 0)
        {
            if (moveInput.x > 0 && !isFacingRight.Value) Flip(true);
            else if (moveInput.x < 0 && isFacingRight.Value) Flip(false);
        }

        float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetFloat("Speed", currentSpeed);
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsHolding", isHoldingObject);
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        float currentSpeedMultiplier = isHoldingObject ? pushSpeedMultiplier : 1f;
        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed * currentSpeedMultiplier, rb.linearVelocity.y);

        if (isHoldingObject && grabbedBoxRb != null)
        {
            float offset = 1.1f;
            Vector2 targetPos = new Vector2(rb.position.x + (isFacingRight.Value ? offset : -offset), grabbedBoxRb.position.y);
            grabbedBoxRb.MovePosition(targetPos);
        }
    }

    public void OnMove(InputValue value)
    {
        if (!IsOwner) return;
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (!IsOwner) return;

        if (value.isPressed && isGrounded && !isHoldingObject)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    public void OnInteract(InputValue value)
    {
        if (!IsOwner) return;

        if (!isHoldingObject)
        {
            if (nearbyPushable != null)
            {
                grabbedObject = nearbyPushable;
                grabbedBoxRb = grabbedObject.GetComponent<Rigidbody2D>();
                isHoldingObject = true;

                if (grabbedBoxRb != null)
                {
                    grabbedBoxRb.constraints = RigidbodyConstraints2D.FreezeRotation;
                }

                if (transform.position.x > grabbedObject.position.x)
                {
                    if (isFacingRight.Value) Flip(false);
                }
                else
                {
                    if (!isFacingRight.Value) Flip(true);
                }
            }
        }
        else
        {
            if (grabbedBoxRb != null)
            {
                grabbedBoxRb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
                grabbedBoxRb.linearVelocity = new Vector2(0, grabbedBoxRb.linearVelocity.y);
            }

            isHoldingObject = false;
            grabbedObject = null;
            grabbedBoxRb = null;
        }
    }

    public void OnPunch(InputValue value)
    {
        if (!IsOwner || !value.isPressed || isHoldingObject) return;
        PunchServerRpc();
    }

    [ServerRpc]
    private void PunchServerRpc()
    {
        TriggerPunchClientRpc();

        bool currentFacing = isFacingRight.Value;
        Vector2 directionOffset = new Vector2(currentFacing ? punchOffset.x : -punchOffset.x, punchOffset.y);
        Vector2 punchPos = (Vector2)transform.position + directionOffset;

        Collider2D[] hitObjects = Physics2D.OverlapBoxAll(punchPos, punchSize, 0f, destroyableLayer);
        foreach (Collider2D hit in hitObjects)
        {
            Destroy(hit.transform.root.gameObject);
        }
    }

    [ClientRpc]
    private void TriggerPunchClientRpc()
    {
        animator.SetTrigger("Punch");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Pushable"))
        {
            nearbyPushable = other.transform.root;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Transform rootTransform = other.transform.root;
        if (rootTransform == nearbyPushable) nearbyPushable = null;
    }

    private void Flip(bool faceRight)
    {
        if (IsOwner)
        {
            isFacingRight.Value = faceRight;
        }
    }

    private void OnFacingDirectionChanged(bool previousValue, bool newValue)
    {
        ApplyVisualFlip(newValue);
    }

    private void ApplyVisualFlip(bool faceRight)
    {
        Vector3 localScale = transform.localScale;
        localScale.x = Mathf.Abs(localScale.x) * (faceRight ? 1f : -1f);
        transform.localScale = localScale;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}