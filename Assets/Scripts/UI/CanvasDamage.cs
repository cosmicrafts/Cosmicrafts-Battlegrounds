using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;  // Add this for Image component

public class CanvasDamage : MonoBehaviour
{
    [SerializeField]
    TMP_Text damageText;

    [Header("Critical Hit Effect")]
    [SerializeField] private Image criticalHitImage;  // Reference to the critical hit image

    [Header("Animation Settings")]
    [SerializeField] private float bounceHeight = 1f;  // Increased default height
    [SerializeField] private float bounceDuration = 0.5f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float startHeight = 1.5f;  // Height above unit to start
    [SerializeField] private float randomHorizontalOffset = 0.3f;  // Random horizontal offset for variety

    [Header("Colors")]
    [SerializeField] private Color normalDamageColorTop = Color.white;
    [SerializeField] private Color normalDamageColorBottom = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color criticalDamageColorTop = Color.red;
    [SerializeField] private Color criticalDamageColorBottom = new Color(1f, 0f, 0f, 0.5f);
    [SerializeField] private Color shieldDamageColorTop = Color.cyan;
    [SerializeField] private Color shieldDamageColorBottom = new Color(0f, 1f, 1f, 0.5f);

    [Header("UI Positioning")]
    [SerializeField] private bool maintainConstantScale = true;
    [SerializeField] private float baseOrthographicSize = 60f;
    [SerializeField] private float minScaleMultiplier = 0.5f;
    [SerializeField] private float maxScaleMultiplier = 2f;

    private float damageValue;
    private bool isCritical;
    private bool isShieldDamage;
    private Camera mainCamera;
    private Vector3 originalScale;
    private Vector3 targetPosition;
    private Vector3 startPosition;
    private Vector3 randomOffset;

    // Cache for performance
    private static readonly WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
    private static readonly Vector3 upVector = Vector3.up;

    void Awake()
    {
        mainCamera = Camera.main;
        originalScale = transform.localScale;
        
        // Generate random offset for variety
        randomOffset = new Vector3(
            Random.Range(-randomHorizontalOffset, randomHorizontalOffset),
            0f,
            Random.Range(-randomHorizontalOffset, randomHorizontalOffset)
        );

        // Enable vertex gradient on the text
        damageText.enableVertexGradient = true;
    }

    void Start()
    {
        // Set initial position higher above the target
        startPosition = targetPosition + (upVector * startHeight) + randomOffset;
        transform.position = startPosition;

        // Configure text
        damageText.text = damageValue.ToString();
        UpdateTextGradient();
        
        // Start animation
        StartCoroutine(BounceAndFadeAnimation());
    }

    private void UpdateTextGradient()
    {
        VertexGradient gradient = new VertexGradient();
        
        if (isShieldDamage)
        {
            gradient.topLeft = shieldDamageColorTop;
            gradient.topRight = shieldDamageColorTop;
            gradient.bottomLeft = shieldDamageColorBottom;
            gradient.bottomRight = shieldDamageColorBottom;
        }
        else if (isCritical)
        {
            gradient.topLeft = criticalDamageColorTop;
            gradient.topRight = criticalDamageColorTop;
            gradient.bottomLeft = criticalDamageColorBottom;
            gradient.bottomRight = criticalDamageColorBottom;
        }
        else
        {
            gradient.topLeft = normalDamageColorTop;
            gradient.topRight = normalDamageColorTop;
            gradient.bottomLeft = normalDamageColorBottom;
            gradient.bottomRight = normalDamageColorBottom;
        }
        
        damageText.colorGradient = gradient;
    }

    void LateUpdate()
    {
        if (!mainCamera) return;

        // Match camera rotation exactly like UIUnit.cs
        transform.rotation = mainCamera.transform.rotation;

        // Scale UI based on camera zoom
        if (maintainConstantScale && mainCamera.orthographic)
        {
            float currentOrthographicSize = mainCamera.orthographicSize;
            float scaleFactor = Mathf.Clamp(
                currentOrthographicSize / baseOrthographicSize,
                minScaleMultiplier,
                maxScaleMultiplier
            );
            transform.localScale = Vector3.Scale(originalScale, Vector3.one * scaleFactor);
        }
    }

    public void SetDamage(float newDamage, bool critical = false, bool shieldDamage = false)
    {
        damageValue = newDamage;
        isCritical = critical;
        isShieldDamage = shieldDamage;
        targetPosition = transform.position;
        
        // Activate/deactivate critical hit image
        if (criticalHitImage != null)
        {
            criticalHitImage.gameObject.SetActive(critical);
        }
        
        if (damageText != null)
        {
            UpdateTextGradient();
        }
    }

    IEnumerator BounceAndFadeAnimation()
    {
        float elapsedTime = 0f;
        Color startColorTop = damageText.colorGradient.topLeft;
        Color startColorBottom = damageText.colorGradient.bottomLeft;
        Color endColorTop = new Color(startColorTop.r, startColorTop.g, startColorTop.b, 0f);
        Color endColorBottom = new Color(startColorBottom.r, startColorBottom.g, startColorBottom.b, 0f);
        Vector3 currentPosition = startPosition;

        while (elapsedTime < bounceDuration)
        {
            float progress = elapsedTime / bounceDuration;
            float bounce = Mathf.Sin(progress * Mathf.PI) * bounceHeight;

            // Update position with bounce
            currentPosition = startPosition + (upVector * bounce);
            transform.position = currentPosition;

            // Start fading out after bounce
            if (progress > 0.5f)
            {
                float fadeProgress = (progress - 0.5f) * 2f;
                VertexGradient currentGradient = new VertexGradient(
                    Color.Lerp(startColorTop, endColorTop, fadeProgress),
                    Color.Lerp(startColorTop, endColorTop, fadeProgress),
                    Color.Lerp(startColorBottom, endColorBottom, fadeProgress),
                    Color.Lerp(startColorBottom, endColorBottom, fadeProgress)
                );
                damageText.colorGradient = currentGradient;
            }

            elapsedTime += Time.deltaTime;
            yield return waitForEndOfFrame;
        }

        // Final fade out
        float remainingFadeTime = fadeDuration;
        while (remainingFadeTime > 0)
        {
            float fadeProgress = 1f - (remainingFadeTime / fadeDuration);
            VertexGradient currentGradient = new VertexGradient(
                Color.Lerp(startColorTop, endColorTop, fadeProgress),
                Color.Lerp(startColorTop, endColorTop, fadeProgress),
                Color.Lerp(startColorBottom, endColorBottom, fadeProgress),
                Color.Lerp(startColorBottom, endColorBottom, fadeProgress)
            );
            damageText.colorGradient = currentGradient;
            remainingFadeTime -= Time.deltaTime;
            yield return waitForEndOfFrame;
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Clean up any references
        damageText = null;
        mainCamera = null;
    }
}