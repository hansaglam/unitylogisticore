# BACKEND-10 — LOCAL LiveOps sözleşmesi

26 Ekim 2026. Yalnız `demo-logisticore-local`. Bütün schedule/oran/priority değerleri UNAPPROVED SYNTHETIC CANDIDATES. Gerçek haber, hava, trafik, GPS veya oyuncu aktivitesi kullanılmaz.

## Katalog

`v2LiveOpsCatalog/{version}` strict schema1:

| Alanlar | Kural |
| --- | --- |
| schemaVersion, version, approval | 1, safe ID, synthetic_emulator_only |
| economyVersion/hash, logisticsVersion/hash, marketVersion/hash | Canonical immutable kataloglarla tam eşleşir |
| policy, policyHash | version1, highest_priority_then_ascii_id, add_bps_clamp_single_floor, maxEffectiveEvents1; canonical SHA-256 |
| currency, scale | Canonical economy ile aynı |
| minEventBps, maxEventBps | -1000..0 / 0..1500 |
| minCombinedBps, maxCombinedBps | Pinned market min/max değerleriyle aynı; global envelope genişlemez |
| maxEvents, events | maxEvents1..16; events0..maxEvents, unique eventId |

Her event exact `{eventId,type,titleKey,descriptionKey,startsAt,endsAt,cityIds,cargoIds,adjustmentBps,priority,enabled}` taşır. Tarihler UTC Unix integer milliseconds;0..253402300799999, başlangıç<bitiş, duration<=31 gün. Priority integer0..1000; enabled boolean; cityIds1..16 canonical logistics ID, cargoIds1..5 pinned market cargo ID. Duplicate scope/ID, unsafe sayı, ekstra/eksik alan ve hash/version drift reddedilir. Localization keys `events.` ile başlayan bounded lowercase ID'dir; TR/EN/DE çeviri client'a aittir.

Türler: regional_demand_surge pozitif bps; logistics_congestion negatif bps; seasonal_trade_activity signed bps. Congestion yalnız yeni fiyat koşuludur; yol kapatma, teslimat cezası/süresi/hasarı yoktur. Enabled immutable katalog sürümünün parçasıdır; aynı version yerinde edit edilmez.

## Aktivasyon ve overlap

Aktiflik `enabled && startsAt <= serverNow < endsAt`. Okuma/generation anında pure değerlendirme yapılır. Scheduler, global active document, tick write, per-company activation veya scheduled payout yoktur. Uygun destination/cargo için yalnız bir etkinlik fiyatı etkiler: highest priority, eşitlikte ASCII lexicographically smallest eventId. Diğer aktif etkinlikler bilgi amaçlı görünebilir; finansal bonuslar stack edilmez.

V5 contract catalog `liveOpsVersion/liveOpsHash` pin'ler. Mevcut `v2MarketSelection/{canonicalEconomyVersion}` exact selector V4/V5 contract config seçebilir. Public version/event/time override veya publication endpoint yoktur. Fixtures yalnız emulator'da açık trusted katalog/selector oluşturur. Eski pinned katalogları temizleme/migration yapılmaz.

## getMyWorldEvents

LOCAL-only callable us-central1/maxInstances1. Auth ve exact `{protocolVersion:1}` zorunlu. Canonical UID/company/economy/starter readiness, ownership ve migration kontrolü yapılır. V5 seçimi ve bütün bağlı kataloglar/hash'ler doğrulanır; V4-only veya eksik/corrupt selector LiveOps varmış gibi boş başarı üretmez, fail-closed olur. `getMyMarketState` V5 selector altında mevcut market-only DTO'sunu korur.

Envelope `{protocolVersion:1,serverTime,result:{synthetic:true,catalogVersion,policyVersion,currentEventIds,events}}`. Her event DTO'su eventId/type/titleKey/descriptionKey, ISO UTC startsAt/endsAt, cityIds/cargoIds, informational adjustmentBps ve upcoming/active/ended status içerir. currentEventIds bilgi amaçlı bütün dönen aktif event'lerdir; aynı fiyat için hepsi uygulanmaz.

Yanıt politikası: yalnız enabled event'ler; başlangıcı şimdi+7gün veya önce, bitişi şimdi-24saatten sonra. En fazla16 kayıt; startsAt ardından ASCII ID sırası. Unbounded lifetime history/pagination/scan yoktur. Disabled kayıt dönmez. ServerTime her okumada farklı olabilir; command receipt değildir. DTO seed, path, admin, wallet, debt veya ham katalog açmaz.

Read-only transaction; wallet/XP/event/offer/receipt yazısı, bootstrap/repair veya ödül yoktur. Env guard DB erişiminden önce çalışır. Default-deny Rules korunur. Mevcut UNAUTHENTICATED/INVALID_ARGUMENT/MIGRATION_REQUIRED/FAILED_PRECONDITION/INTERNAL kodları kullanılır; private error ayrıntıları yayımlanmaz.

## Operasyon ve ertelenenler

Event read yaklaşık18 point read (foundation8 + starter4 + selector1 + contract/logistics/cost/market/liveOps5), write0; en fazla16 event değerlendirme. V5 market read de18 olur, V4 read17 kalır. Bunlar statik tahmin, production ölçümü değildir; retry/billing minimumları ayrıca gelir.

Gerçek LiveOps admin/publication, onay/activation/retention, App Check/edge abuse, gerçek demand aggregation, push notification ve external feeds ertelendi. Production/real DEV/deploy/migration/untrusted CI BLOCK. LOCAL dependency istisnası30 Ekim2026'da sona erer; otomatik uzatılmaz. Fiyat kanıtı [EVENT_PRICING_CONTRACT](EVENT_PRICING_CONTRACT.md), doğrulama [BACKEND-10_REPORT](../backend/BACKEND-10_REPORT.md).
