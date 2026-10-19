# BACKEND-07 — Authoritative Deliveries, Fleet Assignment & Atomic Rewards

Tarih: 20 Ekim 2026. Kapsam: yalnız güvenilir LOCAL/EMULATOR, `demo-logisticore-local`.

## Başlangıç ve sınırlar

Repository root `D:/Projects/unitylogisticore` doğrulandı. Başlangıç çalışma ağacı temizdi; BACKEND-06 merge commit'i `1ab33fe84a6d55cb4967b017c847df7bae026733` origin/main üzerinde doğrulandı. Main fast-forward sonrası `feature/backend-07` bu tabandan oluşturuldu. BACKEND-00–06 ve BACKEND-08A kaynak/sözleşmeleri incelendi. `feature/unity-02a-map` yalnız Git üzerinden okundu; checkout/merge yapılmadı.

Assets/, Packages/, ProjectSettings/, C# ve localization dosyaları değiştirilmedi. Bağımlılıklar, lockfile, emulator launcher, environment guard ve default-deny Firestore Rules değişmedi. Gerçek DEV/PROD, gerçek oyuncu, deployment, migration veya dış oyun servisi kullanılmadı.

## IMPLEMENTED

- Canonical `v2Companies/{companyId}/deliveries/{deliveryId}`; company+contract üzerinden deterministic ID, strict active/completed şemaları, immutable terms/rules hash, tam asset selection ve trusted timestamps.
- Contract lifecycle accepted → in_progress → completed. acceptedAt/terms/reward/pool acceptedCount korunur; getMyContracts ve historical accept replay çalışır.
- `startDelivery`: trusted UID/company, starter/economy/legacy/migration, accepted pool/terms, current capability/capacity/availability, rules pin ve timestamp bounds doğrulanır. Selection accepted candidate setine sabittir. Tek transaction assignment+assets+delivery+contract+receipt yazar; finance/XP yazmaz.
- `claimDeliveryReward`: arrival sınırı, matching active assignment, immutable reward/currency/scale, bounded XP/wallet ve claim/ledger yokluğu doğrulanır. Tek transaction delivery+contract completion, tam asset release, canonical wallet/progression/derived level, append-only ledger, permanent claim ve receipt yazar.
- `getMyDeliveries`: trusted owner, document-ID pagination (max20), safe DTO ve trusted arrivalReady. Read finalize/reward/repair/release yapmaz.
- `prepareAssignment` read/validate ardından write-only apply döndürür. Eski writeAssignment adapter ve fingerprints korunur. Ekonomi yazımı ortak planEconomyMutation içine taşındı; prepareEconomyReward yalnız domain-verified kaynağı hazırlar. Nested transaction ve ilk write sonrası read yoktur.

Ayrıntılı şema, payload/DTO, error codes, maliyet ve recovery: [DELIVERY_CONTRACT](../api-contracts/DELIVERY_CONTRACT.md).

## Zaman, replay ve recovery

Süre server-selected, route+cargo ile eşleşen versioned synthetic rules'dan gelir; positive integer ve teknik olarak en çok 7 gün. XP açıkça yapılandırılır, varsayılan yoktur. Client zaman/position/reward kabul edilmez. Start başarılı transaction attempt'inin trusted saatini kullanır; aynı key replay özgün sonucu/zamanı korur. Zarf serverTime ayrıca üretilir. Farklı key deterministic delivery ve permanent reward claim/ledger kontrollerini aşamaz.

Lazy completion: arrivalReady bir gösterimdir; yalnız başarılı claim completion/settlement yapar. Offline oyuncunun ekipmanı claim'e kadar reserved kalır. Wallet/XP overflow, bozuk assignment veya rules drift halinde partial payout/release yoktur. Recovery ayrı onaylı domain/state/config işlemi gerektirir; otomatik repair/capping yoktur.

## Gerçek doğrulama sonuçları

20 Ekim 2026 tarihinde backend/ altında `npm run validate` exit0 ile tamamlandı:

| Kontrol | Sonuç | Kanıt / kapsam |
| --- | --- | --- |
| TypeScript build | PASS | validate/check ve emulator öncesi build |
| Typecheck | PASS | functions workspace |
| Unit | PASS | 20 test, 0 fail |
| Auth/Firestore/Functions Emulator | PASS | 9 üst grup +31 alt test; Node toplam40, 0 fail |
| Yeni delivery grubu | PASS | 7 alt test |
| Default-deny Rules | PASS | delivery/rules yolları dahil mevcut suite |
| Environment isolation/regresyon | PASS | mevcut unit ve emulator suite |
| Emulator shutdown | PASS | log shutdown; 9099/8080/9150/5001/4400/4500/8719 loopback portları boş |
| Runtime audit | PASS | npm audit --omit=dev --json: 0 vulnerability |
| Full audit | HOLD | 3 high +4 moderate package finding; 3 distinct advisory |
| Unity/protected path diff | PASS | Assets/Packages/ProjectSettings ve manifest/lockfile/Rules farkı yok |
| Diff/whitespace, strict UTF-8, sınırlı secret scan | PASS | final Git kontrolü; trailing EOF boşlukları düzeltildi |
| Yeni npm ci / dependency upgrade | NOT RUN | bu fazda istenmedi; bağımlılıklar değişmedi |
| Gerçek cihaz/production testi | NOT RUN | kapsam dışı |

Yerel ignored kanıtlar: backend/.local/backend-07-validation.log, backend-07-audit-runtime.json, backend-07-audit-full.json. Rapor testleri yeniden yapılmış gibi göstermez; validate sonrası yalnız EOF whitespace ve dokümantasyon düzenlendi.

Gerçek eşzamanlı Firestore işlemleri: aynı contract için 8 start, aynı delivery için 8 claim, yalnız truck paylaşan ve yalnız driver paylaşan ayrı start yarışları. Her yarışta tek başarı doğrulandı. Kontrollü fault injection ayrıca queued-write rollback, SDK ABORTED retry ve commit sonrası kayıp response/replay'i sınadı; bunlar gerçek yarış testlerinden ayrıdır.

Diğer kapsam: unauthenticated/spoof/foreign access; missing readiness, legacy/migration, yanlış assets/rules; erken claim ve exact arrival boundary; start finance invariant; claim wallet/ledger/XP/level; overflow rollback; bozuk/stale assignment; completed sonrası historical accept/start replay; read-only snapshots, pagination ve HTTP callable auth/read/claim replay. Cleanup yalnız test-owned sentetik belgeler ve test Auth hesabını siler.

## Güvenlik istisnası ve karar

Kalan advisory'ler görünürdür:

- GHSA-vfj7-8cjw-p6xm — braces, nested pattern stack-exhaustion.
- GHSA-8988-4f7v-96qf — @opentelemetry/core, W3C baggage memory allocation.
- GHSA-w5hq-g745-h8pq — uuid, buffer bounds.

7 package finding 7 bağımsız exploit yolu değildir. Sabit bağımlılıklar değişmedi; önceki BACKEND-01-S1 dar kapsamlı değerlendirme ve proje sahibinin 16–30 Ekim 2026 onayı yalnız güvenilir yerel bilgisayar, mevcut sabit tooling ve demo emulator için uygulanır. Mandatory reassessment: **23 Ekim 2026**. Açıklar düzeltilmiş sayılmaz; tarama kapatılmadı veya bulgular bastırılmadı. Bağımlılık/güven sınırı değişirse yeniden onay gerekir. Bu faz yeni dış güven sınırı veya prod erişimi eklemez.

- FUNCTIONAL GATE: **PASS**.
- LOCAL SECURITY GATE: **CONDITIONAL PASS**, mevcut süreli istisna koşullarıyla.
- GENERAL SECURITY AUDIT: **HOLD — unresolved advisories**.
- CI/untrusted code, real Firebase DEV/PROD, deployment, migration: **BLOCK**.
- BACKEND-08B: yalnız ayrı faz incelemesine uygun; **başlatılmadı**.

## Maliyet ve ertelenen kararlar

P≤5 pool contract, A=2..3 asset, C≤3 catalog için yaklaşık direct read/write: start `18+P+2A+C` / `A+4`; claim `18+P+A+C` / `A+8`; commit sonrası receipt +1 read. Replay yaklaşık9 read/0 write. List foundation +max21 query doc +satır başına bounded pool/rules/active assignment lookup; aynı pool tekrar okunabilir. Bunlar billing ölçümü değildir, retry/index maliyeti ayrıca değerlendirilir.

PROPOSED: bağımsız finalize, list pool cache, onaylı recovery. DEFERRED: scheduler, failure/penalty, VIP, production rollout ve migration. UNKNOWN: gerçek duration/distance, fuel, salary, maintenance, tolls, XP/economic balance. LGC/15000 gibi önceki adaylar production default değildir; test TST/ödül6/süre1000ms/XP10 yalnız sentetiktir.

## Değişen dosyalar

Backend: README.md; functions/src/{commands,contracts,economy,fleet,index,deliveries,deliveryModel}.ts; tests/{unit,emulators,deliveries.emulators}.test.mjs.

Dokümantasyon: docs/api-contracts/{CONTRACT_CONTRACT,FLEET_CONTRACT,ECONOMY_CONTRACT,DELIVERY_CONTRACT}.md; docs/backend/BACKEND-07_REPORT.md. Toplam16 dosya. Yalnız bu dosyalar commit kapsamındadır. PR main'e inceleme için hazırlanır; merge/deploy yapılmaz.
