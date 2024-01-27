using System;
using System.Collections.Generic;
using UnityEngine;

namespace kakunvr.FacialLockGenerator.Scripts
{
    [CreateAssetMenu()]
    public class FacialList : ScriptableObject
    {
        [SerializeField] public List<FacialData> FacialData = new List<FacialData>();
    }

    [System.Serializable]
    public class FacialData
    {
        [SerializeField] public string Name;
        [SerializeField] public string Folder = "";
        
        [SerializeField] public List<BlendShapeData> BlendShapeData = new List<BlendShapeData>();
    }

    [System.Serializable]
    public class BlendShapeData
    {
        [NonSerialized]
        public SkinnedMeshRenderer Target;
        
        [SerializeField] public string TargetObjectName;
        [SerializeField] public string Name;
        [SerializeField] public int Value;
    }
}
