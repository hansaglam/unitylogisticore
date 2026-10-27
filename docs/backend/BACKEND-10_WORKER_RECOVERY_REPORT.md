# BACKEND-10 RECOVERY-01 — Windows Functions worker incelemesi

27 Ekim 2026, Europe/Istanbul. **BACKEND-10 RECOVERY GATE: HOLD — aralıklı sorunun kök nedeni açıklanamadı.** Yeni tek full validation **PASS**, fakat kalıcı recovery/final-review acceptance verilmedi. LOCAL dependency security **CONDITIONAL PASS**; general audit **HOLD**; production/real DEV/deploy/migration/untrusted CI **BLOCK**. Commit/push/PR/merge yapılmadı. Önceki BACKEND-10_REPORT.md değiştirilmedi.

## Korunan başlangıç

Repo D:/Projects/unitylogisticore; branch feature/backend-10-liveops; HEAD ve beklenen main merge `e6d2682154d6ba66fd74c40afc36401adf4b3bef`. Başlangıçta11 tracked modified +12 untracked =23 BACKEND-10 dosyası; staged kayıt yok. Tam dosya listesi ve SHA256'ları ignored `.local/recovery-01-before.json` içinde. Unity Assets/Packages/ProjectSettings, manifests ve lock diff'i boş. Önceki iki validation logu, exit kayıtları, audit ve offline kanıtları mevcut.

Önceki ayrıntılı backend/firebase-debug.log bu incelemenin başlangıcında yoktu. Eski stdout loglarından worker spawn/listening için kesin UTC zamanları çıkarılamaz. Mevcut firestore-debug.log ayrı ignored kopyaya alındı; eski dosyalar silinmedi. Yeni baseline sırasında CLI debug logu ayrı recovery dosyasına kopyalanarak korunur. Raw loglar token içerebilir; Git'e eklenmez.

Node22.23.2, npm10.9.8, Java Temurin21.0.12; Admin14.5.0, Functions7.4.0, CLI15.34.0. Yeni install/update/downgrade yok. LOCAL16–30 Ekim istisnası27 Ekim'de geçerli; uzatılmadı. Güncel audit runtime0/exit0; full5 moderate/exit1, iki mevcut advisory GHSA-8988-4f7v-96qf ve GHSA-w5hq-g745-h8pq. Yeni advisory yok.

## Eski başarısız çağrılar ve zaman çizelgesi

| Koşu | Gözlenen sıra ve sonuç |
| --- | --- |
| İlk backend-10-validation.log | L309 emulator start → L316 discovery8658 → L318 tanımlar yüklendi → L344 tek Node test girişi → L1600 commerce HTTP FAIL → L1671 buyVehicle worker failure → L1878 getMyBankState worker failure → L1881 BANK HTTP FAIL → L2098 worldEvents worker failure → L2102 L1 FAIL →128 PASS/6 FAIL →shutdown |
| İkinci backend-10-validation-final.log | L309 emulator start → L316 discovery8282 → L318 tanımlar yüklendi → L344 test girişi → L1025 company HTTP FAIL → L1110 economy HTTP FAIL → L1452 commerce HTTP FAIL → L1523 buyVehicle worker failure → L1730 bank worker failure → L1823 market worker failure → L1945 worldEvents worker failure →123 PASS/11 FAIL →shutdown |

İlk emulator test süresi714873.393ms; ikinci802170.342ms. İkinci exit kaydı26 Ekim22:23:46+03:00; ilk exit kaydında wall-clock yok. Bunlar ordinal log sırası ve TAP süreleridir; kesin request/spawn/ready zamanları değildir. İkinci altı temel failure: company bootstrap createCompany sekizli çağrısı, getMyEconomyState, commerce buyVehicle, getMyBankState, getMyMarketState M1, getMyWorldEvents L1. Beş parent failure ayrı exploit veya ayrı handler hatası değildir. Eski rapordaki geç listening gözlemi tarihsel kanıttır; kayıp debug log üzerinden yeniden ölçülmüş gibi sunulmaz.

## Orchestration ve timeout ayrımı

Launcher `node --test tests/emulators.test.mjs` çalıştırır: **tek seçilmiş test dosyası**, runner+bir test child process. Dokuz domain test modülü aynı child içinde dynamic import ile kayıt olur; bağımsız test-file process'leri değildir. Top-level test concurrency açılmamıştır. Bu nedenle file-level `--test-concurrency=1` mevcut düzende anlamlı bir karşılaştırma/fix değildir.

Suite içi Promise.all/Promise.allSettled yarışları gerçek ve korunur. CLI AUTO worker pool trigger başına idle worker arar; yoksa yeni Node runtime başlatır. Hazır olmayan CREATED worker idle değildir; eşzamanlı istekler aynı function için ek worker oluşturabilir. maxInstances1 burada tek emulator process garantisi değildir. Worker Windows named pipe üzerinden `/__/health` yanıtını bekler; ardından gerçek HTTP handler'a proxy eder. SDK/client başlangıcı test process'inde ve worker'da ayrı gerçekleşebilir.

| Mekanizma | Kurulu kaynakta davranış |
| --- | --- |
| HTTP client | Foundation/economy/logistics15s; commerce/delivery/settlement20s; BANK/market/LiveOps60s AbortSignal |
| CLI discovery | discovery/index.js detectFromPort: varsayılan10s; functions.yaml/spec keşfi |
| Worker readiness | functionsRuntimeWorker.js waitForSocketReady: varsayılan30s; pipe health endpoint polling100ms |
| Function execution | Worker request proxy timeout ayrı trigger timeoutSeconds üzerinden |

CLI15.34.0'da discovery ve worker readiness ayrı mekanizmalardır fakat her ikisi `getFunctionDiscoveryTimeout()` okuyabilir. Eski issue sürümlerindeki davranış aynen varsayılmadı. Secure launcher allowlist'i FUNCTIONS_DISCOVERY_TIMEOUT aktarmadığından dış shell'e bu değişkeni eklemek mevcut child'a ulaşmaz. Bu incelemede hiçbir timeout/env/launcher/CLI internal değişikliği yapılmadı.

## Dış referansların sınırı

[firebase-tools #6285](https://github.com/firebase/firebase-tools/issues/6285) Windows intermittent loading ve farklı discovery/worker hatalarını içerir; bizim kök nedenimizin kanıtı değildir. [#7165](https://github.com/firebase/firebase-tools/issues/7165) uzun module yüklemesinin discovery'yi etkileyebileceğini gösterir. [#8238](https://github.com/firebase/firebase-tools/issues/8238) belirsiz discovery hata mesajının iyileştirilmesidir. Hiçbirindeki timeout/vendor patch önerisi otomatik uygulanmadı.

## Kontrollü deneyler

A: değiştirilmemiş tam `npm run validate`, tek baseline. Output satırları parent observer tarafından UTC receipt timestamp ile kaydedilir; bunlar application içi clock ölçümü değildir. CLI debug timestamp'leri ve10s aralıklı CIM process/bellek gözlemleri ayrı tutulur. Process telemetry yalnız pid/parent/role/creation/CPU/working-set içerir; command line veya credential yazılmaz. İlgisiz süreçler durdurulmaz. Başlangıçta tek test runner+child doğrulandı.

B: aynı baseline'daki doğal cold/warm çağrıların debug kabul/dispatch/finish zamanları karşılaştırılır; baseline'a ek trafik verilmez. localHealth ilk request21:55:14.047Z, worker registration14.057Z, initialized/listening17.120/17.121Z, dispatch17.130Z, finish17.355Z. Sonraki request17.358Z→dispatch17.359Z→finish17.365Z. Yaklaşık3.3s cold ve7ms warm server-side süre; payload/auth farkı vardır, aynı-input kontrollü benchmark değildir. Örnek27 Ekim00:55 Europe/Istanbul'a karşılık gelir.

C: file concurrency deneyi NOT RUN; tek seçilmiş dosya olduğundan uygulanabilir hipotez değil. Suite içi yarışlara dokunulmaz. Identity altılı yarışında beş yeni worker yaklaşık21:55:19.75Z'de oluşup22.04–22.10Z'de listening verdi. Company sekizli yarışında yedi ek worker21:55:41.25–41.30Z→44.36–44.46Z. Çoklu cold worker oluşumu gözlendi; bunun geçmiş failure'ların nedeni olduğu henüz kanıtlanmadı.

D: CPU/bellek/process lifetime örnekleri `.local/recovery-01-resources.jsonl`. Örnekleme10s; kısa ömürlü child kaçabilir. CPU cumulative counter'dan türetilir, güvenlik yazılımı/disk I/O sebebi bu ölçümle kanıtlanmaz.71 örnekte en fazla48 Node process (ilgisiz Node süreçleri de dahil),37 worker;15.915GiB fiziksel bellekte minimum2.720GiB boş. Örneklenmiş Node+Java CPU delta tepe değeri yaklaşık1.019 çekirdek eşdeğeri/12 logical CPU; bütün sistem CPU ölçümü değildir ve örnek arası peak/exit eden süreçleri kaçırabilir. Java process ve test child kapanışı izlendi; son örnekte emulator/test process'i yok.

37 worker'ın CIM creation→runtime initialized süresi1710–3215ms. Admin stub tamamlandı→runtime initialized28–119ms: uygulama require/loadTriggers, Express setup ve kalan init için üstten bir aralık, saf module import profiler ölçümü değildir. Initialization ile pipe listening bildirimleri bitişik; readiness timeout veya application FATAL bu baseline'da görülmedi. Aynı source/lock ile önceki başarısız handler'ların tamamı geçti. Kaynak yorgunluğu/disk/Defender/pipe health bug hipotezleri ispatlanmadı; işletim sistemi güvenlik ayarına dokunulmadı.

B2: baseline tamamen kapandıktan sonra mevcut `npm run emulators` ile ikinci, yalnız diagnostic oturum açıldı. Tek node client üç endpoint'e sırayla ikişer **aynı** `{data:{protocolVersion:1}}` gövdeli, tokensız istek gönderdi. Redirect:error ve mevcut60s sınırı; retry/timeout artışı yok. Bu6 request finansal state yazmaz, tümü beklenen401/UNAUTHENTICATED ile tamamlandı:

| Endpoint | Cold ms | Warm ms |
| --- | ---: | ---: |
| getMyWorldEvents | 1815.271 | 7.420 |
| getMyMarketState | 1819.400 | 9.152 |
| getMyBankState | 1798.401 | 13.596 |

Ölçüm27 Ekim01:06:16–01:06:21+03:00. Client performance.now süresi response JSON okumasını da kapsar. Ayrı process/OS filesystem cache tamamen soğuk garanti edilmez; yalnız function worker cold/warm ayrımıdır. Bu diagnostic tüm business assertion'ların yerine geçmez; onlar A full validate içinde çalıştı. Toplam **bir full baseline + bir6-request diagnostic oturum**; yeşil sonuç aramak için sınırsız rerun yapılmadı. E: unrelated üç callable aynı sağlıklı init profilini gösterdi; tek endpoint'e özgü post-ready handler defect yeniden üretilmedi.

## Kök neden sınıflandırması

| Hipotez | Kanıt/karar |
| --- | --- |
| A: application module yüklenmeden worker failure | Eski stdout evreyi ayırmaz; yeni deneylerde yok. CONFIRMED değil |
| B: module yüklü, socket readiness sinyali eksik | Yeni initialized/listening/dispatch başarılı; eski ayrıntılı log eksik. CONFIRMED değil |
| C: worker HTTP deadline sonrası hazır | Eski raporda late listening gözlemi var; bu recovery'de yeniden üretilemedi. Kesin gecikme sebebi açık |
| D: ready worker'a rağmen HTTP timeout | Eski company çağrısı önceden warm worker kullanmıştı, fakat8'li yarış ek cold worker açabilir; timeout alan request'in hangi worker'a ait olduğu belirlenemiyor |
| E: application init exception | Yeni logda FATAL/loadTriggers exception yok; discovery her iki eski koşuda da tamamlanmış. İstikrarlı init exception lehine kanıt yok |
| F: startup resource contention | Birden fazla worker gözlendi; bellek/CPU doygunluğu veya bunun failure'a sebep olduğu gösterilmedi |
| G: birden fazla failure mekanizması | Client15/20/60s ve worker30s farklı sınırlar; eski kısa HTTP timeout'u worker error'dan önce olabiliyor. Ayrı root cause sayısı doğrulanamadı |

Sonuç: geçmiş failure gerçek, bugünkü baseline'da yeniden üretilemedi. Gereksiz file parallelism hipotezi elendi; kök nedeni çözülmüş ilan edecek bir narrow patch yok. Runtime/iş mantığını değiştirmek için kanıt yok.

## Korunan testler ve alternatifler

Hiçbir HTTP assertion kaldırılmadı/direct-call ile değiştirilmedi; auth/migration/Rules korunur. V1–V5 contract, V2 cost/V3 settlement, wallet/XP/capped bank repayment, immutable snapshot/ledger/journal/receipt, pool quota ve8-yollu finansal yarışlar aynı kaynaklarla çalışır. Event tuning ve simülasyon çıktıları değişmedi.

File serialization için kanıt yok; worker'ları debug/sequential moda geçirmek gerçek concurrency'yi değiştirebilir; warm-up başarısız ilk çağrıyı gizlemek için kullanılmaz. Dependency veya vendored CLI patch kapsam dışıdır. Launcher değişikliği veya timeout artışı gerekirse ayrı onay istenir; şu an uygulanmış bir düzeltme yoktur.

## Doğrulama ve kapanış

| Kontrol | Son sonuç |
| --- | --- |
| npm run validate, değiştirilmemiş baseline | PASS exit0 |
| Build/typecheck | PASS |
| Unit | 43 PASS/0 FAIL/0 SKIP |
| Auth/Firestore/Functions emulator | 134 PASS/0 FAIL/0 CANCELLED/0 SKIP;613026.0713ms |
| Önceki altı failed callable | Bu aynı full koşuda hepsi PASS; L1 tamamı dahil |
| BANK/settlement/market/LiveOps gerçek concurrency | PASS; kaynak ve Promise.all yarışları aynen korundu |
| Default-deny Rules / environment unit guards | PASS |
| Tarihsel offline | 85/85 PASS |
| LiveOps offline | 8/8 PASS |
| Runtime audit | PASS0, exit0 |
| Full audit | HOLD5 moderate/2 mevcut advisory, exit1 |
| Cold/warm diagnostic | 6/6 beklenen401; timeout0 |
| Protected files | Başlangıç23 dosya+7 kritik config/dependency/guard dosyası toplam30 SHA256 aynı |

Baseline emulator start27 Ekim00:54:44 civarı; discovery00:54:57.060→functions.yaml response00:54:58.806; test script00:55:01 civarı, exit01:05:15.714+03:00. UTC timestamp içindeki26 Ekim tarihleri Europe/Istanbul'da27 Ekim'e karşılık gelir. Full run exit0 ve shutdown kaydı saklandı. B2 diagnostic Ctrl+C ile kapatıldı; wrapper exit1 kullanıcı-sinyali sonucudur,6-request script exit0'dır. Son process kontrolünde CLI/worker/Java emulator süreci yok, port bind kontrolleri boş. İlgisiz process öldürülmedi.

Ignored kanıtlar: recovery-01-before.json, original-timelines.json, baseline.log/baseline-exit.json/baseline-firebase-debug.log, resources.jsonl/resource-summary.json, worker-summary.json, baseline-startup-events.json, cold-warm.jsonl/cold-warm-emulators.log/cold-warm-firebase-debug.log, offline.log/liveops-offline.log, runtime-audit.json/full-audit.json ve final-ports.json. Raw loglar Git'e eklenmedi. Bu fazın tek reviewable dosyası bu yeni rapordur; önceki23 dosya ve eski raporlar byte-for-byte korunur.

**Recovery/final review readiness HOLD**: temiz full test kanıtı elde edildi, ancak worker sorunu açıklanmış veya kalıcı düzeltilmiş değil. Mevcut talimat root cause açıklanamazsa HOLD istediği için RECOVERY PASS verilmez. Ayrı onay gerektiren bir launcher/timeout patch'i uygulanmadı; ölçülen1.7–3.2s başlangıçlar mevcut15/20/60s HTTP veya30s worker sınırını artırmayı gerekçelendirmiyor, bu nedenle önerilen yeni timeout değeri yok.

Bir sonraki kontrollü inceleme, failure tekrarlandığında aynı timestamp+PID+pipe readiness kanıtlarını yakalamalı; gerekirse kullanıcı onayıyla daha ayrıntılı Windows I/O/process-start tracing tasarlanmalıdır. Eksik tarihsel telemetry geriye dönük uydurulamaz. Mevcut dosyalar final review için incelenebilir, fakat bu recovery gate'i kapanmış sayılmamalıdır.30 Ekim sonrası LOCAL risk onayı ayrıca yeniden değerlendirilmelidir. Commit/push/PR/merge/deploy/migration ve yeni faz **NOT RUN**.
