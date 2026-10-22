# BANK-00 — Faz raporu

23 Ekim 2026. **BANK-00 FUNCTIONAL GATE: PASS (design/offline). GENERAL SECURITY: HOLD. PRODUCTION: BLOCK.** Sayısal ürün değerleri onaylanmadı; sonraki fazlar başlamadı.

## IMPLEMENTED — taban ve kapsam

Root D:/Projects/unitylogisticore doğrulandı; başlangıç feature/economy-00 çalışma ağacı temizdi. Fetch sonrası origin/main beklenen ECONOMY-00 merge0f60f0abbfb9a49f4fbaa20d52cb4c47588e0da6 ile eşleşti. Main yalnız fast-forward edildi, feature/bank-00-policy oluşturuldu. Unity geliştiricisinin kaynakları ve mevcut runtime korundu. Reset/discard/force/merge-PR/deploy yok.

BACKEND-00..08B rapor/sözleşme mimarisi; özellikle canonical finance/progression, executeCommand receipt/claim, delivery settlement, commerce planı, local guard/launcher ve23 Ekim dependency reassessment incelendi. ECONOMY-00 model/tests/design/report tarihsel olarak korundu. Runtime banka endpoint, Firestore schema/rules, dependencies/lockfile, Unity veya gerçek veri değişmedi.

## APPROVED PRODUCT POLICY — yeni yön

Hazır şirket onboarding sonrası sıfır teslimatta kredi isteyebilir. Canonical level sınırında oyuncu principal seçer. Tek aktif borç; tam payoff sonrası beklemeden tekrar kredi. Teslimat unlock, cooldown, lifetime everBorrowed yasağı yok. Level-up yalnız sonraki kredi limitini etkiler; mevcut şartlar pinli kalır. VIP etkisiz/ertelenmiş. Partially initialized veya migration-blocked state eligible değildir.

## IMPLEMENTED offline / PROPOSED ekonomi

Ayrı bank-00-v1 model: strict versioned tiers/min/step/fee/wallet bounds; trusted readiness fixture; active pointer ve sequence; her kredi için principal/fee kalanları; lifetime financial aggregates; fee-first otomatik/manuel capped ödeme; immutable paid history; config drift/overflow/conservation checks. Bunlar server güvenlik mekanizması değildir.

Onaysız tier adayları 1–4:20000,5–9:35000,10–19:60000,20–50:120000 LGC. Min/step1000, fixed8%, repay35%, reserve5000; minFee0.01 rounding floor. Bütün tutarlar exact minor units, BigInt ara işlem. MaxBalance fixture ayrı; BANK-01 canonical economy limitini kullanmalı.

| Net kâr | Borçsuz edinim | Sıfır işte kredi | Beşinci işte kredi | İhtiyaç anında kredi |
|---|---|---|---|---|
|2500|16|13|10|8. iş →8|
|3100|13|10|8|7. iş →7|
|3800|11|9|6|6. iş →6|

Base 10000 kısmi kredi day-zero edinim14, fifth13; 20000 kredi day-zero10, fifth8. Erken kesinti nakit edinimini geciktirebilir; kredi özgürlüğü her stratejiyi avantajlı kılmaz. Aynı30 işte tam ödenen20000 kredi net1600 fee maliyeti taşır. Loan principal gelir/XP değildir; repayment ikinci operasyon gideri değildir.

100000 başlangıç nakdiyle teslimat yapmadan20000/35000/60000 principal döngüleri sonunda nakit90800, borç0, toplam ücret9200; pozitif servet oluşmaz. 100 küçük kredi döngüsü de testli. Paid loan eski terms'i ve source'u değişmez, yeni sequence ayrı kimliktir. Level1 kredisi sırasında level5'e çıkmak mevcut21600 liability'yi değiştirmez, sonraki limit35000 olur.

Kredi+net−400 zarar86 iş sonrası cash600 ve upfront800 engeline ulaşır; debt21600 kalır. Reserve zararı kapatmaz, bailout yok. Gerçek bekleme/offer supply için rapor yalnız kapasite alt sınırı verir; ikinci filo otomatik gelir çarpanı değildir.

## PROPOSED — sözleşme değişikliği

getMyBankState read-only; quoteCompanyLoan requestedMinorUnits intent'ini server doğrular, offerId+terms/hash üretir; takeCompanyLoan yalnız offerId kullanır. Tek current quote, short TTL, replacement/cancel/expiry, canonical level/state/config pins ve bounded quote budget önerildi. Quote lifecycle anti-spam önlemidir, borç sonrası oyun cooldown'u değildir.

bank/current wallet değildir; current loan/quote pointer ve monotonic lifecycle anchor'dır. Payout+liability+claim+receipt veya repayment+closure atomik gelecekteki transaction'dadır. Historical same-key replay yeni kredi sonrasında eski sonucu korur; cross-key eski offer kalıcı claim ile tüketilemez. BANK-01 gerçek quote/transaction concurrency/lost-response/failure tests ister. Default-deny ve existing environment guard değişmedi.

## IMPLEMENTED — doğrulama

| Kontrol | Sonuç |
|---|---|
| BANK-00 offline model | PASS48, fail/skip0 |
| Tarihsel ECONOMY-00 model | PASS27, fail/skip0 |
| Birleşik model suite | PASS75 |
| Report determinism / checked-in reproduction | PASS |
| npm run validate build/typecheck | PASS |
| Mevcut backend unit | PASS21 |
| Auth/Firestore/Functions emulator | PASS50 test kaydı (10 üst+40 alt), fail/skip0 |
| Local proje | Yalnız demo-logisticore-local |
| Emulator shutdown | PASS; CLI success/shutdown ve port kontrolü |
| Git whitespace/protected paths/UTF-8/limited secret scan | PASS |
| Yeni banka Firebase transaction testleri | NOT RUN / henüz implementation yok |
| Production/real DEV/deploy/migration | NOT RUN / BLOCK |

Komutlar: repository kökünden `node --test backend/tools/economy-sim/bank-model.test.mjs backend/tools/economy-sim/model.test.mjs`; `node backend/tools/economy-sim/bank-report.mjs`; backend altında `npm run validate` exit0. Ignored kanıtlar backend/.local/bank-00-model-tests.log ve bank-00-backend-validation.log. Model immutability testleri Firebase transaction güvenliği kanıtı değildir; mevcut emulator suite banka implementation test etmez.

Bağımlılıklar kurulmadı/değişmedi. Lockfile SHA2563C2CC424D6651A69181A15A5D6ADDD8E4994983E23DB4B8745B794E4968C97D6. Aynı23 Ekim BACKEND-08B security reassessment ve16–30 Ekim insan onayı dar trusted LOCAL koşullarında korunur. BANK-00'da yeni npm audit çalıştırılmadı; tarihsel runtime0/full3 high+4 moderate ve üç advisory GHSA-vfj7-8cjw-p6xm, GHSA-8988-4f7v-96qf, GHSA-w5hq-g745-h8pq çözülmüş sayılmaz. General HOLD, production/CI/untrusted BLOCK; süre uzatılmadı.

## IMPLEMENTED — dosyalar

- backend/tools/economy-sim/bank-model.mjs (yeni)
- backend/tools/economy-sim/bank-model.test.mjs (yeni)
- backend/tools/economy-sim/bank-report.mjs (yeni)
- backend/tools/economy-sim/README.md (ayrı BANK-00 komutları)
- docs/api-contracts/BANK_CONTRACT_DRAFT.md (yeni ürün politikasına göre revize)
- docs/backend/BANK-00_POLICY.md (yeni)
- docs/backend/BANK-00_SIMULATION_REPORT.md (yeni, deterministic generated)
- docs/backend/BANK-00_REPORT.md (yeni)

## DEFERRED / UNKNOWN — kalan kararlar

Final prices/tiers/min/step/fee/minFee/repay/reserve/maxBalance/XP thresholds, fee allocation/rebate, cost timing/recovery, quote TTL/budget, legacy active-delivery rollout ve anchor bootstrap policy ayrı inceleme ister. Sayısal onay verilmedi. Mevcut borçların immutable terms'i korunur; config migration bu fazda yok.

BACKEND-09A için tasarım girdileri hazır, cost timing/journal/compatibility review gerekir. BANK-01 readiness HOLD: BANK-00 incelemesi ve BACKEND-09A net settlement temeli şart. Ardından BACKEND-09B ve BACKEND-10 gelir. Hiçbir sonraki faz başlatılmadı. Yalnız bu sekiz dosya commit/push/main PR kapsamındadır; commit SHA/PR link final yanıtta, merge/deploy yok.
