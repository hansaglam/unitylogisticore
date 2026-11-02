using UnityEngine;

namespace LogistiCore.Map
{
    public sealed class MapCityMarker : MonoBehaviour
    {
        [SerializeField] string cityId;

        public string CityId => cityId;
    }
}
