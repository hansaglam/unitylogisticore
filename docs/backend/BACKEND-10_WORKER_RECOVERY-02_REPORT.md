# BACKEND-10 RECOVERY-02 — Kontrollü worker incelemesi

28 Ekim 2026, Europe/Istanbul. **RECOVERY-02: HOLD; WORKER RELIABILITY: HOLD.** Üç sınırlı diagnostic oturumun yürütülmesi PASS; geçmiş aralıklı hata yeniden üretilemedi (OUTCOME D). Root cause veya doğrulanmış mitigation yoktur. Feature functional önceki aynı-source kanıtıyla PASS; LOCAL security mevcut istisna altında CONDITIONAL PASS; general audit HOLD; production/deploy/migration BLOCK.

## Başlangıç ve korunan kapsam

Repo `D:/Projects/unitylogisticore`, branch `feature/backend-10-liveops`, HEAD `f2b7058e006a2b6f2fc46d12546e347b6dedf722`; main tabanı `e6d2682154d6ba66fd74c40afc36401adf4b3bef`. PR16 OPEN/DRAFT ve aynı head SHA ile doğrulandı. Başlangıç temizdi. 101 kaynak/test/config/belge ve 45 önceki ignored kanıt dosyasının SHA256 değerleri `.local/recovery-02-before.json` içinde kaydedildi. Önceki raporlar ve RECOVERY-01 logları değiştirilmedi.

BACKEND-10_REPORT, WORKER_RECOVERY_REPORT, FINAL_REVIEW, BACKEND-09B_REPORT ve LiveOps/event pricing sözleşmeleri okundu. Launcher, package/config, index/localEnvironment, emulator runner ve LiveOps testleri incelendi. Kurulu Firebase CLI15.34.0 worker/discovery/runtime kaynakları salt okunur incelendi. Node22.23.2, Admin14.5.0, Functions7.4.0 ve bağımlılık kilidi korundu.

Önceki BACKEND-10 koşuları128 PASS/6 FAIL ve123 PASS/11 FAIL olarak kalır; parent failure sayıları ayrı neden sayısı değildir. 27 Ekim recovery ve28 Ekim final review full validate koşuları ayrı ayrı43/43 unit,134/134 emulator, exit0 idi. Önceki clean koşular hata giderildiği anlamına gelmez. RECOVERY-02 sırasında kullanım limiti nedeniyle bir araç onay değerlendirmesi çalıştırılamadı; o çağrıda dosya veya emulator oluşturulmadı. Devam sonrası kontrollü plan bir kez yürütüldü.

## Kurulu CLI yaşam döngüsü

| Evre | Sahip / ölçüm / timeout ve hata |
| --- | --- |
| Emulator başlatma | Mevcut npm run emulators → build → guarded launcher → CLI; yalnız demo ID, loopback, allowlisted child env. Build çıktısı ve CLI başlangıç logları. |
| Definitions discovery | Ayrı Functions SDK discovery process ve CLI `detectFromPort`; `/__/functions.yaml`, varsayılan10s. Başarısızlık `User code failed to load / Cannot determine backend specification`. Discovery başarılı olması request worker hazır demek değildir. |
| HTTP kabul | CLI `handleHttpsTrigger`, `Accepted request` UTC timestamp ve URL query request ID. `Runtime ready! Sending request!` mesajı kaynakta pool readiness kontrolünden ÖNCE yazılır; gerçek readiness kanıtı sayılamaz. |
| Worker seçimi/spawn | AUTO pool yalnız IDLE worker seçer; CREATED/BUSY worker uygun değildir. Yoksa startRuntime/startNode ile yeni Node process; UUID ve trigger pool kayıtları, CIM PID/creation time. Bu akışta maxInstances1 bir process sınırı olarak uygulanmaz. |
| Runtime initialization | Worker verifyDeveloperNodeModules → network/Functions/Admin stubs → loadTriggers(require/import uygulama) → Express setup. PID içeren runtime logları vardır; her require için ayrı süre yoktur. |
| Socket/listening | Windows `\\?\pipe\fire_emu_<random>`; worker app.listen callback PID ile loglanır. `Functions runtime initialized` main promise completion mesajıdır; health kabulüyle aynı olay değildir. |
| Health readiness | CLI `waitForSocketReady`, named pipe üzerinden GET `/__/health`; varsayılan30s, ECONNREFUSED/ENOENT için100ms polling. Başarı worker IDLE yapar. Poll attempt/error detayları varsayılan logda yok; süre aşımı `Failed to load function.`. |
| Dispatch | CLI pool.submitRequest → worker.request, UUID BUSY; Begin execution ve proxy. INFO begin/finish satırları her zaman UTC/PID/request ID içermez; kesin çoklu-request→PID eşlemesi bu loglarla yapılamaz. |
| Handler/response | Worker runHTTPS → gerçek callable; auth yokluğu uygulamada401 üretir. Client UTC başlangıç/bitiş ve performance.now elapsed kaydeder. Proxy timeout trigger.timeoutSeconds üzerinden ayrı uygulanır; hazır olma30s ile birleştirilmez. |
| Hata/exit | FATAL runtime log process kill; child exit UUID FINISHED kaydı. HTTP proxy timeout/error ayrı; startup catch hata loglar ve bu yolda HTTP yanıtını tamamlamaz, client kendi deadline'ına ulaşabilir. |

Kaynaklar: `backend/node_modules/firebase-tools/lib/emulator/functionsRuntimeWorker.js`, `functionsEmulator.js`, `functionsEmulatorRuntime.js`, `functionsEmulatorShared.js`; `lib/deploy/functions/runtimes/discovery/index.js`. Discovery ve worker readiness `FUNCTIONS_DISCOVERY_TIMEOUT` override okuyabilir; güvenli launcher bunu aktarmıyor. Değiştirilmedi. Mevcut test client süreleri15/20/60s, tanı client60s; proxy function-execution süresi bunlardan ayrıdır. İlk client timeout, asıl worker gecikmesinin nedeni olarak sunulmaz.

## Deney düzeni ve sınırlar

Tam olarak **3 diagnostic emulator oturumu**, hepsi mevcut `npm run emulators` launcher'ıyla. Yeni full validation yok; business/test-orchestration patch yapılmadığından mevcut iki successful full run kanıtı korundu. Yeni bağımlılık yok. Ignored `.local/recovery-02-probe.mjs`, `observe.ps1`, `analyze.mjs` tanı yardımcıları finansal koddan ayrıdır; stage edilmedi.

Endpoints: localHealth, getMyWorldEvents, getMyMarketState, getMyBankState. POST `{data:{protocolVersion:1}}`; Authorization yok, gerçek/sentetik token yok, kullanıcı oluşturma yok, finansal mutation yok. Query `recoveryRequestId=sN_K` yalnız CLI request correlation içindir; fiyat/zaman/kimlik yetkisi sağlamaz. Redirect:error,60s bounded AbortSignal, retry yok, max concurrency8. Beklenen sonuç401/UNAUTHENTICATED; status/body code ayrıca doğrulanır, yanıt gövdesi loglanmaz.

S1 her endpoint ilk çağrı cold, ardından iki aynı-input warm çağrı. S2 taze oturumda aynı localHealth endpoint'ine8 cold +8 warm concurrent, ardından3 sequential warm. S3 taze oturumda dört endpoint'e ikişer, toplam8 cold +8 warm concurrent, ardından endpoint başına1 warm. OS disk cache soğukluğu garanti edilmez; cold burada henüz oluşturulmamış Functions worker anlamındadır. Fixture-free401, authenticated finansal iş yükü veya uzun süreli soak testi değildir.

## Sonuçlar

Client elapsed süreleri ms, performance.now ile response JSON okumasını içerir. Min / median / max:

| Oturum / faz | n | Min | Median | Max | Sonuç |
| --- | ---: | ---: | ---: | ---: | --- |
| S1 sequential cold | 4 | 1690.65 | 1745.79 | 1819.70 | 4 beklenen401 |
| S1 sequential warm | 8 | 4.97 | 14.33 | 16.35 | 8 beklenen401 |
| S2 concurrent cold | 8 | 2876.50 | 2938.95 | 2997.52 | 8 beklenen401 |
| S2 concurrent warm | 8 | 12.96 | 23.71 | 30.56 | 8 beklenen401 |
| S2 sequential warm | 3 | 8.85 | 14.79 | 16.46 | 3 beklenen401 |
| S3 concurrent cold | 8 | 2860.27 | 2973.45 | 2986.23 | 8 beklenen401 |
| S3 concurrent warm | 8 | 17.19 | 19.13 | 29.34 | 8 beklenen401 |
| S3 sequential warm | 4 | 4.81 | 15.66 | 15.84 | 4 beklenen401 |

Toplam **51/51 beklenen401, timeout0, beklenmeyen HTTP0, startup failure0**. Probe process exit0 üçünde de. İlk/ikinci sekizli cold gruplar16/16; warm concurrent16/16. Yeni full validate çalışmış gibi gösterilmez.

| Oturum | Worker PID sayısı | Pool registration ilk→son | CIM creation→listening min/median/max ms | Admin stub→init ms |
| --- | ---: | ---: | --- | --- |
| S1 | 4 | 5356ms; sıralı ayrı endpoint | 1646 /1651.5 /1660 | 27–28 |
| S2 | 8 | 61ms | 2741 /2810.5 /2832 | 46–72 |
| S3 | 8 | 58ms | 2723 /2833.5 /2859 | 44–71 |

S2/S3 sekiz worker kaydı ilk worker listening'inden önce oluştu; concurrent cold amplification doğrulandı. Warm gruplarda yeni worker oluşmadı; mevcut UUID BUSY→IDLE geçişleri reuse gösterir. Bu, geçmiş failure nedeninin amplification olduğunun kanıtı değildir.

Örnek kesin PID korelasyonu S1: CLI request s1_1 kabul23:09:44.803Z; PID19240 CIM creation23:09:44.807636Z; pool registration44.812Z; Admin stub46.425Z; initialized46.452Z; listening46.453Z. Worker creation→listening1646ms. Tam20 PID kaydı `.local/recovery-02-summary.json` içinde. Yerel tarihler28 Ekim02:09–02:11; UTC logları27 Ekim23:09–23:11'e karşılık gelir.

Request ID→CLI accepted URL kesin; PID→creation/initialized/listening kesin; endpoint→worker grubu kesin. Concurrent request→belirli PID/UUID dispatch eşleşmesi **UNKNOWN**: Begin/finish logları request ID/PID taşımaz. Yakın timestamp veya sıralama üzerinden kesin bağ uydurulmadı. Handler body giriş/çıkışı ayrıca instrument edilmedi; proxy begin/finish handler'ın saf işlem süresi değildir. Health poll başarısı IDLE/dispatch ile dolaylı görülür, tek tek polling RTT'leri mevcut değil.

## Kaynak ölçümleri ve hipotezler

Yaklaşık1s aralıklı non-invasive CIM örnekleme: S1 64, S2 44, S3 35 örnek. En fazla Node15/19/19 (ilgisiz süreçler dahil), doğrulanmış eşzamanlı worker4/8/8. Minimum free RAM6.09/5.75/5.75GiB. Node+Java cumulative CPU delta tepe değerleri2.95/8.73/8.41 çekirdek eşdeğeri; bütün sistem CPU veya disk/antivirus ölçümü değildir. Yeni/örnekler arası kapanan süreçler delta hesabında eksik kalabilir; kısa peak'ler kaçabilir.

Observer'ın ilk command-line tabanlı role sınıflandırması worker'ları `other` işaretledi. Ham PID/creation kayıtları korunarak analiz runtime PID loglarıyla birleştirildi; rapordaki worker sayısı bu doğrulanmış PID kümesidir. İlk role=worker0 sonucu gerçek worker yokluğu şeklinde yorumlanmadı. Command line ve env içeriği ölçüm dosyasına yazılmadı.

| Hipotez | Karar |
| --- | --- |
| A concurrent cold amplification | CONFIRMED davranış:8 worker/58–61ms. Gecikme sıralıdan yüksek; failure nedenselliği UNCONFIRMED. |
| B named-pipe gecikmesi | Üç oturumda listening ve başarılı HTTP var; late-listening, health başarısızlığı veya ready-sonrası proxy hatası görülmedi. Eski hata için mekanizma UNKNOWN. |
| C client vs worker timeout | Kaynakta farklı deadline/catch davranışı doğrulandı. Eski ordinal stdout sırası tek başına kesin request/PID zaman çizelgesi sağlamaz. Yeni timeout yok. |
| D application init hotspot | Admin stub→init27–72ms, startup toplam1.65–2.86s; bu sağlıklı örneklerde uygulama yükleme baskın görünmüyor. Saf import profiler değil; eski failure'ı açıklamaz. Import/guard kaldırılmadı. |
| E environment contention | Sekizli startup'ta CPU ve süre artıyor; bellek tükenmesi, disk/Defender veya Windows platform defect nedenselliği gösterilmedi. İnvasive tracing/OS ayarı yapılmadı. |

Sonuç OUTCOME D. Test orchestration defect kanıtlanmadı. Warm-up, serialization, timeout override, launcher/vendor patch veya dependency upgrade uygulanmadı. Gerçek finansal8-yollu origination/generation/claim, quote consume/replacement, manual/automatic repayment, iki delivery, boundary retry ve historical replay testleri byte-for-byte korundu. Dar correction olmadığı için full validate NOT RUN; eski functional PASS kanıtı geçerli kaynakla korunur. Offline regression yeniden85/85 ve LiveOps8/8 PASS; fail/skip/cancelled0.

## Güncel dependency güvenliği

28 Ekim read-only npm audit: runtime0/exit0; full5 moderate affected package/exit1, high0/critical0. **İki distinct advisory, yeni bulgu yok.** npm ls, lock ve kurulu package.json sürümleri karşılaştırıldı; runtime ve development farklı gaxios sürümleri karıştırılmadı. Tam ağaç `.local/recovery-02-tree.json` içinde.

| Advisory / exact affected chain | Yama ve mevcut kısıt |
| --- | --- |
| GHSA-8988-4f7v-96qf: firebase-tools15.34.0 → @google-cloud/pubsub5.3.1 → @opentelemetry/core1.30.1 | Yama2.8.0. CLI pubsub^5.2.0 ister; registry'de bu aralıktaki5.2.0–5.3.1 sürümlerinin core kısıtı^1.30.1. Yamalı2.8.0 bu parent kısıtında değil. Latest core3.0.0 olması uyum kanıtı değildir. |
| GHSA-w5hq-g745-h8pq: firebase-tools15.34.0 → gaxios6.7.1 → uuid9.0.1 | Yama11.1.1 (ayrıca12.0.1/13.0.1). CLI gaxios^6.7.0;6.7.1 uuid^9.0.1,6.7.0 uuid^10.0.0 ister. İkisi de patched11.1.1 kabul etmez. Latest uuid14.0.3 doğrudan override gerekçesi değildir. |

Published advisory kaynakları: [OpenTelemetry](https://github.com/advisories/GHSA-8988-4f7v-96qf), [uuid](https://github.com/advisories/GHSA-w5hq-g745-h8pq). İlki inbound W3C baggage parsing allocation; ikincisi v3/v5/v6 caller-provided buffer/offset bounds davranışı. Kurulu gaxios kullanımında uuid.v4() var; Pub/Sub telemetry dosyasında W3CTraceContextPropagator var. İncelenen bu yollar vulnerable API'ye erişim kanıtlamaz; tüm CLI komutları veya güvenilmeyen ortamlarda erişilemezlik iddiası yapılmaz. Diagnostic yalnız sabit tokensız loopback body kullanır, untrusted baggage/buffer girdi kaynağı yoktur.

5 paket sayısı5 exploit yolu değildir. Npm'nin14.23.0 CLI downgrade önerisi uygulanmadı. Mevcut parent aralıklarında doğrulanmış patched çözüm yok; remediation locked toolchain değişikliği ve ayrı uyumluluk/güvenlik incelemesi gerektirir. Kurulum, lock değişikliği veya override yok.

**Mevcut LOCAL yetki:** 28 Ekim'de16–30 Ekim istisnası geçerli; trusted local machine, fixed dependencies, demo project ve mevcut izolasyon koşullarıyla CONDITIONAL PASS. **Süre sonrası:** insanın yeni başlangıç/bitiş tarihli dar risk onayı veya ayrı onaylı dependency remediation + audit/regression kanıtı gerekir; otomatik uzatma yok. CI/untrusted code, real DEV/PROD, deploy/migration mevcut onayın dışında kalır. Runtime0 genel audit'i temiz yapmaz. Yeni advisory/trust-boundary/dependency değişikliği mevcut değerlendirmenin tekrarını gerektirir.

## Kapanış, kanıtlar ve PR kararı

Her oturum tek Ctrl+C ile mevcut CLI shutdown akışına bırakıldı; PTY wrapper exit1 bu sinyalin sonucudur, probe/test failure değildir. Son kontrolde emulator/worker/Java process yok;4400/4500/5001/8080/9099/9150 ve discovery8909/8721/8741 portlarına bind başarılı. İlgisiz process öldürülmedi.

Ignored kanıtlar: `recovery-02-before.json`, s1/s2/s3 emulators.log/debug.log/requests.jsonl/probe-summary.json/timeline.json, resources.jsonl, summary.json, runtime/full-audit.json/audit-exits.json, tree.json ve npm metadata JSON'ları, offline log/exit kayıtları, final-ports.json. Debug dosyası CLI tarafından append edilebildiğinden analiz session request UTC aralığıyla filtrelenir; eski satırlar yeni deney diye sayılmaz. Tanı ham logları yüklenmedi. Credential/Authorization/token girdisi kullanılmadı; paylaşılacak rapor kişisel path veya gizli env içermez.

Bu fazın reviewable değişikliği yalnız **bu rapordur**. Tanı helper'ları ignored `.local/` içinde kalır; ürün/test/launcher değişikliği değildir. Önceki101 protected hash ve45 evidence hash son kontrolde aynı kaldı; Unity/manifests/lock diff'i boş. Strict UTF-8, limited credential-pattern ve whitespace kontrolü PASS. Pattern taraması mutlak secret-yok garantisi değildir; rapor içeriği ayrıca gözden geçirildi. Lock SHA256 `AA1F6BA0D7A5CCAC1C9551A3941B8F1A2FD66B7482FD487C5C4D354ACBC5CC9F`.

PR16 DRAFT olarak değişmeden korunur; commit/push/merge/ready/deploy/migrate/BACKEND-11 **NOT RUN**. İnsan kaynak incelemesi devam edebilir, worker gate kapatılamaz. Önerilen sonraki adım: doğal failure tekrarında mevcut PID/UTC/request-ID tanısını kullanmak için ayrı bounded takip planı onaylamak; gerekirse yalnız repository Node/pipe başlangıcını hedefleyen süreli Windows tracing kapsamını ayrıca onaylatmak. Mevcut sonuçlar belirli bir timeout artırımı, warm-up veya vendor patch önermeye yeterli değildir. Her launcher/environment/dependency değişikliği ve invasive tracing ayrı insan onayı gerektirir; hiçbirisi uygulanmadı.
