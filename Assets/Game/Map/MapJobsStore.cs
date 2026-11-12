using LogistiCore.Backend;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Jobs read model. Trusted rows come from getMyContracts via MapJobsContractsRead.
    /// This store does not call getMyDeliveries.
    /// </summary>
    public static class MapJobsStore
    {
        static bool _hasOverride;
        static MapJobsSnapshot _override;
        static bool _hasTrusted;
        static MapJobsSnapshot _trusted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            ClearOverride();
            ClearTrusted();
            MapJobsEquipmentSelection.Clear();
        }

        public static bool HasOverride => _hasOverride;

        public static MapJobsSnapshot Current
        {
            get
            {
                if (_hasOverride)
                    return _override;
                if (_hasTrusted)
                    return _trusted;
                return FromBackend(FirebaseClient.Current);
            }
        }

        public static MapJobsSnapshot FromBackend(ClientBackendStatus status)
        {
            switch (status.Phase)
            {
                case ClientBackendPhase.Starting:
                    return MapJobsSnapshot.Loading();
                case ClientBackendPhase.Failed:
                    return MapJobsSnapshot.Error();
                default:
                    return MapJobsSnapshot.Unconfigured();
            }
        }

        public static void Override(MapJobsSnapshot snapshot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!snapshot.Synthetic)
                return;
            _override = snapshot;
            _hasOverride = true;
#endif
        }

        public static bool TryApplyTrusted(MapJobsSnapshot snapshot)
        {
            if (snapshot.Synthetic || snapshot.Status == MapJobsStatus.Unconfigured)
                return false;
            var category = _hasTrusted ? _trusted.Category : snapshot.Category;
            var selected = _hasTrusted ? _trusted.SelectedId : snapshot.SelectedId;
            _trusted = snapshot.Reconcile(category, selected);
            _hasTrusted = true;
            return true;
        }

        public static void Select(string contractId)
        {
            var previous = Current.SelectedId;
            if (_hasOverride)
                _override = _override.WithSelected(contractId);
            else if (_hasTrusted)
                _trusted = _trusted.WithSelected(contractId);
            if (Current.SelectedId != previous)
                MapJobsEquipmentSelection.Clear();
        }

        public static void SetCategory(MapJobCategory category)
        {
            if (_hasOverride)
                _override = _override.WithCategory(category);
            else if (_hasTrusted)
                _trusted = _trusted.WithCategory(category);
            MapJobsEquipmentSelection.Clear();
        }

        public static void ClearOverride()
        {
            _hasOverride = false;
            _override = default;
            MapJobsEquipmentSelection.Clear();
        }

        public static void ClearTrusted()
        {
            _hasTrusted = false;
            _trusted = default;
            MapJobsEquipmentSelection.Clear();
        }

        public static bool TryJoinDeliveries(ClientDeliveryPage page)
        {
            if (!_hasTrusted || _trusted.Synthetic)
                return false;
            var items = _trusted.Items;
            var next = new MapJobsItem[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                var row = items[i];
                if (TryMatch(page, row.ContractId, out var delivery))
                {
                    next[i] = row.WithDelivery(
                        delivery.DeliveryId,
                        MapJobsItem.ParseDelivery(delivery.Status),
                        delivery.ExpectedArrivalAt,
                        TruckId(delivery));
                }
                else
                    next[i] = row;
            }

            _trusted = new MapJobsSnapshot(_trusted.Status, next, _trusted.SelectedId, _trusted.Category, false);
            return true;
        }

        static bool TryMatch(ClientDeliveryPage page, string contractId, out ClientDeliveryRow delivery)
        {
            delivery = default;
            if (string.IsNullOrEmpty(contractId) || page.Items == null)
                return false;
            for (var i = 0; i < page.Items.Length; i++)
            {
                if (page.Items[i].ContractId == contractId)
                {
                    delivery = page.Items[i];
                    return true;
                }
            }

            return false;
        }

        static string TruckId(ClientDeliveryRow delivery)
        {
            var assets = delivery.Assets;
            if (assets == null)
                return "";
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i].Kind == "truck")
                    return assets[i].AssetId;
            }

            return "";
        }
    }
}
