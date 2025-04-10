using System.Collections;
using UnityEngine;
using TMPro;

public class CanvasDamage : MonoBehaviour
{
    [SerializeField]
    TMP_Text damageText;

    [SerializeField]
    float bounceHeight = 0.5f;  // The height of the bounce
    [SerializeField]
    float bounceDuration = 0.5f;  // The duration of the bounce animation

    float damageValue;
    Camera mainCamera;

    void Start()
    {
        damageText.text = damageValue.ToString();
        StartCoroutine(BounceAnimation());  // Start the bouncing animation
        Destroy(gameObject, .5f);  // Destroy the game object after 2.5 seconds
    }

    public void SetDamage(float newDamage)
    {
        mainCamera = Camera.main;
        damageValue = newDamage;

        // Update the damage text
        damageText.text = damageValue.ToString();

        // The UI always looks at the camera
        if (mainCamera)
        {
            transform.LookAt(mainCamera.transform);
        }
    }

    IEnumerator BounceAnimation()
    {
        Vector3 originalPosition = transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < bounceDuration)
        {
            float progress = elapsedTime / bounceDuration;
            float bounce = Mathf.Sin(progress * Mathf.PI) * bounceHeight;

            transform.position = originalPosition + new Vector3(0, bounce, 0);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = originalPosition;  // Reset to the original position
    }
}