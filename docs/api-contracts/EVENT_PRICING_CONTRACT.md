# BACKEND-10 — V5 event fiyatlama sözleşmesi

26 Ekim 2026. Contract/pricing V5, market-only snapshot V4, cost snapshot V2, bank-aware settlement V3. Bu sürümler birbirinden bağımsızdır. Yeni settlement V5 veya ikinci wallet yoktur.

## Tek hesap

```
marketGross = floor(baseGross * (10000 + pinnedMarketBps) / 10000)
eventBps = selectedEvent.adjustmentBps OR 0
combinedBps = clamp(pinnedMarketBps + eventBps, globalMinBps, globalMaxBps)
finalGross = floor(baseGross * (10000 + combinedBps) / 10000)
netProfit = finalGross - pinnedOperatingCosts
bankPayment = existing capped V3 repayment(netProfit, currentLoan, reserve)
finalWalletDelta = netProfit - bankPayment
```

MarketGross açıklayıcı ve bağımsız pinned kanıttır; ikinci yüzde ona uygulanmaz. Final para hesabı base üzerinden bir kez BigInt floor yapar. Örnek base101, market250bps, event250bps: market103, final106; iki aşamalı floor yanlış biçimde105 üretirdi. Combined envelope pinned market ile aynı-1000/+1500bps aday sınırını korur. Base/market-only/final gross safe integer ve catalog/canonical cap içinde, final net pozitif olmalıdır. Bir geçersiz template bütün pool'u reddeder. No-event final gross market-only gross'a eşittir.

## V5 ve validator dispatch

V5 config, V4 alanlarına liveOpsVersion/liveOpsHash ekler. Pool/contract/delivery schema5 olur; mevcut company/contractEpoch/slot ID, quota, acceptedCount ve receipt kimliği değişmez. Terms V4 market quote'u ve yeni liveOps snapshot'ı taşır; terms.reward final gross'tur. V4 validator hâlâ reward==market.adjustedGrossReward ister. Yalnız V5 dispatch:

- Market quote kendi market-only arithmetic/hash kanıtıyla doğrulanır.
- liveOps.marketGrossReward == market.adjustedGrossReward.
- liveOps.baseGrossReward == market.baseGrossReward.
- terms.reward == liveOps.finalGrossReward.
- settlement.grossReward == terms.reward; netReward == gross-totalCosts.

V1/V2/V3/V4 kayıt, hash, receipt ve journal'lar yerinde upgrade edilmez. V4 missing-liveOps normaldir; V5 missing-liveOps hatadır.

LiveOps snapshot exact alanları: pricingVersion5, catalogVersion/hash, policyVersion/hash, selectedAt, selectedEvent (tam bounded event veya açık null), marketQuoteHash, baseGrossReward, marketGrossReward, marketAdjustmentBps, eventAdjustmentBps, min/maxCombinedBps, combinedAdjustmentBps, finalGrossReward, snapshotHash. SelectedAt contract.createdAt Unix milliseconds ile aynı ve original market epoch aralığındadır. No-event marker null, eventAdjustment0 ve doğru hash taşır. Hash trusted backend boundary içinde bütünlük kanıtıdır; kötü niyetli Admin'e karşı imza değildir.

Public optional liveOps DTO: pricingVersion5, catalogVersion, nullable eventId, ISO selectedAt, decimal-string baseGrossReward/marketGrossReward/finalGrossReward, signed eventAdjustmentBps/combinedAdjustmentBps. Existing market DTO market-only, reward final gross anlamındadır; UI ikisini toplamamalıdır. Gerçek repayment/finalWalletDelta mevcut claim bankRepayment açıklamasındadır.

## Immutable lifecycle ve retry

Yeni teklif yalnız üretildiği anda geçerli event'ten etkilenir. Kabul öncesi/sonrası event sona ermesi, start ve claim fiyatı değiştirmez; event-expiry cezası yoktur. Acceptance/start/claim/completed read original config/market/liveOps catalog ve ORIGINAL selectedAt üzerinden full terms recompute eder. Current selector veya bugünkü active list tarihsel doğrulamanın yerine geçmez. Missing/drift orijinal katalog fail-closed; repair/fallback yoktur.

Generation ilk attempt'ın event selection zamanını ve config/catalog hash'leri ile enabled event'lerin temporal durumlarını sabitler. SDK retry sırasında zaman gerilerse, market epoch değişirse, herhangi bir enabled event upcoming/active/ended sınırı geçerse, katalog/config değişirse veya V5 pin'i kaldırılırsa ret olur. Aynı dönemde ve event durumu değişmeden retry original timestamp ve quote'u korur. Tam bir kısa event interval'ının üzerinden atlamak da upcoming→ended değişimiyle reddedilir. Client time/event override kabul edilmez.

Aynı-key replay durable original receipt'i döndürür, event kontrolüyle yeni quote üretmez. Farklı-key mevcut pool'u yenileyemez; eventId contract ID'ye eklenmedi. Market/event aktivasyonu ek pool kotası sağlamaz. Historical replay mevcut foundation authorization altında write0 olarak kalır.

## Banka, atomicity ve maliyet

Banka beş endpoint'i, principal/fee/limit/reserve ve current-loan-at-settlement policy değişmez. Final gross→costs→positive net→bankPayment tek transaction'da wallet/XP/ledger/claim/journal/loan/history/status/asset release/receipt ile commit edilir. Event için ayrı gelir kaydı veya ödeme kaynağı yoktur. Final wallet cap actual delta sonrasında kontrol edilir; XP trusted delivery rules'tan gelir, event bonusu XP üretmez.

V4'e göre V5 generation ve acceptance+1 LiveOps point read; start/claim/historical item+1 LiveOps read (mevcut pinned market reader üzerinden). Ek belge write0; snapshot mevcut bounded contract terms içindedir. Generation N+2 mevcut writes; accept3 mevcut writes; claim V3 write sayısı aynıdır. Existing pagination max20 korunur. Tarihsel completed20 item için en çok20 ek LiveOps read; retry/platform maliyeti ayrıca. Production billing ölçülmedi.

Numeric schedule/priority/envelope ve inflation onaysız adaydır. Fixed costs nedeniyle gross bonusu net üzerinde daha yüksek oran yaratabilir; zero supply, negative-event positive-net retleri, wallet/XP caps ve tiny-profit repayment rounding soft-lock riskleri kalır. Bailout, grants, penalties, interest, VIP ve gerçek rota etkileri kapsam dışıdır.
