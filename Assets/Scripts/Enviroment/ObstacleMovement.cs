using UnityEngine;

public class ObstacleMovement : MonoBehaviour
{
    void Update()
    {
        // NEW: We grab the globalScrollSpeed directly from the PlayerMovement class.
        // If the player holds 'W', this number goes up, making the obstacle fly at them faster.
        // If they hold 'S', the obstacle slows to a crawl.
        transform.Translate(Vector3.down * PlayerMovement.globalScrollSpeed * Time.deltaTime);

        // Destroy the obstacle if it goes way off the bottom of the screen
        if (transform.position.y < -10f)
        {
            Destroy(gameObject);
        }
    }
}