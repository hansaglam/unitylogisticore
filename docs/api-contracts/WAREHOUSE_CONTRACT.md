# WAREHOUSE_CONTRACT — BACKEND-08A

20 Ekim 2026. **IMPLEMENTED: yalnız sentetik LOCAL foundation. Production BLOCK.** Starter package warehouse vermez. Gerçek city catalog, fiyat ve kapasite default'u yoktur.

## Canonical model ve katalog

Path `v2Companies/{companyId}/warehouses/{warehouseId}`; tek şirket mülkiyeti trusted UID→canonical mapping ile çözülür. Exact schema:
`schemaVersion:1,warehouseId,companyId,catalogId,catalogVersion,catalogHash,cityId,type,level,status,capacityModelId,usedCapacity,acquiredAt`.

Server `v2LogisticsCatalog/{version}` warehouse definition: `{id,cityId,type,level,capacityModelId,capacityUnits}`. City aynı version'ın server-controlled `cities` listesinde olmalı. Level safe integer 1..1000 (teknik guard, oyun maximum level kararı değildir). Capacity positive safe integer, usedCapacity 0..capacityUnits. Unknown definition/city, yanlış owner, hash drift, schema/level/capacity mismatch fail-closed; repair yoktur. Operational status `operational|out_of_service`, acquiredAt server timestamp. SHA-256 canonical catalog hash pin edilir. Synthetic approval dışında config kabul edilmez.

## Tekillik ve reusable validators

Bu foundation policy'si: company path altında SHA-256 `{cityId,type}` ID. Aynı company/city/type yalnız bir warehouse; farklı type ayrı ID. Bu **nihai ürünün şehir başına bir depo izni olduğu varsayımı değildir**; future multi-site policy ayrıca şema/migration review gerektirir. Catalog version veya level değişikliği aynı city/type için ikinci kayıt yaratamaz.

`validateWarehouse` canonical owner/reference/status/capacity checks; `warehouseId` deterministic uniqueness; `requireCapacity(used,requested,max)` exact safe integer ve taşma olmadan kalan kapasite doğrular. Kapasiteyi client belirlemez. Bu faz inventory/cargo reservation yazarı uygulamaz; future caller capacity kontrolüyle ilgili state update'i aynı transaction'a koymalıdır.

Internal `createSyntheticWarehouse` ve transaction-bound `createWarehouseInTransaction` yalnız explicit sentetik test tanımıyla foundation kanıtı içindir. Company readiness/legacy/migration mevcut executor üzerinden kontrol edilir. Create-only, existing warehouse overwrite edilmez; receipt ile aynı transaction, same-key replay, different-key duplicate reject. Otomatik çağrı, public purchase/upgrade/grant callable yoktur. Wallet/XP'ye dokunmaz. Gerçek warehouse acquisition kapalıdır.

## getMyWarehouseState

LOCAL authenticated callable, `us-central1`, maxInstances 1. Request yalnız `protocolVersion:1` ve optional `limit` 1..20 (default 20), optional `after` 1..64 güvenli document ID. Şirket veya UID parametresi yoktur. Canonical company warehouse collection document-ID order, limit+1; cursor farklı şirkete yol açmaz. Sayfalar ayrı snapshot; client concurrent değişiklikte refresh yapmalıdır.

Sentetik response:
```json
{"protocolVersion":1,"serverTime":"2026-10-20T12:00:00.000Z","result":{"hasCompany":true,"economyReady":true,"items":[{"warehouseId":"<sha256>","catalogId":"test_warehouse","catalogVersion":"synthetic_fixture","cityId":"synthetic_city","type":"synthetic_depot","level":1,"status":"operational","capacityModelId":"synthetic_slots","capacityUnits":"10","usedCapacity":"0"}],"nextCursor":null}}
```
Sayılar test örneğidir. Hiç depo yoksa items []; pending oyuncuya depo uydurulmaz. Read-only transaction; config/hash/owner validation; auth/admin/ledger/entitlement sızıntısı veya read repair yoktur. Starter readiness yalnız fleet endpoint'indedir; economyReady warehouse sahipliği veya gameplay readiness anlamına gelmez.

Error codes/messageKeys FLEET_CONTRACT ile aynıdır; TR/EN/DE client sabit codes ile yerelleştirir, backend cümlelerine bağımlı olmaz.

## Maliyet ve future purchase

Create: foundation 8 + receipt 1 + catalog 1 + warehouse 1 = 11 transaction read; warehouse+receipt 2 write; post-commit receipt +1 read. Replay current foundation + receipt = 9 read/0 write; tarihsel result canlı warehouse snapshot değildir. Read foundation(7/8) + en çok 21 query doc + en çok 20 distinct catalog read. Retry/index/empty-query maliyetleri eklenebilir. Broad scans/listeners/global locks yoktur.

**PROPOSED:** future opening/upgrading command server catalog/price, progression, owner, canonical wallet, ledger, durable receipt, uniqueness ve capacity kontrollerini tek transaction'da kullanmalı. Bütün domain reads yazılardan önce bitmeli. Bu helper'ın ardından yeni Firestore read eklenmemeli; future orchestrator önce tüm finans/domain reads'i toplamalı. İkinci para otoritesi veya client-supplied price yoktur.

**DEFERRED/UNKNOWN:** gerçek cities/types/capacity/price/upgrade policy, multi-site uniqueness, storage goods, actual economy/migration/rollout. No real user data; no deploy. 16–30 Ekim trusted local exception, 23 Ekim zorunlu reassessment; genel audit HOLD, production BLOCK.

## BACKEND-08B — ücretli açılış ve yükseltme

23 Ekim 2026: LOCAL openWarehouse/upgradeWarehouse artık explicit sentetik offer ile exact canonical debit uygular. Önceki internal ücretsiz fixture helper public değildir ve değişmedi. Schema1/company+city+type ID korunur. Upgrade eski definition'ı doğrular; yeni immutable definition/version için tek level artışı, aynı capacity model, nondecreasing maximum ve preserved usedCapacity/acquiredAt zorunludur. No migration/in-place catalog edit. [COMMERCE_CONTRACT](COMMERCE_CONTRACT.md).
