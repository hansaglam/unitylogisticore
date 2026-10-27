# BACKEND-10 — LOCAL LiveOps ve dünya olayları

Tarih: 26 Ekim 2026. Branch: `feature/backend-10-liveops`. Başlangıç: main/BACKEND-09B merge `e6d2682154d6ba66fd74c40afc36401adf4b3bef`; temiz çalışma ağacından türetildi. Bu belge yalnız sentetik LOCAL/emulator kapsamını değerlendirir.

## Mimari ve fiyat kanıtı

Mevcut market, contract, eligibility, cost, delivery, settlement, bank, command, logistics ve environment guard akışları incelendi. Yeni `liveOpsModel.ts` saf katalog/event/fiyat doğrulaması; `liveOps.ts` salt okunur API sağlar. Mevcut server-owned `v2MarketSelection/{economyVersion}` V4/V5 config seçer. V5 config immutable `v2LiveOpsCatalog/{version}` hash'ini sabitler. Public event publishing, scheduler, global counter veya activation write yoktur.

Katalog economy/logistics/market/policy hash, para birimi/scale, event/combined sınırları ve en fazla16 event taşır. Exact alan kontrolü, kanonik city/cargo, benzersiz ID, güvenli UTC zamanları, en fazla31 günlük süre, startsAt<endsAt ve localization ID zorunludur. Üç sentetik tür: pozitif regional_demand_surge, negatif logistics_congestion, signed seasonal_trade_activity. Congestion yalnız fiyat koşuludur; yol kapanması/gecikme/ceza değildir.

Aktiflik `startsAt <= trustedNow < endsAt`. Uygun destination/cargo için en yüksek priority, eşitlikte ASCII olarak en küçük eventId seçilir. Tek finansal modifier vardır; event'ler üst üste çarpılmaz. Bilgi amaçlı birden fazla event aktif olabilir.

`combinedBps = clamp(marketBps + eventBps, market.minBps, market.maxBps)`; `finalGross = floor(baseGross * (10000 + combinedBps) / 10000)`. BigInt ara hesap ve tek floor; combined envelope pinned market sınırlarıyla aynı kalır. Market-only gross ayrıca korunur. Base101/market250bps/event250bps için market103, final106; iki kez yuvarlama105 olurdu. Event için ikinci wallet credit yoktur.

V1 gross-only, V2 cost, V3 bank, V4 market yolları korunur. V4 reward==market-only gross invariant'ı gevşetilmedi. V5 contract/pricing ayrı sürümdür: reward==liveOps.finalGrossReward; market.adjustedGrossReward market-only kalır; cost quote gross==reward ve net==gross-cost. **Cost snapshot V2 ve settlement policy V3 değişmedi.**

V5 snapshot original selectedAt, catalog/policy hash, tam seçilen event veya açık null, market quote hash, base/market/final gross ve bütün bps kanıtlarını taşır. Acceptance/start/claim/completed read orijinal katalog ve zamanla yeniden hesaplar. Event sona ermesi fiyatı değiştirmez. Missing/drift katalog fail-closed; güncel katalogla fallback yoktur. No-event alanının eksik olması başarı değildir.

Generation retry ilk zaman, config/catalog hash ve enabled event'lerin upcoming/active/ended durumlarını sabitler. Aynı durumda aynı quote; event/market sınırı geçişi, zaman gerilemesi, hash değişmesi veya V5 pin kaldırılması reddedilir. Kısa event interval'ının tamamından atlama da reddedilir. Company/epoch/slot contract ID, acceptedCount, pool quota ve durable replay değişmez; event ek pool sağlamaz.

## API, banka ve sınırlar

`getMyWorldEvents`: Auth + exact protocolVersion1 + canonical ready company; LOCAL guard DB'den önce. Read-only transaction, write0. Yanıt synthetic=true, serverTime, catalog/policy version, currentEventIds ve güvenli event DTO'larıdır. Enabled, başlangıcı sonraki7 gün içinde veya geçmişte ve bitişi son24 saatten sonra olan en fazla16 kayıt döner. Seed/path/admin/wallet/loan açılmaz. V5 selector altında getMyMarketState market-only anlamını korur. V4-only selector LiveOps API için fail-closed olur.

Banka beş public endpoint'i, loan principal/fee/limit/reserve ve claim anındaki current-loan seçimi değişmedi. Final gross→cost→positive net→capped repayment→final wallet delta tek mevcut settlement transaction'ındadır. Wallet/XP/ledger/claim/journal/loan/history/asset release/receipt atomik kalır. Event XP, bağımsız ödül, ikinci wallet veya debt forgiveness üretmez.

## 55 kabul kategorisinin kanıt eşlemesi

L1..L21 yeni `liveops.emulators.test.mjs` alt testleridir. Gerçek Auth/Firestore kullanılır; L1 gerçek Functions callable'larını da çağırır. Concurrent testler gerçek paralel SDK transaction'larıdır. L14/L19 code10 ABORTED ve L15 postcommit read fault kontrollü hata enjeksiyonudur; gerçek yarış olarak sunulmaz.

| İstek kategorileri | Kanıt |
| --- | --- |
| 1 unauthenticated, 5 client override, 6 read-only, 7 statuses, 9 private seed, 10 market callable | L1 HTTP ve recursive state/updateTime karşılaştırması |
| 2 incomplete, 3 migration-blocked | L16 |
| 4 canonical isolation | L6 foreign accept ve aynı katalog/farklı şirket |
| 8 default-deny Rules | Mevcut Rules suite'e LiveOps get/list/set ret hedefi eklendi |
| 11 positive, 12 negative, 13 no match, 14 start, 15 expiry, 16 overlap, 17 tie, 18 cap | L17 sekiz varyant ve unit priority/half-open/scope/cap |
| 19 catalog missing/corrupt, 20 wrong hash | L18 read/generate/lifecycle, L2 ve unit strict pins |
| 21 V1, 22 V2, 23 V3, 24 V4 | Mevcut delivery/settlement/BANK/market regression suite'leri |
| 25 V5 terms, 26 positive net | L3/L4/L17/L21; unit final cost quote |
| 27 immutable acceptance, 28 expired before start, 29 expired before claim | L7 original event1000bps, zaman ilerletme ve exact snapshot |
| 30 pinned drift | L18 accept/start/claim/completed read no-write ret |
| 31 eight generation, 32 same-key, 33 different-key | L5 gerçek8 istek, tekpool/3contract ve receipt replay |
| 34 quota, 35 IDs | L20 acceptedCount2 ve quota ret; L17 company/epoch/slot ID |
| 36 cost once, 37 atomic bank | L7/L9/L12; V3 policy/schema2 journal korunur |
| 38 manual-vs-claim | L10 partial/payoff gerçek Promise.allSettled yarışı |
| 39 two deliveries | L11 ayrı asset setleri; wallet309/debt0, iki journal |
| 40 eight claims | L12 tekpayout, wallet209 ve tekhistory |
| 41 XP, 42 final wallet cap | L7 trusted XP10; L13 intermediate10036/final9999, overflow/XP ret |
| 43 ledger/journal/claim/receipt, 44 no second bonus | L9/L10/L11/L12/L14 finansal conservation |
| 45 historical completed | L7/L18 original evidence; expiry fiyat değiştirmez |
| 46 rollback, 47 SDK retry | L14 queued-write rollback ve aynı-durum original quote/time |
| 48 boundary retry | L19 startsAt/endsAt/tüm interval, hash drift ve V5→V4 downgrade ret |
| 49 lost response | L15 postcommit receipt read fault, original result/write0 replay |
| 50 unsupported schema/policy | L2/L18/unit strict catalog ve snapshot |
| 51 dependencies, 52 Unity | Son Git diff ve lock hash kontrolleri |
| 53 BANK, 54 market, 55 commerce/fleet/delivery | Tam validate koşusu; sonuçlar aşağıda |

## Simülasyon ve Firestore maliyeti

[Simülasyon raporu](BACKEND-10_SIMULATION_REPORT.md):24 deterministik senaryo,8 test PASS. Tarihsel economy/BANK/market85 test PASS; eski artefaktlar değişmedi. İkinci filo nötr13, pozitif12, negatif16 iş;10–15 benchmark koşulsuz hedef değildir. Aktif kredi senaryosunda9 iş; loan gelir sayılmaz, fee maliyettir. İki teklif/kısa süre48 tamamlanma, uzun süre10; arz/süre etkisi event'ten büyük olabilir. Expiry sonrası original495000 gross korunur. Her settlement cash+capital-debt=initial+profits-fees doğrular. Zero supply/tiny profit borcu kalıcı bırakabilir. XP/level/session/gerçek trafik modellenmez.

Statik tahmin, production billing ölçümü değildir: event API18 point read/0write; V5 market API18 (V4:17). V4'e göre generation+1 LiveOps read, N+2 mevcut writes; acceptance+1 read/3 mevcut writes; start+1 read/mevcut writes; claim+1 read/mevcut V3 writes; historical item+1 read/0write. Bounded20 historical item en çok20 ek katalog okuması. Retry/platform minimum billing ayrıca; global scan/hot counter/per-tick write yoktur.

## Güvenlik kapsamı ve audit

Koddan önce mevcut16–30 Ekim2026 insan onaylı LOCAL istisnası ve gerçek npm audit yeniden kontrol edildi. Node22, Admin14.5.0, Functions7.4.0, CLI15.34.0 aynı. İlk ve son audit: runtime0/exit0; full5 moderate affected package/exit1,2 distinct mevcut advisory: GHSA-8988-4f7v-96qf ve GHSA-w5hq-g745-h8pq. Etkilenen paketler firebase-tools, @google-cloud/pubsub, @opentelemetry/core, gaxios ve uuid. Paket sayısı exploit sayısı değildir. Yeni advisory yok. Advisory'ler bastırılmadı; bağımlılık/lock/scan ayarı değişmedi. npm önerdiği CLI14 downgrade'i uygulanmadı; bu faz bağımlılık remediation kapsamı değildir.

İstisna yalnız güvenilir yerel bilgisayar, sabit bağımlılıklar, demo-logisticore-local ve mevcut izolasyon koşulları içindir; otomatik uzatılmaz. General audit HOLD; gerçek DEV/production/untrusted CI/deploy/migration BLOCK. Yeni event endpoint mevcut LOCAL/auth/read-only güven sınırındadır; harici servis/publishing yetkisi eklemez. Default-deny Rules, secure launcher ve credential guards değişmedi.

## Doğrulama kayıtları ve faz kapısı

İlk check build/typecheck ve43 unit PASS. İlk full validate: unit43/43; emulator134 node içinde128 PASS/6 FAIL/0 SKIP, exit1. Commerce, BANK ve yeni LiveOps L1 callable testlerinde Functions worker başlatma/HTTP timeout sorunu tekrarlandı; üç parent suite hatası da6 sayısına dahildir. LiveOps L2..L21 geçti. Önceki BACKEND-09B Windows startup kararsızlığıyla benzer belirtiler vardır, kök neden doğrulanmış değildir. Bu ara koşu son V5→V4 retry guard/test eklenmeden başlatıldığı için son kaynak kanıtı değildir. Timeout/guard/dependency gevşetilmedi. Farklı koşuların kısmi başarıları full PASS olarak birleştirilmeyecektir. Emülatörler kendi shutdown akışıyla kapandı.

Son kaynaklarla ikinci tam `npm run validate`: **exit1**, build/typecheck PASS; **unit43 PASS/0 FAIL/0 SKIP**; **emulator134 node:123 PASS/11 FAIL/0 CANCELLED/0 SKIP**. Altı başarısız callable testi: company bootstrap eşzamanlı createCompany HTTP isteği, economy read, commerce callable, BANK public wrappers, market M1 ve LiveOps L1. Beş parent suite hatası da11 toplamına dahildir. HTTP süre sınırları ve CLI `Failed to load function` kayıtları vardır. Commerce worker için debug log, hata kaydından sonra runtime initialization/listening bildirimi gösterir; sistemsel gecikmenin kesin kök nedeni bu fazda çözülemedi. Başarısızlıkları güvenlik/işlev kabulünden çıkarmak veya timeout artırmak yoluna gidilmedi.

Son koşuda yeni **L2..L21 yirmi alt test PASS**; L19 son V5→V4 downgrade guard'ını, event başlangıç/bitiş/tüm-interval geçişini ve catalog drift retlerini kapsar. L1 ilk callable çağrısında başarısız olduğundan onun sonraki auth/spoof/read-only/status/market-compatibility HTTP assertion'ları bu koşuda tamamlanmadı. Saf model/unit ve diğer Firestore kontrolleri bunların yerine tam callable PASS kanıtı sayılmaz. Legacy BANK/market suite'leri de ilgili callable failure nedeniyle bütün olarak PASS değildir. Bu nedenle bütün55 kabul kategorisi tamamlandı iddiası yoktur.

| Kontrol | Son sonuç |
| --- | --- |
| Build / typecheck / environment unit guards | PASS |
| Unit | PASS43/43 |
| Tam Auth/Firestore/Functions emulator | FAIL:123 PASS/11 FAIL,134 total |
| Yeni LiveOps alt testleri | L2..L21 PASS; L1 FAIL; parent FAIL |
| Default-deny Rules | PASS; LiveOps catalog get/list/set dahil |
| Tarihsel economy/BANK/market offline | PASS85/85 |
| LiveOps offline | PASS8/8;24 deterministik senaryo |
| Runtime audit | PASS0; exit0 |
| Full audit | HOLD5 moderate/2 mevcut advisory; exit1 |
| Unity/dependencies/lock/guard/launcher/Rules kaynak korunumu | PASS; diff boş, lock SHA256 aynı |
| Değişen/yeni23 dosya UTF-8 / whitespace / limited secret-pattern | PASS;0 bulgu; geçmişin tamamına yönelik secret audit değildir |
| Emulator shutdown / port kontrolü | PASS;9099/8080/5001/4400/4500/9150/8658/8282 bind kontrolünde boş |

**BACKEND-10 FUNCTIONAL GATE: HOLD.** LOCAL dependency security mevcut kapsamda **CONDITIONAL PASS**, general audit **HOLD**, production/migration **BLOCK**. İşlevsel kabul verilmedi. Sonraki adım Windows Functions worker/HTTP başlangıç kararsızlığını ayrı ve kanıta dayalı incelemek, ardından hiçbir test atlamadan tek temiz full validate almaktır. İki başarısız koşunun kısmi sonuçları birleştirilmedi. Commit/push/PR **NOT RUN**;23 dosya feature/backend-10-liveops üzerinde unstaged olarak korunur. Merge/deploy/migration veya sonraki faz yapılmadı.

Ignored `.local/backend-10-*` logları yerel kanıttır; raw emulator logları/token'lar commit edilmez. İlk validation.log ve ikinci validation-final.log/final-exit.json ayrı tutuldu. Son koşu26 Ekim2026 22:23:46+03:00'te exit1 ile tamamlandı. Offline/liveops-sim logları ve runtime/full-audit-final JSON'ları ayrı kanıttır. Lock SHA256 başlangıç ve son: `AA1F6BA0D7A5CCAC1C9551A3941B8F1A2FD66B7482FD487C5C4D354ACBC5CC9F`.

Çalıştırılan komutlar (`backend/`):

```powershell
npm run validate
node --test tools/economy-sim/model.test.mjs tools/economy-sim/bank-model.test.mjs tools/market-sim/model.test.mjs
node --test tools/liveops-sim/model.test.mjs
npm audit --omit=dev --json --fetch-retries=0 --fetch-timeout=20000
npm audit --json --fetch-retries=0 --fetch-timeout=20000
```

Simulator raporunu yeniden üretmek için build ardından `node tools/liveops-sim/report.mjs` stdout kullanılabilir. Bu fazda npm ci/install yapılmadı; reproducible install için yeni kanıt iddia edilmez.

## Onay bekleyen ürün kararları

Event schedule/priority/scope, ±bps, -1000/+1500 global envelope, market period ve offer supply sentetik/onaysız adaylardır. Fixed cost nedeniyle gross bonusu net kârı daha yüksek oranda artırabilir; uzun dönem inflation/credit/fleet throughput ayrıca incelenmelidir. Gerçek veri, roads/weather/penalties/grants/VIP/interest/admin console/push/scheduler bu fazda yoktur. Sonraki öneri immutable katalog yayınlama/onay/aktivasyon/retention ve operasyonel güven sınırı tasarımıdır; yeni faz başlatılmadı.

## Dosya kapsamı

Toplam23 dosya:9 backend source,5 test/fixture,3 yeni offline simulator,6 doküman. Başlangıçta teammate değişikliği yoktu; Unity/Packages/ProjectSettings ve tarihsel simülasyonlar korunur.

```text
backend/functions/src/contractEligibility.ts
backend/functions/src/contractModel.ts
backend/functions/src/contracts.ts
backend/functions/src/deliveryModel.ts
backend/functions/src/index.ts
backend/functions/src/liveOps.ts
backend/functions/src/liveOpsModel.ts
backend/functions/src/market.ts
backend/functions/src/operatingCosts.ts
backend/tests/emulators.test.mjs
backend/tests/liveops.emulators.test.mjs
backend/tests/liveops.fixtures.mjs
backend/tests/liveops.unit.test.mjs
backend/tests/unit.test.mjs
backend/tools/liveops-sim/model.mjs
backend/tools/liveops-sim/model.test.mjs
backend/tools/liveops-sim/report.mjs
docs/api-contracts/DYNAMIC_PRICING_CONTRACT.md
docs/api-contracts/EVENT_PRICING_CONTRACT.md
docs/api-contracts/LIVEOPS_CONTRACT.md
docs/api-contracts/MARKET_CONTRACT.md
docs/backend/BACKEND-10_REPORT.md
docs/backend/BACKEND-10_SIMULATION_REPORT.md
```
