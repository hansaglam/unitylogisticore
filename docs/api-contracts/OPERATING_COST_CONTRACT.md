# OPERATING_COST_CONTRACT — BACKEND-09A

23 Ekim 2026. **IMPLEMENTED yalnız demo-logisticore-local / synthetic_emulator_only. GENERAL SECURITY HOLD, PRODUCTION BLOCK.** Bu faz gerçek yol yakıt tüketimi veya maaş takvimi değildir. Sayılar yalnız **UNAPPROVED TEST CANDIDATES**.

## Ürün yaklaşımı

Yeni settlement-v2 işler için NET SETTLEMENT AT SUCCESSFUL DELIVERY CLAIM. Acceptance/start sırasında wallet/XP/finance claim değişmez; upfront fuel/toll, escrow, ikinci wallet, günlük maaş, zamanla maliyet tahakkuku yoktur. Başarılı claim yalnız gross−totalCosts tutarını canonical wallet'a uygular. Yeni tekliflerde gross>totalCosts zorunlu; eşit/zararlı herhangi bir template bütün pool hazırlığını yazı yapmadan reddeder. Silent omit veya repricing yoktur. Bu geçici sentetik politika kalıcı oyunda zararın yasak olduğu anlamına gelmez.

## Katalog

`v2OperatingCostCatalog/{version}` strict schema1:

- schemaVersion:1, version, approval:synthetic_emulator_only
- economyVersion, economyHash; logisticsVersion, logisticsHash
- policy:synthetic_net_claim_v1
- currency, scale; canonical EconomyConfig ile aynı
- maxCost: 0..economy.maxBalance safe integer
- entries: 1..5; unique templateId
- Her entry: templateId, routeId, cargoId, costs:{fuelCost,driverCost,maintenanceCost,tollCost}

Kimlikler mevcut versionId sınırındadır (1..64 ASCII safe ID). Logistics schema2 gerekir. Her kategori nonnegative safe integer<=maxCost; toplam BigInt ile hesaplanır ve maxCost'u aşamaz. Float/NaN/unsafe integer/negative/extra/missing fields reddedilir. Katalog para birimi/scale/hash/version/approval doğrulanır. Uygulanacak template+route+cargo üçlüsü tam eşleşmelidir. Gerçek km, Unity koordinatı veya araç specs'inden fiyat uydurulmaz; tutarlar explicit fixture girdisidir.

Hash bağımlılığı: contract catalog → cost catalog → economy/logistics. Cost catalog contract catalog hash'i taşımaz; circular hash yoktur. Config digest canonical JSON'dur; version içerikleri immutable kabul edilir. Katalog yayım callable'ı veya üretim default'u yoktur.

## Explicit eski/yeni sözleşme

`v2ContractCatalog` schema1 aynen kalır. Yeni schema2 yalnız costCatalogVersion ve costCatalogHash alanlarını ekler. Yeni canonical contract ve pool schemaVersion2 olur; v1 pool yalnız v1 contract, v2 pool yalnız v2 contract içerir. Pool termsHashes listesi, acceptedCount, max5 slot, epoch ve ID formülleri korunur. Version değiştirerek aynı company/epoch'ta ikinci pool yaratılmaz.

V1 terms unchanged. V2 terms mevcut alanlara **zorunlu** settlement objesi ekler:

```text
settlementVersion:2
costCatalogVersion, costCatalogHash, policy
 grossReward, fuelCost, driverCost, maintenanceCost, tollCost
 totalOperatingCosts, netReward
```

Gross, mevcut terms.reward ile aynıdır. Currency/scale/economy/logistics/config version+hash mevcut terms içinde pinlidir. Hash bütün terms'i, settlement dahil, kapsar. Missing settlement v1 gibi yorumlanmaz; schema2 strict validation reddeder. Cost snapshot başka bir bakiye otoritesi değildir.

Generation catalog ve quote'u doğrular; accept aynı config/catalog üzerinden tekrar hesaplayıp tüm terms digest'ini karşılaştırır. Start, claim ve ilgili list reads pinned cost catalog **aynı version** kaydını doğrular; latest pointer okunmaz. Başka version eklemek kabul edilmiş işi değiştirmez. Pinli version yerinde değişirse fail-closed olur, yeni fiyat uygulanmaz. Drift old v1'e maliyet eklemez. Hash integrity trusted backend boundary içindedir; kötü niyetli Admin bütün kayıtları yeniden yazarsa hash imza değildir.

## Delivery ve DTO

Yeni delivery schema2, mevcut alanlara aynı settlement snapshot'ını ekler. Delivery termsHash ve snapshot canonical contract ile eşit olmak zorundadır. ID domain/formülü değişmez. Acceptance/start intent unchanged; start result unchanged. Start equipment reservation ve receipt dışında ekonomik write üretmez. Accepted offer expiry geçmiş olsa bile delivery arrival/claim koşulları kendi kurallarıyla geçerlidir.

V2 accept/claim result, getMyContracts ve getMyDeliveries item'ları ek `settlement` DTO'su içerir: settlementVersion, policy, grossReward, dört kategori, totalOperatingCosts, netReward, currency, scale. Bütün para miktarları decimal minor-unit string. Mevcut `reward` alanı yeniden adlandırılmaz; **gross reward** anlamını korur. V2 gerçek wallet artışı settlement.netReward'dür. V1 result/item alanları ve receipt fingerprint'leri değişmez. getMyEconomyState canonical mevcut balance sunmaya devam eder; yeni breakdown/history alanı eklenmedi.

## Sınırlar ve maliyet

Generation/accept için v2 ek1 cost catalog point read; start ve active claim/read için ek1 cost catalog+1 pinned logistics read. Bounded catalog5 entries, pool5, page20 korunur. Mevcut helper'ların katalog okumaları per-page paylaşılmadığından bu ek okumalar listede her item için tekrarlanabilir. Global scan, timer, Cloud Tasks/PubSub/cron/dependency yok. Estimate transaction retry/index maliyeti içermez.

## Ertelenenler

Gerçek prices, XP/route cost, dinamik yakıt, driver salary, failed/cancelled delivery cost/refund, recovery, upfront/escrow, real DEV/production config ve migration yok. Positive-net ve net-at-claim başlangıç likidite engelini azaltır; wallet/XP üst sınırı doluysa atomik claim yine reddedilir. BANK-01 repayment bu fazda yok; [SETTLEMENT_CONTRACT](SETTLEMENT_CONTRACT.md).
