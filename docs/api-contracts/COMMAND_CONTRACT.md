# LogistiCore — Command Contract

Durum: BACKEND-03. **IMPLEMENTED:** yalnız internal TypeScript `executeCommand`; callable endpoint eklenmedi. `localHealth` ve `ensurePlayerIdentity` sözleşmeleri değişmedi. Aşağıdaki request/response örnekleri gelecekteki komuta özgü Unity entegrasyonuna hazırlıktır; bugün çağrılabilir oyun endpoint'i değildir.

## Request ve trusted sınır

```json
{"protocolVersion":1,"idempotencyKey":"synthetic-request-0001","payload":{"label":"proof"}}
```

Komut adı server'ın sabit CommandDefinition nesnesinden gelir, request'ten gelmez. Yalnız üç üst alan kabul edilir. UID yalnız callable adapter'ın doğrulanmış request.auth context'inden alınmalıdır. CommandDefinition.validate payload'ın alanlarını/tiplerini allowlist ile doğrulayıp normalize etmek zorundadır; client UID/owner/role, nihai fiyat, ödül veya bakiye kabul edilemez. Sentetik label yalnız test fixture'ındadır; para API'si değildir. Ortak executor komuta özgü validator'ın yerine geçmez.

Protokol yalnız numeric 1. Key 1–128 ASCII karakter: A–Z, a–z, 0–9, underscore, hyphen; `__...__` reserved biçimi reddedilir. Slash/space/unicode key yok. Unity yeni intent için en az 128-bit rastgele key üretmeli ve aynı intent'in retry'larında aynı key/payload'ı saklamalı; biçim kontrolü entropy garantisi değildir. UID değişiminde eski hesabın intent/cache'i yeni UID'ye taşınmaz.

Request canonical JSON UTF-8 en fazla 8192 byte, result en fazla 8192 byte; azami depth 12 ve 2048 ziyaret edilen değer. Yalnız plain JSON, finite numbers, ordered arrays; object key'leri sıralanır, -0 → 0. Undefined, Date, class, getter, cycle ve prototype anahtarları reddedilir. Domain string normalizasyonu (trim/case vb.) command validator'a aittir; generic executor rastgele metni değiştirmez. Validator sonrası payload da bounded/canonical JSON kopyasıdır.

SHA-256 fingerprint: canonical `{command, protocolVersion:1, companyId, payload}`. Scope yoksa companyId=null. Client'ın şirket ID'si yalnız intent olabilir; optional resolveCompany aynı transaction içinde canonical mapping okur. Scope 1–128 ASCII alnum/underscore/hyphen. Resolver read-only olmalı ve aynı mantıksal intent için kararlı scope çözmelidir. Mapping değişip farklı scope çözerse eski key IDEMPOTENCY_CONFLICT verir; yeni scope'a eski etki taşınmaz. Resolver replay sırasında da çalışır; erişilebilirliği ve bu scope kararlılığı replay önkoşuludur. Gelecek şirket transferi/remapping politikası ayrı review gerektirir.

## Receipt — IMPLEMENTED schema v1

`v2Players/{trustedUid}/commands/{idempotencyKey}`:

| Alan | Tür / kural |
|---|---|
| receiptSchemaVersion | 1 |
| command | Server-selected sabit komut adı, 1–64 ASCII alnum; ilk harf |
| protocolVersion | 1 |
| requestFingerprint | 64 karakter SHA-256 hex |
| companyId | resolved canonical scope veya null |
| result | En fazla 8192 byte public JSON |
| committedAt | Firestore serverTimestamp |

Receipt toplamı metadata dahil yaklaşık 10 KiB altında tutulur. Key belge yolundadır; tokens, credentials, email veya raw request saklanmaz. Result whitelist'inin sensitive veri taşımamasından command yazarı sorumludur. Başarılı receipt kalıcıdır: TTL, delete job veya otomatik pruning yok. Bozuk receipt overwrite/repair edilmez; FAILED_PRECONDITION. Semantik/protocol/normalizasyon değişiklikleri eski receipt'lerle uyum gerektirir; aynı komut adı altında sessiz semantik değişimi yapılmamalıdır.

## Transaction garantisi ve replay

Auth/envelope/validator → transaction receipt read → optional scope read → fingerprint → mevcut receipt doğrulama/replay veya domain apply → receipt create. Domain callback ownership ve tüm değişebilir önkoşulları aynı transaction içinde, bütün yazılardan önce okumalıdır. `requireCompanyOwner(db, auth, companyId, transaction)` bu read aşamasında kullanılabilir. Native permission-denied normalize edilir; not-found/failed-precondition ortak FAILED_PRECONDITION olur, özel şirket bilgisi sızmaz.

Domain writes ve receipt **aynı transaction** ile commit olur. Callback tekrar çalışabilir; dış HTTP, mail, log tabanlı etki, rastgele business sonucu veya transaction dışı write yapamaz. Callback trusted server kodudur; executor keyfi server kodunu sandbox etmez. Callback receipt silmemeli/değiştirmemeli ve farklı UID'nin state'ine yazmamalıdır. Firestore read-after-write'ı reddeder; test bunu rollback ile doğrular. [Resmi transaction davranışı](https://firebase.google.com/docs/firestore/manage-data/transactions).

İlk başarıda serverTimestamp commit sonrası receipt read ile çözülür. Bu ek read başarısız olsa bile commit geri alınmış sayılmaz; aynı key ile retry yapılır. Replay receipt result ve özgün committedAt-derived serverTime'ı döndürür; yeni response zamanı üretmez ve domain callback'i çalıştırmaz. Scope aynı kaldığında aynı UID+key+normalize request aynı committed outcome verir. Farklı payload/command/scope IDEMPOTENCY_CONFLICT. Farklı UID bağımsız namespace'tir.

```json
{"protocolVersion":1,"serverTime":"2026-10-16T12:00:00.000Z","result":{"count":1,"status":"recorded"}}
```

Bu sentetik internal başarı örneği ve replay cevabı aynıdır. serverTime UTC ISO-8601 millisecond gösterimidir; Firestore timestamp nanosecond hassasiyeti receipt'te korunur. Replay saati güncel zaman senkronizasyon kaynağı değildir. BACKEND-02 identity callable'ının her yanıtta taze serverTime üretmesi değişmedi.

Başarısız transaction başarılı receipt bırakmaz. Önkoşullar sonradan sağlanırsa daha önce hata veren key başarılı olabilir. **Farklı key'ler aynı reward'ı iki kez talep etmeyi engellemez:** gelecekte claim marker/domain uniqueness aynı transaction'a eklenmelidir. Şirket tekilliği ve finans invariant'ları uygulanmış değildir.

## Hatalar ve Unity retry

| details.code | Callable code | messageKey |
|---|---|---|
| UNAUTHENTICATED | unauthenticated | errors.unauthenticated |
| INVALID_ARGUMENT | invalid-argument | errors.invalid_argument |
| UNSUPPORTED_PROTOCOL | failed-precondition | errors.unsupported_protocol |
| IDEMPOTENCY_CONFLICT | already-exists | errors.idempotency_conflict |
| PERMISSION_DENIED | permission-denied | errors.permission_denied |
| FAILED_PRECONDITION | failed-precondition | errors.failed_precondition |
| RETRY_LATER | unavailable | errors.retry_later |
| INTERNAL | internal | errors.internal |

```json
{"code":"IDEMPOTENCY_CONFLICT","messageKey":"errors.idempotency_conflict"}
```

Örnek HttpsError.details'tır; yeni callable wire endpoint'i değildir. EN/TR/DE client localization. Stack, DB path, private state veya raw exception public hata içine konmaz. Tanınan transient gRPC 4/10/14 unavailable olarak eşlenir; bilinmeyen hatalar INTERNAL kalır, beklenen domain hatası diye sunulmaz. Framework/network hatası details taşımayabilir; client fallback gerekir. Production observability/redaction politikası ertelendi.

Timeout/kayıp cevap/RETRY_LATER için bounded exponential backoff+jitter ve aynı key/intent kullanın; hatada yeni key üretmeyin. INTERNAL commit sonucunu belirsiz bırakabilir; aynı key korunmalı, sonsuz otomatik döngü yapılmamalı. Conflict/invalid/protocol hatasında otomatik retry yok. UID'ye bağlı pending intent cache'i client yetki kanıtı değildir. Direct Firestore read/write default-deny; receipt istemci tarafından değiştirilemez.

## Kapsam, maliyet ve ertelenen işler

IMPLEMENTED: internal executor, canonical fingerprint, durable receipt, bounded result, structured errors, gerçek emülatör transaction kanıtı. Test operasyonu sadece tests dosyasında; runtime export veya admin/grant callable yok.

Receipt overhead: ilk başarıda transaction içinde 1 receipt read + 1 receipt write ve commit sonrası 1 receipt read; replay 1 receipt read/0 write. Scope resolver ve domain okumaları eklenir; contention callback retry'ları ek read maliyeti getirir. UID+key başına contention var, global sayaç/hotspot yok. Index fan-out maliyeti bu sayıların dışındadır; receipt alanlarının index exemption/retention ölçeği production öncesi ayrıca tasarlanmalı.

PROPOSED: gelecek komuta özgü callable adapter'ları; immutable scope policy ve Unity retry UX.
DEFERRED: BACKEND-04 company/player bootstrap; BACKEND-05 finans/progression; rate limits, App Check/IAM, domain uniqueness/ledger, migration eligibility, production observability ve release review.
UNKNOWN: legacy UID/company canonical mapping, money/cash ve level/companyLevel anlamları. Gerçek Firebase DEV/PROD veya gerçek player data kullanılmadı. Yalnız demo-logisticore-local ve mevcut loopback guard. Yerel risk istisnası 16–30 Ekim 2026, ilk kontrol 23 Ekim; CI/untrusted/production'a taşınmaz.
