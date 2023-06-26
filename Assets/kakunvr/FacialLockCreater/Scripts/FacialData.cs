using System.Collections.Generic;
using UnityEngine;

namespace kakunvr.FacialLockCreater.Scripts
{
    public class FacialData
    {
        public string Name;
        public List<BlendShapeData> BlendShapeData = new List<BlendShapeData>();
    }

    public class BlendShapeData
    {
        public SkinnedMeshRenderer Target;
        public string Name;
        public int Value;
    }
}