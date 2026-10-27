using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadTheRing : MonoBehaviour
{
    public Transform cameraTransform;
    public float distance = 0.4f;

    [Header("Fade Settings")]
    public float fadeDuration = 2.0f;

    private List<MaterialAlphaInfo> cachedMaterialInfos = new List<MaterialAlphaInfo>();
    private List<Transform> childTransforms = new List<Transform>();

    [System.Serializable]
    private class MaterialAlphaInfo
    {
        public Material material;

        public bool hasColor;
        public float originalColorAlpha;

        public bool hasBaseColor;
        public float originalBaseColorAlpha;
    }

    private void Awake()
    {
        CacheChildrenAndMaterials();
    }

    private void CacheChildrenAndMaterials()
    {
        cachedMaterialInfos.Clear();
        childTransforms.Clear();

        // 收集所有子物体（不包含自己）
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform) continue;
            childTransforms.Add(child);
        }

        // 收集所有子物体上的 Renderer 材质
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            foreach (Material mat in r.materials)
            {
                MaterialAlphaInfo info = new MaterialAlphaInfo();
                info.material = mat;

                if (mat.HasProperty("_Color"))
                {
                    info.hasColor = true;
                    info.originalColorAlpha = mat.color.a;
                }

                if (mat.HasProperty("_BaseColor"))
                {
                    info.hasBaseColor = true;
                    info.originalBaseColorAlpha = mat.GetColor("_BaseColor").a;
                }

                cachedMaterialInfos.Add(info);
            }
        }
    }

    public void Load()
    {
        gameObject.SetActive(true); 

        Vector3 spawnPos = cameraTransform.position + cameraTransform.forward * distance;
        transform.position = spawnPos;
        transform.LookAt(cameraTransform.position);

        StopAllCoroutines();
        StartCoroutine(FadeInObjects());
    }

    private IEnumerator FadeInObjects()
    {
        foreach (MaterialAlphaInfo info in cachedMaterialInfos)
        {
            SetMaterialAlpha(info, 0f);
        }

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            float t = elapsed / fadeDuration;

            foreach (MaterialAlphaInfo info in cachedMaterialInfos)
            {
                SetMaterialAlpha(info, t);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        foreach (MaterialAlphaInfo info in cachedMaterialInfos)
        {
            SetMaterialAlpha(info, 1f);
        }
    }

    private void SetMaterialAlpha(MaterialAlphaInfo info, float t)
    {
        if (info == null || info.material == null) return;

        if (info.hasColor)
        {
            Color c = info.material.color;
            c.a = Mathf.Lerp(0f, info.originalColorAlpha, t);
            info.material.color = c;
        }

        if (info.hasBaseColor)
        {
            Color c = info.material.GetColor("_BaseColor");
            c.a = Mathf.Lerp(0f, info.originalBaseColorAlpha, t);
            info.material.SetColor("_BaseColor", c);
        }
    }

}