# COMMERCE_CONTRACT — BACKEND-08B

23 Ekim 2026. **IMPLEMENTED: synthetic-emulator-only**. Project demo-logisticore-local; trusted Auth, canonical company, fail-closed guards ve default-deny Rules. Gerçek DEV/PROD/deploy/migration BLOCK. Bütün sayısal örnekler sentetiktir.

## Authority ve konfigürasyon

Client yalnız offerId intent gönderir. Fiyat, owner, level, stats, target, config ve zaman server tarafından belirlenir. Public offer publisher, refresh/generator, generic spend/grant endpoint yoktur. Fixture yayını yalnız kontrollü emulator test setup'ındadır; read işleminden teklif veya ücretsiz asset üretilmez.

`v2CommerceCatalog/{version}` strict schema:
`schemaVersion:1,version,approval:synthetic_emulator_only,economyVersion,economyHash,rulesVersion,logisticsVersion,logisticsHash,items`.

Mevcut economy config/hash/rulesVersion ve logistics **v2** version/hash ile tam bağ gerekir. Items 1..32 unique ID:
`{id,kind,catalogId,price,requiredLevel,expectedLevel}`.
Kind yalnız buyVehicle/hireDriver/acquireTrailer/openWarehouse/upgradeWarehouse. Price positive safe integer minor units, economy.maxBalance üst sınırı; ücretsiz offer yoktur. RequiredLevel 1..configured threshold count. Asset kind catalog tarafından doğrulanır; driver qualification ve trailer capabilities/capacity mevcut v2 tanımından gelir, client stats yoktur. Asset expectedLevel=0. Warehouse open expectedLevel=0 ve catalog level1; upgrade expectedLevel≥1 ve target definition level=expected+1. Bunlar sentetik versiyonun açık koşullarıdır; production price/capacity/qualification/unlock default'u yoktur.

Immutable katalog in-place değiştirilmez. Hash drift/missing/unapproved config fail-closed. Yeni onaylı version ayrı document olarak yayınlanmalıdır; bu faz publisher/migration uygulamaz.

## Şirket teklifi

Path `v2Companies/{companyId}/acquisitionOffers/{offerId}`, strict fields:
`schemaVersion:1,offerId,companyId,catalogVersion,catalogHash,itemId,kind,price,currency,scale,requiredLevel,expectedLevel,targetId,sourceId,createdAt,expiresAt,status`.
Yalnız consumed durumunda ayrıca `consumedAt,operationId`. Available'da bu alanlar yasaktır. Price/kind/requirements tam pinned item ile, currency/scale canonical economy ile eşleşir. Timestamps server-owned Firestore Timestamp, created<expires, azami7 gün teknik TTL bound (oyun teklif süresi kararı değildir). İşlem anında `createdAt <= trustedNow < expiresAt`; exact expiry reddedilir. ConsumedAt bu aralık içinde olmalı. Expired available teklif silinmez/yenilenmez.

Asset target ID SHA-256 canonical `{domain:commerce_v1,companyId,offerId}`. Model ID inventory ID değildir; iki ayrı teklif aynı modelden iki ayrı asset sağlar. Source `commerce_{hash}`; aynı offerId config/item değiştirerek tekrar kullanılamaz. Warehouse target mevcut SHA-256 `{cityId,type}` ve company collection scope. Bu local policy nihai şehir başına depo ürün kararı değildir.

## Beş mutation API

LOCAL authenticated callables, us-central1/maxInstances1:
`buyVehicle`, `hireDriver`, `acquireTrailer`, `openWarehouse`, `upgradeWarehouse`.

Aynı request biçimi:
```json
{"protocolVersion":1,"idempotencyKey":"synthetic_purchase_001","payload":{"offerId":"synthetic_offer_001"}}
```
Payload exactly offerId; extra price/owner/UID/company/level/time/config/target reddedilir. Endpoint kind ile teklif kind aynı olmalıdır. Protocol/idempotency formatı COMMAND_CONTRACT ile aynıdır.

Result: `{offerId,kind,targetId,status:consumed,price:<decimal minor-unit string>,currency,scale,balance:<decimal minor-unit string>,operationId}`. Zarf protocolVersion/serverTime/result. Command serverTime receipt committedAt'ten gelir ve replay'de **aynıdır**; current server clock olarak kullanılmaz.

Resolver: readFoundation UID/company/identity/economy/legacy/migration + starter integrity + offer/catalog/target + permanent claim/ledger. Eligible fresh apply: available, trusted expiry, required level, target prerequisites, yeterli wallet. Bütün reads ve bounded calculation bitince tek write phase çalışır:

1. Asset create veya warehouse create/update.
2. Ortak planEconomyMutation ile exact signed debit, canonical wallet/progression, ledger ve claim.
3. Offer consumed/consumedAt/operationId.
4. executeCommand durable receipt.

Nested transaction veya write sonrası read yok. Retry planı aynı attempt içinde yeniden hesaplar; external side effect yok. XP delta0, derived level aynı; common economy writer progression.updatedAt'i yeniler fakat XP ödülü vermez. Claim schema mevcut source/operation/config/committedAt; ledger reason commerce_purchase, delta=-price, balanceAfter ve currency/scale. Operation ID mevcut SHA-256(companyId+':'+source). TTL/pruning/repair yok.

## Asset ve warehouse invariants

Truck/driver/trailer mevcut asset schema1 ile create-only: canonical owner, logistics version/hash/catalogId, operational, assignmentId=null, acquiredAt serverTimestamp. Starter asset/marker'a yazılmaz. Existing target overwrite edilmez. Aktif delivery/assignment satın alma ile değiştirilmez. Yeni asset'ler mevcut validateAsset/eligibility/prepareAssignment/start/claim tarafından doğrudan kullanılabilir.

Warehouse open existing strict schema1, level1, usedCapacity0; ücret zorunlu ve duplicate city/type reddedilir. Upgrade stable ID/owner/city/type/status/acquiredAt/usedCapacity korur; yalnız catalogId/version/hash, level ve aynı capacityModelId güncellenir. Current warehouse eski immutable katalogla doğrulanır, sonra yeni definition için expected old level, one-step transition, operational status, **capacity shrink olmaması** ve used≤newMax kontrol edilir. Aynı katalogdaki farklı tanım veya ayrı immutable katalog version kullanılabilir; mevcut şema extension/migration gerekmez. Next catalog aynı city/type ID'ye bağlanmalıdır. Stale offer/skip/model change/corruption fail-closed.

## Replay, hata ve recovery

Same key+intent original result ve committed serverTime; different valid intent IDEMPOTENCY_CONFLICT. Different key+same consumed offer ikinci debit/grant yapamaz. Distinct offers bağımsızdır ama ortak wallet transaction contention ile korunur; overspend/negative balance yok.

Bu fazın resolver'ı replay'de de consumed teklifin canonical target ve permanent claim/ledger bütünlüğünü doğrular. Asset atanmış/out_of_service olabilir; historical purchase bunu değiştirmez. Warehouse legitimate sonraki yükseltmelerde replay geçerli kalır. Silinmiş/bozuk asset, kayıp claim/ledger veya config drift halinde historical replay de fail-closed olur; receipt repair/grant yoktur. Kayıp post-commit response için aynı key ile tekrar çağırın. Corrupt kayıtlar ayrı yetkili recovery gerektirir, client yeni key ile bypass edemez.

Yeni structured error codes: OFFER_CONSUMED, OFFER_NOT_ACTIVE, ACQUISITION_INELIGIBLE, WAREHOUSE_UPGRADE_INVALID, ACQUISITION_TARGET_EXISTS, INSUFFICIENT_FUNDS; mevcut OFFER_EXPIRED, FAILED_PRECONDITION, MIGRATION_REQUIRED, INVALID_ARGUMENT, UNAUTHENTICATED, IDEMPOTENCY_CONFLICT, RETRY_LATER, INTERNAL korunur. `errors.<lowercase_code>` TR/EN/DE localization key; path/stack/admin detay yok.

## getMyAcquisitionOffers

Request `{protocolVersion:1,limit?:1..20,after?:safeDocumentId}`; default20, document-ID ascending, limit+1 lookahead. Arbitrary UID/company/status query yok. Economy ready zorunlu; sağlam ancak starter pending company için teklifler ineligible, boş collection items[]. Max20 DTO:
`offerId,kind,itemId,catalogId,targetId,price,currency,scale,requiredLevel,expectedLevel,cityId,nextLevel,catalogVersion,logisticsVersion,rulesVersion,createdAt,expiresAt,status,expired,eligible,ineligibilityCode`.

Price decimal minor-unit string. City/nextLevel nonwarehouse için null. Status consumed ise eligible=false. Eligibility sunucu saati, wallet/level/starter ve warehouse/target koşullarından türetilir; satın alma garantisi değildir, mutation yeniden doğrular. Corrupt state satırı gizlemek yerine request'i reddeder. Ledger/balance/private identity read DTO'ya çıkmaz. Envelope taze serverTime ve result `{items,nextCursor}`. Sayfalar ayrı snapshot; refresh gerekebilir. Read-only transaction offer oluşturmaz/tüketmez, para/asset/warehouse yazmaz.

Unity mevcut fleet/warehouse/economy/contracts/deliveries API'lerini kullanmaya devam eder. UI fiyatı server minor-unit string+scale ile gösterir; submit yalnız offerId/key. Purchase sonucu historical balance'tır; current state tekrar okunmalıdır. Map animasyonu/canonical pozisyon yazımı bu fazda yoktur. Android/iOS integration testi yapılmadı.

## Maliyet ve kapsam

Fresh acquisition yaklaşık19 direct transaction read (receipt1, foundation8, starter3+catalog1, offer1, commerce1, logistics1, target1, claim+ledger2); current target farklı catalog'daysa ek1 read. Common starter/catalog lookup iki ayrı cache kullanabilir. Her beş mutation7 write (target, finance, progression, ledger, claim, offer, receipt), commit sonrası receipt+1 read. Consumed replay yaklaşık19..20 read/0 write; config/target integrity doğrulaması maliyetidir.

List foundation8 +starter4 +en çok21 query doc; satır başına4 point read (offer reread,target,claim,ledger) +distinct commerce/logistics/current-target catalog reads. Catalog cache yalnız transaction attempt içindedir. Query document reread bounded duplication'dır; future optimize edilebilir. Retry/empty query/index billing ek olabilir; bu gerçek billing benchmark değildir. Global counter/unbounded scan/worker/scheduler/dependency yok.

IMPLEMENTED: synthetic vertical slice, atomic spending, strict pinned offers, compatible upgrades. PROPOSED: future authenticated publisher/admin recovery ayrı review. DEFERRED: satış/marketplace/trading/transfer/loan/payroll/fuel/repair/IAP/VIP/dynamic repricing/production catalog. UNKNOWN: gerçek LGC balance/prices/salary/cities/capacity/progression unlocks. 15000 LGC production onaylı değildir. Security ayrıntısı BACKEND-08B_SECURITY_REASSESSMENT.md; full audit HOLD, yalnız mevcut30 Ekim'e kadar local istisna. BACKEND-09 başlamaz.
