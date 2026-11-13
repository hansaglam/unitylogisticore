using LogistiCore.Map;

namespace LogistiCore.Localization
{
    /// <summary>
    /// Player-facing copy for landscape management panels. Values live in the Ops string table.
    /// </summary>
    public static class OpsContent
    {
        public const string Table = "Ops";

        public const string TitleFleet = "ops.fleet.title";
        public const string TitleJobs = "ops.jobs.title";
        public const string TitleWarehouses = "ops.warehouses.title";
        public const string TitleMarket = "ops.market.title";
        public const string TitleRoad = "ops.road.title";
        public const string BodyFleet = "ops.fleet.unconnected";
        public const string BodyJobs = "ops.jobs.unconnected";
        public const string BodyWarehouses = "ops.warehouses.unconnected";
        public const string BodyMarket = "ops.market.unconnected";
        public const string BodyRoad = "ops.road.unconnected";
        public const string Expand = "ops.action.expand";
        public const string Collapse = "ops.action.collapse";
        public const string Switch = "ops.action.switch";
        public const string StripLoading = "ops.strip.loading";
        public const string StripError = "ops.strip.error";
        public const string StripIdle = "ops.strip.idle";
        public const string StripSingle = "ops.strip.single";
        public const string StripMultiple = "ops.strip.multiple";
        public const string FleetLoading = "ops.fleet.loading";
        public const string FleetEmpty = "ops.fleet.empty";
        public const string FleetError = "ops.fleet.error";
        public const string FleetReady = "ops.fleet.ready";
        public const string FleetAvailable = "ops.fleet.available";
        public const string FleetBusy = "ops.fleet.busy";
        public const string FleetOutOfService = "ops.fleet.out_of_service";
        public const string FleetUnknown = "ops.fleet.unknown";
        public const string FleetKindTruck = "ops.fleet.kind.truck";
        public const string FleetKindDriver = "ops.fleet.kind.driver";
        public const string FleetKindTrailer = "ops.fleet.kind.trailer";
        public const string FleetAssigned = "ops.fleet.assigned";
        public const string FleetUnassigned = "ops.fleet.unassigned";
        public const string FleetCapacity = "ops.fleet.capacity";
        public const string FleetLocationUnknown = "ops.fleet.location.unknown";
        public const string FleetLocationCity = "ops.fleet.location.city";
        public const string FleetLocationGps = "ops.fleet.location.gps";
        public const string FleetShowCity = "ops.fleet.action.show_city";
        public const string FleetShowGps = "ops.fleet.action.show_position";
        public const string FleetBack = "ops.fleet.action.back";
        public const string JobsLoading = "ops.jobs.loading";
        public const string JobsEmpty = "ops.jobs.empty";
        public const string JobsError = "ops.jobs.error";
        public const string JobsReady = "ops.jobs.ready";
        public const string JobsCategoryEmpty = "ops.jobs.category.empty";
        public const string JobsAvailable = "ops.jobs.available";
        public const string JobsActive = "ops.jobs.active";
        public const string JobsHistory = "ops.jobs.history";
        public const string JobsOffered = "ops.jobs.status.offered";
        public const string JobsAccepted = "ops.jobs.status.accepted";
        public const string JobsInProgress = "ops.jobs.status.in_progress";
        public const string JobsCompleted = "ops.jobs.status.completed";
        public const string JobsFailed = "ops.jobs.status.failed";
        public const string JobsUnknown = "ops.jobs.status.unknown";
        public const string JobsExpired = "ops.jobs.expired";
        public const string JobsCargo = "ops.jobs.cargo";
        public const string JobsCargoUnknown = "ops.jobs.cargo.unknown";
        public const string JobsCargoGeneralFreight = "ops.jobs.cargo.lc_dev_general_freight_v1";
        public const string JobsStatusField = "ops.jobs.field.status";
        public const string JobsReward = "ops.jobs.reward";
        public const string JobsRewardUnavailable = "ops.jobs.reward.unavailable";
        public const string JobsVehicle = "ops.jobs.vehicle";
        public const string JobsRequirements = "ops.jobs.requirements";
        public const string JobsDeliveryActive = "ops.jobs.delivery.active";
        public const string JobsDeliveryCompleted = "ops.jobs.delivery.completed";
        public const string JobsOriginUnknown = "ops.jobs.origin.unknown";
        public const string JobsDestinationUnknown = "ops.jobs.destination.unknown";
        public const string JobsShowOrigin = "ops.jobs.action.show_origin";
        public const string JobsShowDestination = "ops.jobs.action.show_destination";
        public const string JobsBack = "ops.jobs.action.back";
        public const string JobsCloseDetail = "ops.jobs.action.close_detail";
        public const string JobsAccept = "ops.jobs.action.accept";
        public const string JobsAcceptBusy = "ops.jobs.accept.busy";
        public const string JobsAcceptUncertain = "ops.jobs.accept.uncertain";
        public const string JobsAcceptUnavailable = "ops.jobs.accept.unavailable";
        public const string JobsAcceptUnauthenticated = "ops.jobs.accept.unauthenticated";
        public const string JobsAcceptDenied = "ops.jobs.accept.denied";
        public const string JobsAcceptInvalid = "ops.jobs.accept.invalid";
        public const string JobsAcceptConflict = "ops.jobs.accept.conflict";
        public const string JobsAcceptExpired = "ops.jobs.accept.expired";
        public const string JobsAcceptNotOffered = "ops.jobs.accept.not_offered";
        public const string JobsAcceptIneligible = "ops.jobs.accept.ineligible";
        public const string JobsAcceptQuota = "ops.jobs.accept.quota";
        public const string JobsEquipment = "ops.jobs.equipment";
        public const string JobsEquipmentAvailable = "ops.jobs.equipment.available";
        public const string JobsEquipmentUnavailable = "ops.jobs.equipment.unavailable";
        public const string JobsEquipmentMissing = "ops.jobs.equipment.missing";
        public const string JobsEquipmentAssigned = "ops.jobs.equipment.assigned";
        public const string JobsEquipmentMismatch = "ops.jobs.equipment.mismatch";
        public const string JobsEquipmentSelect = "ops.jobs.equipment.select";
        public const string JobsEquipmentSelected = "ops.jobs.equipment.selected";
        public const string WarehousesLoading = "ops.warehouses.loading";
        public const string WarehousesEmpty = "ops.warehouses.empty";
        public const string WarehousesError = "ops.warehouses.error";
        public const string WarehousesReady = "ops.warehouses.ready";
        public const string WarehousesOperational = "ops.warehouses.operational";
        public const string WarehousesOutOfService = "ops.warehouses.out_of_service";
        public const string WarehousesUnknown = "ops.warehouses.unknown";
        public const string WarehousesOwned = "ops.warehouses.owned";
        public const string WarehousesCapacity = "ops.warehouses.capacity";
        public const string WarehousesUsedUnknown = "ops.warehouses.used.unknown";
        public const string WarehousesLocation = "ops.warehouses.location";
        public const string WarehousesCityUnknown = "ops.warehouses.city.unknown";
        public const string WarehousesLevel = "ops.warehouses.level";
        public const string WarehousesType = "ops.warehouses.type";
        public const string WarehousesShowCity = "ops.warehouses.action.show_city";
        public const string WarehousesShowPosition = "ops.warehouses.action.show_position";
        public const string WarehousesBack = "ops.warehouses.action.back";
        public const string MarketLoading = "ops.market.loading";
        public const string MarketEmpty = "ops.market.empty";
        public const string MarketError = "ops.market.error";
        public const string MarketReady = "ops.market.ready";
        public const string MarketAll = "ops.market.all";
        public const string MarketCondition = "ops.market.condition";
        public const string MarketCargo = "ops.market.cargo";
        public const string MarketLocation = "ops.market.location";
        public const string MarketCityUnknown = "ops.market.city.unknown";
        public const string MarketAdjustment = "ops.market.adjustment";
        public const string MarketDemandIndex = "ops.market.demand_index";
        public const string MarketSupplyIndex = "ops.market.supply_index";
        public const string MarketPriceUnavailable = "ops.market.price.unavailable";
        public const string MarketShowCity = "ops.market.action.show_city";
        public const string MarketBack = "ops.market.action.back";
        public const string RoadLoading = "ops.road.loading";
        public const string RoadEmpty = "ops.road.empty";
        public const string RoadError = "ops.road.error";
        public const string RoadReady = "ops.road.ready";
        public const string RoadActive = "ops.road.status.active";
        public const string RoadUnknown = "ops.road.status.unknown";
        public const string RoadTruckUnknown = "ops.road.truck.unknown";
        public const string RoadArrivalUnknown = "ops.road.arrival.unknown";
        public const string RoadShowOrigin = "ops.road.action.show_origin";
        public const string RoadShowDestination = "ops.road.action.show_destination";
        public const string RoadBack = "ops.road.action.back";

        // Keys already present in the EN/TR/DE Ops string table assets.
        public const string HudLevel = "ops.hud.level";
        public const string HudXp = "ops.hud.xp";
        public const string HudLoading = "ops.hud.loading";
        public const string HudUnavailable = "ops.hud.unavailable";
        public const string HudReputation = "ops.hud.reputation";

        public const string LongestTitle = "Lagerhäuser";
        public const string LongestBody = "Keine Live-Fuhrparkverbindung.";
        public const string LongestExpand = "Erweitern";
        public const string LongestCollapse = "Kompakt";
        public const string LongestStrip = "Operationen konnten nicht geladen werden.";
        public const string LongestFleet = "Filodaten konnten nicht geladen werden.";
        public const string LongestJobs = "Auftragsdaten konnten nicht geladen werden.";
        public const string LongestWarehouses = "Lagerdaten konnten nicht geladen werden.";
        public const string LongestMarket = "Marktdaten konnten nicht geladen werden.";
        public const string LongestRoad = "Lieferdaten konnten nicht geladen werden.";

        public static readonly string[] Keys =
        {
            TitleFleet,
            TitleJobs,
            TitleWarehouses,
            TitleMarket,
            TitleRoad,
            BodyFleet,
            BodyJobs,
            BodyWarehouses,
            BodyMarket,
            BodyRoad,
            Expand,
            Collapse,
            Switch,
            StripLoading,
            StripError,
            StripIdle,
            StripSingle,
            StripMultiple,
            FleetLoading,
            FleetEmpty,
            FleetError,
            FleetReady,
            FleetAvailable,
            FleetBusy,
            FleetOutOfService,
            FleetUnknown,
            FleetKindTruck,
            FleetKindDriver,
            FleetKindTrailer,
            FleetAssigned,
            FleetUnassigned,
            FleetCapacity,
            FleetLocationUnknown,
            FleetLocationCity,
            FleetLocationGps,
            FleetShowCity,
            FleetShowGps,
            FleetBack,
            JobsLoading,
            JobsEmpty,
            JobsError,
            JobsReady,
            JobsCategoryEmpty,
            JobsAvailable,
            JobsActive,
            JobsHistory,
            JobsOffered,
            JobsAccepted,
            JobsInProgress,
            JobsCompleted,
            JobsFailed,
            JobsUnknown,
            JobsExpired,
            JobsCargo,
            JobsCargoUnknown,
            JobsCargoGeneralFreight,
            JobsStatusField,
            JobsReward,
            JobsRewardUnavailable,
            JobsVehicle,
            JobsRequirements,
            JobsDeliveryActive,
            JobsDeliveryCompleted,
            JobsOriginUnknown,
            JobsDestinationUnknown,
            JobsShowOrigin,
            JobsShowDestination,
            JobsBack,
            JobsCloseDetail,
            JobsAccept,
            JobsAcceptBusy,
            JobsAcceptUncertain,
            JobsAcceptUnavailable,
            JobsAcceptUnauthenticated,
            JobsAcceptDenied,
            JobsAcceptInvalid,
            JobsAcceptConflict,
            JobsAcceptExpired,
            JobsAcceptNotOffered,
            JobsAcceptIneligible,
            JobsAcceptQuota,
            JobsEquipment,
            JobsEquipmentAvailable,
            JobsEquipmentUnavailable,
            JobsEquipmentMissing,
            JobsEquipmentAssigned,
            JobsEquipmentMismatch,
            JobsEquipmentSelect,
            JobsEquipmentSelected,
            WarehousesLoading,
            WarehousesEmpty,
            WarehousesError,
            WarehousesReady,
            WarehousesOperational,
            WarehousesOutOfService,
            WarehousesUnknown,
            WarehousesOwned,
            WarehousesCapacity,
            WarehousesUsedUnknown,
            WarehousesLocation,
            WarehousesCityUnknown,
            WarehousesLevel,
            WarehousesType,
            WarehousesShowCity,
            WarehousesShowPosition,
            WarehousesBack,
            MarketLoading,
            MarketEmpty,
            MarketError,
            MarketReady,
            MarketAll,
            MarketCondition,
            MarketCargo,
            MarketLocation,
            MarketCityUnknown,
            MarketAdjustment,
            MarketDemandIndex,
            MarketSupplyIndex,
            MarketPriceUnavailable,
            MarketShowCity,
            MarketBack,
            RoadLoading,
            RoadEmpty,
            RoadError,
            RoadReady,
            RoadActive,
            RoadUnknown,
            RoadTruckUnknown,
            RoadArrivalUnknown,
            RoadShowOrigin,
            RoadShowDestination,
            RoadBack,
            HudLevel,
            HudXp,
            HudLoading,
            HudUnavailable,
            HudReputation
        };

        public readonly struct Line
        {
            public readonly string Key;
            public readonly string English;
            public readonly string Turkish;
            public readonly string German;

            public Line(string key, string english, string turkish, string german)
            {
                Key = key;
                English = english;
                Turkish = turkish;
                German = german;
            }
        }

        public static readonly Line[] Lines =
        {
            new Line(TitleFleet, "Fleet", "Filo", "Flotte"),
            new Line(TitleJobs, "Jobs", "İşler", "Aufträge"),
            new Line(TitleWarehouses, "Warehouses", "Depolar", "Lagerhäuser"),
            new Line(TitleMarket, "Market", "Pazar", "Markt"),
            new Line(TitleRoad, "Road", "Karayolu", "Straße"),
            new Line(BodyFleet, "No live fleet data connection.", "Canlı filo verisi bağlantısı yok.", "Keine Live-Fuhrparkverbindung."),
            new Line(BodyJobs, "No live contracts connection.", "Canlı sözleşme bağlantısı yok.", "Keine Live-Auftragsverbindung."),
            new Line(BodyWarehouses, "No live warehouse connection.", "Canlı depo bağlantısı yok.", "Keine Live-Lagerverbindung."),
            new Line(BodyMarket, "No live market connection.", "Canlı pazar bağlantısı yok.", "Keine Live-Marktverbindung."),
            new Line(BodyRoad, "No live delivery connection.", "Canlı teslimat bağlantısı yok.", "Keine Live-Lieferverbindung."),
            new Line(Expand, "Expand", "Genişlet", "Erweitern"),
            new Line(Collapse, "Compact", "Daralt", "Kompakt"),
            new Line(Switch, "Modules", "Modüller", "Module"),
            new Line(StripLoading, "Checking operations…", "Operasyonlar kontrol ediliyor…", "Operationen werden geprüft…"),
            new Line(StripError, "Operations could not be loaded.", "Operasyonlar yüklenemedi.", "Operationen konnten nicht geladen werden."),
            new Line(StripIdle, "No active operations.", "Aktif operasyon yok.", "Keine aktiven Operationen."),
            new Line(StripSingle, "One active operation.", "Bir aktif operasyon.", "Eine aktive Operation."),
            new Line(StripMultiple, "Several active operations.", "Birden fazla aktif operasyon.", "Mehrere aktive Operationen."),
            new Line(FleetLoading, "Loading fleet…", "Filo yükleniyor…", "Flotte wird geladen…"),
            new Line(FleetEmpty, "No vehicles in this fleet.", "Bu filoda araç yok.", "Keine Fahrzeuge in dieser Flotte."),
            new Line(FleetError, "Fleet data could not be loaded.", "Filo verisi yüklenemedi.", "Filodaten konnten nicht geladen werden."),
            new Line(FleetReady, "Owned trucks and drivers are listed.", "Sahip olunan kamyon ve sürücüler listeleniyor.", "Eigene Lkw und Fahrer sind aufgeführt."),
            new Line(FleetAvailable, "Available", "Müsait", "Verfügbar"),
            new Line(FleetBusy, "On assignment", "Görevde", "Im Einsatz"),
            new Line(FleetOutOfService, "Out of service", "Hizmet dışı", "Außer Betrieb"),
            new Line(FleetUnknown, "Unknown", "Bilinmiyor", "Unbekannt"),
            new Line(FleetKindTruck, "Truck", "Kamyon", "Lkw"),
            new Line(FleetKindDriver, "Driver", "Sürücü", "Fahrer"),
            new Line(FleetKindTrailer, "Trailer", "Dorse", "Anhänger"),
            new Line(FleetAssigned, "Currently assigned", "Şu an görevde", "Derzeit zugewiesen"),
            new Line(FleetUnassigned, "No current assignment", "Aktif görev yok", "Keine aktuelle Zuweisung"),
            new Line(FleetCapacity, "Capacity", "Kapasite", "Kapazität"),
            new Line(FleetLocationUnknown, "Location unknown", "Konum bilinmiyor", "Standort unbekannt"),
            new Line(FleetLocationCity, "City location on file", "Kayıtlı şehir konumu", "Stadtstandort bekannt"),
            new Line(FleetLocationGps, "Reported vehicle position", "Bildirilen araç konumu", "Gemeldete Fahrzeugposition"),
            new Line(FleetShowCity, "Show city", "Şehri göster", "Stadt zeigen"),
            new Line(FleetShowGps, "Show position", "Konumu göster", "Position zeigen"),
            new Line(FleetBack, "Fleet list", "Filo listesi", "Fuhrparkliste"),
            new Line(JobsLoading, "Loading contracts…", "Sözleşmeler yükleniyor…", "Aufträge werden geladen…"),
            new Line(JobsEmpty, "No contracts in this snapshot.", "Bu özette sözleşme yok.", "Keine Aufträge in diesem Stand."),
            new Line(JobsError, "Contract data could not be loaded.", "Sözleşme verisi yüklenemedi.", "Auftragsdaten konnten nicht geladen werden."),
            new Line(JobsReady, "Contract snapshot loaded.", "Sözleşme özeti yüklendi.", "Auftragsstand geladen."),
            new Line(JobsCategoryEmpty, "No contracts in this category.", "Bu kategoride sözleşme yok.", "Keine Aufträge in dieser Kategorie."),
            new Line(JobsAvailable, "Available", "Açık", "Verfügbar"),
            new Line(JobsActive, "Active", "Aktif", "Aktiv"),
            new Line(JobsHistory, "History", "Geçmiş", "Verlauf"),
            new Line(JobsOffered, "Offered", "Teklif", "Angeboten"),
            new Line(JobsAccepted, "Accepted", "Kabul", "Angenommen"),
            new Line(JobsInProgress, "In progress", "Devam ediyor", "In Bearbeitung"),
            new Line(JobsCompleted, "Completed", "Tamamlandı", "Abgeschlossen"),
            new Line(JobsFailed, "Failed", "Başarısız", "Fehlgeschlagen"),
            new Line(JobsUnknown, "Unknown status", "Bilinmeyen durum", "Unbekannter Status"),
            new Line(JobsExpired, "Offer expired", "Teklif süresi doldu", "Angebot abgelaufen"),
            new Line(JobsCargo, "Cargo", "Yük", "Fracht"),
            new Line(JobsCargoUnknown, "Unknown cargo", "Bilinmeyen yük", "Unbekannte Fracht"),
            new Line(JobsCargoGeneralFreight, "General Freight", "Genel Yük", "Stückgut"),
            new Line(JobsStatusField, "Status", "Durum", "Status"),
            new Line(JobsReward, "Reward", "Ödül", "Prämie"),
            new Line(JobsRewardUnavailable, "Reward is not in this snapshot.", "Ödül bu özette yok.", "Prämie ist in diesem Stand nicht enthalten."),
            new Line(JobsVehicle, "Assigned vehicle", "Atanan araç", "Zugewiesenes Fahrzeug"),
            new Line(JobsRequirements, "Requirements", "Gereksinimler", "Anforderungen"),
            new Line(JobsDeliveryActive, "Delivery active", "Teslimat aktif", "Lieferung aktiv"),
            new Line(JobsDeliveryCompleted, "Delivery completed", "Teslimat tamamlandı", "Lieferung abgeschlossen"),
            new Line(JobsOriginUnknown, "Origin unknown", "Kalkış bilinmiyor", "Startort unbekannt"),
            new Line(JobsDestinationUnknown, "Destination unknown", "Varış bilinmiyor", "Zielort unbekannt"),
            new Line(JobsShowOrigin, "Show origin", "Kalkışı göster", "Startort zeigen"),
            new Line(JobsShowDestination, "Show destination", "Varışı göster", "Zielort zeigen"),
            new Line(JobsBack, "Contract list", "Sözleşme listesi", "Auftragsliste"),
            new Line(JobsCloseDetail, "Close details", "Detayı kapat", "Details schließen"),
            new Line(JobsAccept, "Accept", "Kabul Et", "Annehmen"),
            new Line(JobsAcceptBusy, "Accepting contract…", "Sözleşme kabul ediliyor…", "Auftrag wird angenommen…"),
            new Line(JobsAcceptUncertain, "Acceptance result is not confirmed.", "Kabul sonucu doğrulanmadı.", "Annahmeergebnis ist unbestätigt."),
            new Line(JobsAcceptUnavailable, "This contract cannot be accepted now.", "Bu sözleşme şu an kabul edilemez.", "Dieser Auftrag kann jetzt nicht angenommen werden."),
            new Line(JobsAcceptUnauthenticated, "Sign in is required to accept.", "Kabul için oturum gerekli.", "Zum Annehmen ist eine Anmeldung nötig."),
            new Line(JobsAcceptDenied, "Acceptance is not allowed.", "Kabul izni yok.", "Annahme ist nicht erlaubt."),
            new Line(JobsAcceptInvalid, "This accept request is not valid.", "Bu kabul isteği geçerli değil.", "Diese Annahmeanfrage ist ungültig."),
            new Line(JobsAcceptConflict, "This accept request already exists.", "Bu kabul isteği zaten var.", "Diese Annahmeanfrage existiert bereits."),
            new Line(JobsAcceptExpired, "This offer is no longer available.", "Bu teklif artık geçerli değil.", "Dieses Angebot ist nicht mehr verfügbar."),
            new Line(JobsAcceptNotOffered, "This contract cannot be accepted.", "Bu sözleşme kabul edilemez.", "Dieser Auftrag kann nicht angenommen werden."),
            new Line(JobsAcceptIneligible, "Fleet is not eligible for this contract.", "Filo bu sözleşme için uygun değil.", "Die Flotte ist für diesen Auftrag nicht geeignet."),
            new Line(JobsAcceptQuota, "No more contracts can be accepted now.", "Şu an başka sözleşme kabul edilemez.", "Es können jetzt keine weiteren Aufträge angenommen werden."),
            new Line(JobsEquipment, "Equipment", "Ekipman", "Ausrüstung"),
            new Line(JobsEquipmentAvailable, "Available", "Müsait", "Verfügbar"),
            new Line(JobsEquipmentUnavailable, "Unavailable", "Müsait değil", "Nicht verfügbar"),
            new Line(JobsEquipmentMissing, "Not in this fleet", "Bu filoda yok", "Nicht in dieser Flotte"),
            new Line(JobsEquipmentAssigned, "On assignment", "Görevde", "Im Einsatz"),
            new Line(JobsEquipmentMismatch, "Type mismatch", "Tür uyuşmuyor", "Typ stimmt nicht"),
            new Line(JobsEquipmentSelect, "Select", "Seç", "Wählen"),
            new Line(JobsEquipmentSelected, "Selected", "Seçildi", "Ausgewählt"),
            new Line(WarehousesLoading, "Loading warehouses…", "Depolar yükleniyor…", "Lager werden geladen…"),
            new Line(WarehousesEmpty, "No owned warehouses in this snapshot.", "Bu özette sahip olunan depo yok.", "Keine eigenen Lager in diesem Stand."),
            new Line(WarehousesError, "Warehouse data could not be loaded.", "Depo verisi yüklenemedi.", "Lagerdaten konnten nicht geladen werden."),
            new Line(WarehousesReady, "Warehouse snapshot loaded.", "Depo özeti yüklendi.", "Lagerstand geladen."),
            new Line(WarehousesOperational, "Operational", "Çalışır", "Betriebsbereit"),
            new Line(WarehousesOutOfService, "Out of service", "Hizmet dışı", "Außer Betrieb"),
            new Line(WarehousesUnknown, "Unknown status", "Bilinmeyen durum", "Unbekannter Status"),
            new Line(WarehousesOwned, "Owned warehouse", "Sahip olunan depo", "Eigenes Lager"),
            new Line(WarehousesCapacity, "Capacity", "Kapasite", "Kapazität"),
            new Line(WarehousesUsedUnknown, "Used capacity unavailable", "Kullanılan kapasite yok", "Belegte Kapazität unbekannt"),
            new Line(WarehousesLocation, "Location", "Konum", "Standort"),
            new Line(WarehousesCityUnknown, "City unknown", "Şehir bilinmiyor", "Stadt unbekannt"),
            new Line(WarehousesLevel, "Level", "Seviye", "Stufe"),
            new Line(WarehousesType, "Type", "Tür", "Typ"),
            new Line(WarehousesShowCity, "Show city", "Şehri göster", "Stadt zeigen"),
            new Line(WarehousesShowPosition, "Show position", "Konumu göster", "Position zeigen"),
            new Line(WarehousesBack, "Warehouse list", "Depo listesi", "Lagerliste"),
            new Line(MarketLoading, "Loading market…", "Pazar yükleniyor…", "Markt wird geladen…"),
            new Line(MarketEmpty, "No market conditions in this snapshot.", "Bu özette pazar koşulu yok.", "Keine Marktbedingungen in diesem Stand."),
            new Line(MarketError, "Market data could not be loaded.", "Pazar verisi yüklenemedi.", "Marktdaten konnten nicht geladen werden."),
            new Line(MarketReady, "Market snapshot loaded.", "Pazar özeti yüklendi.", "Marktstand geladen."),
            new Line(MarketAll, "All cargo", "Tüm yük", "Alle Fracht"),
            new Line(MarketCondition, "Market condition", "Pazar koşulu", "Marktbedingung"),
            new Line(MarketCargo, "Cargo", "Yük", "Fracht"),
            new Line(MarketLocation, "Location", "Konum", "Standort"),
            new Line(MarketCityUnknown, "City unknown", "Şehir bilinmiyor", "Stadt unbekannt"),
            new Line(MarketAdjustment, "Adjustment bps", "Ayar bps", "Anpassung bps"),
            new Line(MarketDemandIndex, "Demand index", "Talep indeksi", "Nachfrageindex"),
            new Line(MarketSupplyIndex, "Supply index", "Arz indeksi", "Angebotsindex"),
            new Line(MarketPriceUnavailable, "Price is not in this snapshot.", "Fiyat bu özette yok.", "Preis ist in diesem Stand nicht enthalten."),
            new Line(MarketShowCity, "Show city", "Şehri göster", "Stadt zeigen"),
            new Line(MarketBack, "Condition list", "Koşul listesi", "Bedingungsliste"),
            new Line(RoadLoading, "Loading deliveries…", "Teslimatlar yükleniyor…", "Lieferungen werden geladen…"),
            new Line(RoadEmpty, "No active road deliveries.", "Aktif karayolu teslimatı yok.", "Keine aktiven Straßenlieferungen."),
            new Line(RoadError, "Delivery data could not be loaded.", "Teslimat verisi yüklenemedi.", "Lieferdaten konnten nicht geladen werden."),
            new Line(RoadReady, "Active road deliveries.", "Aktif karayolu teslimatları.", "Aktive Straßenlieferungen."),
            new Line(RoadActive, "Active", "Aktif", "Aktiv"),
            new Line(RoadUnknown, "Unknown delivery status", "Bilinmeyen teslimat durumu", "Unbekannter Lieferstatus"),
            new Line(RoadTruckUnknown, "Truck unknown", "Kamyon bilinmiyor", "Lkw unbekannt"),
            new Line(RoadArrivalUnknown, "Arrival time not provided", "Varış zamanı yok", "Ankunftszeit nicht angegeben"),
            new Line(RoadShowOrigin, "Show origin", "Kalkışı göster", "Start zeigen"),
            new Line(RoadShowDestination, "Show destination", "Varışı göster", "Ziel zeigen"),
            new Line(RoadBack, "Delivery list", "Teslimat listesi", "Lieferliste"),
            new Line(HudLevel, "Level", "Seviye", "Stufe"),
            new Line(HudXp, "XP", "XP", "XP"),
            new Line(HudLoading, "Loading…", "Yükleniyor…", "Wird geladen…"),
            new Line(HudUnavailable, "Unavailable", "Kullanılamıyor", "Nicht verfügbar"),
            new Line(HudReputation, "Reputation", "İtibar", "Ruf")
        };

        public static string CommandErrorKey(string detailsCode)
        {
            switch (detailsCode)
            {
                case "UNAUTHENTICATED":
                    return JobsAcceptUnauthenticated;
                case "PERMISSION_DENIED":
                    return JobsAcceptDenied;
                case "INVALID_ARGUMENT":
                    return JobsAcceptInvalid;
                case "IDEMPOTENCY_CONFLICT":
                    return JobsAcceptConflict;
                case "OFFER_EXPIRED":
                case "CONTRACT_EXPIRED":
                    return JobsAcceptExpired;
                case "CONTRACT_NOT_OFFERED":
                    return JobsAcceptNotOffered;
                case "CONTRACT_INELIGIBLE":
                    return JobsAcceptIneligible;
                case "CONTRACT_QUOTA_REACHED":
                    return JobsAcceptQuota;
                case "RETRY_LATER":
                    return JobsAcceptUncertain;
                default:
                    return JobsAcceptUnavailable;
            }
        }

        public static string CargoKey(string cargoId)
        {
            if (cargoId == "lc_dev_general_freight_v1")
                return JobsCargoGeneralFreight;
            return "";
        }

        public static string TitleKey(MapModule module)
        {
            switch (module)
            {
                case Map.MapModule.Jobs:
                    return TitleJobs;
                case Map.MapModule.Warehouses:
                    return TitleWarehouses;
                case Map.MapModule.Market:
                    return TitleMarket;
                case Map.MapModule.Road:
                    return TitleRoad;
                default:
                    return TitleFleet;
            }
        }

        public static string BodyKey(MapModule module)
        {
            switch (module)
            {
                case Map.MapModule.Jobs:
                    return BodyJobs;
                case Map.MapModule.Warehouses:
                    return BodyWarehouses;
                case Map.MapModule.Market:
                    return BodyMarket;
                case Map.MapModule.Road:
                    return BodyRoad;
                default:
                    return BodyFleet;
            }
        }
    }
}
