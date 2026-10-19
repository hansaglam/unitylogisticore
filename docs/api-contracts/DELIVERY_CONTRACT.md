# DELIVERY_CONTRACT — BACKEND-07

20 Ekim 2026. **IMPLEMENTED yalnız demo-logisticore-local. Production/real DEV/deploy/migration BLOCK.** Bütün süre/XP/para örnekleri sentetik; ürün default'u değildir.

## Authority ve composition

Client yalnız intent gönderir. Trusted Firebase Auth UID → readFoundation canonical mapping/owner/identity/economy/migration kontrolü → executeCommand receipt. Fleet veya finance için nested transaction açılmaz.

`prepareAssignment` bütün assignment/asset/catalog okumalarını ve doğrulamalarını yapar, ardından yalnız write queue eden `apply()` closure döndürür. Eski `writeAssignment` prepare+apply adapter'ıdır; eski command fingerprint/sonuç davranışı aynı. `planEconomyMutation` mevcut wallet/progression/claim/ledger yazımını tek ortak hesaplama/write planına taşır; `runEconomyCommand` bunu kullanır. Delivery için `prepareEconomyReward` canonical config/currency/scale/hash, bounded amount/XP ve permanent claim/ledger yokluğunu önce okur. Domain caller gerçek reward source'u doğrulamakla yükümlüdür; generic public grant yoktur.

Planlar yalnız hazırlandıkları transaction attempt içinde kullanılır, retry'de yeniden hazırlanır. Start/claim bütün planları hazırlar; ilk apply sonrası hiçbir Firestore read yapmaz. apply closure dış işlem/network/side effect içermez. Receipt mevcut executor tarafından aynı transaction'a eklenir. Başarısız precondition veya exception bütün queued writes'i geri alır.

## Delivery rules

Server lookup `v2DeliveryRules/{contract.terms.configVersion}`; client rules/version seçemez. Strict schema:
`schemaVersion:1,version,approval:synthetic_emulator_only,contractConfigHash,logisticsHash,economyHash,routes`.
Routes en çok 5 unique routeId+cargoId kaydı: `{routeId,cargoId,durationMs,xp}`. Duration positive safe integer, en çok 604800000 ms teknik guard (oyun süresi kararı değildir). XP nonnegative safe integer ve ekonomi maxXp sınırı. Contract/logistics/economy pin'leri accepted terms ile aynı olmalı; route+cargo tam match gerekir. Sıfır XP açıkça seçilebilir; varsayılan XP/süre yoktur. Rules version/hash delivery'ye pin edilir; içerik drift veya missing rules fail-closed. In-place edit yerine ayrı onaylı version/migration gerekir.

Clock callable'da Date.now; internal ServerClock injection yalnız test kontrolüdür. Start zamanı transaction attempt'in son read aşamasından sonra alınır; arrival=start+duration, üst sınır Firestore timestamp yılı9999 sonu ve safe arithmetic ile doğrulanır. Retry'nin yalnız başarılı attempt zamanı commit olur; aynı receipt replay bu zamanı korur. Client elapsed/start/arrival/position/reward alanları reddedilir. Unity map koordinatları duration veya yol mesafesi authority değildir.

## Canonical delivery

Path `v2Companies/{companyId}/deliveries/{deliveryId}`. ID SHA-256 `{companyId,contractId,domain:delivery_v1}`; contract başına bir delivery. Strict alanlar:

- `schemaVersion:1,deliveryId,companyId,contractId,status`
- `assignmentId`, `assets:[{assetId,kind}]` (tam truck+driver, optional trailer)
- `termsHash,routeId,cargoId`
- `startedAt,expectedArrivalAt` trusted server Timestamp
- `deliveryRulesVersion,deliveryRulesHash`
- `rewardState` active için unclaimed; completed için claimed
- Yalnız completed'da `completedAt` (arrival'dan önce olamaz).

Delivery'de ikinci balance/XP/reward amount otoritesi yoktur. Reward accepted contract snapshot'tan, XP pinned rules'dan; canonical finance/progression kendi belgelerinde kalır. Assignment ID mevcut SHA-256(company/source) helper'ı ile source=deliveryId'den türetilir; released ID tekrar kullanılamaz. Contract status accepted→in_progress→completed; terms/acceptedAt/pool acceptedCount hiç değiştirilmez. `failed` writer/penalty uygulanmadı.

## startDelivery

LOCAL callable us-central1/maxInstances1. Request:
```json
{"protocolVersion":1,"idempotencyKey":"synthetic_start_001","payload":{"contractId":"<id>","truckId":"starter_truck","driverId":"starter_driver"}}
```
Optional trailerId yalnız accepted candidate setinde varsa kabul edilir; bu faz alternatif equipment seçmez. Payload exactly contractId/truckId/driverId/[trailerId]; extra UID/company/time/price/state alanları hata.

Transaction: foundation → accepted contract + readPool integrity → deterministic delivery yokluğu → selected set birebir accepted candidates → actual current v2 capabilities/capacity/availability ve starter readiness → pinned delivery rules/economy → prepareAssignment reserve → trusted clock → apply assignment + create delivery + contract in_progress + receipt. Ekonomi yazısı yok. Bir truck veya driver aynı anda iki delivery'ye atanamaz; acceptance araç garantisi değildir. Expired original offer timestamp accepted işi iptal etmez.

Result `{deliveryId,contractId,status:active,startedAt,expectedArrivalAt}`; zarf protocolVersion/serverTime/result. Same key replay özgün result/commit zamanını tamamlanmadan sonra da korur; farklı key contract status veya deterministic delivery varlığı nedeniyle reddedilir. Replay resolver current company authorization/consistency kontrol eder, active-only şartı uygulamaz.

## claimDeliveryReward

Request:
```json
{"protocolVersion":1,"idempotencyKey":"synthetic_claim_001","payload":{"deliveryId":"<id>"}}
```
Tam deliveryId dışında alan yoktur. Transaction delivery+contract+pool+rules integrity, current company economy, starter readiness, exact active assignment/asset ownership, permanent claim ve ledger yokluğu, accepted reward currency/scale/economy hash, current safe wallet/XP bounds ve trusted now>=arrival kontrol eder.

Tek commit: delivery completed/claimed, contract completed, assignment released/releasedAt, tüm exact reserved assets assignmentId=null, canonical wallet credit, XP/derived level, append-only ledger, permanent reward claim ve receipt. Pool acceptedCount değişmez; non-offered count aynı kalır. Aynı/different key ikinci ödül veremez. Yanlış/stale assignment başka delivery'nin asset'ini boşaltamaz.

Source `delivery_{deliveryId}`; claim `.../claims/{source}`; ledger ID mevcut economy SHA-256(companyId + ':' + source), reason delivery_reward. Mevcut finance schema/configHash/rulesVersion, claim ve ledger şemaları korunur. No TTL/deletion. Claim result `{deliveryId,status:completed,completedAt,reward:<decimal string>,currency,scale,operationId,balance:<decimal string>,xp,level}`. Bu historical receipt current wallet state değildir.

Wallet veya XP tam yetkili reward'u alamıyorsa işlem tamamen reddedilir: capping, partial payout, release veya completed işareti yoktur. Recovery: authoritative state/rules incelemesi ve ayrı onaylı bounded state/config migration veya gerçek domain işlemi gerekir; client retry bounds hatasını kendi başına çözmez. Otomatik admin bypass/repair yoktur. Rules/catalog drift de ayrı operasyonel inceleme gerektirir.

## Lazy completion ve offline

Arrival yalnız derived readiness; backend timer/scheduler yoktur. Countdown sıfır olsa da active kalır, reward verilmez ve equipment reserved kalır. İlk başarılı claim settlement yapar. Offline Unity estimated progress gösterebilir; reconnect sonrası getMyDeliveries/claim trusted server koşullarını kontrol eder. Retry aynı idempotencyKey ile yapılır. **PROPOSED** bağımsız finalize gelecekte ayrı domain kararıdır; bu fazda uygulanmadı.

## getMyDeliveries

Auth LOCAL-only; request `{protocolVersion:1,limit?:1..20,after?:safeDocumentId}` (default20). UID/company/path/status-filter yok. Document-ID ascending order, limit+1 lookahead, nextCursor son dönen ID veya null; farklı sayfalar ayrı snapshot, concurrent değişimde refresh gerekebilir. Read hiçbir finalize/claim/reward/release/repair yapmaz. Active records için prepareAssignment release kontrolü okunur ama apply çağrılmaz. Tamamlanan asset'ler daha sonra başka delivery'ye atanabilir; history read onları available olmaya zorlamaz.

DTO item:
```json
{"deliveryId":"<sha256>","contractId":"<sha256>","status":"active","originCityId":"synthetic_city","destinationCityId":"synthetic_destination","routeId":"synthetic_route","cargoId":"synthetic_cargo","quantity":"2","unit":"synthetic_units","assets":[{"assetId":"starter_driver","kind":"driver"},{"assetId":"starter_truck","kind":"truck"}],"startedAt":"2026-10-20T12:00:00.000Z","expectedArrivalAt":"2026-10-20T12:00:01.000Z","arrivalReady":false,"completedAt":null,"reward":"6","currency":"TST","scale":2}
```
Envelope `{protocolVersion:1,serverTime,result:{items,nextCursor}}`. active reward expected, completed reward settled contract amount; XP/wallet/admin/entitlement alanları listede yok. Completed arrivalReady false (already settled). Ready türetimi read snapshot ve server clock'tandır; claim yeniden kontrol eder.

Error code/messageKey: yeni DELIVERY_NOT_ARRIVED/errors.delivery_not_arrived, DELIVERY_STATE_INVALID/errors.delivery_state_invalid; mevcut INVALID_ARGUMENT, UNAUTHENTICATED, CONTRACT_INELIGIBLE, MIGRATION_REQUIRED, COMPANY_STATE_INVALID, FAILED_PRECONDITION, IDEMPOTENCY_CONFLICT, RETRY_LATER, INTERNAL. Hata details sabittir; TR/EN/DE localization backend English cümlesine bağlı değildir. Public error'a path/stack/credential eklenmez.

Unity landscape map animation/portrait screens presentation'dır. Stable city/cargo IDs, server timestamps ve reward strings kullanılır. Per-second canonical position yazıları, client clock authorization veya money arithmetic yoktur. Gerçek Android/iOS/device network testi NOT RUN.

## Maliyet ve future scope

P=pool contract sayısı<=5, A=assets2..3, C=distinct catalog<=3. Start yaklaşık 18+P+2A+C direct reads (receipt/foundation, contract/pool, existence, eligibility/starter, rules, assignment); A+4 writes (assets, assignment, delivery, contract, receipt), commit sonrası receipt +1 read. Claim yaklaşık 18+P+A+C read (delivery/contract/pool/rules, starter, assignment, claims/ledger), A+8 write (assets, assignment, wallet/progression, ledger/claim, delivery/contract, receipt), +1 receipt read. Read list 7/8 foundation + max21 query docs + page başına bounded delivery/contract/pool/rules ve active assignment lookups; mevcut implementation aynı pool'u birden çok satır için tekrar okuyabilir. Replay 9 read/0 write. Retry/index/empty query billing ek olabilir; actual bill/performance ölçümü değildir.

IMPLEMENTED: atomic lazy completion, server duration, permanent reward uniqueness ve common two-phase primitives. PROPOSED: bağımsız finalize, shared per-page pool cache, ürün onaylı recovery/migration. DEFERRED/UNKNOWN: real duration/fuel/wages/maintenance/tolls/penalties/failures/XP/balance, VIP ve production rollout. No scheduler/cron/PubSub/Cloud Tasks/new DB/dependency. 16–30 Ekim trusted local exception, mandatory23 Ekim reassessment; general security HOLD, production/DEV/deploy/migration/untrusted CI BLOCK. BACKEND-08B başlamaz.
