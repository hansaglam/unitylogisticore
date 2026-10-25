# BANK_CONTRACT — BANK-01 LOCAL uygulama

26 Ekim 2026 kapanışı. **Functional PASS; LOCAL SECURITY CONDITIONAL PASS; GENERAL AUDIT HOLD.**24 Ekim final validate95/95 emulator PASS. Production/real DEV/deploy/migration/untrusted CI BLOCK. Bu sözleşme yalnız sentetik LOCAL uygulamayı tanımlar. Ayrıntı: [BANK-01_REPORT](../backend/BANK-01_REPORT.md).

## Yetki ve kapsam

Tek bakiye `v2Companies/{companyId}/state/finance`; borç harcanabilir ikinci wallet değildir. Auth UID → mevcut readFoundation → ownership/migration/economy/progression → readStarter → bank anchor/katalog. Başarılı komutlar executeCommand ile protocolVersion 1, özgün committed serverTime ve result döndürür. Aynı key/intent historical replay güncel foundation yetkisini doğrular, eski işlemi yeniden uygulamaz. Farklı intent IDEMPOTENCY_CONFLICT. Read serverTime yenidir.

Onboarding sonrası sıfır teslimatla borçlanma, canonical level limiti, kullanıcı seçimi, bir aktif kredi, tam ödeme sonrası yeni kredi ürün yönleridir. Teslimat sayısı kilidi, lifetime everBorrowed, bekleme süresi, pasif faiz, ceza, starter confiscation veya VIP yoktur. Tüm fiyat/oran/bütçe değerleri onaysız sentetik adaylardır.

## Katalog ve bootstrap

`v2BankCatalog/{version}` strict schema1: version, approval=synthetic_emulator_only, economyVersion/hash, operatingPolicy, bankPolicyHash, currency/scale, minLevel/maxLevel, tiers, minPrincipal/increment, chargeBps/minFee, repaymentBps/reserve, quoteTtlMs, budgetWindowMs, maxQuoteRequests/maxQuoteWrites, maxLiability. Seviye aralığı 1..canonical thresholds.length, tier'lar sıralı ve boşluksuz; unsupported level reddedilir. Currency/scale/hash canonical ekonomiyle birebir; BigInt fee ara hesabı, safe integer sonuçlar. Mevcut version yerinde değiştirilmez.

Trusted internal `initializeBank` yalnız LOCAL guard altında, tamamlanmış identity/company/economy/starter sonrasında kullanılabilir. Public export yoktur; eski onboarding receipts değişmez. Anchor + kalıcı provisioning/bank marker + receipt tek commit; wallet/XP/loan yazmaz. Farklı key ikinci initialization reddedilir. Read/quote eksik anchor yaratmaz. Emulator fixture şirketleri açıkça bu adımı çağırır. Gerçek şirket backfill/rollout stratejisi uygulanmadı; ayrıca onay gerekir. Catalog geçiş komutu yoktur; anchor hash drift fail-closed.

`bank/current`: companyId, catalogVersion/hash, sequence, activeLoanId, lastClosedLoanId, currentQuoteId, generation, budgetWindow/requests/writes, createdAt. Şema tam doğrulanır. Mevcut aktif veya son kapanan kredi ve son ödeme kanıtı point-read ile okunur; tarih taraması yoktur. İlk anchor finansal kayıt yokluğunu toplu scan ile ispatlamaz; internal trusted final onboarding sınırı ve permanent marker korunmalıdır. Admin corruption otomatik onarılmaz.

## Callables

LOCAL us-central1/maxInstances1. Public request server UID/company/level/config/fee/time kabul etmez.

| Callable | Request / payload | Sonuç |
| --- | --- | --- |
| getMyBankState | `{protocolVersion:1}` | bankReady, level, eligible/ineligibilityCode, maximumPrincipal/minimumPrincipal/increment/availableCredit, currency/scale/catalogVersion, currentQuote, loan veya null |
| quoteCompanyLoan | command payload `{requestedMinorUnits:"20"}` | offerId, principal, fee, liability, repaymentBps, reserve, currency/scale, catalogVersion, expiresAt, status |
| cancelCompanyLoanQuote | `{offerId}` | offerId, cancelled |
| takeCompanyLoan | `{offerId}` | loanId, principal, fee, liability, balance, currency/scale |
| repayCompanyLoan | `{loanId,requestedMinorUnits:"20"}` | actual, principal, fee, remainingDebt, balance, active/paid, currency/scale |

Command zarfı `{protocolVersion:1,idempotencyKey:"stable_key",payload:{...}}`. Bütün para alanları decimal minor-unit string; oran bps ve scale/level tamsayıdır. Amount wire `[1-9][0-9]*`, safe integer; zero/leading zero/sign/exponent/fraction/number JSON reddedilir. AvailableCredit mevcut tier ve wallet boşluğunun üst sınırıdır; exact min/increment uygunluğu quote sırasında ayrıca aranır. Loan DTO original principal/fee, remaining components, total debt ve pinned rate/reserve içerir. History endpoint eklenmedi.

## Quote lifecycle ve kötüye kullanım sınırı

Server ID company+generation hash; quote sequence/generation, company, canonical XP/level, immutable terms/version/hash ve trusted createdAt/expiresAt taşır. Tek current available pointer. Aynı amount ve aynı eligibility snapshot, geçerli süre içinde reuse; farklı teklif eskisini replaced yapar. Cancellation pointer'ı temizler. Consume status consumed yapar ve pointer'ı kaldırır. Süresi biten teklif read sırasında derived expired görünür; read yazmaz. Sonraki quote yeni generation üretir. Historical replay expiry yenilemez veya replaced/cancelled/consumed kaydı yeniden açmaz.

Fixed-window company-scoped budget: başarıyla commit edilen yeni quote key'leri (reuse dahil) ve cancellation requests artırır; offer oluşturma/replacement ve cancel writes sayacına tabidir. Reddedilen/rollback olan intent counter veya receipt yazmaz; bu bir ağ/edge request-rate limiter değildir. Aynı owner canonical company ile per-user sınır da sağlanır; cross-company identity kurulamaz. Same-key replay write yapmaz. Sayılar fixture: 60 saniye pencere, 20 request, 10 offer mutation; TTL60 saniye. Bunlar oyun cooldown'u değildir ve onaysızdır. Yalnız açık quote intent'i gönderilmeli, slider hareketi başına yazılmamalı. Pencere başına writes bounded; ömür boyu receipt/history storage bounded değildir.

## Finansal işlemler

Fee=max(minFee,floor(principal×chargeBps/10000)); initialLiability=principal+fee. Origination wallet +principal, borç +liability; gelir/XP artmaz. Source bank_origination_{loanId}; ID company+sequence hash. Quote/pointer/current level+XP/config/expiry/wallet cap ve claim/ledger/journal/loan yokluğu writes öncesi doğrulanır. Tek commit: wallet/progression ortak planı, loan, anchor, consumed quote, ledger, permanent claim, bankJournal, receipt. Ortak writer XP0 ile progression timestamp'ını yeniler; XP miktarı aynı kalır.

Manual actual=min(request,debt,max(0,cash-pinnedReserve)). Zero capacity hata. Fee-first ve erken ödeme fee rebate olmaması yalnız sentetik politika. Aynı transaction finance debit, loan components, paymentSequence/history, ledger/claim/journal/receipt ve kapanış anchor'ı yazar. Paid loan fresh key reddedilir. Full payoff kalanları sıfırlar, activeLoanId=null, lastClosedLoanId atar; sonraki origination yeni sequence/ID alır. Historical records/claims/receipts TTL ile silinmez. Manual source server hash(uid,idempotencyKey); kullanıcı source seçemez.

Tüm transaction okumaları plan/apply öncesindedir; write closure içinde read/nested transaction/network side effect yoktur. Yarışlar canonical finance/loan/anchor üzerindeki Firestore conflict/retry ile serialize edilmek üzere yazıldı. Gerçek yarış kanıtları ve gözlenen sıralar [FINAL_REVIEW](../backend/BANK-01_FINAL_REVIEW.md) içinde kayıtlıdır. Teklif XP snapshot'ı settlement ile değişirse concurrent take reddedilebilir; yeni quote gerekir. Eski krediye ait manual intent yeni krediye yönlendirilmez.

## Hatalar ve maliyet

Mevcut auth/protocol/ownership/migration/conflict/retry kodları korunur. Yeni BANK_RATE_LIMITED(resource-exhausted), BANK_QUOTE_STALE, BANK_ACTIVE_LOAN, BANK_LOAN_NOT_ACTIVE, BANK_NO_CAPACITY; generic corruption FAILED_PRECONDITION. `details.messageKey=errors.<lowercase code>`; client TR/EN/DE localization yapar. Backend private path veya stack dönmez. Kayıp yanıt aynı key/intent ile kurtarılmalıdır.

Logical point-read tahmini (retry/index/network hariç): foundation8 + starter4 + bank anchor/catalog/marker3 =15. Aktif veya son kapanmış loan+origination evidence5, son payment evidence4, current quote1 ekler: bankread15..25 read/0 write. New command ayrıca receipt başlangıç ve commit sonrası read (+2). Yeni quote existence+1; reuse1 anchor+1 receipt write; yeniquote3 write, replacement4; cancel3. Origination existence+uniqueness4 read, 9 write. Manual uniqueness/history4 read, active debtte8 write; kapanış9. Historical replay9 read/0 write. Rakamlar statik tahmin; emulator ölçümü yapılmadı. Delivery ek maliyeti ayrı [BANK_SETTLEMENT_CONTRACT](BANK_SETTLEMENT_CONTRACT.md).

## Rollout sınırı

Katalog değiştirme, gerçek onboarding/bootstrap/backfill, retention, App Check/edge rate controls, production numeric pricing ve stress/load incelemesi ertelendi. Yeni dependency bulguları ayrı SECURITY-02 onayıyla giderildi; kalan iki dev advisory yalnız mevcut16–30 Ekim trusted LOCAL istisnası altındadır. Genel audit HOLD; insan PR incelemesi gerekir, production/deploy/migration BLOCK.
