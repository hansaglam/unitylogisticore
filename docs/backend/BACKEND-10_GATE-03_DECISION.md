# BACKEND-10 GATE-03 — İnsan merge kararı paketi

28 Ekim 2026. **Varsayılan karar HOLD.** Bu belge merge izni, yeni güvenlik istisnası veya production yetkisi vermez. Seçenek B seçilmiş değildir.

## Mevcut durum ve kanıt

Repo `D:/Projects/unitylogisticore`, branch `feature/backend-10-liveops`, HEAD `f2b7058e006a2b6f2fc46d12546e347b6dedf722`. [PR16](https://github.com/hansaglam/unitylogisticore/pull/16) aynı SHA ile OPEN/DRAFT; değiştirilmedi. Başlangıçta yalnız untracked BACKEND-10_WORKER_RECOVERY-02_REPORT.md vardı; korundu.

BACKEND-10 implementation/final review/RECOVERY-01/RECOVERY-02 raporları, SECURITY-01 remediation planı, SECURITY-02 raporu ve önceki security reassessment incelendi. Launcher ve npm workflow değişmedi.

| Ayrı kapı | Karar ve dayanak |
| --- | --- |
| Feature functional | PASS — mevcut aynı kaynakta unit43/43, emulator134/134; iki başarılı full run. Historical offline85/85, LiveOps8/8. GATE-03 yeni test koşusu değildir. |
| Worker reliability | HOLD — önceki128/6 ve123/11 emulator sonuçları korunur; aralıklı Windows startup kök nedeni bulunmadı. |
| Development dependency security | CONDITIONAL PASS yalnız mevcut16–30 Ekim trusted LOCAL koşullarında; general audit HOLD. |
| Production / real Firebase / migration | BLOCK. LOCAL main entegrasyonu bunları yetkilendirmez. |
| Numeric economy parameters | HOLD / UNAPPROVED SYNTHETIC — schedule, priority, bps/caps, supply, duration ve ekonomi dengesi ürün onayı değildir. |
| LOCAL merge | HOLD — ayrı açık insan risk kararı ve merge yetkisi yok. |

RECOVERY-02 üç bounded oturumda51/51 beklenen401, timeout0; sıralı cold1.69–1.82s, sekizli cold2.86–3.00s, warm4.81–30.56ms. Sekiz simultaneous cold request sekiz worker oluşturdu; creation→listening1.65–2.86s. Amplification gözlendi, geçmiş failure ile nedensellik gösterilmedi. Sağlıklı örneklerden named-pipe defect, application hotspot veya OS contention root cause çıkarılamaz. Diagnostic401 testi authenticated finansal iş yükünün yerine geçmez. Burada yeni diagnostic/full validate/stress testi yapılmadı.

## Manuel capture tasarımı

Ignored `backend/.local/worker-capture/capture-validation.ps1`; kısa yönerge aynı dizindeki README.md. Yalnız geliştirici açıkça çağırdığında çalışır, otomasyon/scheduler yok. Önce repo/branch/exact HEAD, backend Git değişmezliği, lock SHA256, kurulu CLI/Admin/Functions sürümleri, Node22/PowerShell7/git/npm/java, expected Firebase loopback config, credential/project/endpoint overrides, CIM erişimi ve emulator portları kontrol edilir. Geliştirici switch ile mevcut trusted-local koşullarını teyit eder; yeni onay yaratmaz. Onay30 Ekim2026 bitiminden sonra helper başlamayı reddeder; tarih bypass seçeneği yoktur.

Mevcut backend dizininde tam olarak `npm.cmd run validate` çağrılır; mevcut guarded launcher bütün Firebase isolation/lifecycle davranışını yönetmeye devam eder. Test/timeout/env/CLI flag değiştirilmez, install/deploy veya retry yok. Normal tamamlanmada orijinal npm exit code döndürülür. Ctrl+C/terminal kaybı normal test exit sonucu sayılmaz; interrupted/null veya eksik completion açık limitation'dır.

Benzersiz UTC+UUID klasörlerine invocation/completion, timestamped birleşik stdout/stderr, seçilmiş runtime/worker/startup/failure/timeout olayları ve PID/creation/CPU örnekleri yazılır. Process command line, environment ve credential değerleri yazılmaz; hassas stdout satırı değer yerine redaction marker olur. Debug body/header/metadata aktarılmaz. Firebase'in kendi ham ignored logları yüklenmez. İki saniyelik observer kısa event'leri/log deletion sonunu kaçırabilir; capture error ayrı dosyada bildirilir. Shutdown port evidence listener varsa bildirir, süreç öldürmez. Kesin request→worker bağlantısı logda yoksa UNKNOWN kalır.

**Helper çalıştırılmadı.** Yalnız PowerShell parser/syntax ve statik güvenlik incelemesi; gerçek exit-code/observer entegrasyonu bu görevde dinamik doğrulanmadı. Bu bir reliability mitigation değil, gelecekteki normal koşuda pasif kanıt hazırlığıdır. Invasive tracing/vendor patch/timeout artışı ayrıca onay ister.

## Güncel read-only güvenlik değerlendirmesi

GATE-03 fresh npm audit runtime0/exit0; full5 moderate affected package/exit1, high0/critical0; aynı iki advisory, yeni bulgu yok. Kanıtlar ignored gate-03-runtime-audit.json, full-audit.json, audit-exits.json. Kurulu package.json ve mevcut lock/RECOVERY-02 npm metadata kontrol edildi; dependency değişimi yapılmadı.

| Advisory | Kurulu dev zinciri ve risk | Patch / parent sınırı |
| --- | --- | --- |
| GHSA-8988-4f7v-96qf | CLI15.34.0 → @google-cloud/pubsub5.3.1 → @opentelemetry/core1.30.1. Attacker-controlled büyük W3C baggage extractor girdisi ek memory allocation/availability riski. | core2.8.0 patch; pubsub^5.2.0 aralığındaki incelenmiş sürümler core^1.30.1 istiyor. Patch doğrudan bu range'e sığmaz. |
| GHSA-w5hq-g745-h8pq | CLI15.34.0 → gaxios6.7.1 → uuid9.0.1; CLI google-auth-library9.15.1 ve gcp-metadata6.1.1/gtoken7.1.0 üzerinden aynı gaxios/uuid'ye ek yollar. Dış buffer/offset ile v3/v5/v6 partial/out-of-range write davranışı; native RCE iddiası yok. | uuid11.1.1 (ayrıca12.0.1/13.0.1) patch; gaxios6.7.1 uuid^9.0.1 ister;6.7.0'ın^10.0.0 aralığı da yamayı içermez. |

Kaynaklar: [OpenTelemetry advisory](https://github.com/advisories/GHSA-8988-4f7v-96qf), [uuid advisory](https://github.com/advisories/GHSA-w5hq-g745-h8pq); aynı gün RECOVERY-02 registry metadata ve kurulu consumer incelemesi. Pub/Sub consumer TraceContext propagator kullanıyor; desteklenen launcher Pub/Sub açmıyor. Gaxios argümansız uuid.v4 çağırıyor. Bu incelenmiş yollarda vulnerable API reachability doğrulanmadı; bütün CLI komutları veya saldırgan yerel prosesler için erişilemezlik ispatı değildir. Runtime gaxios7/8 yolları dev uuid9 bulgusuyla karıştırılmaz. Beş affected paket iki advisory'nin parent propagation'ını içerir, beş exploit yolu değildir.

Kontroller: trusted workstation/checkout, sabit toolchain, yalnız demo/loopback, credential rejection, default-deny Rules, dış forwarding/untrusted code/CI yok, public catalog publication yok. Aynı makinedeki kötü niyetli süreçlere karşı loopback tek başına güvenlik sınırı değildir. Residual risk: gözlenmeyen vulnerable consumer/girdi yolları, yerel DoS/identifier bozulması ve aralıklı worker kaynaklı test başarısızlığı/teşhis belirsizliği. Geçmiş başarılı testler yeni risk veya uzun dönem güvenilirlik garantisi değildir.

Yeni advisory, dependency/config/guard değişimi, yeni UUID/baggage consumer, public port/forwarding, untrusted fork/kod, credential veya real Firebase kullanımı değerlendirmeyi geçersiz kılar. Kurulu parent kısıtları altında doğrudan patched çözüm doğrulanmadı; gelecekte remediation ayrı lock/toolchain uyumluluk incelemesi ister. Global override/downgrade/audit suppression uygulanmadı.

## İnsan için iki alternatif

### A — Mevcut HOLD sürsün (varsayılan)

PR16 Draft kalır; merge yok. Worker root cause veya ayrı onaylı mitigation ve güncel güvenlik kararı beklenir. Bu tercih unresolved worker/dependency riskini main'e entegre etmez, entegrasyon gecikmesi ve feature branch bakım maliyetini kabul eder. Açıkları düzeltmez.30 Ekim sonrası yeni güvenlik kararı olmadan emulator çalıştırılmaz; süre içinde de yalnız mevcut dar koşullar geçerlidir.

### B — Açık, süreli LOCAL-only risk kabulü önerisi (ONAY DEĞİL)

Proje sahibi aralıklı Windows worker failure riskini ve iki unresolved dev advisory'yi isimleriyle kabul ederse yalnız ayrıca onaylanan bounded LOCAL devam ve LOCAL main entegrasyonu değerlendirilebilir. Worker olayı açık kalır; test failure'ları gizlenmez/atlatılmaz. Bu tercih developer test kesintileri, false-negative suite sonuçları, ek bakım/teşhis maliyeti ve sınırlı yerel güvenlik riskini kabul eder; production güvenliğini veya deterministik test ortamını iddia etmez.

Yeni karar insan adı/sorumlusu, tarih, exact commit/lock, kapsam, başlangıç ve **ayrı açık bitiş tarihi**, yeniden kontrol tarihi, durdurma kriterleri ve güvenlik koşullarını içermelidir. Bounded continuation izni ile merge komutu yetkisi ayrı açıkça belirtilmelidir. Bu belge tarih seçmez, istisnayı uzatmaz ve B'yi seçmez. Eski16–30 Ekim dependency istisnası unresolved worker riskinin main'e alınması için otomatik yetki değildir.

## Herhangi bir merge öncesi gerekli koşullar

1. İnsan kaynak/finans/API incelemesi; V1–V5, tek wallet, immutable pricing/receipts ve gerçek finansal concurrency kanıtlarının kabulü.
2. Exact PR head ile test edilen kaynak/hash ve lock bütünlüğü; unexpected/Unity/teammate değişikliği yok. Source değişirse mevcut43/134 sonucu yeni source'a taşınmaz; ayrıca yetkili validation planı gerekir.
3. Güncel audit, aynı residual advisory kapsamı ve geçerli tarihli açık risk kararı. Yeni bulgu/sınır değişikliği varsa HOLD.
4. A seçildiyse worker root cause/approved mitigation; B seçildiyse unresolved worker incident'ı açık tutan süreli insan kabulü. Temiz validation tek başına gate'i çözmez.
5. PR ready/merge için ayrı açık işlem yetkisi. Bu paket üzerinde böyle bir işlem yapılmaz.

Production, gerçek Firebase DEV/PROD, untrusted CI, migration ve deployment her iki seçenekte BLOCK. Numeric synthetic parametrelerin main'de bulunması ürün/production onayı değildir. BACKEND-11 başlatılmaz.

## Dosyalar ve önerilen karar

Yeni reviewable belge: `docs/backend/BACKEND-10_GATE-03_DECISION.md`. Yeni ignored yardımcı/yönerge: `backend/.local/worker-capture/capture-validation.ps1`, `README.md`. Önceden untracked RECOVERY-02 raporu aynen korunur; stage/commit/push yok. Diğer kaynak/test/Unity/manifests/lock/launcher ve tarihsel ignored kanıtlar değiştirilmedi. Başlangıç hash envanteri gate-03-before.json; son preservation/syntax sonucu ayrıca kaydedilir.

Önerilen insan kararı: varsayılan A/HOLD'u korumak veya B için yukarıdaki alanlarla açık süreli risk kararı vermek. B seçilse bile merge ayrıca yetkilendirilmelidir. Capture helper doğal bir sonraki developer validation için hazırdır; bu görev yeni koşu başlatma talimatı değildir.

## 28 Ekim 2026 — Sonraki proje sahibi kararı (GATE-04)

Yukarıdaki default-HOLD değerlendirmesi ve imzasız Option B önerisi tarihsel kayıt olarak aynen korunur. Proje sahibi daha sonra28 Ekim konuşmasında **Option B'yi açıkça onayladı**: yeni bağımsız LOCAL-only karar28 Ekim2026 başlangıçlı,6 Kasım2026 saat23:59 Europe/Istanbul bitişlidir. Eski16–30 Ekim istisnasının tarihleri değiştirilmedi.

Kesin kapsam, commit/lock, kabul edilen worker riski ve iki advisory, durdurma koşulları [LOCAL_RISK_ACCEPTANCE](BACKEND-10_LOCAL_RISK_ACCEPTANCE.md) içinde kayıtlıdır. GATE-03'teki30 Ekim helper sınırı o aşamanın tarihsel durumudur; GATE-04 yalnız yeni onaya bağlı tarih denetimini güncelledi. Worker reliability HOLD, general audit HOLD ve production/migration BLOCK devam eder. LOCAL merge readiness koşullu uygun; **merge için ayrı açık yetki hâlâ gereklidir**. Bu ek PR'ı değiştirmez veya merge etmez.
