# BACKEND-09B — Market sözleşmesi

26 Ekim 2026. Yalnız `demo-logisticore-local`; sentetik LOCAL/EMULATOR kapsamı. Gerçek oyuncu aktivitesi, gerçek şehir talebi, production fiyatı veya sayısal ürün onayı değildir.

## Yetki ve okuma API'si

`getMyMarketState`, Auth zorunlu callable, us-central1, maxInstances=1. Tek kabul edilen istek `{protocolVersion:1}`. Company, katalog sürümü, epoch, seed, multiplier veya demand alanı kabul edilmez. Sunucu UID'den canonical company/economy ve tamamlanmış starter durumunu çözer. Legacy/migration veya bozuk state fail-closed. Yerel ortam guard'ı DB erişiminden önce uygulanır.

Read-only transaction, yazma/initializer/repair/ödül/tahsilat yoktur. `v2MarketSelection/{canonicalEconomyVersion}` sunucuya ait seçim kaydıdır: `{schemaVersion:1,approval:"synthetic_emulator_only",contractVersion,contractHash}`. Seçim doğrulanmış V4 contract catalog ve onun market pin'ine gider. Eksik seçim otomatik oluşturulmaz; istemci sürüm seçemez. Bu faz yalnız emulator fixture'larında seçim oluşturur; publish/activation API'si yoktur.

Yanıt `{protocolVersion:1,serverTime,result}`; result alanları:

| Alan | Anlam |
| --- | --- |
| synthetic | Daima true |
| marketVersion | Doğrulanmış katalog kimliği |
| policy / policyVersion | synthetic_destination_pressure_v1 / 1 |
| epoch / periodEndsAt | Sunucu saatinden dönem ve ISO bitiş zamanı |
| conditions | En çok 16 canonical şehir × 5 cargo = 80 kayıt |

Her koşul `{cityId,cargoId,demandIndex,supplyIndex,adjustmentBps,trendBps}`. `trendBps:0` bu fazda trend hesaplanmadığını belirtir; geçmiş oyuncu talebi ölçümü değildir. Seed, özel asset/company/bank/admin kayıtları ve ham katalog yayımlanmaz. Aynı katalog ve epoch bütün şirketlerde aynı koşulları verir; wallet/loan/VIP kişiselleştirmesi yoktur. Read yanıtı idempotent command receipt değildir; `serverTime` ve dönem değişebilir.

## Katalog ve snapshot

`v2MarketCatalog/{version}` exact alanlı schema1; `approval:synthetic_emulator_only`, canonical economy/logistics version+hash, currency/scale, policy+policyHash, periodMs, indexMax, jitter, seed, sensitivityBps, min/maxAdjustmentBps, maxGrossReward, regions, cargo içerir. Region `{cityId,demand,supply,weightBps}`, cargo `{cargoId,weightBps}`. Şehirler mevcut logistics catalog içinden; cargo mevcut contract catalog içinden seçilir. Duplicate/unknown ID, ekstra/eksik alan, yanlış policy/hash/currency/version veya güvenli olmayan sayı reddedilir. Sürümler immutable kabul edilir; drift işlemi durdurur, eski kayıtları düzeltmez.

Sınırlar: period 1..604800000 ms, indexMax 1..10000, jitter 0..indexMax, sensitivity 0..1000, weights 0..10000 bps, adjustment alt sınırı -1000..0 ve üst sınırı 0..1500. Bunlar teknik sınırlar; aday numeric değerler ürün onayı değildir. maxGrossReward canonical wallet üst sınırını aşamaz; base ve adjusted gross bu sınıra uyar.

Minimal state seçildi: ayrı market snapshot koleksiyonu/yazarı yok. Pure snapshot, seed+epoch+city+cargo+side+policyHash SHA-256 girdisinden demand/supply jitter'ı türetir; baseline+jitter index aralığına clamp edilir. Snapshot schema/version/hash/policy/epoch/start/end/city/cargo/indices/bps ve snapshotHash ile contract terms içinde saklanır. Seed hash girdisi ekonomik yetki değildir; catalog pin ve server-only erişim yetkidir. Global counter, scheduler, random, full scan veya real-player event collection yoktur.

## İzolasyon, maliyet ve hata

Firestore Rules default-deny değişmedi; market catalog ve selection için authenticated/unauthenticated read/list/write retleri emulator testine eklendi. Auth/strict request/migration/FAILED_PRECONDITION/INTERNAL mevcut hata sözleşmesi kullanılır; private hata ayrıntısı sızdırılmaz.

Ready state için yaklaşık 17 point read (foundation8, starter4, selector1, contract/logistics/cost/market4), 0 write, en çok80 pure koşul hesabı. Retry ve platform billing ayrıdır; ölçülmüş production maliyeti değildir. Snapshot retention/sürüm activation/gerçek talep toplama ve abuse-resistant aggregation ertelendi. Pinned catalog/config'ler aktif ve tarihsel işler için korunmalıdır.

Fiyatlama: [DYNAMIC_PRICING_CONTRACT](DYNAMIC_PRICING_CONTRACT.md). Test ve güvenlik kararı: [BACKEND-09B_REPORT](../backend/BACKEND-09B_REPORT.md).

## BACKEND-10 uyumluluk eki

V5 contract config aynı server-owned selector üzerinden seçilebilir. getMyMarketState mevcut market-only alanlarını korur; event bonusu adjustmentBps'e karıştırılmaz. V5 config'in liveOps pin'i de doğrulanır; eksik/drift LiveOps catalog ret verir. V4 okuma17, V5 okuma yaklaşık18 point read;0write. Event bilgisi ayrı [getMyWorldEvents](LIVEOPS_CONTRACT.md) API'sindedir. V4 tarihsel kayıtlar değiştirilmez.
