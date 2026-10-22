# BANK-00 — Beklemesiz, seviyeye bağlı kredi politikası

23 Ekim 2026. Bölüm etiketleri kapsamı belirler. Tüm örnek sayılar **UNAPPROVED DESIGN CANDIDATES**; üretim ekonomi ayarı değildir.

## APPROVED PRODUCT POLICY

Tamamlanmış onboarding sonrası banka anında açıktır: trusted identity, canonical owner/economy/progression ve starter fleet; migration engeli olmamalı. Sıfır teslimat uygundur. Tek aktif ödenmemiş kredi; tamamen kapandıktan sonra yeni kredi için cooldown, teslimat sayısı veya lifetime ban yok. Oyuncu principal seçer; sınır canonical şirket seviyesine bağlıdır. VIP ertelendi. Seviye yükselişi mevcut kredi şartlarını değiştirmez.

ECONOMY-00'ın unlockDeliveries:5, sabit principal ve everBorrowed lifetime yasağı yalnız tarihsel v1 deneyinde kalır. Bu dosyalar/sonuçlar yeniden yazılmadı. BANK-01 yeni BANK-00 sözleşmesini esas alır.

## PROPOSED — Sayısal adaylar

| Şirket seviyesi | Maximum principal LGC |
|---|---|
| 1–4 | 20000 |
| 5–9 | 35000 |
| 10–19 | 60000 |
| 20–50 | 120000 |

Minimum1000, step1000, fixed fee800bps (%8), positive-net repayment3500bps (%35), reserve5000 LGC. MinFee1 minor unit (0.01LGC) rounding güvenlik tabanı; örnek principal aralığında floor fee zaten daha büyüktür. MaxBalance100000000 LGC yalnız standalone fixture; gerçek backend canonical EconomyConfig.maxBalance kullanmalı. LGC scale2, tüm miktarlar exact integer minor units ve BigInt ara işlem.

Tier sınırları boşluksuz ve çakışmasız; min<=max; step grid minimuma göre tanımlıdır. Amount strict positive decimal integer string, safe bound; sign/float/exponent/leading zero reddedilir. Canonical config hash/version drift fail-closed. Numeric adayların onayı ürün politikası onayından ayrıdır.

## PROPOSED — Quote ve güven sınırı

Read-only getMyBankState limit/status döndürür, slider preview local olabilir. Explicit request quoteCompanyLoan yalnız requestedMinorUnits niyeti taşır; backend canonical state/level/config/amount/step/wallet cap doğrulayıp kısa ömürlü server quote üretir. takeCompanyLoan yalnız offerId tüketir. Her slider hareketinde yazı yok.

Bir current quote, server generation, lifecycle sequence, state/config pins; replacement/expiry/cancellation eski quote'u geçersiz kılar. Bounded per-company/per-UID request/write budget gerekir; quote reuse yeni teklif yazısını azaltır. Replay eski quote'u diriltmez. Bunlar fraud/write-volume kontrolüdür, payoff sonrası gameplay cooldown değildir. Ayrıntılar [BANK_CONTRACT_DRAFT](../api-contracts/BANK_CONTRACT_DRAFT.md).

## IMPLEMENTED offline — Muhasebe ve çoklu kredi

Yeni bank-model.mjs ayrı bank-00-v1 config hash kullanır; ECONOMY-00 modelinden yalnız exact integer yardımcılarını alır. Aktif pointer + immutable dönüşümlü per-loan history + cumulative principalReceived/feesCharged/principalRepaid/feesRepaid tutar. Paid kayıt sonraki işlemlerde değişmez; ID/source artan sequence ile ayrılır. Backend için tüm geçmiş taraması önerilmez; offline invariant kontrolü bu küçük in-memory history'yi tarar.

```text
cash = initialCash + revenue - costs + principalReceived
       - principalRepaid - feesRepaid - capitalSpent
outstanding = principalReceived + feesCharged - principalRepaid - feesRepaid
cash + capitalSpent - outstanding = initialCash + revenue - costs - feesCharged
```

Loan origination revenue/completion/XP değildir; repayment operating expense değildir. Dolayısıyla iş yapmadan her borrow/pay cycle yalnız finansman ücreti kadar ekonomik kayıp yaratır. Model XP threshold uydurmaz; levelUp trusted fixture olayıdır. Future server level'i canonical XP/config ile türetir.

Fee=max(minFee,floor(principal*chargeBps/10000)). Repayment=min(debt,floor(max(0,gross-allCosts)*pinnedBps/10000),max(0,availableCash-pinnedReserve)). Manual=min(request,debt,max(0,cash-reserve)); kapasite0 ise hata. Fee-first allocation ve no early fee rebate yalnız PROPOSED. Loan status/anchor closure ve payout/liability tek immutable model dönüşümüdür; Firebase atomicity kanıtı değildir. Overflow veya yetersiz nakit eski state'i değiştirmez.

## IMPLEMENTED offline — Sonuç ve sınırlama

Low/base/high borçsuz edinim16/13/11 teslimat; hedef10–15 bütün koşullarda garanti değildir. Aynı20000 krediyle day-zero13/10/9; beşinci iş10/8/6. İhtiyaç anı low8/base7/high6 krediyle en erken nakit edinimi8/7/6. Borcu erken almak kesintileri erken başlatır; gereksiz erken kredi edinimi geciktirebilir. 10000 kısmi base kredi day-zero14 işte edinim yapar: borçsuz13'ten bile yavaştır; lower fee tek başına fayda garantisi değildir.

Temel eşit30 iş sonunda tam ödenen20000 kredinin maliyeti1600; iş sayısı değişmedikçe kapasite otomatik gelir yaratmaz. Tek teklif/dalga varsa ikinci filo süreyi kısaltmaz. İki teklif/sabit süre/yeterli nakit varsayımı yalnız alt sınırdır; gerçek route duration, matching offer supply ve kullanıcı oturumu modellenmez.

Kredi ile zarar senaryosu86 başarılı işten sonra600 nakitte800 upfront karşılayamaz; borç21600 kalır. Reserve zarar sigortası değildir. Immediate credit yanlış harcanırsa nakit açığını büyütür; borç normal delivery'yi yasaklamaz fakat prefunding yetersizliği işi durdurabilir. Acil kurtarma/pozitif-net teklif arzı/UX ürün kararı bekler; bailout uygulanmadı.

Model yalnız tek ek truck+driver, sequential successful deliveries, prefund guard, fixed cost/gross ve bounded horizon içerir. Quote lifecycle, receipt/idempotency, gerçek server clock, concurrent transaction, start/cancel/refund, dinamik ekonomi ve actual progression henüz uygulanmadı. 60 iş repeated-loan senaryosu payoff anında otomatik yeni kullanıcı niyeti varsayar; ürün otomatik kredi almaz. Son ufukta yeni kredi alınırsa nakit ile kalan borç birlikte karşılaştırılmalı.

## PROPOSED — Mimari uyum ve sonraki fazlar

BACKEND-00 legacy/migration belirsizliği;01 environment/default-deny;02 identity;03 receipts;04 canonical company;05 bounded finance/progression/ledger/claims;08A starter fleet;06 pinned contracts;07 single-transaction settlement;08B canonical commerce korunur. No new runtime authority. getMyBankState hazır olmayan şirketi banka için eligible göstermez.

BACKEND-09A operating cost/net settlement, pinned costs ve journal compatibility'yi önce sağlamalı. Start debit varsa completion yalnız unpaid costs'u düşürür; repayment target ALL costs üzerinden hesaplanır. BANK-01 payout/loan/claim/receipt ve repayment/settlement/release/closure'ı aynı transaction'a bağlar; manual-auto/reborrow/lost-response emulator testleri şarttır. Legacy schema1 journal ve delivery geçmişi yeniden yazılmaz.

Sıra: BANK-00 onayı → BACKEND-09A → BANK-01 → BACKEND-09B → BACKEND-10. Sonraki fazlar başlamaz. Final fiyat/XP/limits/rates/reserve/cost timing/minFee/rebate/quote TTL/budget UNKNOWN veya PROPOSED. General security HOLD, production BLOCK; yerel bağımlılık istisnası30 Ekim2026 sonu, uzatma yok.
