# BACKEND-10 — Bağımsız LOCAL risk kabul kaydı

## Kaynak ve karar

Onay kaynağı: proje sahibinin **28 Ekim 2026 konuşmasındaki GATE-04 talimatı**, Option B için açık onay. Bu kayıt söz konusu onayı belgeler; imza, kimlik doğrulaması veya başka kişinin onayı uydurulmaz.

Yeni ve bağımsız LOCAL-only risk kararı: **28 Ekim 2026'dan 6 Kasım 2026 saat23:59 Europe/Istanbul'a kadar**. Helper başlangıcı `2026-10-28T00:00:00+03:00`, son izinli anı `2026-11-06T23:59:00+03:00` olarak denetler; son andan sonra reddeder. Eski16–30 Ekim istisnası ayrı tarihsel karardır; eski tarihler değiştirilmez. Bu onay otomatik yenilenmez.

Kod commit'i `f2b7058e006a2b6f2fc46d12546e347b6dedf722`, branch `feature/backend-10-liveops`, [Draft PR16](https://github.com/hansaglam/unitylogisticore/pull/16). Kilit SHA256 `AA1F6BA0D7A5CCAC1C9551A3941B8F1A2FD66B7482FD487C5C4D354ACBC5CC9F`. Node22; Firebase CLI15.34.0, Admin14.5.0, Functions7.4.0 ve mevcut bütün locked dependency graph. Dependency değişikliği yetkilendirilmez.

## Açıkça kabul edilen residual riskler

1. Aralıklı Windows Firebase Functions worker initialization/HTTP timeout hataları. Geçmiş failure gerçektir; root cause belirsiz, kalıcı mitigation yoktur. Developer validation kesintisi, teşhis ve bakım maliyeti kalır. Worker reliability **HOLD**.
2. **GHSA-8988-4f7v-96qf:** firebase-tools15.34.0 → @google-cloud/pubsub5.3.1 → @opentelemetry/core1.30.1. Inbound baggage allocation riski; patch2.8.0 mevcut core^1.30.1 parent aralığı dışında. Desteklenen local workflow'da vulnerable extractor yolu gösterilmedi; genel erişilemezlik ispatlanmadı.
3. **GHSA-w5hq-g745-h8pq:** firebase-tools15.34.0 → gaxios6.7.1 → uuid9.0.1. CLI → google-auth-library9.15.1 → gaxios ve aynı auth üzerinden gcp-metadata6.1.1/gtoken7.1.0 ek yolları aynı dev kaydına gider. v3/v5/v6 dış buffer/offset riski; gaxios consumer v4 kullanır. Patch11.1.1 mevcut uuid^9.0.1 aralığı dışında. Bütün CLI kullanım yolları için exploit-yok garantisi verilmez.

28 Ekim GATE-04 fresh audit: runtime0; full5 moderate affected package/2 mevcut advisory, high0/critical0. Kapsam GATE-03 baseline'ıyla aynı. **General security audit HOLD**; onay açıkları düzeltmez ve full audit'i temiz yapmaz. Yeni/incelemesiz advisory'ler kapsam dışıdır.

## İzinli kapsam ve koşullar

Yalnız güvenilir yerel workstation/checkout, mevcut sentetik veri, `demo-logisticore-local`, mevcut loopback emulator isolation, authentication/environment/security guards ve default-deny Rules. Guard/launcher/timeout gevşetmesi, test skip/financial concurrency azaltılması yok. Bulgular taramada görünür kalır; historical failure/receipt/catalog kanıtları korunur. Yerel kötü niyetli süreçlere karşı emulator izolasyonu tek başına güvenlik sınırı değildir.

Passive capture yalnız developer'ın açık manuel invocation'ıyla; tek mevcut guarded validation, retry yok, credential/telemetry upload yok. Kaynak, dependency veya trust boundary değişirse bu exact baseline kabulü otomatik aktarılmaz.

## Durdurma ve yeniden değerlendirme

Süre sonu, yeni/değişen advisory, lock/dependency değişimi, kaynak veya güven sınırı değişimi, guard zayıflaması, credentials/gerçek proje/gerçek oyuncu verisi, forwarding/untrusted code/CI kullanımı durumunda kapsam dışı çalışmayı durdur ve HOLD bildir. Yeni insan kararı olmadan onayı genişletme veya uzatma. Worker failure tekrarında başarısızlığı koru; otomatik rerun-until-green veya test bypass yapma, worker incident açık kalsın.

Önerilen yeniden kontrol: her merge kararı öncesi, değişiklik/failure tetikleyicisinde ve en geç bitişten önce güncel audit/kapsam incelemesi. Bunlar önerilen operasyonel kontrollerdir; yapılmış veya ayrıca onaylanmış bir gelecek inceleme olarak sunulmaz.

## Yasaklar ve merge ayrımı

Gerçek Firebase DEV/PROD, production deployment, migration, gerçek player data, untrusted CI, dependency değişimi, güvenlik bypass, yeni backend fazları ve otomatik merge **yetkilendirilmedi**. Production/migration **BLOCK**. Numeric economy değerleri onaysız sentetik aday olarak kalır.

Bu karar altında feature **LOCAL main entegrasyonu için koşullu uygun** değerlendirilebilir; fakat **ayrı açık merge işlem yetkisi gerekir**. PR hazır işaretleme/commit/push/merge bu kayıtla yapılmaz. Worker reliability HOLD ve general audit HOLD merge hazırlığı sırasında da korunur.
