using System.Collections.Generic;
using UnityEngine;

public class ScaleWhenHovering : MonoBehaviour
{
    public int id = -1;

    private readonly List<Material[]> cachedMaterials = new List<Material[]>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        foreach (var mr in transform.GetComponentsInChildren<MeshRenderer>())
        {
            cachedMaterials.Add(mr.materials);
        }
    }

    public void ScaleUp()
    {
        //Vector3 localscale = transform.localScale;
        //localscale *= 2f;
        //transform.localScale = localscale; 
    }

    public void ScaleDown()
    {
        //Vector3 localscale = transform.localScale;
        //localscale /= 2f;
        //transform.localScale = localscale;
    }

    public void EnableVisualization(float transparency = 255f)
    {
        foreach (var mr in transform.GetComponentsInChildren<MeshRenderer>())
        {
            mr.enabled = true;
            foreach (var mats in cachedMaterials)
            {
                foreach (var m in mats)
                {
                    Color c = m.color;
                    c.a = transparency / 255f;
                    m.color = c;
                }
            }
        }
    }

    public void DisableVisualization()
    {
        foreach (var mr in transform.GetComponentsInChildren<MeshRenderer>())
        {
            mr.enabled = false;
        }
    }


}
