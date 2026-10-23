# SETTLEMENT_CONTRACT — BACKEND-09A

23 Ekim 2026. **IMPLEMENTED synthetic LOCAL/EMULATOR. GENERAL SECURITY HOLD; PRODUCTION BLOCK.** BANK-01 uygulanmadı.

## Authority ve version uyumu

CLIENT SENDS INTENT. BACKEND VALIDATES AND DECIDES. claimDeliveryReward aynı envelope/protocol/key ve yalnız `{deliveryId}` payload'ını korur. UID/company/amount/cost/rate/XP/time client girdisi olamaz. Trusted Auth → canonical foundation/ownership/migration → strict delivery/contract/pool/rules/cost integrity → equipment assignment → safe finance/progression.

V1 delivery/contract gross-only kalır, reason delivery_reward, journal yokluğu normaldir. V2 explicit schema/snapshot, reason delivery_net_reward ve zorunlu journal kullanır. Tarihsel v1/v2 receipt replay özgün result ve committed serverTime döndürür. Read serverTime tazedir. Fresh claim mutable source/state doğrular; historical receipt replay mevcut foundation authorization/consistency kontrolünden sonra eski sonucu verir, fiyatı yeniden hesaplayıp ikinci etki yaratmaz.

## Hesaplama ve prepare/apply

```text
totalOperatingExpenses = fuelCost + driverCost + maintenanceCost + tollCost
netOperatingProfit = grossReward - totalOperatingExpenses
finalWalletDelta = netOperatingProfit
newBalance = previousBalance + finalWalletDelta
newXp = previousXp + pinnedEligibleXp
```

Tüm miktarlar integer minor units; toplam BigInt ara işlem, stored değerler safe integer ve canonical maxBalance/maxXp sınırında. Reward source quantity×per-unit ve maxBalance kontrollerini korur. V2 üretimde net>0; XP delivery rules'daki yetkili miktardır, net/gross çarpanından türetilmez. Currency/scale/economy hash tam eşleşir. Harcama ve net gelir iki ayrı wallet hareketi değildir: bir net credit, başka expense debit yoktur.

Internal `settlementAccounting` grossRevenue,totalOperatingExpenses,netOperatingProfit,finalWalletDelta,eligibleXp,source döndürür. Çağıran validated canonical terms/rules vermelidir. `prepareDeliverySettlement` aynı transaction'da journal yokluğu ve existing prepareEconomyReward claim/ledger yokluğunu okur; planEconomyMutation canonical finance yazarı olarak kalır. Bütün reads ve planlar, arrival/bounds kontrolü ilk write'tan önce tamamlanır. Apply closure yalnız writes yapar; nested transaction, background payout veya dış side effect yoktur.

Tek commit:

- Delivery completed/claimed, contract completed.
- Exact assignment release ve ilgili asset assignmentId=null.
- Canonical finance net balance; progression XP/derived level.
- Existing schema1 ledger ve permanent delivery claim.
- V2 immutable settlement journal.
- Mevcut executor durable command receipt.

Any error/overflow/arrival öncesi claim hiçbir partial write bırakmaz. Retry her transaction attempt için güncel wallet ve aynı pinned terms ile planı tekrar hazırlar. Diğer delivery/commerce işlemleri aynı wallet'a yazdığında Firestore conflict/retry güncel bakiyeyi korur.

## Immutable accounting journal

Path `v2Companies/{companyId}/settlementJournals/{operationId}`. Create-only schema:

```text
schemaVersion:1, settlementVersion:2
companyId, deliveryId, source, operationId
termsHash, quote (exact pinned settlement object)
currency, scale, economyVersion, economyHash, logisticsVersion, logisticsHash
previousBalance, balanceAfter, eligibleXp, committedAt
```

Quote dört kategori/gross/total/net ile policy/costCatalog version+hash içerir. termsHash contract config pin'leri dahil full accepted terms'e bağlar. Timestamp gerçek server commit timestamp'tır. Journal ikinci wallet değildir; state/finance tek balance otoritesidir. Aynı commit'in ledger.delta=quote.netReward ve ledger.balanceAfter=journal.previousBalance+quote.netReward=journal.balanceAfter. Journal, ledger ve claim timestamp/source/operation eşleşir.

Source mevcut `delivery_{deliveryId}`; operationId SHA256(companyId+':'+source). İkinci claim veya yeni source yaratılmaz. Existing ledger schema1 exact fields değişmedi; yalnız yeni reason delivery_net_reward kullanılır. Yeni journal version ile gelecekte daha fazla breakdown eklenebilir; geçmiş schema1 journal veya ledger yerinde upgrade edilmez. V2 completed delivery read journal/ledger/claim tutarlılığını point-read ile doğrular; bozuk/eksik kayıt fail-closed, auto repair yoktur.

## Idempotency, concurrency, hata davranışı

Aynı key+intent historical result; aynı key farklı intent IDEMPOTENCY_CONFLICT. Farklı key aynı delivery status/claim/ledger/journal varlığı nedeniyle ikinci payment yapamaz. Kayıp response sonrası aynı key tekrar edilir. Kalıcı receipt/source/claim silinmez. Equipment release current assignmentId'yi doğrular; eski delivery başka atamayı bırakamaz. Catalog drift fail-closed; accepted sözleşme latest fiyatla değiştirilmez. Offer TTL kabul edilmiş işi iptal etmez.

Yeni public error code yok. Cost/catalog/schema/hash/amount/journal/bounds corruption FAILED_PRECONDITION (`errors.failed_precondition`); intent ekstra alan INVALID_ARGUMENT; mevcut auth/ownership/migration/DELIVERY_NOT_ARRIVED/DELIVERY_STATE_INVALID/IDEMPOTENCY_CONFLICT/RETRY_LATER korunur. TR/EN/DE UI sabit messageKey kullanır; private path/stack/raw accounting state hata içine konmaz.

## Read uyumu ve operasyon maliyeti

V1 getMyContracts/getMyDeliveries alanları değişmez. V2 ek settlement DTO'su gross ile neti açıkça ayırır; tüm para string'dir. getMyEconomyState API aynı. Read-only transaction, page1..20, limit+1 lookahead ve default-deny Rules korunur. Read finalize/pay/repair yapmaz. Journal/private bank/admin dokümanı public DTO'ya açılmaz.

V1 ek Firestore okuma/yazısı yok. V2 claim eski akışa göre yaklaşık **3 ek point read** (cost catalog, pinned logistics, journal absence) ve **1 ek write** (journal) yapar. Start+2 read, acceptance/generation+1 read. V2 completed delivery read+5 point read (cost/logistics+journal/ledger/claim); active delivery read+2. N<=20 list item için bu ek okumalar N ile bounded. Receipt sonrası existing1 read değişmedi; retry/index/network maliyeti ayrıca gelir. Per-document boyutları bounded config5 entries; command/result8192-byte ve mevcut transaction sınırları korunur. Gerçek production fatura/latency ölçümü değildir.

## BANK-01 sınırı

APPROVED yön: onboarding sonrası no-wait credit, selected principal/canonical level cap, bir aktif loan, payoff sonrası hemen reborrow. Bu faz loan collection/anchor/quote/loan callable/disbursement/repayment eklemez.

Future extension: `netOperatingProfit → capped loan repayment → finalWalletDelta`. Loan reads ve repayment planı aynı prepare aşamasında; debt reduction/closure, repayment history ve yeni version journal aynı delivery transaction'ına eklenmelidir. Teslimattan sonra ayrı tahsilat yasaktır. Mevcut journal'lar bank-aware hale getirilecek diye rewrite edilmez; future writer/reader explicit version dispatch ve legacy drain testleri gerekir.

## Sınırlamalar

Sentetik costs gerçek tüketim/maaş değildir. Failed/cancelled refund/penalty, dynamic economy, bank, production/migration/backfill, gerçek kullanıcı ve cihaz testleri yok. Positive net wallet/XP cap soft-lock'unu tamamen çözmez; otomatik rescue grant/repair yok. Dar LOCAL exception30 Ekim2026'da sona erer; general HOLD, gerçek DEV/production/untrusted CI/deploy/migration BLOCK.
