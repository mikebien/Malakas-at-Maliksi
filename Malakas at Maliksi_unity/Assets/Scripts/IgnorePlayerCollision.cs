using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class IgnorePlayerCollision : MonoBehaviour
{
    private void Start()
    {
        Collider2D myCollider = GetComponent<Collider2D>();

        // Find both player scripts in the scene
        MalakasPlayerMovement malakas = FindAnyObjectByType<MalakasPlayerMovement>();
        MaliksiPlayerMovement maliksi = FindAnyObjectByType<MaliksiPlayerMovement>();

        // Ignore collision with Malakas if this isn't Malakas
        if (malakas != null && malakas.gameObject != gameObject)
        {
            Collider2D otherCol = malakas.GetComponent<Collider2D>();
            if (otherCol != null) Physics2D.IgnoreCollision(myCollider, otherCol, true);
        }

        // Ignore collision with Maliksi if this isn't Maliksi
        if (maliksi != null && maliksi.gameObject != gameObject)
        {
            Collider2D otherCol = maliksi.GetComponent<Collider2D>();
            if (otherCol != null) Physics2D.IgnoreCollision(myCollider, otherCol, true);
        }
    }
}