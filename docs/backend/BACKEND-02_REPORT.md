# LOGISTICORE — BACKEND-02 REPORT

Tarih: 16 Ekim 2026, Europe/Istanbul. Kapsam: yalnız güvenilir LOCAL/EMULATOR; tüm kimlikler sentetik.

## 1 — Yönetici özeti

Authenticated `ensurePlayerIdentity`, idempotent transaction bootstrap ve sunucu içi company ownership primitive uygulandı. Sağlayıcı credential'ları backend'e taşınmaz. Şirket/ekonomi oluşturma, migration ve production endpoint uygulanmadı. BACKEND-03 başlatılmadı.

## 2 — Repository ve branch

Başlangıç: `git rev-parse --show-toplevel` → `D:/Projects/unitylogisticore`; `git branch --show-current` → `feature/backend-02`; `git status --short --branch` → yalnız `## feature/backend-02` (temiz). Komutlar bu makinenin sahiplik kontrolü için yalnız çağrıya özel `-c safe.directory=D:/Projects/unitylogisticore` ile çalıştırıldı; Git config değiştirilmedi. Beklenmeyen önceki değişiklik bulunmadı. BACKEND-00/01, foundation ve Unity client sözleşmeleri, kaynaklar, guard, launcher ve testler incelendi.

## 3 — Oluşturulan dosyalar

- `backend/functions/src/identity.ts`
- `docs/api-contracts/IDENTITY_CONTRACT.md`
- `docs/backend/BACKEND-02_REPORT.md`

## 4 — Değiştirilen dosyalar

- `backend/functions/src/index.ts`: yerel callable ve tek Firestore istemcisi.
- `backend/functions/tsconfig.json`: Node16 → NodeNext module/resolution.
- `backend/tests/unit.test.mjs`: malformed auth ve strict request testleri.
- `backend/tests/emulators.test.mjs`: identity, concurrency, linking/collision, ownership, legacy fixtures ve Rules kapsamı.
- `backend/README.md`: BACKEND-02 kapsamı, Firestore initialization ve derleyici uyumu.

Dependency manifestleri/lockfile, launcher, environment guard, emulator config, Rules, Unity ve önceki audit/raporlar değiştirilmedi.

## 5 — Authentication tasarımı

Yalnız callable `request.auth.uid` kimlik kanıtıdır. Eksik/boş/bozuk/path olarak geçersiz UID unauthenticated; payload tam olarak `{protocolVersion:1}` olmalıdır. UID/owner/role/admin/finans dahil her ek alan invalid-argument. HttpsError native codes ve localization-friendly details kullanılır. Framework kaynaklı hatalar details taşımayabilir. Response yalnız çağıranın playerId/schema/status bilgisi ve serverTime içerir; e-posta/token/claim yoktur. Uygulama bu verileri loglamaz.

## 6 — Identity şeması

Aday yol `v2Players/{trustedUid}`. Dört alan: `schemaVersion:1`, `authUid:trustedUid`, `identityStatus:active`, `createdAt:FieldValue.serverTimestamp()`. Transaction read + absent ise create. Var olan zorunlu invariant'lar yanlışsa IDENTITY_CONFLICT; repair veya overwrite yoktur. Retry geçerli belgenin ekstra metadata'sını da korur. Cash/XP/level/reputation/assets/company/ledger yazısı yoktur.

## 7 — Auth UID continuity

Sağlayıcı değişimi backend kimliğini değiştirmez; backend provider alanı tutmaz. Aynı Auth user'a başarılı linking aynı belgeyi kullanır. Ayrı sign-in farklı UID döndürürse başka hesap kabul edilir; şirket/progress transfer edilmez. Emulator sonucu production continuity kanıtı değildir.

## 8 — Anonymous → permanent linking

Kurulu Firebase JS SDK `signInAnonymously` + `EmailAuthProvider.credential` + `linkWithCredential` ile gerçek Auth Emulator işlemi sınandı. UID aynı kaldı, isAnonymous=false oldu, refresh token sonrası callable aynı identity'yi döndürdü ve Firestore metadata korundu. İkinci client Auth instance üzerinden email/password sign-in aynı UID'yi döndürdü. Bu ikinci oturum testidir; gerçek mobil cihaz testi değildir. [Firebase linking davranışı](https://firebase.google.com/docs/auth/web/account-linking).

## 9 — Collision ve recovery

İkinci sentetik guest aynı email credential'ını link etmeye çalışınca SDK collision hatası verdi; test email-already-in-use veya credential-already-in-use sınıfını kabul eder. İkinci guest UID'si ve iki identity belgesi korundu. Otomatik merge/transfer yok. Recovery UI, kullanıcıya hesap seçimi ve duplicate resolution ayrı faz/ürün kararıdır; ikinci parola sistemi kurulmadı.

## 10 — Company ownership

`requireCompanyOwner(db, trustedAuth, companyId, transaction?)` yalnız server primitive. Canonical `v2Companies/{companyId}.ownerUid` trusted UID ile karşılaştırılır. Unauthenticated, invalid ID, missing, malformed ve foreign owner ayrı hata sınıflarıdır; owner/private şirket içeriği dönmez. Public callable olarak export edilmez. Gelecek mutation aynı Firestore transaction'ını geçirerek check/write yarışını önlemelidir. Public endpoint'ler varlık bilgisinin dahi gizlenmesi gerekiyorsa iç not-found/permission-denied ayrımını normalize etmelidir. Şirket create/transfer/merge implementasyonu yok; seed yalnız testte.

## 11 — Firestore Rules

Default-deny değişmedi. Authenticated ve unauthenticated get/list/set/update/delete negatif testleri identity, company root ve finance yollarını kapsar. Unity identity'yi callable cevabından alır. Admin transport Rules'ı aşar; auth/ownership doğrulaması açıkça server'dadır.

## 12 — API sözleşmesi

`IDENTITY_CONTRACT.md`: endpoint, us-central1, request/response örnekleri, hata/messageKey tablosu, Unity cache sınırı, provider sorumluluğu, direct access yasağı ve açık sorular. IMPLEMENTED / PROPOSED / DEFERRED / UNKNOWN ayrıdır. FOUNDATION_CONTRACT tarihsel sağlık sözleşmesi korunmuştur. C# veya Assets değişikliği yoktur.

## 13 — Doğrulama

Komut: backend kökünde `npm run validate`; build + typecheck + Node unit + korumalı launcher ile Auth/Firestore/Functions integration. Ayrı runtime/full `npm audit --audit-level=high --json` çağrıları yapıldı; runtime için `--omit=dev` eklendi.

| Kontrol | Sonuç | Kanıt / kapsam |
|---|---|---|
| TypeScript build | PASS | tsc, strict; skipLibCheck=false |
| Typecheck | PASS | tsc --noEmit |
| Unit ve isolation | PASS | 5/5; trusted auth, payload, project/endpoint/credentials, gerçek entrypoint fail-closed |
| Auth integration | PASS | Synthetic signup, linking, ikinci oturum, collision |
| Functions integration | PASS | Health ve identity; auth/payload reddi ve safe structured errors |
| Firestore identity | PASS | 6 eşzamanlı ilk çağrı, tek belge; retry/metadata/server timestamp; farklı UID; dört invariant conflict |
| Ownership | PASS | owner, foreign owner, missing, invalid ID, malformed owner; transaction kullanım yolu |
| Security Rules | PASS | Identity/company root dahil client deny |
| Legacy sentetik koruma | PASS | users + serverState fixture değerleri değişmedi |
| Emulator integration toplam | PASS | 3/3 üst seviye test; skip yok |
| Emulator shutdown | PASS | CLI cleanup ve ayrıca port kontrolü |
| Runtime dependency audit | PASS | 0 bulgu |
| Full dependency audit | FAIL | 7 paket kaydı: 3 high + 4 moderate; 3 temel advisory |
| Git diff / protected paths | PASS | Yalnız belirtilen 8 dosya; korunan yollar değişmedi |
| Gerçek Apple/Google ve cihaz testleri | NOT RUN | Ayrı entegrasyon gerektirir |
| Production / migration / CI | NOT RUN | Yetkisiz, kapsam dışı |
| Yeni npm ci | NOT RUN | Bağımlılık değişmedi; bu faz yeniden kurulum iddiası yapmaz |

İlk denemeler ve düzeltmeler: repo kökünde yanlışlıkla npm validate denemesi package.json bulunamadığından hiçbir script çalıştırmadı; backend kökünde tekrarlandı. İlk build yeni Firestore tip zincirinde TS1479 verdi. NodeNext sonrası geçti. Sandbox altında hub 4400 startup timeout ve audit DNS ENOTFOUND görüldü; onaylı execution ile aynı launcher ve npm audit tekrarlandı. İlk emulator run custom Admin app credential'ın Firestore tarafından reddedilmesini ortaya çıkardı; guarded constructor düzeltildi. Negatif fixture'da yanlışlıkla aynı UID kullanılmıştı; başka UID ile düzeltilip suite tekrarlandı. Önceki başarısız denemeler başarı sayılmadı.

## 14 — Performans ve güvenilirlik

Kimlik için deterministic document ID, normalde bir transaction okuması; yalnız ilk oluşturmada bir yazı. Contention'da SDK retry ek okumalar yaratabilir. Cevap timestamp için tekrar DB read yok. Şirket kontrolü tek ID okuması; global hotspot/query/listener/heartbeat yok. Ekonomi idempotency receipts bu fazda yok. Emulator süreleri production latency/cost benchmark değildir.

## 15 — Güvenlik ve dependency compatibility

Node 22.23.2, npm 10.9.8, Java 21.0.12; manifest/lockfile ve CLI 15.34.0, Functions 7.4.0, Admin 14.5.0, TypeScript 5.9.3 sabit kaldı. Lock SHA-256: `84735D1B295F7B0FA86FA05AFB13A32C9A3ECF9085C19E8A208D383843EF50BB` (BACKEND-01 ile aynı).

Firestore importu google-gax → google-auth-library 10.5.0 → gtoken 8.0.0 tiplerini görünür kıldı. Eski Node16 modülü CJS/ESM tip importunu reddetti. NodeNext Node 22'nin require(ESM) davranışını modeller; Node 22 üzerinde google-gax require smoke check geçti. Strict/skipLibCheck değişmedi; runtime CommonJS kalır. [TypeScript resmi açıklaması](https://www.typescriptlang.org/docs/handbook/modules/reference.html). Paket override/upgrade, Node major veya launcher değişikliği yoktur.

Admin `getFirestore(app)` özel health credential'ını kabul etmediğinden mevcut Admin export'u `new Firestore` kullanıldı. Önce aynı guard, sonra sabit demo project/127.0.0.1:8080/ssl=false. Kurulu Firestore emulator transport kaynağı ssl=false için Bearer owner kullanır; gerçek credentials/ADC gerekmiyor. Factory SDK'nin varsayılan trace utility'sini `FIRESTORE_ENABLE_TRACING=off` ile açıkça kapatır. Tracing/exporter/PubSub veya yeni external telemetry consumer yoktur. Bu, emulator-only tasarımdır; production'a taşınamaz.

Mevcut risk istisnası BACKEND-01 §18.8/§19 kapsamıyla sınırlı: 16–30 Ekim 2026, ilk recheck 23 Ekim. Yeni kod sabit güvenilen source watcher yolunda; glob/config girişi, UUID consumer veya baggage propagator eklemez. Mevcut CLI advisory maruziyetinde yeni yol saptanmadı; bu kapsamlı exploit kanıtı değildir. Derleyici uyumu ve mevcut SDK'nin loopback veritabanı kullanımı yeni cloud/güvenilmeyen input sınırı açmaz. Yeni production/CI/untrusted/network sınırı veya dependency değişimi yeniden onay ister.

| Kalan advisory | Mevcut zincir | Durum |
|---|---|---|
| GHSA-vfj7-8cjw-p6xm | firebase-tools → chokidar 3.6.0 → braces 3.0.3 | Unresolved dev; trusted glob/path koşulu |
| GHSA-8988-4f7v-96qf | firebase-tools → pubsub 5.3.1 → core 1.30.1 | Unresolved dev; Pub/Sub/baggage consumer yok |
| GHSA-w5hq-g745-h8pq | firebase-tools → gaxios 6.7.1 → uuid 9.0.1 | Unresolved dev; yeni UUID consumer yok |

Audit downgrade önerisi uygulanmadı; findings bastırılmadı. Genel security audit HOLD. Runtime clean sonucu production güvenlik onayı değildir. Sır kontrolü değişen kaynak/belgelerde kalıp taraması ve diff incelemesiyle sınırlıdır; test parolası yalnız sentetik example.invalid fixture içindir, gerçek credential değildir.

## 16 — Bilinen sınırlamalar

Local callable boş v2 identity oluşturabilir; legacy migration eligibility kontrolü uygulamaz. Bu yüzden production bootstrap olarak kullanılmaz. Emulator Auth production token imza doğrulaması kanıtı değildir. App Check, rate limiting, production IAM ve device/recovery UI ayrı iş. Ownership primitive trusted server çağrıları içindir; TypeScript yapısal tipi tek başına auth doğrulaması değildir.

## 17 — Production compatibility UNKNOWN

Gerçek provider'lar, eski UID mapping, canonical save/serverState, company cardinality/ownership ve migration marker bilinmiyor. money=cash veya level=companyLevel varsayılmadı. Gerçek player/UID okunmadı, export/import edilmedi.

## 18 — Migration önkoşulları

Ayrı yetkiyle UID koruması ve legacy kayıt tespiti; migration eligibility ve canonical ownership kanıtı; provider/linking/collision/recovery politikası; duplicate çözümüne insan kararı; backup/restore ve rollback provası; legacy writers/cutover planı gerekir. Şimdiki fixtures yalnız sentetiktir; migration PASS değildir.

## 19 — Sonraki faz

BACKEND-03 LOCAL teknik temeli hazır; faz kapsamı ve insan incelemesi için HOLD. Bu çalışma BACKEND-03 başlatmaz. Unity developer review: SDK/provider matrisi, localization keys, collision/recovery UX ve UID cache temizliği açık.

## 20 — Karar

BACKEND-02 FUNCTIONAL GATE: PASS (yerel emulator kapsamı).
BACKEND-02 LOCAL SECURITY GATE: CONDITIONAL PASS (BACKEND-01 süreli dar istisna koşulları).
GENERAL SECURITY AUDIT: HOLD — unresolved advisories.
PRODUCTION DEPLOYMENT / MIGRATION: BLOCK.
BACKEND-03 READINESS: HOLD — ayrı faz incelemesi/onayı.

İlk uygulama incelemesinde commit/push yapılmadı. Sonraki finalization talimatı yalnız sekiz BACKEND-02 dosyasının commit/push işlemini ve main hedefli PR açılmasını yetkilendirir; merge/deploy ve BACKEND-03 kapsam dışıdır.

## 21 — Final verification

16 Ekim 2026 finalization: branch feature/backend-02, başlangıçta yalnız beklenen sekiz dosya değişikliği doğrulandı. Identity sözleşmesi strict UTF-8 decoder ile geçti; replacement character veya Türkçe karakter bozulması bulunmadı. Yalnız retry açıklaması netleştirildi: identity verisi sabit, serverTime her yanıt için ayrı üretilir ve farklı olabilir. Kritik implementation/contract uyuşmazlığı saptanmadı.

Son `npm run validate` exit 0: build/typecheck, 5/5 unit ve 3/3 emulator integration PASS; skip/fail yok. Emulator kapanışı ve loopback portlarının serbest kalması doğrulandı. Runtime audit ayrı çalıştırıldı: exit 0, sıfır bulgu. Full audit ayrı çalıştırıldı: exit 1, aynı üç advisory ve 3 high + 4 moderate paket kaydı; FAIL/HOLD görünür kalır. Mevcut 16–30 Ekim yalnız yerel güvenlik istisnası uygulanır; ilk recheck 23 Ekim. Yeni bağımlılık/lockfile değişikliği yok.

Git whitespace/diff ve sınırlı secret-pattern kontrolü PASS; gerçek credential bulunmadı. Assets/Packages/ProjectSettings ve izolasyon dosyaları değişmedi. Functional gate PASS; production BLOCK; genel audit HOLD. Git commit/push/PR işlem sonuçları finalization yanıtında ve Git geçmişinde raporlanır.
