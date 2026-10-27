# BACKEND-10 — LiveOps simülasyon raporu

26 Ekim 2026. UNAPPROVED SYNTHETIC CANDIDATES. Gerçek şehir, hava, trafik, oyuncu talebi veya production ekonomisi değildir. Çalıştırma: backend içinde npm run build ardından node tools/liveops-sim/report.mjs. Offline; Firestore/credential/network kullanmaz. Para hesapları integer/BigInt; decimal yalnız tabloda sunumdur.

Başlangıç15000 LGC, base gross4500, costs1400, ikinci truck+driver50000 ve korunacak nakit5000. Kredi20000+1600 fee, netten3500bps capped payment ve reserve5000. Bunlar önceki sentetik karşılaştırma değerleridir; runtime kataloglarına yayımlanmaz. Dönem1 dakika, varsayılan delivery1 dönem, başlangıç1 araç ve dönem başına1 teklif. En fazla2 teklif/2 araç karşılaştırılır. Teklifler üretildiğinde sabitlenir; kabul/seyahat boyunca event sona erse bile değiştirilmez.

| Senaryo | Tamamlanan iş | İkinci filo için iş | Edinim dönemi | Nakit LGC | Borç LGC | Ret |
| --- | --- | --- | --- | --- | --- | --- |
| no-event | 30 | 13 | 13 | 58000.00 | 0.00 | 0 |
| positive | 30 | 12 | 12 | 71500.00 | 0.00 | 0 |
| negative | 30 | 16 | 16 | 44500.00 | 0.00 | 0 |
| begins-period5 | 30 | 12 | 12 | 69250.00 | 0.00 | 0 |
| expires-period5 | 30 | 13 | 13 | 60250.00 | 0.00 | 0 |
| overlap-priority | 30 | 14 | 14 | 51250.00 | 0.00 | 0 |
| equal-priority | 30 | 13 | 13 | 64750.00 | 0.00 | 0 |
| city-matching | 30 | 12 | 12 | 71500.00 | 0.00 | 0 |
| cargo-matching | 30 | 12 | 12 | 71500.00 | 0.00 | 0 |
| city-nonmatching | 30 | 13 | 13 | 58000.00 | 0.00 | 0 |
| event-high-demand | 30 | 11 | 11 | 78250.00 | 0.00 | 0 |
| event-high-supply | 30 | 13 | 13 | 58000.00 | 0.00 | 0 |
| maximum-cap | 30 | 11 | 11 | 78250.00 | 0.00 | 0 |
| minimum-cap | 30 | 16 | 16 | 44500.00 | 0.00 | 0 |
| unprofitable | 0 | yok | yok | 15000.00 | 0.00 | 30 |
| credit-free-growth | 30 | 12 | 12 | 71500.00 | 0.00 | 0 |
| active-debt | 30 | 9 | 9 | 69900.00 | 0.00 | 0 |
| repayment-negative | 30 | 12 | 12 | 42900.00 | 0.00 | 0 |
| expires-before-completion | 10 | yok | yok | 46450.00 | 0.00 | 0 |
| two-offers-short | 48 | 12 | 12 | 135400.00 | 0.00 | 0 |
| two-offers-long | 10 | yok | yok | 50500.00 | 0.00 | 0 |
| zero-offers-debt | 0 | yok | yok | 35000.00 | 21600.00 | 0 |
| low-cash-tiny-profit | 30 | yok | yok | 20000.30 | 21600.00 | 0 |
| alternating-series | 30 | 13 | 13 | 58967.50 | 0.00 | 0 |

## Yorum ve sınırlar

No-event13, sürekli pozitif event12, sürekli negatif event16 başarılı teslimatta ikinci filo. 10–15 hedefi evrensel değildir. Bekleme/seyahat süresi arttığında aynı takvim süresinde daha az teslimat ve daha geç edinim vardır. Son bekleyen işler horizon sonrasında tamamlanır; tablodaki tamamlanan iş sayısı tüm üretilmiş işlerdir. XP/level progression, gerçek oyuncu oturumu veya rota mesafesi modellenmez.

Aynı öncelikte ASCII eventId sırası ve en çok1 financial winner uygulanır. Market+event signed bps toplamı-1000/+1500 global envelope içinde clamp edilir; base üzerinden bir kez floor yapılır. Market gross ile event gross ayrı cüzdan kredileri değildir. Yüksek talep+pozitif event üst cap'i aşmaz; fixed costs nedeniyle net-profit yüzde artışı gross artışından büyük olabilir. İki teklif/ikinci filo arz varsayımı inflation etkisini büyütür.

Her completion cash+capital-debt=initial+profits-fees eşitliğini doğrular. Loan origination gelir değildir; fee/debt event bitince silinmez. Sıfır teklif, kârsız teklifler ve çok küçük pozitif nette integer repayment0 kalması soft-lock/uzun borç riski yaratır. Rescue, debt forgiveness veya free event grant yoktur.

Manipulation duyarlılığı: aşırı event bps ve duplicate ID katalogda reddedilir; overlap bonusları stack edilmez. Gerçek server pool uniqueness/retry/receipt ve client-time reddi emulator testlerinin konusudur; offline tablo bu güvenlik mekanizmalarının yerine geçmez. Tüm schedule/priority/weights/duration/availability değerleri ayrıca ürün onayı gerektirir. Tarihsel ECONOMY-00/BANK-00/BACKEND-09B simülasyonları değiştirilmedi.
