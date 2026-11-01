using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerInput))]
public class MalakasPlayerMovement : MonoBehaviour
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
    private bool facingRight = true;
    private bool isHoldingObject = false;

    private Transform grabbedObject;
    private Rigidbody2D grabbedBoxRb;
    private Transform nearbyPushable;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        if (!isHoldingObject && moveInput.x != 0)
        {
            if (moveInput.x > 0 && !facingRight) Flip();
            else if (moveInput.x < 0 && facingRight) Flip();
        }

        float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetFloat("Speed", currentSpeed);
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsHolding", isHoldingObject);
    }

    private void FixedUpdate()
    {
        float currentSpeedMultiplier = isHoldingObject ? pushSpeedMultiplier : 1f;
        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed * currentSpeedMultiplier, rb.linearVelocity.y);

        if (isHoldingObject && grabbedBoxRb != null)
        {
            float offset = 1.1f;
            Vector2 targetPos = new Vector2(rb.position.x + (facingRight ? offset : -offset), grabbedBoxRb.position.y);
            grabbedBoxRb.MovePosition(targetPos);
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed && isGrounded && !isHoldingObject)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    public void OnInteract(InputValue value)
    {
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
                    if (facingRight) Flip();
                }
                else
                {
                    if (!facingRight) Flip();
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
        if (!value.isPressed || isHoldingObject) return;

        animator.SetTrigger("Punch");

        // Calculate punch box position based on facing direction
        Vector2 directionOffset = new Vector2(facingRight ? punchOffset.x : -punchOffset.x, punchOffset.y);
        Vector2 punchPos = (Vector2)transform.position + directionOffset;

        // Detect and destroy any destroyable objects in the hitbox
        Collider2D[] hitObjects = Physics2D.OverlapBoxAll(punchPos, punchSize, 0f, destroyableLayer);
        foreach (Collider2D hit in hitObjects)
        {
            Destroy(hit.transform.root.gameObject);
        }
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

    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1f;
        transform.localScale = localScale;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        // Visualize the punch hitbox in the Scene view when Malakas is selected
        Gizmos.color = Color.red;
        Vector2 drawOffset = new Vector2(facingRight ? punchOffset.x : -punchOffset.x, punchOffset.y);
        Gizmos.DrawWireCube((Vector2)transform.position + drawOffset, punchSize);
    }
}