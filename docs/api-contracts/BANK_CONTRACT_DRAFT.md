# BANK_CONTRACT_DRAFT — BANK-00

23 Ekim 2026. **APPROVED PRODUCT POLICY** yalnız aşağıda açıkça belirtilen ürün yönüdür. Path/schema/API ve transaction tasarımı **PROPOSED**; bütün parasal değerler/oranlar **UNAPPROVED DESIGN CANDIDATES**. **IMPLEMENTED** yalnız ayrı BANK-00 offline modelidir; hiçbir banka callable'ı export edilmedi. Kurgusal, nonredeemable LGC; gerçek kredi/ödeme hizmeti değildir.

## APPROVED PRODUCT POLICY — hazır şirket, beklemesiz kredi

Trusted identity, canonical ownership, geçerli finance/progression ve starter fleet ile onboarding tamamen hazırsa banka hemen kullanılabilir. İlk kredi için tamamlanmış teslimat gerekmez. Tek aktif ödenmemiş kredi vardır; tamamen kapandıktan sonra yeni kredi alınabilir. Teslimat sayısı, cooldown veya ömür boyu kredi yasağı yoktur. VIP DEFERRED ve eligibility'ye etkisizdir. Migration-blocked veya kısmi initialized state fail-closed olur.

Oyuncu canonical şirket seviyesinin izin verdiği aralıkta principal seçer. Level yalnız server progression/XP/config doğrulamasından gelir. Level-up mevcut kredinin principal, fee, reserve, repayment oranı veya borcunu değiştirmez; yalnız yeni kredi kapasitesini etkiler. ECONOMY-00'daki unlockDeliveries:5 ve everBorrowed yasağı tarihsel deneyde korunur; BANK-01'e taşınmaz.

## PROPOSED — versioned katalog ve exact amount

Catalog her tier, minimum, step, fee ve repayment kuralını kontrol eder. Örnek tier'lar: 1–4 → 20000; 5–9 → 35000; 10–19 → 60000; 20–50 → 120000 LGC. Minimum principal 1000, step 1000 LGC; tümü onaysız test adayları. Desteklenen level aralığı 1..50 adaydır; runtime progression üst sınırı varsayılmaz. Catalog aralığı canonical progression ile açık uyumluluk ister; dışındaki seviye silent clamp yerine fail-closed olur.

Tier'lar sıralı, contiguous, overlap/gap içermeyen inclusive sınırlar taşır. Min<=max; `(requested-minPrincipal) % step == 0`. Tier max aynı grid üzerinde olmalıdır. Wire tutar canonical positive decimal integer string; sign/exponent/fraction/leading zero/NaN/unsafe integer reddedilir. LGC scale2: 12000 LGC = "1200000". Client miktarı niyettir; client level/limit/rate/fee/hash/currency/UID/companyId kabul edilmez.

Bütün persistent para safe integer minor units; BigInt ara toplama/çarpma/bölme ve sonuç bounds kontrolü. Wallet yalnız canonical state/finance; maxBalance canonical EconomyConfig'ten gelir. Offline maxBalance da onaysız fixture'dır. Origination cash+principal sınırı quote ve consume transaction'ında yeniden kontrol edilir. Overflow partial payout bırakmaz.

## PROPOSED — minimal documents

Exact field allowlists/status-specific schemas BANK-01 implementation review'de kesinleştirilecek. Buradaki belgeler henüz yazılmıyor.

| Path | Önerilen alanlar/invariant |
|---|---|
| v2BankCatalog/{version} | schemaVersion, version, approval, economyVersion/hash, costRulesVersion/hash, currency, scale, supportedLevelRange, tiers, minPrincipal, principalStep, chargeBps, minFinanceFee, repaymentBps, reserveFloor, quoteTtlMs, quoteBudgetPolicyVersion. İlk gerçek uygulama synthetic_emulator_only. |
| v2Companies/{companyId}/loanOffers/{offerId} | schemaVersion, companyId, offerId, sourceId, quoteGeneration, expectedLifecycleSequence, eligibilityRevision, progressionSnapshot/hash, bank/economy/cost versions+hashes, immutableTerms/hash, createdAt, expiresAt, status; consumed ise loanId/consumedAt. Status available/replaced/cancelled/consumed; expiry server clock ile türetilir. |
| .../loans/{loanId} | schemaVersion, companyId, loanId, lifecycleSequence, offerId, sourceId, immutableTerms/hash, principalOriginal, feeOriginal, principalRemaining, feeRemaining, status, originatedAt; paid ise paidAt. Paid kayıt sonradan değiştirilmez. |
| .../bank/current | schemaVersion, companyId, activeLoanId veya null, lifecycleSequence, currentQuoteId veya null, quoteGeneration, eligibilityRevision, lastClosedLoanId, bounded quoteBudget counters/window, updatedAt. Balance/cash yok; lifecycle ve uniqueness anchor. |
| .../loans/{loanId}/repayments/{sourceId} | immutable schemaVersion, loanId, sourceType, sourceId, total, feePart, principalPart, liabilityAfter, journalOperationId, committedAt. Create-only. |

Terms principal/fixedFee/initialLiability/repaymentBps/reserveFloor/currency/scale ve version/hash pin'lerini taşır. Sürüm içeriği yerinde değiştirilemez. Yeni katalogya geçiş explicit compatible transition gerektirir; eski kredinin immutable terms'i tekrar fiyatlanmaz. Tamamlanmış teslimat history taraması veya eligibility aggregate'ı gerekmez; teslimat adedi artık bankanın koşulu değildir.

No active loan: activeLoanId=null. Yeni origination sequence+1 (checked integer) kullanır; loanId ve permanent source company+sequence+offer üzerinden server hash ile türetilir. Partial repayment anchor'ı korur. Son ödeme loan.status=paid, debt=0, activeLoanId=null ve lastClosedLoanId güncellemelerini tek transaction'da yapar. Yeni kredi yeni sequence/source alır. Eski paid loans, origination claims, repayment claims ve receipts kalıcıdır; TTL veya yeniden kullanım yoktur.

Her request anchor ve onun doğrudan gösterdiği current loan/quote'u okur; bütün geçmişi taramaz. Anchor/loan çelişkisi, eksik active kayıt veya orphan state fail-closed; otomatik onarım yok. Initial anchor gelecekte onboarding ile aynı güvenilir transaction'da kurulmalıdır; mevcut şirkete anchor backfill bu fazda yapılmaz. Absent anchor'ı read veya quote sessizce yaratmaz; açık bootstrap/migration politikası BANK-01 önkoşuludur.

## PROPOSED — getMyBankState ve quoteCompanyLoan

`getMyBankState`: authenticated `{ "protocolVersion":1 }`; read-only snapshot. Current canonical level, min/step/max decimal strings, borrowing eligibility code, active loan pinned terms/debt, current quote durumu ve serverTime döner. Read current state yaratmaz, quote üretmez, ücret tahakkuk ettirmez. History ayrı bounded pagination (öneri max20); sınırsız liste yok. Slider local preview yapabilir fakat authoritative fiyat değildir.

`quoteCompanyLoan` future callable:
```json
{"protocolVersion":1,"idempotencyKey":"loan_quote_request_001","payload":{"requestedMinorUnits":"1200000"}}
```

Trusted Firebase Auth UID → readFoundation owner/migration/identity/economy → starter integrity → canonical progression → bank catalog → anchor/no-active → requested bounds/grid → wallet max. Tüm mutable kontroller aynı transaction snapshot'ında, bütün writes öncesinde yapılır. Server-owned quote ID/source/generation üretilir; caller ID veya financial terms seçemez. Response offerId, principal, fixedFee, initialLiability, repaymentBps, reserveFloor, currency/scale, expiresAt ve config version içerir. Quote yazımı wallet/XP/gelir etkisi yaratmaz. Receipt aynı transaction'dadır.

### PROPOSED — bounded quote lifecycle

- Bir şirket için en fazla bir current usable quote; yeni onaylı intent eskisini replaced yapar ve generation artırır. Teklif yalnız açık kullanıcı onayında istenir, her slider hareketinde değil.
- Kısa server TTL katalogdan gelir. Expired quote consume edilemez; get read hiçbir cleanup write yapmaz. Bir sonraki açık quote isteği expired pointer'ı atomik olarak değiştirir. TTL dolması borçlanma cooldown'u değildir.
- Aynı current amount + aynı terms/state fingerprint isteyen farklı key mevcut uygun quote'u yeniden kullanabilir; yeni offer yaratmaz. Yine de command receipt yazımı rate-budget'a tabidir. Same-key replay eski quote/result/serverTime'ı döndürür, expiry'yi yenilemez veya replaced quote'u tekrar etkinleştirmez.
- Cancellation için future explicit idempotent `cancelCompanyLoanQuote` intent'i yalnız offerId taşır; matching pointer clear + cancelled marker + receipt atomik. Endpoint bu fazda yok. Replaced/expired/cancelled tüketilemez; cancel hiçbir nakit hareketi yaratmaz.
- Catalog seçilmiş server token-bucket budget, per-company ve per-UID limit; bütün yeni quote/cancel key'leri, replacements ve receipt üreten farklı-key reuse dahil aynı budget içinde. Başarılı same-key replay write gerektirmez. Rate aşımı write öncesinde bounded retry hint ile reddedilir. App Check/edge abuse controls ayrı production tasarımıdır. Budget sayıları henüz onaysız; uygulanmadan banka açılmaz.
- Budget ödeme sonrası oyun bekleme süresi değildir: debt closure anında yeniden eligibility vardır; teknik spam sınırı tüm çağrılara eşit uygulanır. Kredi geçmişini veya teslimat sayısını anti-abuse kısayolu olarak kullanmaz.
- Eski quote/claim/receipt kayıtları historical kanıttır, yeniden kullanılmaz. Bu tasarım canlı quote sayısını ve zaman başına writes'i sınırlar; ömür boyu depolamayı sabit sınıra getirdiği iddia edilmez. Arşiv/index/retention kapasite planı ayrı release kararıdır; paid loan/kalıcı finans claim'i silinmez.

Quote expected lifecycleSequence/generation, current pointer, readiness/ownership/migration revision, progression snapshot ve bank/economy/cost hash'e pinlidir. Level değişimi veya başka incompatible transition quote'u consume için geçersiz kılar ve yeni quote ister; mevcut active loan etkilenmez. Wallet'ın değişmesi tek başına quote fiyatını değiştirmez fakat consume güncel maxBalance'ı yeniden doğrular. Bir kredi origination tüm eski quote'ları anchor/generation üzerinden geçersiz kılar; repayment sonrası sequence korunur, eski quote tekrar kullanılamaz.

## PROPOSED — takeCompanyLoan ve repayCompanyLoan

`takeCompanyLoan` mevcut önerilen payload'ı korur:
```json
{"protocolVersion":1,"idempotencyKey":"candidate_take_001","payload":{"offerId":"server_offer_id"}}
```

Yalnız offerId. Trusted foundation/starter, current no-active anchor, pointer/generation/sequence, ownership, pinned terms/hash, current canonical level, serverNow<expiresAt, permanent source absence ve wallet max yeniden doğrulanır. Tek commit: wallet+principal, principal+fixedFee liability, new loan, consumed quote, sequence/active anchor, origination journal, permanent claim, receipt. Payout'tan fee kesilmez; borca eklenir. Gelir/completion/XP/leaderboard artmaz. Receipt result loanId, amount breakdown, historical walletBalance içerir.

`repayCompanyLoan`:
```json
{"protocolVersion":1,"idempotencyKey":"candidate_repay_001","payload":{"loanId":"server_loan_id","requestedMinorUnits":"100000"}}
```

Strict positive safe integer string amount yalnız maximum intent. Actual=min(request,outstanding,max(0,cash-pinnedReserve)). Zero capacity yeni key ile hata; paid loan fresh request hata. Loan/owner/anchor/terms/source doğrulaması, wallet debit, per-loan principal/fee azaltımı, repayment journal/claim/receipt ve varsa closure aynı transaction'da. Early repayment ek fee yaratmaz; full fixed fee/no rebate ve fee-first allocation PROPOSED, final ticari kural onayı değildir.

Her command mevcut executeCommand pattern'ini izler. Same key+intent aynı historical result/committed serverTime, farklı intent conflict. Receipt replay güncel owner/migration/state integrity doğrular fakat eski kredinin paid oluşu veya yeni bir kredinin active oluşu eski başarılı origination'ı yeniden çalıştırmaz veya geçersiz kılmaz. Farklı key aynı consumed offer permanent domain claim/pointer/sequence yüzünden ikinci payout yapamaz. Read serverTime ise tazedir. Post-commit cevap kaybında aynı key/intent; yeni key üretmeyin. Transaction callback retry dış side effect/rastgele financial karar yaratmaz; IDs stable company/generation/sequence'den türetilir. Server expiry her attempt'te geçerli olmalı; stale quote başarısız olur.

Proposed error sınıfları NOT_READY, ACTIVE_LOAN, INVALID_AMOUNT, QUOTE_EXPIRED/STALE, RATE_LIMITED, NO_REPAYMENT_CAPACITY, LOAN_ALREADY_PAID, BANK_STATE_INVALID; public codes/messageKey BANK-01'de mevcut auth/protocol/conflict/retry normlarına eşlenecek. Stack/private path dönmez; bu faz runtime error tablosu değişmedi.

## PROPOSED — hesaplama ve atomic delivery integration

```text
floorFee = floor(principal * chargeBps / 10000)
fixedFee = max(configuredPositiveMinFee, floorFee)
initialLiability = principal + fixedFee
operatingProfit = grossReward - allAuthorizedOperatingCosts
target = floor(max(0, operatingProfit) * pinnedRepaymentBps / 10000)
actual = min(target, outstanding, max(0, availableSettlementCash - pinnedReserve))
feePart = min(actual, remainingFee)
principalPart = actual - feePart
```

Ana örneklerde minimum principal1000 ve charge8% zaten pozitif fee80 yaratır; minFee1 minor unit yalnız küçük principal/rounding koruması test adayıdır. Flooring fractional minor unit'i aşağı yuvarlar; minFee etkinse advertised floorFee yerine açıkça minFee uygulanır. UI quote tam final fee'yi gösterir. Bileşik/günlük faiz, offline penalty, seizure veya borç nedeniyle normal-delivery lockout yok. Repayment yeniden operating expense değildir.

Future claimDeliveryReward bütün auth/foundation, pinned delivery/costs, assignment, finance/progression, bank anchor/current loan, claim/source okumalarını önce tamamlar. Tek transaction delivery/contract completion, assets release, operating costs, wallet net delta, loan allocation/closure, repayment history, journal, eligible earned XP, permanent delivery claim ve receipt yazar. Reward sonrası ayrı debit, nested transaction veya read-after-write yoktur.

Manual ve auto repayment aynı wallet/loan/anchor üzerinde conflict/retry yapar. Son ödeme tek closure yapar, diğer auto settlement active loan yoksa sıfır ödeme ile devam eder; diğer fresh manual request paid error verir. Closure sonrası reborrow ile yarışan delivery transaction yeniden okur: proposed settlement-time policy o anki active loan'ı kullanır, eski loanId üzerinden yeni borca yanlış yazmaz. Bu yalnız bank-aware yeni delivery terms için açıkça pinlenen kuraldır; ürün/rollout incelemesi gerekir. Paid loan hiçbir zaman yeniden açılmaz.

Mevcut schema1 ledger strict'tir, gross-only delivery settlement ve commerce mutation aynı canonical finance/progression planını kullanır. BANK-00 bunları değiştirmez. BACKEND-09A ayrı versioned journal veya schema2/dual-reader planını ve old-delivery drain'i onaylamalı; eski ledger/history rewrite yok. Banka settlement'ın breakdown'ında principal/fee tahsilatı ayrı gösterilir; toplam cashDelta ve breakdown iki kez uygulanmaz.

Start'ta gider zaten ödendiyse `availableSettlementCash=currentCash+gross-unpaidCosts`; hedef profit ise `gross-ALL authorizedCosts`. Completion'da upfront gider ikinci kez düşülmez. Prior payment source claim'leri delivery terms'e pinlenmeli. Legacy reward-only deliveries otomatik retroactive cost/bank charge almaz; yeni settlement-v2 terms rollout politikası onaylanmalıdır. Negative net sıfır tahsilat; yetersiz işletme nakdi separate liquidity problemi, otomatik bailout yok.

## PROPOSED — BANK-01 için zorunlu emulator kanıtları

Concurrent iki cihaz quote replacement/consume; same/different key aynı offer; expiry sınırı; replacement/cancel/replay; UID/company scope; canonical level/config değişimi; wallet max; partial onboarding/migration; current anchor corruption; iki origination yarışı; manual-vs-auto son ödeme; closure-vs-reborrow-vs-delivery; historical replay yeni kredi sonrası; claim/receipt TTL olmaması; failure injection/rollback/lost response; bounded quote rate/write volume; legacy delivery drain ve journal reader compatibility testleri. Offline model bunların Firebase transaction güvenliğini kanıtlamaz.

## DEFERRED / UNKNOWN

Gerçek fiyatlar/XP/thresholds/maxBalance, rate/minFee/reserve/minimum/step/tiers, fee allocation/rebate, quote TTL/budget ve cost timing/recovery final onay bekler. Onaysız numeric config production'a yüklenemez. BANK-00 → BACKEND-09A → BANK-01 → BACKEND-09B → BACKEND-10; sonraki fazlar başlamadı. General audit HOLD; production/real DEV/CI/untrusted/deploy/migration BLOCK. Yerel istisna 30 Ekim 2026'da sona erer; bu belge uzatmaz.
# BANK-01 durum eki — 24 Ekim 2026

Bu tarihsel BANK-00 önerisinin runtime taslağı [BANK_CONTRACT](BANK_CONTRACT.md) ve explicit V3 taslağı [BANK_SETTLEMENT_CONTRACT](BANK_SETTLEMENT_CONTRACT.md) altında hazırlanıyor. Yeni dependency bulguları nedeniyle functional/local security HOLD; emulator doğrulaması tamamlanmadı. Bu ek BANK-00 simulation sonuçlarını veya numeric onay durumunu değiştirmez.

## BANK-01 final review eki — 26 Ekim 2026

Yukarıdaki24 Ekim HOLD/not-run notu ilk uygulama anına aittir. SECURITY-02 remediation ve final review tamamlandı: functional PASS, trusted LOCAL security CONDITIONAL PASS, general audit HOLD; production/migration BLOCK. Son validate30 unit/95 emulator ve75 offline PASS. Tam kanıt ve75 gereksinim matrisi [BANK-01_FINAL_REVIEW](../backend/BANK-01_FINAL_REVIEW.md). Tarihsel BANK-00/BACKEND-09A sonuçları ve numeric onay durumu değiştirilmedi.
