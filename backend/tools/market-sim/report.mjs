import {scenarios} from './model.mjs';
const rows=scenarios(),money=n=>(n/100).toFixed(2);
console.log(`# BACKEND-09B — Deterministik market simülasyonu

26 Ekim 2026. Tüm tutarlar/oranlar UNAPPROVED SYNTHETIC CANDIDATES. Gerçek şehir istatistiği, oyuncu talebi veya production fiyatı değildir. Çalıştırma: backend içinde npm run build ardından node tools/market-sim/report.mjs. Hesaplar integer minor units/BigInt; yalnız aşağıdaki görüntüleme decimal string'dir.

Başlangıç15000 LGC; base gross4500; low/base/high costs2000/1400/700; ikinci truck+driver50000 ve elde tutulacak reserve5000. Kredi20000+1600 fee, positive netten3500bps, reserve5000. Bunlar geçmiş tasarım karşılaştırma adaylarıdır; runtime bank tier config'i olarak yayımlanmaz. XP/level progression modellenmez; gerçek progression iddiası yalnız runtime testlerinden gelir.

| Senaryo | Teslimat | İkinci filo için iş | Edinim dönemi | Son nakit LGC | Son borç LGC | Ret |
| --- | --- | --- | --- | --- | --- | --- |`);
for(const r of rows)console.log(`| ${r.name} | ${r.deliveries} | ${r.acquiredAt??'yok'} | ${r.acquiredPeriod??'yok'} | ${money(r.cash)} | ${money(r.debt)} | ${r.rejected} |`);
console.log(`
## Yorum ve sınırlar

Neutral/base borçsuz hedef13 iş; high-demand11, high-supply16, low-profit16 ve high-profit11. Dolayısıyla10–15 hedefi tüm market durumlarında sağlanmaz. Maksimum yükseliş1500bps, düşüş-1000bps; nötr0. Bölgesel best-route seçimi yalnız karşılaştırılan sentetik destination seçenekleri arasındadır. Cargo-half-weight baskıyı azaltır. Epoch-jitter aynı seed/epoch/city/cargo için tekrar üretilebilir; randomness yok.

Kredi bir gelir değildir; cash+capital-outstanding=initialCash+sum(profit)-fixedFees her delivery'de doğrulanır. Market downturn fee/loan terms değiştirmez. Mevcut borç, teklif arzı sıfır veya teklifler kârsızsa kapanmayabilir. Çok düşük pozitif net integer percentage yuvarlaması nedeniyle otomatik ödemeyi0 bırakabilir. Rescue/bailout, gerçek oyuncu aktivitesi, VIP, gerçek km ve dynamic salaries modellenmez.

Yeni ikinci filonun getirisi ancak yeterli teklif arzı ve eşzamanlı iş kapasitesi varsayımıyla artar. Her dönem bir tamamlanabilir iş/araç ve en çok iki teklif varsayılır; gerçek route duration/oyuncu oturumu/load test sonucu değildir. Edinim kararında nakit rezervi korunur; reserve zarar sigortası değildir. Net-at-claim maliyeti yüzünden sıfır nakitle pozitif-net iş yapılabilir, ama wallet/XP cap ve supply yokluğu runtime soft-lock riskleri olarak kalır.

Sabit aynı-epoch pool kimliği sınırsız yeniden üretimi engeller; future player-driven supply/demand, retention, aktivasyon/migration ve sayısal denge ayrı inceleme gerektirir. Bu simülasyon Firestore atomicity kanıtı değildir; gerçek emulator sonuçları BACKEND-09B_REPORT.md içinde ayrıca raporlanır. Tarihsel ECONOMY-00/BANK-00 model ve raporları değiştirilmedi.`);
