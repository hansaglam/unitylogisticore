# BACKEND-08B — 23 Ekim 2026 Security Reassessment

## Karar

**LOCAL: CONDITIONAL PASS. GENERAL AUDIT: HOLD.** 16–30 Ekim 2026 proje sahibi istisnası aynı dar kapsamda sürer; süre uzatılmadı. Production, real DEV, deploy, migration ve CI/untrusted code BLOCK. Yeni dependency veya güven sınırı için ayrı onay gerekir. Sonraki yeniden onay/sonlandırma en geç30 Ekim; arada dependency/config/exposure değişirse hemen yeniden inceleme.

İmplementasyon değişmeden önce Node **22.23.2**, npm **10.9.8**, npm ls --all (problems:[]) ve lockfile kontrol edildi. Manifest/lockfile/launcher/guard geçmişinde foundation commit'i sonrası değişiklik yok; CLI15.34.0, Admin14.5.0 ve Functions7.4.0 korunur. Dosyanın yerel SHA256 değeri `3C2CC424D6651A69181A15A5D6ADDD8E4994983E23DB4B8745B794E4968C97D6`; tarihsel çalışma kopyası byte hash'i satır sonlarından etkilenebilir, Git içerik geçmişi değişmemiştir.

## Gerçek tarama

İlk sandbox audit DNS/ENOTFOUND ile sonuçlanamadı; bu temiz audit değildir. Yetkili registry erişimiyle iki komut tekrar çalıştırıldı: `npm audit --omit=dev --json --fetch-retries=0 --fetch-timeout=20000` exit0, **0 vulnerability**; ayrı full `npm audit --json --fetch-retries=0 --fetch-timeout=20000` exit1, **3 high +4 moderate, 0 critical**. Yedi affected package kaydı üç advisory'den türemiştir; yedi bağımsız exploit değildir. Yeni advisory yok. Kanıtlar ignored backend/.local/backend-08b-{runtime,full}-verified.json ve dependency-tree.json. npm ci, audit fix veya upgrade yapılmadı.

## Zincirler, erişilebilirlik ve düzeltmeler

Aşağıdaki bütün affected lockfile kayıtları dev:true; runtime zinciri/audit temiz. Kaynak incelemesi erişilebilirlik kanıtıyla olası maruziyeti ayırır; exploit/stress testi yapılmadı.

| Advisory | Kesin kurulu zincir | Girdi / mevcut erişim | Düzeltme ve kalan risk |
| --- | --- | --- | --- |
| [GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm) | firebase-tools15.34.0 → chokidar3.6.0 → braces3.0.3 | CLI functionsEmulator.js:300 watcher kullanır; chokidar/index.js:258 braces.expand yolu mevcut. Saldırganın derin brace glob kontrolü gerekir; sabit functions yolu ve kısa trusted ignore girdileriyle tetiklenme doğrulanmadı. | Güncel braces latest3.0.3, yayımlanmış patch yok. Yerel proses stack exhaustion riski; untrusted checkout/path/glob yasak. Chokidar major override uyumluluk kanıtı olmadan uygulanmaz. |
| [GHSA-8988-4f7v-96qf](https://github.com/advisories/GHSA-8988-4f7v-96qf) | firebase-tools15.34.0 → @google-cloud/pubsub5.3.1 → @opentelemetry/core1.30.1 | Büyük attacker-controlled baggage extractor'a ulaşmalıdır. Pubsub telemetry-tracing.js:68 TraceContext propagator kullanır; desteklenen auth/firestore/functions launcher Pub/Sub açmaz. Baggage extractor'a desteklenen akıştan yol doğrulanmadı. | core2.8.0 patch; pubsub5/core1 aralığı dışında major geçiş gerekir. Memory pressure riski; Pub/Sub/custom baggage/telemetry eklenirse yeniden inceleme. |
| [GHSA-w5hq-g745-h8pq](https://github.com/advisories/GHSA-w5hq-g745-h8pq) | firebase-tools15.34.0 → gaxios6.7.1 → uuid9.0.1 | Dış buffer/offset ile v3/v5/v6 gerekir; gaxios.js:417 yalnız argümansız v4 kullanır. Bu consumer'dan savunmasız davranışa erişim bulunmadı. | Patch11.1.1/12.0.1/13.0.1; uuid9 latest9.0.1, gaxios6.7.1 aralığı ^9.0.1. Bozuk/kısmi identifier riski; native RCE iddiası yok. Yeni UUID consumer yasak/yeniden inceleme. |

UUID ek yolları CLI → google-auth-library9.15.1 → gaxios6.7.1; aynı auth üzerinden gcp-metadata6.1.1 ve gtoken7.1.0 → gaxios6.7.1. Hepsi aynı dev UUID9'a yakınsar; Admin gaxios7/8 bu yol değildir.

23 Ekim npm metadata: CLI latest15.34.1 fakat chokidar ^3.6.0, pubsub ^5.2.0, gaxios ^6.7.0 aralıkları aynı; pubsub5 latest5.3.1. Patch yükseltme üç finding'i çözmez. Audit'in önerdiği CLI14.23.0 major downgrade güvenli uyumluluk çözümü kabul edilmedi. Derin global override, lockfile değişimi veya tarama suppression yok.

## Koşullar

Trusted workstation/checkout, kısa sabit path/glob, yalnız demo-logisticore-local/loopback; cloud credentials yok; untrusted fork/kod veya port forwarding yok. Mevcut guard/default-deny/launcher korunur. Aynı bilgisayardaki güvenilmeyen süreçler karşısında emulator güvenlik sınırı değildir. Commerce yalnız sentetik local domain endpoint'leri ekleyebilir; telemetry, public config publisher, dış ödeme veya başka servis eklemez. Böyle bir sınır değişirse bu karar geçersizdir. Açıklar düzelmiş veya production güvenli sayılmaz; önceki insan onayı yalnız belirtilen kalan risk/süreye uygulanır.
