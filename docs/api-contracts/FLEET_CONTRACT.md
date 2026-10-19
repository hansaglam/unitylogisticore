# FLEET_CONTRACT — BACKEND-08A

20 Ekim 2026. **IMPLEMENTED: trusted LOCAL / `demo-logisticore-local` only. Production BLOCK.**

## 1. Yetki, onboarding ve ürün sınırı

Client intent gönderir; UID yalnız callable Auth context'ten alınır. Backend canonical player→company mapping, owner, geçerli ekonomi ve legacy/migration uygunluğunu transaction içinde kontrol eder. Internal TypeScript fonksiyonlar güvenilir backend kodu içindir; `{uid}` nesnesi tek başına bir auth sistemi değildir. Index yalnız iki yeni read-only callable export eder, hiçbir grant/assignment/admin/config writer export etmez.

Onaylı başlangıç paketi tam **bir truck + bir driver**, şirket mülkiyetinde; warehouse veya trailer otomatik verilmez. VIP **DEFERRED**. Gerçek truck özellikleri, trailer olmadan ilk iş uyumluluğu ve contract eligibility **UNKNOWN**. LGC/2 decimal öneridir; 15.000 LGC başlangıç bakiyesi yalnız tasarım adayıdır. Bu faz hiçbir gerçek para/price/fuel/salary/capacity varsayılanı yayınlamaz. Ekonomi initialization synthetic-emulator-only kalır.

**PROPOSED resumable onboarding:** identity → company foundation → economy initialization → starter provisioning → gelecekteki gameplay eligibility. Şimdiki `economy_ready` sadece BACKEND-05 ekonomisini ifade eder. Yeni `starterFleetReady` ayrı göstergedir; ikisinin true olması bile henüz gerçek job eligibility veya production playable onayı değildir. Economy ready/starter false normal bekleme durumudur; next internal provisioning retry aynı key ile sürdürülür. Otomatik gerçek onboarding aktivasyonu yoktur.

## 2. Synthetic server catalog

`v2LogisticsCatalog/{version}` strict schema:

- `schemaVersion:1`, `version`, `approval:"synthetic_emulator_only"`.
- `assets`: en çok 32 `{id,kind}`; kind `truck|driver|trailer`, unique IDs.
- `starter:{packageVersion,truck,driver}`: tam bir truck ve driver katalog referansı; roller doğrulanır.
- `cities`: en çok 32 unique server-controlled ID; gerçek şehir kataloğu değildir.
- `warehouses`: en çok 32 tanım; [WAREHOUSE_CONTRACT](WAREHOUSE_CONTRACT.md).

ID/version: 1–64 ASCII alfanümerik/alt çizgi/tire, ilk karakter alfanümerik. Ek alan/unknown kind/eksik referans/onaysız config reddedilir. Canonical SHA-256 hash varlıklara/marker'a pin edilir; aynı version içeriği değişirse eski kayıtlar fail-closed olur. Test dışı writer veya active-version pointer yoktur. Gerçek katalog aktivasyonu ayrı ürün/security/migration/rollout onayı ister.

## 3. Canonical asset şeması

`v2Companies/{companyId}/assets/{assetId}`:

| Alan | Kural |
|---|---|
| schemaVersion | 1 |
| assetId | document ID ile aynı |
| kind | truck / driver / trailer, katalogla aynı |
| companyId | Auth UID'den çözülen canonical company |
| catalogId / catalogVersion / catalogHash | server catalog tanımı ve içerik pin'i |
| status | operational / out_of_service |
| assignmentId | null veya server-derived SHA-256 assignment ID |
| acquiredAt | server timestamp |

Availability yalnız `status == operational && assignmentId == null`. Aynı company's canonical documents otoritedir; player inventory array veya client ownership/capacity/status yoktur. DTO operational state, assignmentId, catalog refs ve derived available içerir; timestamp/hash/owner/private entitlement gibi gerekli olmayan metadata içermez. Bilinmeyen schema/status, ek alan, yanlış owner/reference/hash fail-closed. Repair yoktur.

## 4. Starter provisioning

Internal `provisionStarterFleet(db, trustedAuth, commandEnvelope, trustedCatalogVersion)`; client envelope payload `{}`. Selector server tarafından seçilir, fingerprint'e girer. Ekonomi ve migration kontrolü BACKEND-05 readFoundation üzerinden yapılır.

Stable document IDs: `starter_truck`, `starter_driver` (company scope içinde). Permanent marker: `v2Companies/{companyId}/provisioning/starterFleet`:
`schemaVersion:1,companyId,packageVersion,catalogVersion,catalogHash,truckId,driverId,committedAt`.

Tekillik **company başına bir starter package, version ile pin edilmiş**. Marker path'inin sabit olması farklı package/catalog version ile ikinci ücretsiz grant'i de engeller. Bu faz version başına yeniden ödül vermez; gelecekte package migration ayrı onaylı mekanizma gerektirir. Marker/asset/receipt için TTL veya delete yoktur.

İlk başarı truck + driver + marker + receipt'i tek transaction'da create eder. Company lifecycle/wallet/XP yazılmaz. Üç kaydın tamamının yokluğu pending, tamamının geçerliliği ready; eksik/kısmi/corrupt kombinasyon hata. Receipt replay öncesinde de current owner/economy/marker/assets doğrulanır. Aynı key özgün result/serverTime'ı döndürür; farklı key hazır paketi reddeder. Sekiz farklı key concurrency testi tek grant kanıtıdır. Testler dışında creator çalıştırılmaz; production starter provisioning DISABLED.

## 5. Assignment transitions

Internal `assignmentCommand` BACKEND-03 receipt adapter'ıdır; `writeAssignment` aynı transaction'da `readFoundation` ile trusted UID'den türetilmiş CompanyState isteyen reusable primitive'dir. Çağıran tüm ek domain okumalarını ilk yazıdan önce bitirmelidir; helper sonrasında yeni Firestore read yapılmaz.

Server-selected stable source → SHA-256 `{companyId,source}` assignment ID. Client delivery ID'si veya rastgele payload source kabul eden endpoint yoktur. Selection tam bir truck + bir driver + isteğe bağlı bir trailer (2–3 farklı ID). Her varlığın existence/owner/kind/catalog/status/assignment'ı doğrulanır; bütün okumalar bitmeden yazı başlamaz.

Reservation: varlıkların hepsi available olmalı; assignment document absent olmalı. Atomik olarak tüm asset.assignmentId alanları yazılır ve `.../assignments/{id}` create edilir: `schemaVersion,assignmentId,companyId,assets:[{assetId,kind}],status:active,createdAt`.

Release: aynı active assignment, birebir aynı asset seti ve her asset'te expected assignment ID gerekir. Tüm reservation'lar birlikte null olur; assignment `released` ve `releasedAt` ile kalıcı kalır. Released ID tekrar reserve edilemez (ABA/stale-release koruması). Aynı receipt replay tarihsel sonuçtur; canlı varlık durumu değildir. Farklı key eski release, yeni assignment'ı boşaltamaz. Reservation sırasında out_of_service reddedilir; release mevcut sahipliği doğruladığında sonradan out_of_service olmuş asset'i serbest bırakabilir, operational status'u değiştirmez.

Gerçek eşzamanlı işlemler shared truck/shared driver üzerinde tek winner üretir; conflict retry Firestore SDK'ye aittir. Kalıcı assignment kaydı yeniden kullanılmaz. Teslimat zaman/fuel/reward hesapları **DEFERRED**.

## 6. getMyFleetState

LOCAL callable `us-central1`, maxInstances 1. Request:
```json
{"protocolVersion":1,"limit":20,"after":"starter_driver"}
```
`limit` opsiyonel (default/max 20, minimum 1); `after` opsiyonel son document ID, path değildir. UID/company/catalog/amount veya ek alan kabul edilmez. Document-ID ascending order ve limit+1 lookahead; offset veya unbounded scan yoktur. Cursor company scope'unu değiştiremez. Sayfalar arası snapshot garantisi yoktur; concurrent değişiklik varsa Unity listeyi yenilemelidir.

Pending sentetik result:
```json
{"hasCompany":true,"economyReady":false,"starterFleetReady":false,"items":[],"nextCursor":null}
```
Ready sentetik item:
```json
{"assetId":"starter_truck","kind":"truck","catalogId":"test_truck","catalogVersion":"synthetic_fixture","status":"operational","assignmentId":null,"available":true}
```
Envelope `protocolVersion:1,serverTime:<response UTC>,result`. Şirketsiz durumda `hasCompany:false`; legacy/mapping/bozuk state hatadır. Read-only transaction; read işlemi create/repair/receipt yazmaz. Ekonomi yokken bulunan asset kayıtları kabul edilmez. Starter marker/iki sabit asset her sayfada ayrıca doğrulanır; hazır olma cursor'a bağlı değildir.

Mevcut CommandError/HttpsError code/messageKey kullanılır: UNAUTHENTICATED, INVALID_ARGUMENT, IDEMPOTENCY_CONFLICT, MIGRATION_REQUIRED, COMPANY_STATE_INVALID, FAILED_PRECONDITION, RETRY_LATER, INTERNAL. Domain preconditions generic FAILED_PRECONDITION; owner/private veri hata metnine taşınmaz. `errors.<lowercase_code>` Unity TR/EN/DE localization içindir; English sentence parsing yapılmaz.

## 7. Maliyet, sınırlar ve sonraki satın alma entegrasyonu

Company economy-ready validation 8 direct read; pending 7. İlk starter komutu receipt + company(8) + marker/truck/driver(3) + catalog(1) = 13 transaction read, 4 write; commit sonrası receipt +1 read. Ready replay 13 read/0 write. Assignment N=2..3 için 1 receipt + 8 foundation + 1 assignment + N asset + C unique catalog read; N+2 write. Release aynı; retry maliyeti eklenir.

Fleet read foundation(7/8) + 3 starter check + bounded page (en çok 21 doc) + distinct catalog okumaları. Bunlar SDK logical counts; boş query minimum read/index billing ayrıca olabilir. Varlıklar main company array'inde değil ayrı doc'larda büyür. Global lock/counter/scheduler/PubSub/index/sharding eklenmedi. Aynı truck/driver contention domain gereğidir; unrelated şirketler aynı global kaydı yazmaz. Gerçek Android/iOS ağ/cihaz veya production load testi yapılmadı.

**PROPOSED:** ileride purchase/hire/acquire handler server-approved price/catalog, progression eligibility, wallet/ledger, durable receipt ve asset uniqueness'i tek transaction'a bağlar. Fleet modülünde ikinci finans otoritesi yoktur. **UNKNOWN:** gerçek katalog specs, contract compatibility, migration ve rollout; **DEFERRED:** gerçek purchase/public assignment/upgrade, VIP ve production. 16–30 Ekim local-only dependency istisnası (recheck 23 Ekim) production veya untrusted CI yetkisi değildir.
