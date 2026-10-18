using UnityEngine;

namespace LogistiCore.Map
{
    public sealed class MapSceneStamp : MonoBehaviour
    {
        public const int Current = 3;

        [SerializeField] int version = Current;

        public int Version => version;
    }
}
