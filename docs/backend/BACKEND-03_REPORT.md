# LOGISTICORE — BACKEND-03 REPORT

Tarih: 16 Ekim 2026 (Europe/Istanbul). Kapsam: trusted LOCAL/EMULATOR.

## 1 — Kapsam ve Git başlangıcı

Repo `D:/Projects/unitylogisticore`, ilk dal `feature/backend-02`, çalışma ağacı temizdi. Origin fetch sonrası main `a43d094` (BACKEND-02 PR #3 merge) doğrulandı. Yerel main yalnız fast-forward ile güncellendi; `feature/backend-03` bu merge'den oluşturuldu. Main'de zaten bulunan Unity geliştiricisi commit'leri korundu; BACKEND-03 diff'i Unity dosyası içermez. Reset/stash/force push yoktur.

BACKEND-00/01/02 raporları, FOUNDATION/IDENTITY sözleşmeleri, README, existing implementation/test/launcher/config incelendi. Kullanıcı talimatı implementation ve testlerden sonra commit/push/main PR açmayı yetkilendirir; merge/deploy yetkisi vermez.

## 2 — Uygulanan altyapı

`commands.ts`: küçük internal executor, CommandError, canonical JSON ve SHA-256. Yeni framework, dependency, repository abstraction veya public callable yok. Command name server tanımından; UID trusted Auth context'ten. Envelope protocolVersion/idempotencyKey/payload strict allowlist; command payload validator zorunlu. İstek ve sonuç sınırı 8192 byte, depth 12/nodes 2048. Key ASCII alnum/underscore/hyphen 1–128, reserved/path biçimleri reddedilir. Hash komut/protokol/resolved company scope/normalized payload'ı kapsar.

Receipt `v2Players/{trustedUid}/commands/{key}`; schemaVersion, protocolVersion, command, fingerprint, scope, bounded result ve Firestore committedAt server timestamp içerir. TTL/otomatik silme yoktur. Başarısız domain işlemi successful receipt bırakmaz; aynı key önkoşul değişince başarılı olabilir. Farklı key için domain uniqueness ayrıca gerekir.

## 3 — Transaction ve replay

Receipt read + optional canonical scope resolution + domain reads/ownership/invariants + domain writes + receipt create aynı transaction callback'inde. Firestore read-before-write ve retry davranışı korunur. Receipt ve state beraber commit olur. İlk cevap commit sonrası receipt read ile çözülen server timestamp'i kullanır; replay stored result ve aynı serverTime döndürür. Replay domain apply'ı çağırmaz veya original receipt/state updateTime'ını değiştirmez. Post-commit read/response kaybı rollback sayılmaz; aynı key retry kurtarma yoludur.

Scope resolver read-only ve kararlı mapping üretmelidir. Mapping farklı scope'a dönerse hash değişir, conflict olur; eski etki başka şirkete taşınmaz. Domain callback trusted server kodudur: dış yan etki, receipt müdahalesi veya transaction dışı write yasaktır; executor arbitrary server kodu için sandbox değildir. Gelecek ownership check aynı transaction'a geçirilmeli; native denied/not-found/precondition güvenli ortak hatalara normalize edilir.

## 4 — Hata sözleşmesi

UNAUTHENTICATED, INVALID_ARGUMENT, UNSUPPORTED_PROTOCOL, IDEMPOTENCY_CONFLICT, PERMISSION_DENIED, FAILED_PRECONDITION, RETRY_LATER, INTERNAL. Stable `errors.*` localization keys. Ham exception/path/token/result private data public hata içine konmaz. Tanınan transient gRPC kodları retry_later; bilinmeyen hata internal. Production logging/observability/redaction politikası ayrıca tasarlanmalı; sessizce beklenen business hatası gibi sınıflandırılmaz.

## 5 — Test tasarımı ve kanıt sınırı

Yeni internal syntheticProof yalnız `tests/emulators.test.mjs` içindedir; Functions index export'u yoktur. Test başına unique sentetik UID/state/scope/company/legacy yolları; cleanup yalnız kayıtlı ref'leri siler, global reset yok. Değişen state bir test sayacıdır; para, inventory veya progression değildir.

Yeni unit kapsamı: protocol/envelope/key/size; canonical JSON sıralaması, -0, unsupported/cyclic/getter verileri; hash command/scope/payload ayrımı; sekiz domain error key'i. Yeni emulator kapsamı: auth/spoof/payload deny; sekiz concurrent identical request/tek effect; normalized replay, timestamp ve updateTime koruması; farklı payload/command/scope conflict; farklı UID; successful commit sonrası discarded response/retry; exception/precondition/permission/transient/oversized result/read-after-write rollback; failed key recovery; bozuk receipt fail-closed; ownership aynı transaction ve legacy fixture koruması. Rules receipt path get/list/write deny genişletildi. Önceki health/identity/linking/ownership/legacy testleri korunur.

Kayıp yanıt testi gerçek commit sonrası cevabı bilinçli atıp transport hatasını simüle eder; gerçek ağ koparma/production crash testi değildir. Emulator Auth production token güvenliği, gerçek provider ve migration kanıtı değildir.

## 6 — Maliyet ve reliability

İlk başarı receipt overhead: 1 transaction read + 1 receipt write + commit sonrası 1 read. Replay: 1 receipt read, 0 writes; optional scope/domain reads ayrıca. Contention transaction retry read maliyetini artırabilir. Global hotspot/counter/heartbeat/scheduled job yok. Sonuç bounded, receipt kalıcı; hacim/index exemption/retention maliyeti production öncesinde ölçülmeli. Farklı key'lerle reward duplication engeli BACKEND-05+ domain claim marker'ına bağlıdır; uygulanmış değildir.

## 7 — Güvenlik ve izolasyon

Yalnız demo-logisticore-local, Auth 127.0.0.1:9099, Firestore 127.0.0.1:8080, Functions 127.0.0.1:5001. Mevcut guard/launcher/default-deny Rules/SDK initialization değiştirilmedi. Yeni callable, cloud servis, tracing, telemetry, Pub/Sub, remote emulator erişimi veya credentials yok. Node major, dependencies, overrides ve Git lockfile içeriği aynı. npm ci bu fazda çalıştırılmadı; yeniden kurulum iddiası yok.

Runtime/full audit ayrı çalıştırıldı: runtime 0 bulgu PASS; full 3 high + 4 moderate paket kaydı FAIL. Üç temel advisory çözülmedi:

- GHSA-vfj7-8cjw-p6xm: CLI → chokidar 3.6.0 → braces 3.0.3.
- GHSA-8988-4f7v-96qf: CLI → pubsub 5.3.1 → OpenTelemetry core 1.30.1.
- GHSA-w5hq-g745-h8pq: CLI → gaxios 6.7.1 → uuid 9.0.1.

Yeni kod mevcut trusted source watcher/loopback sınırında kalır; yeni glob, baggage veya UUID consumer eklemez. BACKEND-01 §19 onayı 16–30 Ekim, ilk recheck 23 Ekim; yalnız trusted local/emulator. CI/untrusted/production'a genişletilmez. Yeni trust boundary/dependency değişimi ayrı review ister. Genel security audit HOLD; production BLOCK.

## 8 — Dosyalar

Yeni: `backend/functions/src/commands.ts`, `docs/api-contracts/COMMAND_CONTRACT.md`, `docs/backend/BACKEND-03_REPORT.md`.
Değişen: `backend/tests/unit.test.mjs`, `backend/tests/emulators.test.mjs`, `backend/README.md`.

Eski raporlar/sözleşmeler, Unity yolları, manifests/lockfile, index.ts/identity.ts/localEnvironment.ts, launcher/config/Rules korunur. README ve yeni contract IMPLEMENTED/PROPOSED/DEFERRED sınırlarını açıklar; Unity için örnekler gelecekteki command adapter'larına hazırlıktır.

## 9 — Ertelenenler ve production bilinmeyenleri

BACKEND-04 company/player bootstrap ve BACKEND-05 finans/progression uygulanmadı. Rate limiting/App Check/IAM, per-command payload/domain validation, migration eligibility, reward uniqueness/ledger, provider/device integration, command version compatibility ve scope remapping politikası ayrı faz/security review gerektirir. Gerçek legacy data okunmadı; money/cash veya level/companyLevel eşitliği varsayılmadı. Geliştirici controlled callback contract'ına uymalıdır; bu altyapı tek başına gelecekteki komutun business güvenliğini kanıtlamaz.

## 10 — Son doğrulama ve Git sonucu

Son npm run validate exit 0: build ve typecheck PASS; 8/8 unit ve 4/4 emulator integration PASS, fail/skip yok. Önceki koşuda yeni ownership hata eşleme testi eski derleme çıktısına karşı INTERNAL gördü; final kaynakların yeniden derlenmesiyle PERMISSION_DENIED doğrulandı. Son koşu sırasında kaynak değiştirilmedi. Emulator cleanup başarılı; 4400/4500/5001/8080/9099/9150 ve bu koşunun discovery portu sonradan loopback bind kontrolünden geçti. Git diff/whitespace, UTF-8 ve sınırlı secret-pattern taraması PASS; gerçek credential bulunmadı. Korunan Unity/manifest/lockfile/guard yolları a43d094 tabanıyla aynı. Git commit/push/PR sonuçları final yanıtta ve Git geçmişinde yer alır. BACKEND-04 başlamaz, PR merge edilmez.

| Son kapı | Karar |
|---|---|
| BACKEND-03 functional | PASS — local emulator |
| LOCAL security | CONDITIONAL PASS — mevcut 16–30 Ekim istisnası |
| Runtime audit | PASS — 0 bulgu |
| Full audit / general security | FAIL / HOLD — üç unresolved advisory |
| Production / migration | BLOCK / NOT RUN |
| BACKEND-04 readiness | Teknik temel hazır; ayrı faz incelemesi/onayı için HOLD |

Yalnız altı amaçlanan dosya commit kapsamındadır. Bu rapor production readiness veya gerçek ekonomik etki testinin yapıldığı iddiası değildir.
