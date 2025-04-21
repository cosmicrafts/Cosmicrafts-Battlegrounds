using System.Collections.Generic;
using UnityEngine;

namespace Cosmicrafts
{
    /// <summary>
    /// Very light‑weight runtime outline. Does a single inverted‑hull pass using a custom shader.
    /// Attach this component to the root object that already has one (or many) Mesh/SkinnedMesh renderers.
    /// No cameras, command buffers or post‑processing involved – so it works both in URP and Built‑in.
    /// </summary>
    [DisallowMultipleComponent]
    public class OutlineController : MonoBehaviour
    {
        private const string OutlineShaderName = "Custom/OutlineHull";

        [ColorUsage(showAlpha: true, hdr: true)]
        [SerializeField] private Color outlineColor = Color.yellow;

        [SerializeField, Range(0.0001f, 0.1f)]
        private float outlineThickness = 0.02f;

        // We spawn one helper GameObject per renderer we are outlining.
        private readonly List<GameObject> outlineObjects = new List<GameObject>();

        private void Awake()
        {
            BuildOutlineObjects();
        }

        private void OnDestroy()
        {
            foreach (var go in outlineObjects)
            {
                if (go != null)
                    Destroy(go);
            }
            outlineObjects.Clear();
        }

        private void BuildOutlineObjects()
        {
            var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            var outlineShader = Shader.Find(OutlineShaderName);
            if (outlineShader == null)
            {
                Debug.LogError($"Outline shader '{OutlineShaderName}' not found. Make sure the shader file exists in the project.");
                return;
            }

            foreach (var srcRenderer in renderers)
            {
                // We ignore the helper objects themselves if this is a hot‑reload call.
                if (srcRenderer.gameObject.name.EndsWith("_Outline"))
                    continue;

                GameObject helper = new GameObject(srcRenderer.gameObject.name + "_Outline");
                helper.transform.SetParent(srcRenderer.transform, false);

                MeshFilter helperFilter = helper.AddComponent<MeshFilter>();
                MeshRenderer helperRenderer = helper.AddComponent<MeshRenderer>();

                // Copy the mesh data depending on renderer type.
                if (srcRenderer is SkinnedMeshRenderer skinned)
                {
                    helperFilter.sharedMesh = skinned.sharedMesh;
                }
                else if (srcRenderer is MeshRenderer)
                {
                    var sourceFilter = srcRenderer.GetComponent<MeshFilter>();
                    helperFilter.sharedMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
                }

                // Prepare the outline material.
                Material mat = new Material(outlineShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                mat.SetColor("_OutlineColor", outlineColor);
                mat.SetFloat("_Thickness", outlineThickness);

                helperRenderer.material = mat;
                helperRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                helperRenderer.receiveShadows = false;

                // Make sure this renderer renders after the original so no z‑fighting.
                helperRenderer.sortingOrder = srcRenderer.sortingOrder - 1;

                outlineObjects.Add(helper);
            }
        }

        #region API

        public void SetColor(Color newColor)
        {
            outlineColor = newColor;
            foreach (var obj in outlineObjects)
            {
                var rend = obj.GetComponent<Renderer>();
                if (rend != null)
                    rend.sharedMaterial.SetColor("_OutlineColor", outlineColor);
            }
        }

        public void SetThickness(float newThickness)
        {
            outlineThickness = Mathf.Max(0.0001f, newThickness);
            foreach (var obj in outlineObjects)
            {
                var rend = obj.GetComponent<Renderer>();
                if (rend != null)
                    rend.sharedMaterial.SetFloat("_Thickness", outlineThickness);
            }
        }

        public void SetEnabled(bool value)
        {
            foreach (var obj in outlineObjects)
            {
                if (obj != null)
                    obj.SetActive(value);
            }
        }

        // Allow other scripts to read the current thickness
        public float Thickness
        {
            get => outlineThickness;
            set => SetThickness(value);
        }
        #endregion

        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Reflect any inspector changes immediately in edit mode
            if (outlineObjects.Count > 0)
            {
                SetColor(outlineColor);
                SetThickness(outlineThickness);
            }
        }
        #endif
    }
} 