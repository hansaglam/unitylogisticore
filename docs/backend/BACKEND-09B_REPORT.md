# BACKEND-09B — Dynamic Regional Economy final raporu

26 Ekim 2026. Kapsam yalnız güvenilir LOCAL/EMULATOR ve `demo-logisticore-local`. Sentetik aday parametreler ürün/production onayı değildir.

## Başlangıç ve değişiklik kapsamı

Repo `D:/Projects/unitylogisticore`; temiz başlangıç. `origin/main` fetch sonrası BANK-01 PR14 merge SHA `108fae3b6b575bc1f36369907f7aaac7e93f0b50` doğrulandı ve `feature/backend-09b` bu commit'ten oluşturuldu. Önceki BANK-01 final commit `d7c00736a58340407ce548986448ef5bb171e281` korunur. Unity, diğer geliştirici branch'leri ve geçmiş ekonomi/banka raporları kapsam dışıdır.

Runtime değişiklikleri: yeni marketModel.ts ve market.ts; contractEligibility, contractModel, contracts, deliveryModel, operatingCosts ve index entegrasyonu. Testler: yeni market fixture/unit/emulator dosyaları, iki mevcut runner'a kayıt ve yeni market collection Rules retleri. Offline: tools/market-sim/model.mjs, model.test.mjs, report.mjs. Belgeler: bu rapor, ayrı simulation report, MARKET_CONTRACT, DYNAMIC_PRICING_CONTRACT ve mevcut CONTRACT_CONTRACT/SETTLEMENT_CONTRACT'e durum eki. Bağımlılık/lock/Rules/launcher/guard/bank writer değişikliği yoktur.

Toplam22 amaçlanan dosya:

| Dizin | Dosyalar |
| --- | --- |
| backend/functions/src (8) | market.ts, marketModel.ts, contractEligibility.ts, contractModel.ts, contracts.ts, deliveryModel.ts, operatingCosts.ts, index.ts |
| backend/tests (5) | market.fixtures.mjs, market.unit.test.mjs, market.emulators.test.mjs, unit.test.mjs, emulators.test.mjs |
| backend/tools/market-sim (3) | model.mjs, model.test.mjs, report.mjs |
| docs/api-contracts (4) | MARKET_CONTRACT.md, DYNAMIC_PRICING_CONTRACT.md, CONTRACT_CONTRACT.md, SETTLEMENT_CONTRACT.md |
| docs/backend (2) | BACKEND-09B_REPORT.md, BACKEND-09B_SIMULATION_REPORT.md |

## Mimari ve yetki

Pure derivation seçildi: global market writer, cron, materialized snapshot koleksiyonu veya oyuncu bazlı talep toplama yok. Canonical logistics şehirleri ve contract cargo ID'leri exact alanlı immutable market catalog ile eşleşir. Read API server-owned economy-version selector üzerinden katalog seçer; istemci selector/version/epoch/multiplier gönderemez. Read-only transaction, bounded80 koşul, özel seed ve finans bilgisi DTO dışında.

Demand/supply, seed+epoch+city+cargo+side+policyHash SHA-256 girdisinden bounded jitter ile hesaplanır. Signed bps truncation ve para floor işlemleri BigInt kullanır. Candidate sınır -1000..+1500bps; gross cap ve positive-net zorunlu. Bölgesel/cargo ağırlığı0..10000bps. Gerçek şehir veya oyuncu piyasası verisi yoktur. Trend toplama yapılmaz; trendBps0.

V4 contract pricing yeni gross'u bir kez üretir; snapshot marketVersion/hash/policy/epoch/indices/adjustment ve kendi hash'ini taşır. Original epoch'ta pinned catalog/config recompute acceptance/start/claim/read bütünlüğünü doğrular. Current selector değişimi historical snapshot'ı etkilemez. Eksik/drift katalogda fallback/repair yok; eski pinned katalogların saklanması gerekir. Aynı contract epoch/company/slot pool kimliği ve acceptedCount korunur; market epoch değişimi ek pool hakkı vermez. SDK retry epoch sınırı aşarsa farklı fiyatla commit yerine ret olur.

V1 gross-only, V2 net-only ve V3 bank-aware korunur. Yeni contract/delivery schema4; costs V2, settlement V3, journal schema2. Gross→pinned costs→positive net→mevcut capped loan repayment→final wallet delta tek transaction. BANK-01 beş endpoint'i, tier/fee/limit/reserve kuralları ve canonical wallet değişmedi. XP trusted delivery rules'tan gelir. Yeni settlement V4, ikinci wallet veya banka para basımı yoktur.

## Güvenlik kararı

Preflight ve final26 Ekim audit aynı: runtime **0 advisory**, full **5 moderate affected package / 2 distinct advisory**, high/critical0. Full audit temiz değildir ve exit1 verir. Bulgular:

| Advisory | Zincir | Değerlendirme |
| --- | --- | --- |
| GHSA-8988-4f7v-96qf | firebase-tools15.34.0 → @google-cloud/pubsub → @opentelemetry/core1.30.1 | Mevcut dev-tool W3C baggage allocation riski; gerçek saldırgan header erişimi bu loopback workflow'da gösterilmedi |
| GHSA-w5hq-g745-h8pq | firebase-tools15.34.0 → gaxios → uuid9.0.1 | Mevcut dev-tool buffer bounds riski; advisory count ayrı beş exploit yolu değildir |

SECURITY-02 remediation veya risk sınırı değiştirilmedi. Önceki 16–30 Ekim 2026 açık proje sahibi istisnası yalnız güvenilir yerel makine/sabit bağımlılık/demo project kapsamında uygulanır; süre uzatımı veya yeni risk onayı verilmedi. 26 Ekim yeniden audit yapıldı; **30 Ekim'den önce yeniden kontrol/onay gerekir**. Dependency/trust boundary değişikliği yeniden inceleme ister. General security audit HOLD; untrusted CI, gerçek Firebase DEV/PROD, deployment ve migration BLOCK. Bulgular taramalarda görünür kalır; force/downgrade/suppression uygulanmadı.

Başlangıç lock SHA256: `AA1F6BA0D7A5CCAC1C9551A3941B8F1A2FD66B7482FD487C5C4D354ACBC5CC9F`. Node22.23.2/npm10.9.8; firebase-tools15.34.0 mevcut sürüm. Yeni paket veya npm install/ci çalıştırılmadı; bu faz yeni reproducible-install iddiası içermez. Launcher allowlist/loopback/credential rejection, Functions-only guard, default-deny Rules ve localHealth sınırı korunur.

Final disk hash başlangıçla aynıdır. Admin14.5.0/Functions7.4.0 mevcut sürümleri korunur. Credential-pattern ve strict UTF-8 kontrolü22 amaçlanan backend/documentation dosyasında geçti; özel anahtar/token bulgusu yok. Pattern taraması tek başına mutlak secret-yok kanıtı olarak kullanılmadı; eklenen içerik diff üzerinden de incelendi. Assets/Packages/ProjectSettings, dependency manifests/lock, launcher, guard ve Rules diff'i boş.

## Doğrulama kaydı

İlk unit denemesinde35 PASS/1 FAIL: test yardımcısının undefined argümanı varsayılan katalogla değiştirmesi eksik-katalog assertion'ını bozdu; test açık undefined doğrulamasıyla düzeltildi. Bu bir runtime güvenlik guard'ını gevşetme değildir. Sonraki check36/36 geçti.

İlk full validate: build/typecheck PASS; unit36/36 PASS; emulator108 PASS/4 FAIL/0 SKIP (112 TAP node). İki eski callable alt testi logistics15s ve commerce20s HTTP süre sınırına takıldı; iki parent failure toplam4'e dahildir. Yeni market16 alt test ve parent, BANK-01 ve settlement kontrolleri geçti. İlk başarısız kayıt saklandı. Timeout sınırları/eskiden var olan testler değiştirilmeden temiz full rerun yapıldı.

İkinci full rerun: build/typecheck PASS, unit36 PASS/0 FAIL/0 SKIP; emulator110 PASS/2 FAIL/0 SKIP (112 TAP node), validate exit1. Logistics/commerce dahil eski95 node geçti. Yeni market M2..M16 geçti; M1 ilk unauthenticated callable isteğinde Functions worker başlamadı,60s istemci timeout'u oluştu; parent failure ikinci FAIL'dir. CLI `functionsRuntimeWorker.js:waitForSocketReady` içindeki30s readiness timeout'u `Failed to load function` hatasını üretir; logda application stack veya market handler execution görülmedi. İlk koşuda aynı M1 geçti, fakat farklı koşuların sonuçları birleştirilerek full PASS iddia edilmedi. Windows worker readiness kararsızlığının kök nedeni doğrulanmadı. Dependency/launcher değişikliği veya timeout artırımı yapılmadı. Bu aşamada gate HOLD tutuldu; commit/push/PR yapılmadı.

26 Ekim devam koşusu: aynı source, dependency, launcher, timeout ve test beklentileriyle tek başına **npm run validate exit0**. Build/typecheck PASS; **unit36 PASS/0 FAIL/0 SKIP**, **emulator112 PASS/0 FAIL/0 SKIP**. Market M1 dahil16 yeni alt test ve bütün eski95 TAP node bu aynı koşuda geçti. Böylece final functional gate PASS oldu. Önceki geçici worker başlatma sorunları giderilmiş kök neden olarak sunulmaz; yerel test ortamı kararsızlığı kaydı korunur. Offline regression ve market testleri tekrar birlikte85/85 PASS; güncel runtime/full audit sonuçları değişmedi.

| Kontrol | Son durum |
| --- | --- |
| Final npm run validate | PASS exit0; build/typecheck PASS, unit36/36, emulator112/112, FAIL0/SKIP0 |
| Economy/BANK offline regression | PASS75, FAIL0, SKIP0 |
| Yeni market offline | PASS10, FAIL0, SKIP0; 18 deterministik senaryo |
| Runtime audit | PASS0, exit0 |
| Full audit | HOLD5 moderate / 2 advisory, exit1 |
| Final diff / secrets / protected paths / port kapanışı | PASS; emulator shutdown logu ve4400/4500/5001/8080/9099/9150/discovery8708 portlarında listener yok |

Yerel ignored kanıtlar: backend/.local/backend-09b-check.log, backend-09b-validation.log, backend-09b-validation-final.log ve exit JSON, backend-09b-offline.log, backend-09b-market-sim.log, runtime/full-audit-before ve runtime/full-audit-final JSON. Auth token içerebilen ham emulator logları commit edilmez.

Son PASS kanıtı: backend/.local/backend-09b-validation-resume.log ve backend-09b-validation-resume-exit.json; offline-resume.log85/85, runtime-audit-resume.json0, full-audit-resume.json5moderate/2advisory. Bunların tamamı backend-09b- öneki taşır ve ignored kalır.

## 48 gereksinim için kanıt eşlemesi

M1..M16, market.emulators.test.mjs alt testleridir; hepsi gerçek Firestore/Auth emulator'larıyla çalışır. M1 ayrıca gerçek Functions callable erişimi kullanır. Controlled rollback/retry/lost-response fault injection ayrı belirtilir; gerçek concurrent yarış yerine sunulmaz.

| İstek kategorileri | Kanıt |
| --- | --- |
| 1 unauthorized, 2 cross-company/private | M1 strict callable ve M6 foreign accept |
| 3 valid, 4 corrupt catalog, 5 required snapshot, 6 wrong pin | M2/M3/M8 |
| 7 client multiplier, 8 server epoch, 9 write-free read | M1/M4, recursive state+updateTime karşılaştırması |
| 10 default-deny Rules | Mevcut Rules integration içinde yeni market catalog/selection get/list/set retleri |
| 11 neutral, 12 demand, 13 supply, 14 caps | M3 exact100/115/90 gross ve0/1500/-1000bps |
| 15 invalid adjusted reward | M4 positive-net ret; unit overflow/cap/rounding |
| 16 duplicate pool, 17 eight concurrent generations | M5 gerçek8 transaction, tekpool/3contract |
| 18 company consistency | M6 aynı catalog farklı wallet/loan |
| 19 preaccept, 20 postaccept, 21 start, 22 claim fiyat sabit | M7 üç epoch değişimi ve exact pinned terms |
| 23 pinned costs, 24 rollback integrity | M7/M8 |
| 25 replay, 26 different-key duplicate | M5/M7/M15 |
| 27 V1, 28 V2, 29 V3 legacy | Mevcut deliveries/settlement/BANK suites; writer dispatch değişmedi |
| 30 V4 pinned settlement, 31 active loan, 32 no loan | M7/M9; settlementVersion3, journal schema2 |
| 33 manual-vs-claim | M10 gerçek Promise.allSettled partial/payoff yarışları |
| 34 two concurrent deliveries | M11 iki ayrı asset seti; wallet289, debt0, iki journal |
| 35 payoff price, 36 atomic accounting, 37 no duplicate | M9/M10/M11/M12/M14; sekiz claim tek effect |
| 38 final wallet cap, 39 XP authority | M13 final9993 PASS/intermediate10026; overflow rollback; M7 XP10 |
| 40 historical receipt/journal readers | M7/M8/M9 ve mevcut BANK final suite |
| 41 controlled rollback | M14 queued-write abort, exact unchanged company/player tree |
| 42 SDK retry deterministic | M14 ABORTED code10 ile gerçek SDK retry; epoch geçişinde ret |
| 43 lost response | M15 postcommit read fault code14, receipt recovery ve write0 |
| 44 unsupported schema/policy | M2, market unit exact/hash policy checks |
| 45 pending/migration | M16 |
| 46 dependencies/Unity | Final Git diff ve lock hash kontrolü |
| 47 BANK regressions, 48 commerce/fleet/delivery | Full validate; ilk koşu timeout'ları yukarıda açıkça kayıtlı |

## Simülasyon ve maliyet

[BACKEND-09B_SIMULATION_REPORT](BACKEND-09B_SIMULATION_REPORT.md)18 senaryo ve sınırlarını içerir. Baseline ikinci filo13 iş, high-demand11, high-supply16, low-profit16, high-profit11; 10–15 hedefi her koşulda sağlanmaz. Credit-neutral10, downturn11, high-demand credit9: loan gelir değildir, fee ekonomik maliyettir. Her teslimatta cash+capital-debt=initial+profits-fees invariant'ı doğrulanır. İkinci filo ancak teklif arzı varsa throughput artırır; sıfır arz/kârsız teklifler/çok küçük profit borcu kalıcı bırakabilir. XP/level, gerçek rota süresi, gerçek player session ve dinamik salaries modellenmez.

Read API yaklaşık17 point read/0write; en çok80 pure koşul. V3'e göre generation/acceptance+1 market read, ek ekonomik write0. Pinned V4 start/claim/read item başına+4 config/logistics/cost/market read; mevcut bounded20 item. Retry ve platform minimum billing ayrıca gelir; production benchmark değildir. Hiçbir yeni global hot document veya scan yoktur.

## Açık kararlar ve faz kapısı

Market period, baselines/jitter/sensitivity/weights, real route/cargo inputs, numeric caps ve offer supply onaysız adaylardır. Fixed costs nedeniyle gross artışı net üzerinde daha büyük yüzde yaratabilir; uzun dönem inflation/credit/supply ölçümü gereklidir. Gerçek player-driven demand/supply collection, retention, immutable catalog activation/publishing ve migration ayrı inceleme ister. Wallet/XP cap, kârsız/sıfır arz ve küçük net repayment soft-lock'ları için rescue mekanizması bu fazda yoktur.

Final functional gate **PASS**: aynı full validate koşusunda tüm112 emulator/callable testleri geçti. Trusted LOCAL dependency security **CONDITIONAL PASS** (mevcut16–30 Ekim istisnası); general audit **HOLD**; production/deploy/migration **BLOCK**.22 dosyalık çalışma feature/backend-09b üzerinde commit/push ve main hedefli insan incelemesine hazırdır. BACKEND-10 LOCAL readiness **ELIGIBLE, ayrı faz incelemesine tabi**; başlamadı. Merge/deploy/migration yapılmaz. Başarısız önceki koşular gizlenmedi veya SKIP'e çevrilmedi; worker başlangıç kararsızlığı tekrarlanırsa ayrıca ortam incelemesi gerekir.
