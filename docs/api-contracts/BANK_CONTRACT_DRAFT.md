# BANK_CONTRACT_DRAFT — BANK-00 review girdisi

23 Ekim 2026. **PROPOSED**: bu belgedeki bütün path/schema/API/kurallar taslaktır; runtime implementasyonu veya ürün onayı değildir. **IMPLEMENTED** yalnız ECONOMY-00 offline modelidir. Gerçek banka, ödeme, IAP, kredi ilişkisi, kripto veya redeemable currency yoktur. Oyun içi LGC/scale2 production'a aktive edilmez.

## PROPOSED — authority ve lifecycle

CLIENT SENDS INTENT. BACKEND VALIDATES AND DECIDES. Trusted Auth UID → readFoundation canonical company/owner/migration/economy → starter integrity → versioned server offer → transaction. Tek aktif loan; unpaid debt yeni krediyi engelleyebilir, normal delivery yetkisini tek başına engellemez. Pasif günlük faiz, gecikme cezası, starter haczi, permanent lockout yoktur. Offline süre borcu büyütmez.

Loan available offer→active loan→paid. Teklif available→consumed; expired computed/read-only. Başarısız precondition kısmi payout/offer consumption bırakmaz. Paid loan/history/claim silinmez. Reborrowing otomatik değildir; modelde everBorrowed sonrası reddedilir. Gelecekte yeni kredi için earned activity since previous loan, cooldown/credit limit ve kalıcı lifecycle sequence ayrı onay gerektirir; yeni offerId tek başına hak yaratmaz.

## PROPOSED — minimal canonical documents

Tüm yazarlarda exact field allowlist ve status-specific schema; no implicit optional defaults. Aşağıdaki fields BANK-00 review'de kesinleştirilecek, bugün Firestore'a yazılmaz.

| Path | Önerilen alanlar ve invariant |
| --- | --- |
| v2BankCatalog/{version} | schemaVersion,version,approval,economyVersion/hash,costRulesVersion/hash,currency,scale,principalLimit,chargeBps,repaymentBps,reserveFloor,minimumCompletedDeliveries,requiredLevel,offerTtlMs,reborrowPolicyVersion. İlk emulator implementasyonunda synthetic_emulator_only zorunlu. |
| v2Companies/{companyId}/loanOffers/{offerId} | schemaVersion,offerId,companyId,bankVersion/hash,immutableTerms,sourceId,createdAt,expiresAt,status; consumed'da loanId/consumedAt. Terms: principal,fixedCharge,totalLiability,currency,scale,repaymentBps,reserveFloor,eligibilitySnapshot. Catalog ile tam eşleşme; offer ID yeniden kullanılamaz. |
| .../loans/{loanId} | schemaVersion,loanId,companyId,offerId,terms/hash,principalOriginal,feeOriginal,principalRemaining,feeRemaining,status,originatedAt; paid'de paidAt. Outstanding=sum components; 0..initial liability. Loan ID company+offer+domain hash. |
| .../bank/current | schemaVersion,companyId,activeLoanId (veya null),lifecycleSequence,everBorrowed,lastClosedLoanId,updatedAt. Yalnız lifecycle/uniqueness anchor; balance/cash yok. |
| .../loans/{loanId}/repayments/{sourceId} | immutable schemaVersion,loanId,sourceType,sourceId,total,feePart,principalPart,liabilityAfter,journalOperationId,committedAt. Deterministic source, append-only. |

Canonical wallet mevcut state/finance olarak kalır. Completed-history eligibility için client count, mutable UI cache veya unbounded delivery scan kullanılmaz. BANK-01 önce doğrulanmış server aggregate/history anchor tasarlamalı; increments yalnız kalıcı successful delivery claim ile aynı transaction'da, loan veya replay ile değil. Existing history backfill explicit migration/kanıt olmadan yapılamaz. XP/level eşikleri UNKNOWN; draft requiredLevel için gerçek sayı seçmez.

Banka absent/current/loan state'in kısmi veya çelişkili olması fail-closed; read auto-create/repair yapmaz. Active anchor ile farklı loan active olamaz; bütün origination/closure yazarları anchor'ı aynı transaction'da okuyup günceller. Read current loan doğrudan anchor ID'sinden doğrulanır; arbitrary client path izlenmez.

## PROPOSED — API taslakları (henüz export yok)

`getMyBankState`: authenticated `{protocolVersion:1}`; optional offer/history list gerekiyorsa ayrı bounded document-ID pagination max20. Response protocolVersion/fresh serverTime/result; result activeLoan veya null, principalRemaining/feeRemaining/totalRemaining minor-unit string, pinned repaymentBps/reserveFloor, eligibility code ve bounded server offer DTO. Wallet ikinci kez saklanmaz; authorization ve invariant kontrolü gerekir. Read offer publish/refresh, accrual veya repayment yapmaz.

`takeCompanyLoan`:
```json
{"protocolVersion":1,"idempotencyKey":"candidate_take_001","payload":{"offerId":"server_offer_id"}}
```
Payload exactly offerId; client principal/rate/fee/UID/company/expiry/level yok. Foundation/starter, completed-history/level/credit limit, no active/unpaid liability, no forbidden reborrow, exact version/hash, serverNow<expiresAt, permanent source absence, wallet upper bound doğrulanır. Tek commit: canonical wallet +principal, fixed liability record, bank anchor, consumed offer, origination journal, permanent claim ve receipt. Fee nakit olarak payout'tan kesilmez; borca eklenir. Revenue/XP/completed count artmaz. Önerilen result loanId,principal,fixedCharge,totalLiability,currency,scale ve historical walletBalance; command serverTime receipt committedAt.

`repayCompanyLoan`:
```json
{"protocolVersion":1,"idempotencyKey":"candidate_repay_001","payload":{"loanId":"server_loan_id","requestedMinorUnits":"100000"}}
```
Requested amount yalnız ödeme niyetinin üst sınırıdır; authoritative debit değildir. Strict positive canonical decimal integer string, safe bound, no exponent/sign/fraction/leading zeros. Sunucu `min(request, outstanding, max(0, cash-reserveFloor))` uygular. Zero uygulanabilir tutar INSUFFICIENT_REPAYMENT_CAPACITY, paid loan yeni key ile LOAN_ALREADY_PAID; same-key historical success korunur. Loan/anchor ownership, pinned terms, wallet ve deterministic source uniqueness tek snapshot'ta doğrulanır. Erken ödeme ek charge yaratmaz; fixed fee rebate yok tercihi PROPOSED. Sonuç actualPayment/feePart/principalPart/outstanding/currency/scale, requested ile fark açık gösterilir.

Her command mevcut executeCommand pattern'ini kullanır: aynı key+intent aynı original result/serverTime; farklı intent IDEMPOTENCY_CONFLICT; farklı key aynı offer permanent claim/anchor ile reddedilir. Post-commit yanıt kaybı aynı key ile tekrar; client yeni key üretmemeli. Current owner/migration/state integrity replay sırasında da doğrulanır, fakat loan'ın artık paid olması başarılı eski receipt'i geçersiz kılan fresh precondition olarak kullanılmaz.

Önerilen structured errors: LOAN_NOT_ELIGIBLE, LOAN_ALREADY_ACTIVE, LOAN_ALREADY_PAID, LOAN_OFFER_EXPIRED, REBORROW_NOT_AUTHORIZED, INSUFFICIENT_REPAYMENT_CAPACITY, BANK_STATE_INVALID; mevcut auth/protocol/conflict/retry errors korunur. codes/messageKey TR/EN/DE; gizli state/path/stack dönmez. Hiçbiri bu fazda exports/errors tablosuna eklenmedi.

## PROPOSED — automatic settlement integration

Tam exact denklemler ECONOMY-00_DESIGN.md'dedir. Positive net profit üzerinden floor(bps) hedeflenir; reserve/debt caps uygulanır. Fee-first dağılımda feePart=min(payment,feeRemaining), principalPart=payment−feePart. Negative profit payment0; cashAfterOperations<0 ise bütün settlement reddedilir, emergency grant veya negative wallet yoktur.

Delivery reward'dan sonra bağımsız follow-up debit **yasaktır**. Future claimDeliveryReward settlement planı bütün reads'i tamamlar: auth/foundation, delivery+contract+cost quote/version, assignment, wallet/progression, bank anchor/active loan, settlement/repayment source uniqueness. Sonra aynı commit içinde delivery/contract complete, assets release, operating expenses, wallet net delta, debt components/paid lifecycle, repayment history, journal, eligible XP, permanent delivery claim ve receipt. Nested transaction ve ilk write sonrası read yoktur. Reconciliation read veya scheduler bağımsız para tahsil etmez.

Manual repayment ile delivery auto-payment aynı wallet/loan/anchor belgelerini okur/yazar. Firestore conflict retry güncel debt/cash ile planı yeniden hesaplar; payment=min'den fazla olamaz. Son borcu kapatan transaction anchor'ı null'a geçirir. Diğeri auto ise0 repayment ile normal settlement; manual fresh ise paid error, mevcut receipt replay ise historical success. İki cihaz origination aynı anchor ve permanent offer source üzerinde yarışır; tek payout. Bu garantiler için gerçek emulator concurrency/rollback/lost-response testleri BANK-01'de zorunludur; offline testler onların yerine geçmez.

## PROPOSED — ledger compatibility ve maliyet zamanı

Mevcut ledger schema1 strict, signed delta/balanceAfter ve tek source taşır; geçmiş satırlar yeniden yazılmaz. Öneri BACKEND-09A/BANK-01 öncesi ayrı schemaVersion2 journal record veya ayrı versioned journal collection kararı ve dual-reader testleri. Bir settlement journal toplam cashDelta ile breakdown içerir: grossReward,operatingCostsByCategory,priorCostsPaid,loanFeeRepayment,loanPrincipalRepayment; debtBefore/after ve config pins. Conservation doğrulanır; toplam+breakdown bağımsız iki kez wallet'a uygulanmaz. State/finance tek otoritedir. Append-only history raporlama kaynağıdır.

Start'ta gider tahsil edildiyse completion cashBefore bu debit sonrası olduğundan walletDelta=gross−unpaidCosts−repayment; repayment target için profit=gross−ALL authorizedCosts. Kullanıcının cashBefore+profit formülü tüm iş öncesi nakit için geçerlidir. Completion içindeki availableForRepayment=currentCash+gross−unpaidCosts−reserve; aynı upfront gider ikinci kez düşülmez. Start claim/journal ID'leri delivery terms'e pinlenip tek settlement'ta doğrulanmalı. Upfront reservation escrow chosen ise reserved amount authority ve release kuralı ayrıca tasarlanmalı; şu an ikinci wallet kurulmaz.

Eski schema1 delivery'leri eski reward-only kurallarıyla bitirmek ve sadece yeni accepted terms'e costs/bank policy pinlemek önerilir; aktif işlere retroaktif ücret yazılmaz. Origination anında var olan eski teslimatların auto-repayment kapsamı ayrıca onaylanmalı (öneri yalnız settlement-v2 işler). Drain/rollout ve reader compatibility testleri olmadan migration yok. Strict EconomyConfig'e alan eklemek veya hash'i değiştirmek mevcut ready state'i kırabilir; yeni catalog ayrı referanslar ve onaylı transition gerekir.

## DEFERRED / UNKNOWN — ürün ve güvenlik

Real principal/credit limit/fee/repayment/reserve/unlock, manual repayment UX, reborrow eligibility, salary/cost timing, negative-net offer policy, recovery, XP ve leaderboard metrics ayrı onay ister. Loan principal ve repayment hacmi gelir/XP/leaderboard farming kaynağı olamaz; server-earned positive operating outcome ölçülmelidir. Hiçbir borç starter confiscation veya passive penalty yaratmaz.

BANK-00 bu draft'ın domain/product review'udur; BANK-01 ancak BACKEND-09A settlement temelinden ve BANK-00 onayından sonra emulator-only uygulanır. Gerçek production IAM/App Check/rate limits, migration/backfill, backup/rollback ve release security ayrı BLOCK kapılarıdır. General dependency audit HOLD; local-only exception30 Ekim2026'da sona erer. Bu sözleşme istisnayı uzatmaz.
