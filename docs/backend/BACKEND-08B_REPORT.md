# BACKEND-08B — Fleet Acquisition, Driver Hiring, Warehouses & Atomic Spending

23 Ekim 2026 (Europe/Istanbul). **FUNCTIONAL PASS; LOCAL SECURITY CONDITIONAL PASS; GENERAL AUDIT HOLD; PRODUCTION/MIGRATION BLOCK.**

## Repository ve güvenlik önkoşulu

Root D:/Projects/unitylogisticore, başlangıç branch feature/backend-07 ve temiz çalışma ağacı doğrulandı. Origin fetch sonrası beklenen BACKEND-07 merge `f9edb5d3195bd97d83b0649406cd99551436b8d6` origin/main HEAD olarak bulundu. Main fast-forward edilip feature/backend-08b oluşturuldu. Reset/stash/discard/force push yok; Unity geliştirme dalına checkout/merge/rebase yapılmadı.

İmplementasyondan önce zorunlu23 Ekim güvenlik değerlendirmesi yapıldı: Node22.23.2/npm10.9.8, pinned dependency tree ve published advisory/registry metadata incelendi. Aynı üç development-tool advisory'si, runtime0; yeni risk/advisory saptanmadı. Ayrıntı ve kaynaklar [BACKEND-08B_SECURITY_REASSESSMENT](BACKEND-08B_SECURITY_REASSESSMENT.md). Mevcut insan onayı yalnız16–30 Ekim trusted LOCAL koşullarında uygulanır; otomatik uzatılmadı. Manifest/lockfile değişmedi, fix/upgrade yapılmadı.

BACKEND-00–07 ve08A mevcut mimari/sözleşmelerine göre readFoundation, executeCommand, canonical finance/progression, ledger/claim, fleet/warehouse validators ve prepare/apply ilkesi yeniden kullanıldı. Yeni framework/transaction executor/para otoritesi yok.

## IMPLEMENTED

- Authenticated LOCAL callables: buyVehicle, hireDriver, acquireTrailer, openWarehouse, upgradeWarehouse, getMyAcquisitionOffers.
- Strict v1 commerce catalog, economy/logistics v2/rulesVersion hash binding, yalnız synthetic_emulator_only approval. Company offers available→consumed; authoritative positive integer price, level/target prerequisites, server expiry.
- Client payload yalnız offerId. Company/price/level/stats/config/time kabul edilmez. Public offer publisher veya sınırsız generator yok; config ve teklifler yalnız controlled emulator test fixture'larından gelir.
- Company+offer hash asset kimliği; aynı model iki farklı offer ile iki farklı truck olabilir. Aynı offer yeniden kullanılamaz. Starter assets/marker korunur, yeni asset operational/unassigned ve canonical schema1'dir.
- Tek Firestore transaction: target create/update + exact wallet debit/progression + append-only ledger/permanent claim + consumed offer + durable receipt. Bütün reads önce tamamlanır; nested transaction/read-after-write yoktur. Common planEconomyMutation kullanılır; yalnız mevcut operationId helper export edildi, eski finans davranışı değişmedi.
- XP delta0; seviye aynı kalır. Common writer progression.updatedAt'i yeniler; yeni reward veya payroll yaratılmaz.
- Warehouse company+city+type ID korunur. Opening level1/used0, ücretli create-only. Upgrade expected level'den bir adım; current definition doğrulandıktan sonra aynı capacity model ve nondecreasing max ile yeni definition/version'a geçer. usedCapacity/acquiredAt/owner/city/type/status korunur. Şema extension/migration gerekmedi; immutable catalog in-place değiştirilmez.
- Read API max20/document-ID cursor, decimal price string ve server-derived eligibility/expiry; no publish/consume/repair. Catalog cache transaction-local, no global counter/listener/scan.

Şema, payload/DTO, errors, replay ve maliyet: [COMMERCE_CONTRACT](../api-contracts/COMMERCE_CONTRACT.md).

## Atomiklik, replay ve uyumluluk

Same key+canonical intent original result ve receipt committedAt-derived serverTime'ı döndürür. Farklı geçerli intent IDEMPOTENCY_CONFLICT; farklı key+aynı offer ikinci grant/debit yapamaz. Kalıcı source commerce_{company-offer-hash}; ledger mevcut operation hash ve commerce_purchase reason kullanır. TTL veya otomatik repair yok.

Consumed offer replay/read sırasında canonical target ve claim/ledger integrity yeniden doğrulanır. Silinmiş veya foreign/corrupt asset fail-closed; historical warehouse replay sonraki legitimate upgrade sonrası geçerlidir. Current config/target bozulursa receipt mevcut olsa da recovery ayrı yetkili inceleme gerektirir. Kayıp response'da aynı key retry, yeni debit yapmaz.

Acquired truck/driver/trailer mevcut v2 contract eligibility ve assignment ile test edildi; accepted terms'in pinned logistics version'ına uygun asset gerekir. Satın alma active delivery/assignment'ı değiştirmez veya ekipmanı serbest bırakmaz. Eski API/fingerprint/schema/guard/Rules değiştirilmedi; generic money/asset endpoint eklenmedi.

## Gerçek test ve kontrol sonuçları

backend/ altında `npm run validate` ilk tam koşuda exit0. Kaynak değişiklikleri koşu sırasında sabitti. Önceki `npm run check` de geçti; final sayımlar aşağıdaki tam koşuya aittir.

| Kontrol | Sonuç | Kanıt |
| --- | --- | --- |
| TypeScript build/typecheck | PASS | validate/check + emulator öncesi build |
| Unit/environment isolation | PASS | 21 test, fail/skip0 |
| Auth/Firestore/Functions Emulator suite | PASS | 10 üst grup +40 alt test, Node toplam50, fail/skip0 |
| Yeni commerce emulator grubu | PASS | 9 alt test |
| Default-deny Rules | PASS | commerce catalog/offers ve önceki yollar |
| Runtime audit | PASS | ayrı komut, exit0, vulnerability0 |
| Full audit | HOLD | ayrı komut exit1, high3/moderate4, üç advisory |
| Emulator shutdown | PASS | log cleanup ve9099/8080/9150/5001/4400/4500/discovery8827 portları boş |
| Diff/whitespace/strict UTF-8 | PASS | final Git/file kontrolleri |
| Limited secret-pattern scan | PASS | private-key/API-key/token kalıpları eşleşmedi; tam secret audit iddiası değil |
| Protected Unity/dependency/guard/Rules paths | PASS | Git farkı yok |
| npm ci / dependency upgrade | NOT RUN | bu faz dependency değişikliği içermez |
| Production, migration, device integration, untrusted CI | NOT RUN / BLOCK | kapsam dışı |

Gerçek concurrent Firestore Emulator yarışları: tek offer'a8 istek→tek başarı; bakiye40 sentetik minor units ile price20 olan8 ayrı offer→tam2 başarı, bakiye0. Exact balance ve insufficient funds sınırları doğrulandı. Bu rakamlar test verisidir.

Kontrollü fault injection ayrı: queued writes sonrası abort tüm asset/payment/claim/receipt'i geri alır; SDK ABORTED retry birden fazla attempt çalıştırır; commit sonrası receipt response kaybı RETRY_LATER ve aynı key ile tek debit recovery verir. Bunlar genuine race testleri yerine sunulmaz.

Dokuz alt test kapsamı: beş valid edinim ve canonical reads; replay/conflict/single-use/distinct same-model; auth/spoof/company/migration/readiness/expiry/config/currency/scale/negative-overflow; iki8-way race; rollback/retry/lost-response; warehouse duplicate/stale/skip/corrupt capacity/status/cross-version upgrade; acquired fleet ile gerçek accept/start/claim ve reserved assets snapshot; read-only pagination/HTTP auth+replay+input/foreign company. Unit strict prices/version/hash/progression/level transition sınırlarını ayrıca doğrular. Cleanup yalnız tracked test-owned synthetic refs ve test Auth hesabına uygulanır; gerçek kullanıcı yoktur.

Ignored yerel kanıtlar: backend/.local/backend-08b-validation.log, backend-08b-{runtime,full}-verified.json ve backend-08b-{runtime,full}-final.json. İlk sandbox DNS hatalı audit ayrı tutuldu; başarılı yetkili taramalarla karıştırılmadı. Final audit sonuçları başlangıçla aynı. Lockfile local SHA256 `3C2CC424D6651A69181A15A5D6ADDD8E4994983E23DB4B8745B794E4968C97D6` korunur.

## Dosyalar ve maliyet

Toplam16 dosya:

- backend/README.md
- backend/functions/src/commands.ts, economy.ts, index.ts
- Yeni backend/functions/src/commerce.ts, commerceModel.ts
- backend/tests/unit.test.mjs, emulators.test.mjs
- Yeni backend/tests/commerce.fixtures.mjs, commerce.emulators.test.mjs
- docs/api-contracts/FLEET_CONTRACT.md, WAREHOUSE_CONTRACT.md, ECONOMY_CONTRACT.md
- Yeni docs/api-contracts/COMMERCE_CONTRACT.md
- Yeni docs/backend/BACKEND-08B_REPORT.md, BACKEND-08B_SECURITY_REASSESSMENT.md

Yeni edinim yaklaşık19..20 transaction read/7 write ve commit sonrası+1 receipt read; replay19..20 read/0 write. List8 foundation+4 starter+max21 query doc+satır başına4 point read+distinct catalogs. Per-attempt catalog cache var; bounded offer reread ve starter/catalog duplication ileride optimize edilebilir. Retry/index/billing ekleri hariç yaklaşık maliyet; gerçek billing benchmark yapılmadı.

## Kalan kararlar ve kapılar

IMPLEMENTED yalnız sentetik local vertical slice. UNKNOWN/ONAYSIZ: gerçek LGC starting balance15000, fiyatlar, salary, warehouse fees, city/capacity/progression unlocks. DEFERRED: selling/marketplace/trading/transfer/loan/payroll/fuel/repair/IAP/VIP/dynamic pricing, production catalog publisher/migration. PROPOSED recovery/publisher ayrı review gerektirir. Yeni güven sınırı veya dependency değişikliği bu istisnayı geçersiz kılar.

FUNCTIONAL **PASS**. LOCAL SECURITY **CONDITIONAL PASS**, mevcut30 Ekim'e kadar koşullu onay. GENERAL SECURITY **HOLD**: GHSA-vfj7-8cjw-p6xm, GHSA-8988-4f7v-96qf, GHSA-w5hq-g745-h8pq görünür. Production/real DEV/deploy/migration/CI-untrusted **BLOCK**. BACKEND-09 teknik temel açısından ayrı faz incelemesine uygun; başlamadı. Kullanıcı talimatıyla yalnız bu16 dosya commit/push ve main hedefli PR kapsamındadır; merge/deploy yapılmaz. Git kimlikleri final yanıtta verilir.
