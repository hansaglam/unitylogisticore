# CONTRACT_CONTRACT — BACKEND-06

20 Ekim 2026. IMPLEMENTED yalnız `demo-logisticore-local`, production/deployment/migration BLOCK. Bütün sayısal örnekler sentetik; ürün fiyatı veya route doğrulama kanıtı değildir.

## Authority ve version ilişkisi

Cities için tek kaynak `v2LogisticsCatalog/{version}.cities`. BACKEND-06 logistics schemaVersion 2 ekler; schemaVersion 1 mevcut kayıt/hash davranışı korunur, contract eligibility için yetersizdir. V2 asset definition `{id,kind,capabilities,capacity}` içerir. Capabilities en çok 16 unique safe ID; truck/trailer capacity `{unit,amount}` positive safe integer; driver capacity null. Gerçek specs, maaş, fuel veya kapasite atanmaz. Eski v1 kataloğu yerinde upgrade edilmez; yeni version ID ve hash gerekir.

`v2ContractCatalog/{version}` exact schema: `schemaVersion:1,version,approval:synthetic_emulator_only,logisticsVersion,logisticsHash,economyVersion,economyHash,epochDurationMs,offerTtlMs,maxOffers,maxAcceptedPerEpoch,routes,cargo,templates`. Logistics ve economy hash/version pin'leri doğrulanır. Route/cargo/template listeleri 1..5; pool en çok 5. Epoch duration 1..604800000 ms teknik sınır, TTL 1..duration; acceptance quota 1..maxOffers. Bunlar ürün default'u değildir.

Route `{id,originCityId,destinationCityId,distance,unit,verification:synthetic_fixture}` aynı logistics city authority içindeki farklı şehirleri bağlar. Distance positive safe integer, sentetik unit. Cargo `{id,unit,capability}`. Template `{id,routeId,cargoId,quantity,rewardPerUnit,minLevel,truckCapability,driverCapability,trailerCapability}`; trailerCapability null trailer-free demektir. Reward = quantity × rewardPerUnit, safe integer ve economy.maxBalance içinde; wire decimal string. Maliyet, delivery duration/deadline, toll ve dynamic market uygulanmadı.

Unity remote map dalındaki izmir/istanbul/ankara/bursa/antalya/adana/diyarbakir/trabzon işaretleri yalnız sunumdur; koordinatlardan mesafe veya ekonomi üretilmez. Backend canonical IDs ile route verisini belirler. Gerçek route verification/cargo compatibility ürün verisi UNKNOWN.

## Offer generation ve bounded pool

Internal `generateContractOffers` trusted server plan `{configVersion,epoch,assets}` ve strict boş payload kullanır; public generator/refresh endpoint yoktur. Epoch `floor(serverNow/epochDurationMs)`; caller arbitrary yeni epoch ile mevcut zamanı bypass edemez. Geçerli pencere epoch başlangıcı..başlangıç+TTL, üst sınır hariç. Aynı company/epoch için marker path sabittir: `v2Companies/{companyId}/contractPools/epoch_{epoch}`. ID config version'dan bağımsızdır; aynı epoch'ta config değişikliği ikinci batch yaratmaz. Epoch duration policy değişimi ayrı rollout gerektirir; internal scheduler/activation yoktur.

Template sırası deterministik slot planıdır; randomness yok. Contract ID SHA-256 `{companyId,epoch,slot}`. Source identity templateId+epoch+slot, future rotation/rarity için kullanılabilir; anti-repetition history/LiveOps DEFERRED. Aynı key replay stable; başka key marker varsa reddedilir. Pool marker ve tüm contracts + receipt atomik create edilir. Unexpected existing slot veya partial/corrupt existing pool fail-closed; repair/overwrite yoktur.

Marker exact fields: `schemaVersion,companyId,epoch,epochDurationMs,offerTtlMs,configVersion,configHash,termsHashes,acceptedCount,maxAcceptedPerEpoch,createdAt`. Hash listesi 1..5; slotların tamamı snapshot olarak doğrulanır; acceptedCount non-offered kayıt sayısıyla tutarlı olmalı. Kota company/epoch total accept sayısıdır, active-delivery veya global counter değildir; gelecekte completion quota iadesi yoktur. Marker/receipts/accepted contracts için TTL yoktur.

## Canonical contract ve lifecycle

`v2Companies/{companyId}/contracts/{contractId}` exact schema: `schemaVersion:1,companyId,contractId,status,epoch,slot,terms,termsHash,createdAt,expiresAt`; offered dışı durumlarda ek `acceptedAt` zorunlu.

Terms snapshot: `templateId,route,cargo,quantity,reward,currency,scale,minLevel,requirements:{truck,driver,trailer},candidateAssets:[{assetId,kind}],configVersion,configHash,logisticsVersion,logisticsHash,economyVersion,economyHash`. Immutable termsHash ve pool hash listesi bütünlüğü kontrol edilir. Hash'ler Admin tehdidine karşı imza değildir; trusted backend + default-deny client boundary içindedir.

Status: offered → accepted IMPLEMENTED; in_progress/completed/failed tanınır ama geçiş writer'ı BACKEND-07 için DEFERRED. Gelecek writer acceptedAt ve terms'i korumalı, yeni metadata için schema review yapmalı. Offer expiration derived: yalnız offered ve serverNow >= expiresAt. Accepted işler original offer TTL'den etkilenmez; delivery deadline ayrı gelecekteki modeldir. Scheduler gerekmez.

Creation/expiration/acceptedAt trusted backend clock ile Timestamp olarak yazılır; client clock alanı yoktur. Internal injectable ServerClock yalnız local test kontrolü; callable her zaman Date.now kullanır. Receipt serverTime executor'ın resolved Firestore commit timestamp'idir; replay ilk zamanı korur. Test clock enjeksiyonu production cihaz testi değildir.

## Eligibility ve acceptance

Generation bütün template'ler için current identity, company ownership/mapping, migration, economy progression, starter readiness, server-selected canonical candidate seti (truck+driver, optional trailer), catalog v2 capabilities, operational ve available durum, cargo unit/capacity kontrol eder. Her truck her cargo'yu taşıyamaz. Trailer-free fixture truck cargo capability/capacity + driver qualification ile kanıtlanır.

Acceptance aynı bound candidate setini ve güncel config pin'lerini tekrar doğrular; bütün fleet'i taramaz. Daha iyi başka araç bulmaya çalışan otomatik seçim bu fazda yoktur. Candidate unavailable ise başka araç olsa bile ret mümkündür. **Eligibility reservation değildir:** acceptance asset.assignmentId yazmaz; accepted response equipmentReserved false. BACKEND-07 delivery start güncel physical assignment/ownership/availability/capacity kurallarını yeniden doğrulayıp atomik reserve etmelidir.

`acceptContract` request:
```json
{"protocolVersion":1,"idempotencyKey":"synthetic_accept_001","payload":{"contractId":"<safe-id>"}}
```
Only contractId; UID/company/reward/route/cargo/status/time ek alanları reddedilir. Transaction current authorization → contract/pool integrity → offered → config/terms match → eligibility → trusted expiration → quota → contract accepted + pool count + receipt. No reads after writes. Wallet/XP/ledger/claim/asset reservation değişmez; delivery başlamaz.

Same key/same intent first committed result/time; same key/different contract IDEMPOTENCY_CONFLICT; farklı key duplicate CONTRACT_NOT_OFFERED. Historical replay sadece güncel company authorization/consistency resolver'ına bağlıdır; offered/expiry/asset availability predicates yeniden uygulanmaz. Meşru in_progress geçişi replay'i bozmaz. Read API snapshot terms'i döndürür; current config update accepted reward'u reprice etmez. Yeni offered kabulü config drift'te fail-closed.

Sentetik accept result:
```json
{"contractId":"<safe-id>","status":"accepted","acceptedAt":"2026-10-20T12:00:00.000Z","reward":"6","currency":"TST","scale":2,"equipmentReserved":false}
```

## getMyContracts

Auth zorunlu, LOCAL-only `us-central1`, maxInstances 1. Request `{protocolVersion:1,limit?:1..20,after?:safeDocumentId}`. Default 20. Status filter bu sürümde kabul edilmez: composite index eklenmedi; Unity bounded sayfaları filtreleyebilir, tüm database scan yapılmaz. Order document ID ascending; limit+1 lookahead; nextCursor son dönen ID veya null. Her sayfa ayrı snapshot, eşzamanlı değişiklikte refresh gerekir. Cursor başka company path'i açamaz.

Envelope `{protocolVersion:1,serverTime,result:{items,nextCursor}}`. Item:
```json
{"contractId":"<sha256>","status":"offered","expired":false,"originCityId":"synthetic_city","destinationCityId":"synthetic_destination","routeId":"synthetic_route","cargoId":"synthetic_cargo","quantity":"2","unit":"synthetic_units","reward":"6","currency":"TST","scale":2,"requirements":{"truck":"synthetic_vehicle","driver":"synthetic_license","trailer":null},"minLevel":1,"configVersion":"synthetic_fixture","logisticsVersion":"synthetic_fixture","economyVersion":"synthetic_fixture","expiresAt":"2026-10-20T12:10:00.000Z","acceptedAt":null}
```
Private candidate asset IDs/hash/entitlement/admin kayıtları DTO'da yoktur. Pending company boş contract listesi okuyabilir; eksik identity/company/migration veya corrupt state hata. Read-only transaction; generation/repair/write yoktur. Bounded page'deki her distinct pool en çok 5 point read ile integrity doğrular.

Structured codes: OFFER_EXPIRED, CONTRACT_NOT_OFFERED, CONTRACT_INELIGIBLE (failed-precondition), CONTRACT_QUOTA_REACHED (resource-exhausted), mevcut UNAUTHENTICATED, INVALID_ARGUMENT, IDEMPOTENCY_CONFLICT, MIGRATION_REQUIRED, COMPANY_STATE_INVALID, FAILED_PRECONDITION, RETRY_LATER, INTERNAL. Message keys `errors.<lowercase_code>`. Unity TR/EN/DE localized city/cargo/requirement adlarını stable ID ile çözer; backend English cümleleri business logic değildir. Map landscape/diğer portrait yalnız UI kararıdır.

## Operasyon maliyeti ve kararlar

Sabit maximum pool 5, selection 3, page 20. Generation mevcut implementation temel 9 receipt/foundation + pool marker checks + config/logistics + N slot read; her template için starter(3), catalog ve candidate(2..3) tekrar doğrulaması vardır. İlk marker yokluğunda ayrıca receipt+5 olası slot corruption check okunur. N+2 write (N contracts+marker+receipt), commit sonrası receipt +1 read. Acceptance 9 foundation/receipt + contract + pool(1+N) + config/logistics + starter/candidate checks; 3 write + post-commit read. Replay accept 9 read/0 write. Read foundation(7/8) + max21 query docs + en çok20 distinct pool×(1+5) point reads; indexes/empty-query minimum/retry ek maliyettir. Bu worst-case sınır üretim fiyat garantisi değildir. Gelecek ölçümle per-attempt shared context optimize edilebilir.

Global locks/counters, cron, PubSub, full scans, new dependencies veya external services yoktur. Aynı pool quota belgesinde bounded company/epoch contention vardır. Gerçek payout/start balance/currency, route data, cargo requirements, quotas, generation interval, delivery duration/reward balance ve migration rollout UNKNOWN. Real activation/payout/penalty/completion/claim, VIP ve BACKEND-07 DEFERRED. General security HOLD; 16–30 Ekim local exception, zorunlu reassessment 23 Ekim; production/deploy/migration BLOCK.

## BACKEND-07 lifecycle eki

accepted→in_progress→completed geçişleri artık delivery start/claim ile atomik uygulanır; contract terms, acceptedAt ve pool acceptedCount korunur. Historical accept replay değişmez. Delivery süreleri original offer expiry'den bağımsızdır. Bkz. [DELIVERY_CONTRACT](DELIVERY_CONTRACT.md). Failed/penalty ve real product activation hâlâ deferred.
