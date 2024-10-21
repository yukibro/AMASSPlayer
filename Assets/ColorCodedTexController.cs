using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ColorCodedTexController : MonoBehaviour
{
    [SerializeField]
    SkinnedMeshRenderer target;


    // Update is called once per frame
    public void UpdateShader(List<Vector3> positions, List<Vector3> colors)
    {
        var mat = target.material;
        
        mat.SetVectorArray("_coords", positions.Select( v => new Vector4(v.x, v.y, v.z)).ToList());
        mat.SetVectorArray("_colors", colors.Select(c => new Vector4(c.x, c.y, c.z)).ToList());
        mat.SetInt("_numPoints", positions.Count);
    }
}
