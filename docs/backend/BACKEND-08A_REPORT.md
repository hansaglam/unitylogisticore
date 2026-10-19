# LOGISTICORE — BACKEND-08A REPORT

20 Ekim 2026 (Europe/Istanbul). Yalnız `demo-logisticore-local`, sentetik Auth/Firestore/Functions Emulator kayıtları. Production BLOCK; gerçek Firebase DEV/PROD veya kullanıcı verisine erişilmedi.

## 1. Başlangıç ve kapsam

Depo kökü `D:/Projects/unitylogisticore`; ilk dal `feature/backend-05`; çalışma ağacı temiz. `origin/main` fetch edildi ve beklenen merge `2644d9b312afbc386ae77be1e60ee00fb230d54b` doğrulandı. Main fast-forward sonrası `feature/backend-08a` oluşturuldu. BACKEND-00..05 raporları/contract sınırları ve mevcut identity/company/command/economy/guard/test altyapısı incelendi; aynı owner/mapping/migration/executor kullanıldı.

Yalnız backend ve ilgili docs değişti. Unity, Packages, ProjectSettings, C#, diğer geliştiricinin remote map dalı, önceki raporlar/sözleşmeler, ekonomi değerleri ve lifecycle değiştirilmedi. Reset/stash/force push/deploy/migration yoktur.

## 2. IMPLEMENTED

- `logisticsModel.ts`: strict synthetic versioned catalog, asset kinds/ownership/operational state, hash pinning, warehouse uniqueness/capacity ve bounded page validators.
- `logisticsContext.ts`: küçük mevcut executeCommand adapter'ı; transaction attempt başına foundation snapshot ve catalog cache. İkinci transaction executor veya finans otoritesi değildir.
- `fleet.ts`: internal starter provisioning, permanent marker, stable starter IDs; transaction-bound multi-asset reserve/release ve permanent assignment lifecycle.
- `warehouses.ts`: internal synthetic create-only warehouse foundation ve uniqueness. Public purchase/upgrade endpoint yoktur.
- `logisticsReads.ts` / index: authenticated local-only `getMyFleetState` ve `getMyWarehouseState`; canonical company path, document-ID cursor, en çok 20 DTO + 1 lookahead.

Starter grant: company başına tam bir truck + bir driver + tek marker + receipt atomik; no warehouse/no additional trailer; wallet/XP/company lifecycle yazısı yoktur. Marker version pin'li ve company başına sabittir; farklı key/package version ile ikinci grant yoktur. Partial/missing/corrupt marker/asset durumu otomatik onarılmaz. Same-key replay ilk result/serverTime'ı korur; güncel owner/economy/starter integrity replay öncesinde de aranır.

Assignments: 2–3 farklı asset, tam bir truck+driver ve optional trailer. Server-owned source'tan deterministic assignment ID; canonical owner/kind/catalog/status/availability kontrolü; bütün reads yazılardan önce. Shared truck veya driver iki aktif operasyona atanamaz. Release sadece expected active assignment'ın birebir asset setini boşaltır; eski assignment ID yeniden kullanılamaz. Internal helper'lar da local guard uygular; future caller aynı transaction'da trusted Auth ile elde edilmiş CompanyState sağlamalıdır.

Warehouse: synthetic policy company+city+type hash'iyle create-only uniqueness; final product multi-site kuralı değildir. Server catalog city/type/level/capacity reference zorunlu; no hardcoded real cities/prices/capacities. Existing/foreign/corrupt document overwrite edilmez. Safe integer capacity check taşmaya dayanıklıdır; actual cargo inventory/reservation DEFERRED.

## 3. Onboarding ve ürün kararı sınırları

`economy_ready` starter fleet kanıtı değildir. Fleet read `starterFleetReady` değerini marker ve iki asset'in current validity'sinden hesaplar. Economy ready + starter false normal resumable ara durum; read grant veya repair yapmaz. True starter readiness, actual contract eligibility veya production playable onayı değildir.

**PROPOSED:** identity → company → economy → starter → sonraki gameplay eligibility. Hiçbir gerçek orchestration bu fazda otomatik etkinleştirilmedi. Gerçek starter activation ürün/migration/security/rollout onayı ister. LGC/2 decimals öneri; 15.000 LGC yalnız tasarım adayıdır ve persistent default yapılmadı.

**UNKNOWN:** gerçek truck specs/brands/capacities, driver salary, fuel, trailer compatibility, starter truck'ın trailersız job eligibility'si, şehir/warehouse katalogları, fiyatlar, progression prerequisites ve production migration. **DEFERRED:** public purchases/hire/upgrade/assignment, delivery timing/reward, VIP, real economy activation, production load/mobile device testleri. BACKEND-06 başlatılmadı.

## 4. Test kapsamı ve kanıt niteliği

Yeni pure unit testleri catalog approval/reference/duplicate/kind checks, canonical asset availability/owner, warehouse capacity/uniqueness, bounded cursor ve internal nonlocal-before-DB guard'ı kapsar.

Yeni gerçek emülatör grubu dokuz alt test:

1. Pending ekonomi, missing/unapproved catalog, missing auth ve spoof payload reddi; read fake assets üretmez.
2. Partial asset/marker, foreign owner/mapping ve migration/legacy blockers; legacy snapshot/updateTime aynı kalır.
3. Sekiz farklı key ile eşzamanlı starter: bir başarı, iki asset, bir marker, tek başarılı receipt; replay stable; wallet/XP/company snapshot değişmez; warehouse verilmez.
4. Post-grant corrupt/missing asset/marker ve catalog drift fail-closed; historical receipt bile partial state'i bypass etmez.
5. Truck+driver+optional trailer atomic reserve/release; replay; unrelated/stale release reddi; released source yeniden kullanılmaz.
6. Shared truck ve shared driver için ayrı gerçek concurrent transaction yarışları: tek winner; loser-only asset boş kalır, loser receipt yoktur.
7. Queued reservation yazıları sonrası kasıtlı transaction exception ile rollback; assignment/asset partial writes yok. Out-of-service, wrong kind ve missing asset reddi.
8. Synthetic warehouse create/replay/duplicate/foreign owner/capacity validation; ekonomik state değişmez. Bu test fixture'sı production oyuncusuna ücretsiz depo verme değildir.
9. 28 synthetic asset üzerinde bounded sayfalama (7'li sayfalar), auth/invalid token/spoof/limit/cursor HTTP testleri; tüm tracked doc data/updateTime snapshot'ları reads sonrasında aynıdır.

Eski BACKEND-01..05 suites, ekonomi claims/ledger/replay/lost-response ve environment isolation testleri korunur. Default-deny Rules testi yeni catalog/assets/provisioning/assignments/warehouses yollarında auth ve unauth get/create/update/delete erişimini reddeder. Bütün cleanup exact test-owned refs ve test Auth hesabıyla sınırlı; global reset veya gerçek kayıt silme yoktur.

Concurrency gerçek Firestore Emulator transaction'larıdır. Kasıtlı rollback / eski ekonomi suite'inin ABORTED/lost-response testleri kontrollü hata enjeksiyonudur; production network/device testi olduğu iddia edilmez.

## 5. Güvenlik taramaları

Ayrı `npm audit --omit=dev --json`: exit 0, 0 bulgu — PASS.
Ayrı `npm audit --json`: exit 1; 3 high + 4 moderate paket kaydı, 0 critical — HOLD.

Görünür kalan üç advisory:

- GHSA-vfj7-8cjw-p6xm — braces.
- GHSA-8988-4f7v-96qf — OpenTelemetry zinciri.
- GHSA-w5hq-g745-h8pq — uuid.

Yedi paket kaydı yedi bağımsız exploit değildir. Dependency/override/lockfile değişikliği, audit suppression veya force fix yoktur. Yeni üçüncü taraf hizmet, IAM veya production güven sınırı eklenmedi.

Proje sahibinin BACKEND-01 §19 **16–30 Ekim 2026 trusted LOCAL/EMULATOR** istisnası uygulanır. **23 Ekim 2026 zorunlu reassessment** hâlâ gereklidir; bu rapor gelecekteki kontrolün yapıldığı anlamına gelmez. Mevcut sabit bağımlılıklar, demo project ve güvenilir girdilerle sınırlı. CI/untrusted code/real DEV/PROD/deploy/migration kapsam dışı. Bağımlılık veya trust boundary değişirse yeniden onay/değerlendirme gerekir. Genel audit HOLD ve production BLOCK devam eder.

## 6. Maliyet ve entegrasyon

Starter ilk transaction: 13 read/4 write + post-commit receipt read 1; ready replay 13/0. Assignment N=2..3: 10+N+C read (C unique catalog), N+2 write + receipt read. Warehouse first create 11 read/2 write + receipt read. Fleet read 7/8 foundation + 3 starter records + bounded page query + distinct catalog read; aynı transaction'da catalog cache shared. Warehouse read 7/8 + bounded query + distinct catalog. Empty query/index/retry billing bu logical count'lara ek olabilir; production maliyet ölçümü değildir.

Array main company inventory yok; varlık başına doc. Global counter/lock, per-second writes, scheduler, Pub/Sub, index veya database eklenmedi. Wallet/XP mutation yoktur. Gelecekte satın alma: server price/catalog + progression + owner + canonical wallet/ledger + durable receipt + asset uniqueness tek transaction; client-supplied amount otorite olamaz. Helper'ların write phase'inden sonra read yapılmamalı; future orchestrator tüm ek reads'i önce toplamalı.

## 7. Değişen dosyalar

14 dosya:

- backend/functions/src/{fleet.ts,warehouses.ts,logisticsModel.ts,logisticsContext.ts,logisticsReads.ts,index.ts}
- backend/tests/{logistics.fixtures.mjs,logistics.emulators.test.mjs,unit.test.mjs,emulators.test.mjs}
- backend/README.md
- docs/api-contracts/FLEET_CONTRACT.md
- docs/api-contracts/WAREHOUSE_CONTRACT.md
- docs/backend/BACKEND-08A_REPORT.md

Şemalar, wire DTO örnekleri, localization keys, lifecycle ve DEFERRED kararlar yeni iki contract'ta açıklanır. Önceki sözleşmeler değişmedi; eski economy/company callable response'ları korunur.

## 8. Son doğrulama ve gate kararı

Son `backend/` → `npm run validate` exit 0. İlk koşu 15 unit/24 emulator kaydı ile geçti; internal primitive guard testi ve transaction-local catalog cache iyileştirmesi sonrası son koşu aşağıdaki gibidir. Sonrasında yalnız whitespace/EOF ve belge düzenlemeleri yapıldı; davranış değişmedi.

| Kontrol | Sonuç |
|---|---|
| TypeScript build / typecheck | PASS |
| Unit | PASS — 16/16 |
| Emulator suite | PASS — 7 ana test + 17 nested alt test; Node toplam 24/24, 0 fail/skip |
| Yeni logistics coverage | PASS — 1 ana grup + 9 alt test; önceki economy grubunda 8 alt test korunur |
| Rules, auth, isolation, BACKEND-01..05 regression | PASS — aynı suites içinde |
| Runtime audit | PASS — 0 |
| Full audit | HOLD — 3 high + 4 moderate paket kaydı, 3 unresolved advisory |
| Emulator shutdown | PASS — shutdown logu ve 9099/8080/9150/5001/4400/4500/discovery 8048 loopback bind |
| Git diff / whitespace / strict UTF-8 | PASS |
| Protected Unity paths / eski runtime core / dependencies-lockfile-guard-Rules-launcher | PASS — değişiklik yok |
| Secret-pattern scan | PASS — private key/Google/GitHub token göstergesi eşleşmedi; kapsamlı geçmiş taraması değildir |
| Gerçek Firebase DEV/PROD, deployment, migration, mobile device/performance | NOT RUN / BLOCK |

Yerel kanıt dosyaları (Git dışında): `.local/backend-08a-validation.log`, `.local/backend-08a-audit-runtime.json`, `.local/backend-08a-audit-full.json`. Yeni npm ci çalıştırılmadı; paket/lockfile değişmedi.

**BACKEND-08A FUNCTIONAL GATE: PASS. LOCAL SECURITY GATE: CONDITIONAL PASS** — mevcut 16–30 Ekim istisnası kapsamında. **GENERAL SECURITY AUDIT: HOLD. PRODUCTION DEPLOYMENT: BLOCK.** Sonraki faz teknik temel olarak ayrı ürün/kapsam/güvenlik incelemesine uygun; BACKEND-06 veya başka faz başlatılmaz.

Kullanıcının talimatıyla yalnız 14 hedef dosya tek commit, `feature/backend-08a` push ve main hedefli PR için hazırlanır. İşlem kimlikleri Git geçmişinde/son teslim yanıtında verilir. PR merge/deploy yapılmaz.
