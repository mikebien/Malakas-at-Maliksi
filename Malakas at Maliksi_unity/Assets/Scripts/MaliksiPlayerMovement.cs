using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerInput))]
public class MaliksiPlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private int maxJumps = 2;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 14f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 0.5f;
    [SerializeField] private float dashEndPause = 0.05f;
    private bool isDashing = false;
    private float dashCooldownTimer = 0f;
    private CapsuleCollider2D capsuleCollider;
    private float originalColliderHeight;
    private Vector2 originalColliderOffset;

    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 moveInput;
    private bool isGrounded;
    private bool facingRight = true;
    private int jumpsRemaining;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        animator = GetComponent<Animator>();

        originalColliderHeight = capsuleCollider.size.y;
        originalColliderOffset = capsuleCollider.offset;
        jumpsRemaining = maxJumps;
    }

    private void Update()
    {
        if (groundCheck != null)
        {
            bool wasGrounded = isGrounded;
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

            if (isGrounded && !wasGrounded)
            {
                jumpsRemaining = maxJumps;
            }
        }

        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        if (!isDashing && moveInput.x != 0)
        {
            if (moveInput.x > 0 && !facingRight) Flip();
            else if (moveInput.x < 0 && facingRight) Flip();
        }

        animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
        animator.SetBool("IsGrounded", isGrounded);
    }

    private void FixedUpdate()
    {
        if (isDashing) return;

        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
    }

    // Matches the 'MoveMaliksi' Action Name in your Input Actions Editor
    public void OnMoveMaliksi(InputValue value)
    {
        if (isDashing) return;
        moveInput = value.Get<Vector2>();
    }

    public void OnJumpMaliksi(InputValue value)
    {
        if (!value.isPressed || isDashing) return;

        if (isGrounded || jumpsRemaining > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpsRemaining--;
        }
    }

    public void OnDash(InputValue value)
    {
        if (!value.isPressed || isDashing || dashCooldownTimer > 0) return;

        StartCoroutine(PerformLowDash());
    }

    private IEnumerator PerformLowDash()
    {
        isDashing = true;
        dashCooldownTimer = dashCooldown;

        moveInput = Vector2.zero;

        capsuleCollider.size = new Vector2(capsuleCollider.size.x, originalColliderHeight * 0.5f);
        capsuleCollider.offset = new Vector2(originalColliderOffset.x, originalColliderOffset.y - (originalColliderHeight * 0.25f));

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        float dashDir = facingRight ? 1f : -1f;
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Apply brief full stop
        rb.linearVelocity = new Vector2(0f, 0f);

        if (dashEndPause > 0f)
        {
            yield return new WaitForSeconds(dashEndPause);
        }

        // Restore physics and collider dimensions
        rb.gravityScale = originalGravity;
        capsuleCollider.size = new Vector2(capsuleCollider.size.x, originalColliderHeight);
        capsuleCollider.offset = originalColliderOffset;
        
        isDashing = false;

        // Directly check if the Left or Right Arrow keys are still held down at the end of the dash
        if (Keyboard.current != null)
        {
            float horizontalInput = 0f;
            if (Keyboard.current.leftArrowKey.isPressed) horizontalInput -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed) horizontalInput += 1f;
            moveInput = new Vector2(horizontalInput, 0f);
        }
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
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}