# LOGISTICORE — BACKEND-04 REPORT

16 Ekim 2026, Europe/Istanbul. Yalnız trusted LOCAL/EMULATOR; production readiness iddiası değildir.

## 1 — Yönetici sonucu ve repo

BACKEND-04 functional PASS. `createCompany` ve `getMyCompanyState` implemented. Bir UID için en fazla bir foundation şirketi; ekonomi/progression yok. BACKEND-05 başlamadı.

Başlangıç repo D:/Projects/unitylogisticore, dal feature/backend-03 ve temiz çalışma ağacı doğrulandı. Fetch sonrası origin/main üzerinde beklenen a040030 BACKEND-03 merge'i bulundu. Main fast-forward edildi, feature/backend-04 bu tabandan oluşturuldu. Başka geliştirici değişikliği yoktu; reset/stash/force push yapılmadı. BACKEND-00–03 raporları, FOUNDATION/IDENTITY/COMMAND sözleşmeleri ve mevcut backend kaynak/test/README/config incelendi. Unity dosyaları değiştirilmedi.

## 2 — Şema ve kimlik politikası

Company ID `co_` + SHA-256(trusted UID UTF-8), tam 64 lowercase hex. Anahtardan ve name'den bağımsızdır; aynı UID'nin farklı request key'leri başka şirket üretemez. Hash sahiplik kanıtı/gizli identifier değildir. Owner invariant uyuşmazlığı overwrite yerine fail-closed olur.

Player schemaVersion=1 kaldı. BACKEND-02 identity invariant helper'ı `assertIdentityRecord` olarak çıkarıldı; eski hata sözleşmesi ve retry metadata koruması aynı. Company bootstrap yalnız player.companyId ekler. Company: schemaVersion=1, ownerUid, name, bootstrapStatus=foundation_pending_economy, createdAt=serverTimestamp. updatedAt/stateVersion/finans/progression varsayılanı yok. API economyReady=false döndürür; bu sıfır bakiye iddiası değildir.

## 3 — Callable ve transaction

İki callable us-central1, aynı guarded localFirestore. createCompany mevcut executeCommand ile strict envelope, name-only payload, normalized request hash ve durable receipt kullanır. Adlar NFC, space collapse/trim, 2–64 code point, raw en fazla 512 UTF-8 byte; control/format/surrogate/line-separator/default-ignorable karakterleri reddedilir. TR/EN/DE desteklenir; global name uniqueness yok. UI adı literal metin olarak render etmelidir.

Receipt read ardından scope resolver beş fixed belgeyi aynı transaction'da okur: player, deterministic company, legacy users, legacy serverState/current, v2MigrationRecords. Güncel kimlik/eligibility/tutarlılık replay öncesinde de kontrol edilir. Resolver snapshot'ı çağrı/transaction-attempt kapsamındaki WeakMap ile apply'a verir; her SDK retry'ında tekrar okunur, process cache veya duplicate read yoktur. Apply bütün okumalar bittikten sonra company create, player mapping update ve executor receipt create işlemlerini tek commit'e koyar.

Aynı key/normalize name ve geçerli current state → original result/committed time replay. Aynı key/farklı name → conflict. Farklı key/geçerli mevcut şirket → COMPANY_EXISTS. Aynı UID farklı key yarışı canonical player/company lock'larıyla tek başarılı bootstrap üretir. Corrupt/suspended/migration state eski receipt üzerinden de aşılmaz. Receipt geçmiş başarıdır; getMyCompanyState güncel snapshot'tır.

getMyCompanyState yalnız protocolVersion:1 alır. Read-only transaction ile beş fixed lookup, sıfır write; keyfi company/UID kabul etmez. Geçerli no-company hasCompany=false; sahip olunan foundation company güvenli metadata döndürür. Foreign mapping takip edilmez; name/owner gibi başka oyuncu bilgisi hataya konmaz. getMyCompanyState serverTime taze; create replay serverTime ilk committed zamandır.

## 4 — Migration ve bozuk veri koruması

Legacy users belgesi veya parent'sız serverState/current dahil herhangi bir legacy kayıt varlığı bootstrap/read için MIGRATION_REQUIRED. v2MigrationRecords belgesi veya player/company üzerinde migration ile başlayan herhangi bir field varlığı da aynı şekilde bloklanır; false/completed anlamı yorumlanmaz. Schema/status/owner/name/timestamp bozukluğu, null/yabancı mapping, missing mapped company veya unmapped deterministic company COMPANY_STATE_INVALID/FAILED_PRECONDITION verir. Auto-repair/reset/merge yok.

Yerel modelin fixed lookupları tüm production orphan/legacy kaynaklarını keşfetmez. Gerçek UID/company mapping, marker semantiği, money/cash ve level/companyLevel UNKNOWN. Production eligibility/discovery, migration, backup/restore ve cutover ayrıca yetkilendirilmeden production bootstrap BLOCK. Gerçek veri okunmadı.

## 5 — Doğrulama ve gerçek kanıt

Son `backend/` kökünde `npm run validate`: exit 0. Node 22 mevcut pinned tooling. Son kaynak build/typecheck sonrası testler çalıştı; test sırasında kaynak değiştirilmedi.

| Kontrol | Sonuç | Kanıt |
|---|---|---|
| Build / strict TypeScript | PASS | tsc |
| Typecheck | PASS | tsc --noEmit |
| Unit | PASS | 9/9, 0 fail/skip; önceki 8 + company name/ID/allowlist |
| Emulator integration | PASS | 5/5, 0 fail/skip; önceki 4 + company callable senaryoları |
| Auth + identity bootstrap | PASS | Synthetic anonymous guest gerçek Auth Emulator ve callable |
| Aynı key concurrency | PASS | 8 eşzamanlı istek, aynı committed result/time |
| Farklı key concurrency | PASS | 8 istek, tek başarı/şirket/receipt, diğerleri COMPANY_EXISTS |
| Replay / changed name | PASS | Stable receipt/updateTime, normalized name replay; conflict |
| Ownership / mapping | PASS | Missing/corrupt/foreign owner ve mapping fail-closed |
| Read-only | PASS | Player/company/receipt updateTime değişmedi; no-company create yapmadı |
| Missing identity | PASS | Player/company/receipt oluşmadı |
| Migration / legacy | PASS | 16 bozuk/legacy/migration fixture varyantı, no repair/no writes |
| Replay current-state koruması | PASS | Suspended/migration/foreign mapping eski key ile reddedildi |
| Identity retry / email linking | PASS | UID/company association ve metadata korundu |
| Default-deny Rules | PASS | Mevcut get/list/create/update/delete identity/company/receipt deny suite |
| BACKEND-03 atomiklik | PASS | Transaction rollback ve receipt/effect tekilliği regression |
| Emulator shutdown | PASS | CLI cleanup; 4400,4500,5001,8080,9099,9150,8428 port bind kontrolü |
| Runtime audit | PASS | Ayrı npm audit --omit=dev --audit-level=high --json, 0 bulgu |
| Full audit | FAIL | Ayrı npm audit --audit-level=high --json; 3 high + 4 moderate |
| Git diff / whitespace / scope | PASS | Yalnız 9 intended dosya; protected paths aynı |
| Secret-pattern / UTF-8 | PASS, sınırlı | Değişen dosyalarda gerçek secret kalıbı yok; belgeler strict UTF-8 |
| Production / real provider / migration / CI | NOT RUN | Kapsam dışı |

Ara koşu: ilk suite geçti. Replay current-state kontrolü eklenirken bir callback parametresinin uid/_uid adı build TS2552 üretti; düzeltildi. Son tam suite geçti. Bu ara derleme hatası gizlenmez; eski başarı son kaynaklar için kanıt sayılmadı.

Company testinde legacy/migration/bozuk fixture'ların before/after content ve updateTime eşitliği, failed receipt yokluğu sınandı. Command katmanının write-sonrası exception rollback testi de korunur. Synthetic cleanup yalnız test-owned refs/Auth users; global database reset yok. Gerçek production migration veya ekonomik etki testi yapılmadı.

## 6 — Güvenlik, dependency ve risk kararı

Mevcut fail-closed localEnvironment/launcher/config/default-deny Rules değişmedi. Yalnız demo-logisticore-local loopback endpoint'leri. Yeni external service, Pub/Sub, tracing, telemetry consumer, remote bind veya credentials yok. Manifest/lockfile/Node major/CLI sürümü değişmedi. Public callable'lar yalnız yerel emülatör entrypoint'inde; production export guard ile reddedilir. Yeni auth/ownership bypass veya financial mutation yok.

Üç advisory unresolved:

- GHSA-vfj7-8cjw-p6xm — firebase-tools → chokidar 3.6.0 → braces 3.0.3.
- GHSA-8988-4f7v-96qf — firebase-tools → pubsub 5.3.1 → core 1.30.1.
- GHSA-w5hq-g745-h8pq — firebase-tools → gaxios 6.7.1 → uuid 9.0.1.

Yedi paket kaydı yedi ayrı exploit değildir. Yeni glob/baggage/UUID consumer yok, input bu advisory yollarına yönlendirilmez. BACKEND-01 §19 onayı yalnız trusted local/emulator 16–30 Ekim 2026, ilk recheck 23 Ekim. Scanning bastırılmadı, npm audit fix/upgrade yok. CI/untrusted, gerçek DEV/PROD, deployment ve migration onaylı değildir. Yeni dependency veya trust boundary ayrı review ister.

## 7 — Maliyet

İlk create: transaction 6 read + commit sonrası receipt 1 read; 3 write. Replay: 6 read/0 write (güncel eligibility güvenliği dahil). Farklı key duplicate: 6 read/0 write. Read: 5 read/0 write. Contention retry/index maliyetleri eklenir. Bounded direct document lookup; global counter, collection scan, heartbeat, schedule veya broad index yok. Production latency/fatura ölçümü değildir.

## 8 — Dosyalar

Yeni:

- backend/functions/src/company.ts
- docs/api-contracts/COMPANY_CONTRACT.md
- docs/backend/BACKEND-04_REPORT.md

Değişen:

- backend/functions/src/commands.ts — üç company domain code eklemesi, executor algoritması aynı.
- backend/functions/src/identity.ts — mevcut invariant shared helper, behavior korunur.
- backend/functions/src/index.ts — iki guarded callable export.
- backend/tests/unit.test.mjs
- backend/tests/emulators.test.mjs
- backend/README.md

Unity/Packages/ProjectSettings, dependencies/lockfile, environment guard, emulator launcher/config/Rules ve önceki faz dokümanları korunur.

## 9 — Gelecek sınırı ve kapılar

COMPANY_CONTRACT implemented local API, sentetik fixtures, Unity startup/read/cache ve localization beklentilerini içerir. Economy-ready transition, ürün onaylı başlangıç finans/progression kuralları, schema uyumu BACKEND-05 için PROPOSED/DEFERRED; mevcut status ready diye kullanılamaz. Name-change ayrı command; transfer UID-derived ID/mapping tasarımının ayrıca ele alınmasını gerektirir. Account merge ve production migration yok.

BACKEND-04 FUNCTIONAL: PASS.
LOCAL SECURITY: CONDITIONAL PASS — süreli yerel istisna.
GENERAL SECURITY AUDIT: HOLD — unresolved advisories.
PRODUCTION / DEPLOYMENT / MIGRATION: BLOCK.
BACKEND-05 READINESS: Teknik foundation hazır; ayrı faz/ürün kuralları ve insan incelemesi için HOLD.

Kullanıcı talimatıyla yalnız bu dokuz dosya commit/push ve main hedefli PR için hazırlanır; işlem sonuçları final yanıt/Git geçmişindedir. PR merge edilmez, BACKEND-05 başlatılmaz.
