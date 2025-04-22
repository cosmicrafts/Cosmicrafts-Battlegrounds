namespace Cosmicrafts
{
    using UnityEngine;

    /// <summary>
    /// Simple component to destroy a GameObject after a set time.
    /// This is useful for effects that need to be destroyed even if their parent is destroyed.
    /// </summary>
    public class DestroyAfterTime : MonoBehaviour
    {
        public float timeToDestroy = 1f;
        private float timer = 0f;

        private void Update()
        {
            timer += Time.deltaTime;
            
            if (timer >= timeToDestroy)
            {
                Destroy(gameObject);
            }
        }
    }
} 