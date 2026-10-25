# BANK_SETTLEMENT_CONTRACT — BANK-01 V3 LOCAL uygulama

26 Ekim 2026 kapanışı. **Functional PASS; LOCAL SECURITY CONDITIONAL PASS; GENERAL AUDIT HOLD; PRODUCTION/MIGRATION BLOCK.**24 Ekim final emulator95/95 PASS. [BANK_CONTRACT](BANK_CONTRACT.md), [BANK-01_REPORT](../backend/BANK-01_REPORT.md).

## Açık sürüm seçimi

V1 gross-only ve V2 cost-net-only değişmez; aktif kredi olsa da banka okunmaz/kesinti yapılmaz. V3 contract catalog schema3, bankPolicy `{settlementVersion:3,policy:synthetic_current_loan_fee_first_v1,policyHash}` taşır. Policy hash canonical policy nesnesine bağlıdır; strict validator başka policy kabul etmez. Contract/pool schema3 ve delivery schema3 ile dispatch edilir. V2 settlement snapshot aynı exact alanlarla V3 terms içinde saklanır; cost/hash otoritesi değişmez. Full termsHash bank policy'yi de kapsar. Mevcut delivery ID/domain, claim source, eski pool hash/ledger/receipts yeniden yazılmaz.

V3 claim mevcut delivery validation → release preparation → bank anchor/current loan/immutable evidence → financial uniqueness → tek prepare/apply aşaması kullanır. Bank loan selection acceptance anından değil, **başarılı settlement transaction snapshot'ından** gelir. Acceptance sonrası kredi açılırsa V3 ona ödeme yapabilir. Loan A kapanıp retry sırasında B aktifse V3 açık policy gereği B'yi seçer; stale loan A ID ile B üzerinde mutation yapılmaz. V1/V2 tamamen muaf kalır. Aktif kredi yoksa repayment0; eksik/corrupt V3 bank anchor fail-closed.

## Tam integer hesap

```text
profit = grossRevenue - (fuel + driver + maintenance + toll)
target = floor(max(0,profit) * pinnedLoanRepaymentBps / 10000)
available = BigInt(previousCash) + BigInt(profit)
capacity = max(0, available - pinnedReserve)
actual = min(target, remainingPrincipal + remainingFee, capacity)
feePart = min(actual, remainingFee)
principalPart = actual - feePart
finalWalletDelta = profit - actual
walletAfter = previousCash + finalWalletDelta
```

V2 cost catalog validation şu anda positive net zorunlu kılar; V3 bunu korur. Percentage ve ara available hesabı BigInt. MaxBalance kontrolü ara available'a değil, gerçekten commit edilen final balance'a uygulanır. Actual debt ile cap edilir; fee/principal ayrıdır ve operating expense değildir. Loan fee/rebate kararı synthetic candidate. XP yalnız pinned delivery rules'dan gelir, borç tutarına bağlı değildir.

Tek commit: delivery+contract completed, assignment/assets released, canonical finance/progression, existing delivery claim+ledger, varsa loan components/paid closure, repayment history, V3 settlementJournal ve command receipt. İkinci wallet, ikinci debit/claim, follow-up transaction, scheduler yoktur. Repayment0 için loan history/loan write yoktur. Counter/IDs retry-stable; bütün reads ilk write'tan önce.

## Journal ve API

Path mevcut settlementJournals/{operationId}; V3 strict **schemaVersion2,settlementVersion3**. V2 schemaVersion1 validator ayrı korunur. V3 fields: companyId/deliveryId/source/operationId, termsHash, V2 quote, bankPolicy, currency/scale/economy/logistics pins, previousBalance/balanceAfter/eligibleXp, loanId nullable/loanTerms/loanLevel, principalBefore/feeBefore, repayment breakdown, finalWalletDelta, committedAt. Repayment total/fee/principal/debtBefore/debtAfter/remainingPrincipal/remainingFee içerir.

Completed V3 read immutable pinned loan terms, source repayment history (payment>0), ledger/claim/timestamp/policy/hash ve tam arithmetic kontrol eder. Current loan sonraki döngüde farklı olabilir; tarihsel journal kendi loanId'si ile doğrulanır. Para kaydı olmadan journal oluşturulmaz; eksik/corrupt journal fail-closed. Bank anchor read latest payment evidence'ını da kontrol eder. Full geçmiş zinciri rutin read'de taranmaz; Admin'in birden fazla kanıt kaydını birlikte değiştirmesi bu domain validator'larının tehdit sınırı dışındadır.

Claim existing reward=gross alanını korur, settlement DTO version3 olur. Optional bankRepayment: settlementVersion3, loanId nullable, total/fee/principal/debtBefore/debtAfter/finalWalletDelta money strings. List DTO settlement policy ve expected net gösterir; actual ödeme breakdown için historical claim receipt vardır. Liste pagination max20; read finalize/repair/accrual yapmaz.

## Maliyet, kanıt ve sınırlar

V2 claim'e göre bank anchor/catalog/marker3 read; aktif veya son paid loan+origination5; son payment4; varsa current quote1; aktif loan repayment history absence1: yaklaşık +3..14 point read. Aynı settlementJournal write count korunur; payment>0 loan+history2 write, full closure anchor1 ek write. Completed V3 read V2'ye göre loan/catalog/history için +0..3 point read. Retry maliyeti ayrıca; bunlar statik tahmin, fatura ölçümü değildir.

Test dosyası real SDK ile sekizli claim, iki delivery, manual-auto, final payoff, payoff-quote ve origination-delivery yarışları; controlled rollback/SDK aborted retry/postcommit response loss senaryoları içerir. Son koşuda29 BANK alt testi ve eski65 emulator kaydı geçti; toplam95 kayıt. Yeni teklif yarışları ve kredi kapanışı/yeni döngü kanıtları [FINAL_REVIEW](../backend/BANK-01_FINAL_REVIEW.md) içinde. Bu LOCAL doğrulama production/release onayı değildir. Gerçek ekonomi fiyatları, production bootstrap/migration, güvenlik istisnası genişletme ve BACKEND-09B ayrı inceleme gerektirir.
