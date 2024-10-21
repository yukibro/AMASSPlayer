using UnityEngine;
using System.Collections.Generic;

public class ThingWithAvatarHiarchy : MonoBehaviour
{
    [SerializeField] protected List<Transform> _jointTransforms;
    public List<Transform> JointTransfroms { get => _jointTransforms; }
}
