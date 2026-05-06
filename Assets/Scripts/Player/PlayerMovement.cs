using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Lane Settings")]
    public float laneDistance = 2.0f; 
    private int currentLane = 2; 

    [Header("Speed State Settings")]
    public float baseSpeed = 10f;
    public float currentSpeed;

    public static float globalScrollSpeed; 

    // NEW: We need a reference to the Physics component
    private Rigidbody2D rb; 

    void Start()
    {
        currentLane = 2;
        currentSpeed = baseSpeed;
        
        // Grab the Rigidbody2D attached to the player
        rb = GetComponent<Rigidbody2D>(); 
    }

    void Update()
    {
        if (Keyboard.current == null) return; 

        HandleLaneSwitching();
        HandleSpeedStates();
    }

    // NEW: All physical movement MUST happen inside FixedUpdate, not Update
    void FixedUpdate() 
    {
        UpdatePlayerPosition();
    }

    void HandleLaneSwitching()
    {
        if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            MoveLane(-1);
        }
        else if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            MoveLane(1);
        }
    }

    void MoveLane(int direction)
    {
        currentLane += direction;
        currentLane = Mathf.Clamp(currentLane, 0, 4); 
    }

    void HandleSpeedStates()
    {
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
        {
            currentSpeed = baseSpeed * 1.5f; 
            GetComponent<SpriteRenderer>().color = Color.red; 
        }
        else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
        {
            currentSpeed = baseSpeed * 0.5f; 
            GetComponent<SpriteRenderer>().color = Color.green; 
        }
        else
        {
            currentSpeed = baseSpeed; 
            GetComponent<SpriteRenderer>().color = Color.blue; 
        }

        globalScrollSpeed = currentSpeed;
    }

    void UpdatePlayerPosition()
    {
        // Calculate the target position using the physics coordinates
        Vector2 targetPosition = rb.position;
        
        if (currentLane == 0) targetPosition.x = -laneDistance * 2f;      
        else if (currentLane == 1) targetPosition.x = -laneDistance;      
        else if (currentLane == 2) targetPosition.x = 0f;                 
        else if (currentLane == 3) targetPosition.x = laneDistance;       
        else if (currentLane == 4) targetPosition.x = laneDistance * 2f;  

        // NEW: We use Time.fixedDeltaTime here, and rb.MovePosition instead of transform.position
        Vector2 newPos = Vector2.Lerp(rb.position, targetPosition, 15f * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Capitalization matters! Ensure your object has "Obstacle" with a capital O.
        if (collision.gameObject.name.Contains("Obstacle"))
        {
            Debug.LogError("NABRAK! You hit an obstacle. GAME OVER.");
            Time.timeScale = 0f; 
        }
    }
}