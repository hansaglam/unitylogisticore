# BACKEND-09A — Authoritative Operating Costs & Net Settlement

23 Ekim 2026. Yalnız güvenilir LOCAL/EMULATOR, demo-logisticore-local. **FUNCTIONAL GATE PASS; LOCAL SECURITY CONDITIONAL PASS; GENERAL SECURITY HOLD; PRODUCTION BLOCK.**

## Başlangıç ve güvenlik kapısı

D:/Projects/unitylogisticore ve temiz feature/bank-00-policy doğrulandı. Origin/main beklenen BANK-00 merge563e1fb4f499d9eee26340d79a6d096387700771 ile eşleşti; main fast-forward ve feature/backend-09a oluşturuldu. Teammate/Unity değişiklikleri yoktu; reset/discard/force/Unity branch checkout yapılmadı.

Koddan önce23 Ekim BACKEND-08B reassessment ve16–30 Ekim insan onaylı yerel istisna incelendi. Aynı lockfile/launcher/guard/default-deny; yeni dependency, telemetry, dış servis veya güven sınırı yok. Runtime audit0; full audit3 high+4 moderate, aynı üç advisory. İstisna uzatılmadı. Gerçek Firebase DEV/PROD, gerçek oyuncu, deploy/migration veya untrusted CI kullanılmadı.

## IMPLEMENTED — en küçük uyumlu extension

Mevcut economy/economyModel, contractModel/eligibility/contracts, deliveryModel/deliveries, fleet/logisticsContext, commands ve commerce akışları incelendi. Canonical finance yazarı planEconomyMutation, permanent delivery source/claim ve executeCommand receipt korunur. Yeni public endpoint, bank collection/anchor/loan writer yoktur.

- operatingCosts.ts: strict synthetic catalog; economy/logistics hash pin; bounded template/route/cargo applicability; dört kategori, BigInt toplam, positive-net policy; pinned catalog doğrulaması ve safe DTO.
- Contract catalog/pool/contract schema2 explicit cost snapshot taşır. Schema1 exact reader/ID/hash/result/fingerprint korunur; missing v2 maliyeti sıfır sayılmaz.
- Start accepted snapshot/cost/version ve equipment kontrolünden sonra yalnız reservation+delivery+contract+receipt yazar. Sıfır wallet ile start mümkündür; wallet/XP/finance claim değişmez.
- Claim gross−totalCost kadar tek net credit uygular. Schema1 ledger alanları aynı, v2 reason delivery_net_reward. Ayrı settlementJournals kaydı aynı transaction'da immutable explanation sağlar.
- Completed v2 read journal/ledger/claim/source/amount/timestamp tutarlılığını doğrular; repair veya payment yapmaz.
- Yeni DB yardımcıları local guard'ı ilk DB erişiminden önce çalıştırır. Existing environment guard/launcher ve callable exports değişmedi.

## Net muhasebe ve örnekler

UNAPPROVED TEST CANDIDATE: gross450000 minor units−fuel70000−driver40000−maintenance20000−tolls10000=net310000. Para float değil exact safe integer; sum BigInt ara işlem ve maxCost/economy.maxBalance kontrolü. Gerçek km/specs/tüketim/maaş takvimi yok.

Emulator küçük TST fixture: gross6−(1+1+1+1)=net2. İlk v2 claim balance0→2, ledger.delta2, journal önceki/sonraki bakiye0/2; XP10 ve level2. Minimal net1 fixture wallet9999→10000 sınırına tam ulaşır. Legacy gross6 fixture balance100→106; yeni journal yaratılmaz.

Yeni templates için total>=gross bütün pool generation'ı writes öncesi reddeder; fiyat değiştirerek kârlı hale getirmez. Accepted quote latest katalogla yeniden fiyatlanmaz: pinli version değişirse fail-closed, farklı version otomatik seçilmez. Pool max5/acceptedCount/full termsHashes korunur. Accepted expiry start/claim'i iptal etmez.

Source delivery_{deliveryId}, operationId existing SHA256(companyId+':'+source). Journal/ledger/claim aynı kaynak kimliğiyle bağlıdır; ikinci financial source veya wallet yok. Bütün reads/prepare/arrival/bounds ilk write öncesi tamamlanır; writes tek transaction, nested transaction yok. Runtime ekonomi, asset release, contract/delivery completion, journal ve receipt birlikte commit/rollback olur.

## Read API ve BANK-01 sınırı

getMyContracts/getMyDeliveries ile v2 accept/claim result ek settlement DTO içerir. reward gross anlamını korur; netReward ve kategori miktarları decimal minor-unit string'dir. V1 alanları unchanged. getMyEconomyState değişmedi; read pagination1..20 ve read-only davranışı korunur.

Internal settlementAccounting: grossRevenue,totalOperatingExpenses,netOperatingProfit,finalWalletDelta,eligibleXp,source. BANK-01 gelecekte aynı transaction içinde net profit→capped repayment→final wallet delta planı ekleyebilir. Bank callable/loan/quote/debt/repayment bu fazda yok. Eski journal rewrite edilmez; bank-aware yeni journal version/reader ve legacy drain ayrı review ister.

## Test kanıtları

İlk npm run validate exit0: build/typecheck PASS, **25 unit PASS**, **65 emulator test kaydı PASS** (11 üst grup+54 alt test), fail/skip0. Yeni settlement grubunda14 alt test; önceki50 kayıt korunur. Sonrasında yeni internal DB yardımcılarına local guard ve buna ait ek unit testi eklendi. Nihai npm run validate tekrar exit0: build/typecheck PASS, 26 unit PASS, 65 emulator test kaydı PASS; fail/skip0. Emülatörler kapandı;9099/8080/9150/5001/4400/4500/discovery8257 portları boş doğrulandı.

Gerçek Firestore transaction yarışları:

- Aynı v2 delivery için8 farklı key eşzamanlı claim: yalnız1 başarı, tek net ledger/claim/journal, tek asset release.
- Aynı wallet üzerinde farklı iki active delivery'nin eşzamanlı claim'i: ikisi başarılı, iki net kazanç korunur; journal balanceAfter sıralı ve kayıpsız.
- Mevcut fleet/delivery/commerce concurrency regresyonları korunur.

Kontrollü fault injection ayrı kanıttır: bütün queued writes sonrası exception ile rollback; SDK aborted code10 ile gerçek transaction callback retry; commit sonrası receipt read code14 ile kayıp yanıt ve aynı-key recovery. Bunlar gerçek network kesintisi veya rastgele contention ölçümü olarak sunulmaz.

Coverage: legacy generate/accept/start/claim/replay; v2 valid/minimal net; equal/excess cost reject; missing/unapproved/drifted catalog; economy/logistics/currency/scale mismatch; unsafe/negative/fractional amounts ve unit BigInt overflow; pool/quote corruption; zero upfront; early/exact arrival; wallet/XP overflow; orphan journal/ledger/claim; stale assignment; same/different key; Auth/Functions DTO/replay/spoof/cross-company; default-deny yeni catalog/journal paths. Test cleanup yalnız bu suite'in sentetik UID ve tracked kayıtlarını siler.

Komut: backend altında npm run validate. Ignored kanıt: backend/.local/backend-09a-validation.log; nihai run backend-09a-final-validation.log. Production/device/migration tests NOT RUN/BLOCK. Offline ECONOMY-00/BANK-00 modelleri bu fazda değiştirilmedi; bu faz sonuçları gerçek emulator testleridir.

## Audit ve korunmuş kapsam

Runtime: npm audit --omit=dev --json --fetch-retries=0 --fetch-timeout=20000. Full: npm audit --json --fetch-retries=0 --fetch-timeout=20000. İlk ve nihai audit aynı: exit0/runtime0 ve exit1/full3 high+4 moderate. Nihai kanıtlar backend/.local/backend-09a-runtime-audit.json ve backend-09a-full-audit.json; registry erişimi başarılı, yeni advisory yok. Yedi package finding, üç advisory'den türemiştir; yedi ayrı exploit iddiası yok.

- GHSA-vfj7-8cjw-p6xm
- GHSA-8988-4f7v-96qf
- GHSA-w5hq-g745-h8pq

Ayrıntılı erişilebilirlik/compensating controls BACKEND-08B_SECURITY_REASSESSMENT.md'de; aynı trusted LOCAL koşulları. Lockfile SHA2563C2CC424D6651A69181A15A5D6ADDD8E4994983E23DB4B8745B794E4968C97D6. npm install/ci/audit fix/upgrade/suppression yok. General audit HOLD; CI/untrusted/production onayı yok;30 Ekim'de yeniden karar gerekir.

Unity Assets/Packages/ProjectSettings, C#, dependency manifests/lockfile, firestore.rules, firebase config, launcher, environment guard ve index exports baseline karşısında değişmedi. Git whitespace/diff review,18 dosyada strict UTF-8 ve limited secret-pattern scan PASS. Final shutdown ve port kontrolleri PASS. Secret scan sınırlı pattern incelemesidir, tüm geçmişi kapsayan denetim değildir.

## Firestore maliyeti — tahmini delta

V1 ek read/write yok. V2 generation/accept +1 catalog read; start +2 point reads; active claim +3 point reads ve+1 journal write. Completed v2 delivery read +5 point reads; active read +2. Max page20/pool5/catalog5 korunur; per-page tekrarlar bounded fakat shared-cache optimizasyonu yok. Retry/index/transport ek maliyeti ve production faturası ölçülmedi. Receipt overhead ve common canonical writer aynı.

## Dosyalar

Değişen source: contractEligibility.ts, contractModel.ts, contracts.ts, deliveries.ts, deliveryModel.ts, economy.ts. Yeni source: operatingCosts.ts, settlement.ts. Test: unit.test.mjs, emulators.test.mjs; yeni settlement.fixtures.mjs ve settlement.emulators.test.mjs. Belgeler: yeni OPERATING_COST_CONTRACT.md, SETTLEMENT_CONTRACT.md, BACKEND-09A_REPORT.md; CONTRACT_CONTRACT.md, DELIVERY_CONTRACT.md, ECONOMY_CONTRACT.md ekleri. Toplam18 dosya; yalnız backend/ ve izinli docs dizinleri.

## Kalan kararlar / readiness

Gerçek fiyatlar, route-specific costs, XP/denge, dinamik yakıt/maaş, cancelled/failed delivery costs/refunds, recovery ve rollout/migration ertelendi. Positive net+net-at-claim upfront liquidity riskini azaltır; max wallet/XP veya catalog drift hâlâ claim'i bloke edebilir. Otomatik bailout/repair yok.

BANK-01 için net accounting/prepare-apply/journal temeli hazır; bu PR incelemesi ve ayrı faz yetkilendirmesi gerekir. BANK-00'ın no-wait/selected amount/one active loan/immediate reborrow politikası değiştirilmedi. BANK-01 başlatılmadı. Production/real DEV/deploy/migration/untrusted CI BLOCK. Commit/push/main hedefli PR yalnız final functional gate sonrasında; merge/deploy yapılmaz.
