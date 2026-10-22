# BANK-00 — Offline simülasyon sonuçları

**IMPLEMENTED offline; tüm miktarlar, oranlar ve süreler UNAPPROVED DESIGN CANDIDATES.** Tarihsel ECONOMY-00 sonuçları değiştirilmedi.

Başlangıç 15000 LGC; ikinci truck+driver 50000; edinim rezervi 5000. Gross 4500; low/base/high net 2500/3100/3800. Banka %8 sabit ücret, %35 pozitif net tahsilatı. Kredi belirtilen başarılı teslimatın sonunda, 0 ise onboarding sonrası alınır.

| Net | Kredi zamanı | Principal | Edinim teslimatı | 30 iş nakit | Kalan borç |
|---|---|---|---|---|---|
| 2500 | yok | 0 | 16 | 40000.00 | 0.00 |
| 2500 | 0 | 20000 | 13 | 38400.00 | 0.00 |
| 2500 | 1 | 20000 | 12 | 38400.00 | 0.00 |
| 2500 | 5 | 20000 | 10 | 38400.00 | 0.00 |
| 2500 | 6 | 20000 | 10 | 39000.00 | 600.00 |
| 2500 | 7 | 20000 | 9 | 39875.00 | 1475.00 |
| 2500 | 8 | 20000 | 8 | 40750.00 | 2350.00 |
| 3100 | yok | 0 | 13 | 58000.00 | 0.00 |
| 3100 | 0 | 20000 | 10 | 56400.00 | 0.00 |
| 3100 | 1 | 20000 | 10 | 56400.00 | 0.00 |
| 3100 | 5 | 20000 | 8 | 56400.00 | 0.00 |
| 3100 | 6 | 20000 | 7 | 56400.00 | 0.00 |
| 3100 | 7 | 20000 | 7 | 56400.00 | 0.00 |
| 3100 | 8 | 20000 | 8 | 56400.00 | 0.00 |
| 3800 | yok | 0 | 11 | 79000.00 | 0.00 |
| 3800 | 0 | 20000 | 9 | 77400.00 | 0.00 |
| 3800 | 1 | 20000 | 8 | 77400.00 | 0.00 |
| 3800 | 5 | 20000 | 6 | 77400.00 | 0.00 |
| 3800 | 6 | 20000 | 6 | 77400.00 | 0.00 |
| 3800 | 7 | 20000 | 7 | 77400.00 | 0.00 |
| 3800 | 8 | 20000 | 8 | 77400.00 | 0.00 |

## IMPLEMENTED — Kısmi kredi ve tekrar borçlanma

| Zaman / tekrar | Principal | Edinim işi | Kredi adedi | Nakit | Borç | Toplam ücret |
|---|---|---|---|---|---|---|
| 0 / 30 iş | 10000 | 14 | 1 | 57200.00 | 0.00 | 800.00 |
| 5 / 30 iş | 10000 | 13 | 1 | 57200.00 | 0.00 | 800.00 |
| 0 / 30 iş | 20000 | 10 | 1 | 56400.00 | 0.00 | 1600.00 |
| 5 / 30 iş | 20000 | 8 | 1 | 56400.00 | 0.00 | 1600.00 |
| 0 / 60 iş tekrar | 20000 | 10 | 4 | 166200.00 | 21600.00 | 6400.00 |

## IMPLEMENTED — İş yapmadan üç tam manuel kredi döngüsü

| Döngü | Nakit | Borç | Birikmiş ücret |
|---|---|---|---|
| 1 | 98400.00 | 0.00 | 1600.00 |
| 2 | 95600.00 | 0.00 | 4400.00 |
| 3 | 90800.00 | 0.00 | 9200.00 |

Başlangıç 100000; seviyeler 1/5/10, principal 20000/35000/60000. Gelir/XP yaratılmadı. Net servet yalnız ücret kadar azalır; eski paid kayıtlar sonraki kredilerde değişmez.
Aktif 20000 kredi sırasında level1→5: yeni tier limiti 35000.00, mevcut borç 21600.00, mevcut fee 1600.00 olarak kalır. Yeni limit ancak aktif borç kapandıktan sonra kullanılabilir.
Manuel sınır testi: 1000 principal +80 fee için 1079.99 ödeme sonrası borç0.01; kalan borçtan büyük sonraki niyet yalnız0.01 tahsil eder. Yeni key ile paid loan tekrar ödenmez. Reserve altındaki nakitten ödeme yapılmaz.

## IMPLEMENTED — Kapasite alt sınırı (gerçek bekleme tahmini değil)

| Kredi zamanı | Teklif/dalga | İş süresi dakika | 30 iş alt sınır dakika |
|---|---|---|---|
| yok | 1 | 30 | 900 |
| yok | 1 | 60 | 1800 |
| yok | 2 | 30 | 660 |
| yok | 2 | 60 | 1320 |
| 0 | 1 | 30 | 900 |
| 0 | 1 | 60 | 1800 |
| 0 | 2 | 30 | 600 |
| 0 | 2 | 60 | 1200 |
| 5 | 1 | 30 | 900 |
| 5 | 1 | 60 | 1800 |
| 5 | 2 | 30 | 570 |
| 5 | 2 | 60 | 1140 |
| 8 | 1 | 30 | 900 |
| 8 | 1 | 60 | 1800 |
| 8 | 2 | 30 | 570 |
| 8 | 2 | 60 | 1140 |

## IMPLEMENTED — Zarar ve nakit yetersizliği

| Senaryo | Başarılı iş | Nakit | Borç | Durma nedeni |
|---|---|---|---|
| zarar ve kredi | 86 | 600.00 | 21600.00 | INSUFFICIENT_OPERATING_CASH |
| nakit yok | 0 | 0.00 | 0.00 | INSUFFICIENT_OPERATING_CASH |
| çok düşük net | 30 | 36950.00 | 20550.00 | ufuk tamamlandı |

## PROPOSED — Yorum ve sınırlar

Borçsuz 10–15 iş hedefi base/high için sağlanır, low 16 ile dışındadır. Erken kredi kesintileri erkenden başlatır: base day-zero edinim 10, beşinci iş kredi 8, ihtiyaç anı yedinci iş kredi 7 olur. Low için ihtiyaç anı 8, high için 6. Bunlar sabit net ve tek satın alma varsayımı altında 20000 krediyle ulaşılabilecek en erken nakit eşikleridir; daha erken borçlanmak kesinti başlatır, daha geç almak edinimi geciktirir.
Aynı principal ve tamamlanan iş sayısı için tam ödenen kredilerin nihai maliyeti aynı sabit ücrettir. Erken kredi serbest harcanırsa ek varlık alınmadan borç ve tahsilat yükü oluşur. İkinci araç geliri otomatik ikiye katlamaz. Teklif arzı tek ise kapasite avantajı yoktur; çift arz tablosu yeterli nakit, uyumlu araç/teklif ve anında claim varsayar.
Model düz sıralı tamamlanmış işler ve tek ek filo çifti kullanır; start/cancel/refund, gerçek kuyruk, level XP eşikleri, bank quote/receipt/concurrency veya Firebase transaction simülasyonu değildir. Upfront yalnız başlangıç likidite kontrolüdür; toplam gider bir kez netlenir. Loan history offline bellekte tutulur ve invariant taraması yapar; backend history taraması önerilmez.
Reserve tahsilatı sınırlar, zararı engellemez. Acil fon/borç silme uygulanmadı. Örnek seviye yükseltmesi trusted fixture olayıdır; client level authority veya XP üretimi değildir.
