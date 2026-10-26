# BACKEND-09B — Dinamik fiyatlama sözleşmesi

26 Ekim 2026. V4 contract pricing, mevcut V3 bank-aware settlement üzerinde çalışır. Settlement V4 veya ikinci wallet yoktur. Sayısal değerler UNAPPROVED SYNTHETIC CANDIDATES.

## Deterministik hesap

Canonical contract template'in eski gross hesabı baseGrossReward olur. Destination city ve cargo için server clock `floor(now/periodMs)` epoch'u seçer. Seed/policy hash'inden bounded demand/supply türetilir; oyuncu girdisi kullanılmaz.

```
rawBps = truncTowardZero((demandIndex-supplyIndex) * sensitivityBps
                        * regionalWeightBps * cargoWeightBps / 100000000)
adjustmentBps = clamp(rawBps, minAdjustmentBps, maxAdjustmentBps)
adjustedGross = floor(baseGross * (10000+adjustmentBps) / 10000)
netOperatingProfit = adjustedGross - pinnedOperatingCosts
finalWalletDelta = netOperatingProfit - cappedV3BankRepayment
```

Çarpım/bölme BigInt; giriş ve sonuçlar safe integer minor units. Gross pozitif ve katalog/canonical cap içinde; net pozitif olmalıdır. Tek geçersiz template bütün pool'u yazmadan reddeder. Oranlar signed bps; para DTO'da decimal string. Örnek sentetik gross100/cost4: nötr100/96net, +1500bps115/111net, -1000bps90/86net. Market yalnız gross'u değiştirir; route quantity, costs, XP, loan limit/fee/reserve/repayment policy değişmez.

## Şema ve pin

V4 contract catalog V3 alanlarına `marketVersion,marketHash` ekler. Yeni pool/contract/delivery schema4 olur. Terms eski alanlara `market:{pricingVersion:4,baseGrossReward,adjustedGrossReward,snapshot}` ekler; `reward` adjusted gross anlamındadır. Snapshot alanları [MARKET_CONTRACT](MARKET_CONTRACT.md) içindedir. Terms/config/pool hash'leri bu veriyi kapsar; createdAt snapshot aralığında olmalıdır. Operating cost quote hâlâ V2; bankPolicy mevcut V3; settlement result/claim/journal dispatch mevcut BANK-01 sözleşmesine bağlıdır. V1 gross-only, V2 net-only, V3 bank-aware davranışları değişmez; backfill yoktur.

Public contract/settlement DTO'sunda isteğe bağlı `market:{pricingVersion:4,baseGrossReward,adjustedGrossReward,adjustmentBps,marketVersion,epoch,cityId,cargoId}` bulunur. Özel seed/candidate/hash/admin state açılmaz. Gross ve net birbirinin yerine kullanılmaz.

## Retry ve lifecycle

Şirket/contract epoch/slot kimliği ve acceptedCount değişmez; market epoch pool kimliğine eklenmez. Market değişse bile aynı contract epoch'ta ikinci pool yoktur. Concurrent sekiz farklı command yalnız bir pool commit edebilir. Aynı-key historical receipt ilk committed sonucu verir; clock veya market değişimi yeni offer üretmez. Farklı-key duplicate mevcut guard'larda reddedilir.

İlk generation attempt market epoch'u sabitler; SDK retry aynı epoch'ta aynı quote'u üretir. Retry sırasında epoch sınırı geçilirse fail-closed; başka fiyatla commit edilmez. Bu controlled abort testi gerçek SDK transaction retry'sını çalıştırır; doğal contention testlerinden ayrıdır.

Acceptance offered snapshot'ın ORIGINAL epoch'unda pinned catalog'dan recompute eder. Acceptance öncesi market değişimi, acceptance sonrası değişim, delivery start/claim ve tarihsel read fiyatı yenilemez. V4 start/claim/read pinned contract config, market hash/policy, snapshot ve costs bütünlüğünü kontrol eder. Eksik/drift eski katalog tamiri veya current katalog fallback'i yoktur. Aktif selector değişikliği historical pin'i değiştirmez. Historical same-key command replay mevcut receipt sözleşmesince original result döndürür; yeni bir ekonomik write veya yeniden fiyatlama değildir.

Claim mevcut tek transaction'da wallet, XP, ledger, journal, claim, equipment release, contract/delivery status, receipt ve varsa loan/history değişikliklerini birlikte commit eder. Manual repayment/iki delivery/eşzamanlı claim aynı canonical wallet/loan sınırlarında serialize olur. Final wallet cap net repayment SONRASI kontrol edilir; intermediate gross cap diye yanlış ret verilmez. XP canonical trusted delivery rules'tan gelir.

## Maliyet ve sınırlar

V3'e göre generation/acceptance başına +1 market point read; ek contract write yok, mevcut bounded belgede snapshot alanları vardır. V4 pinned-cost doğrulamasında +4 config/logistics/cost/market point read; list item sayısı mevcut20 sınırında. Claim için bank/journal write sayısı değişmez. Bunlar implementation read tahminidir; retry/index/platform maliyeti ayrıca gelir.

Sentetik fiyat salınımı gerçek supply/demand dengesi değildir. +15% gross, sabit costs nedeniyle neti daha yüksek oranda artırabilir. Offer arzı arttığında ikinci filo hızla para üretir; inflation ve 10–15 iş hedefi bütün koşullarda onaylanmış değildir. Sıfır arz/kârsız teklifler/minor-unit repayment rounding/wallet-XP caps soft-lock yaratabilir; bailout veya otomatik debt forgiveness yoktur. Production aktivasyonu, migration, retention ve gerçek veri toplama ayrı tasarım ve güvenlik onayı ister.
