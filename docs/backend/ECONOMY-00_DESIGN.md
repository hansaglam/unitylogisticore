# ECONOMY-00 — Ekonomi tasarımı v1

23 Ekim 2026. Etiket kapsamı: her bölümün etiketi o bölümdeki önerileri kapsar; sayısal tablolardaki tüm değerler **PROPOSED / UNAPPROVED DESIGN CANDIDATE**. LGC veya gerçek ekonomi aktive edilmedi.

## APPROVED PRODUCT DIRECTION — hedef ve sınır

BALANCED PROGRESSION. Yeni şirket bir starter truck ve bir starter driver alır; warehouse ve ek trailer almaz. Borçsuz yaklaşık10–15 başarılı starter delivery sonunda ikinci truck+driver ve çalışabilecek rezerv hedeflenir. CLIENT SENDS INTENT. BACKEND VALIDATES AND DECIDES.

LGC çalışma para birimi; LogistiCore Credits, scale2, oyun içi kurgusal/nonredeemable. Gerçek para/borç ilişkisi, kripto, dış exchange, banka entegrasyonu yoktur. LGC'nin production config olarak onayı bu belgeden çıkarılamaz. 15000.00 LGC=1500000 minor units. Sunum TR/EN/DE Unity localization; server/parsing para hesabını float'a dönüştürmez.

## IMPLEMENTED — mevcut mimariyle ilişki

BACKEND-00 legacy money/cash ve level/companyLevel anlamlarının bilinmediğini, migration'ın ayrı inceleme gerektirdiğini kaydeder. BACKEND-01 guard/default-deny, BACKEND-02 trusted identity, BACKEND-03 durable receipts, BACKEND-04 canonical company, BACKEND-05 safe integer wallet/progression/ledger/claims, BACKEND-08A fleet/warehouse, BACKEND-06 pinned contracts, BACKEND-07 atomic delivery reward ve BACKEND-08B atomic acquisitions incelendi. Tarihsel raporların “gelecekte” ifadeleri sonraki fazların ekleriyle okunmalıdır.

Bugünkü EconomyConfig exact schema1; finance ve progression config hash pin'li, aynı version değişimi fail-closed. Mevcut claimDeliveryReward gross reward'u tek delivery_reward ledger satırıyla verir; operating costs/bank yoktur. planEconomyMutation canonical wallet/progression +ledger+claim write planıdır. Commerce exact server price ile debit yapar; current satın alma validator'ında bu tasarımın5000 LGC rezerv kuralı yoktur. Bu faz runtime/API export/Rules/canonical schema değiştirmez.

## PROPOSED — ekonomik adaylar

| Girdi | Base LGC | Durum |
| --- | --- | --- |
| Starting cash | 15000 | Onaysız aday |
| İkinci truck | 45000 | Onaysız aday |
| Driver acquisition | 5000 | Onaysız aday; maaş değildir |
| Edinim sonrası reserve | 5000 | Onaysız aday |
| Gross delivery | 4500 | Onaysız ortalama |
| Fuel | 700 | Onaysız operasyon gideri |
| Wages | 400 | Onaysız iş başına gider; payroll yok |
| Maintenance | 200 | Onaysız iş başına muhasebeleştirme |
| Tolls | 100 | Onaysız iş başına gider |
| Net operating profit | 3100 | Gross−1400 |

Low: gross4500, fuel1000/wages600/maintenance300/tolls100 →net2500. High: gross4500, fuel350/wages200/maintenance100/tolls50 →net3800. Bu varyasyonlar aynı route'un doğrulanmış maliyeti değildir; reproducible stress adaylarıdır.

Borçsuz koşul: `initialCash + deliveries*netProfit >= truckCost+driverCost+purchaseReserve`. Base açık40000 LGC; ceil(40000/3100)=13. Low16, high11. Önerilen test bandı net2800–3800 →15–11 iş. 15 iş için net en az2666.67 LGC; input minor units ile ceil uygulanır. Gerçek dağılım/retention ölçümü yokken “garantili13” denemez. Reward−10% →16; tüm costs+20% →15; birlikte→17. Tablolar ve180 per-delivery satır [simülasyon raporunda](ECONOMY-00_SIMULATION_REPORT.md).

## PROPOSED — gider ne zaman doğmalı?

| Gider | İncelenen tercih | Gerekçe / risk |
| --- | --- | --- |
| Fuel | Start'ta authoritative fixed quote debit/reservation | Operasyonun prefund ihtiyacını görünür yapar; bakiyesiz şirketi kilitleyebilir. Gerçek consumption formülü UNKNOWN. |
| Tolls | Start'ta quote ile fuel yanında | Client route/price manipülasyonu yok; iptal/refund policy ayrıca gerekir. |
| Wages | Completion'da iş başına pinned gider | Offline günlük ücret/ceza yok; hired driver fee ile karıştırılmaz. |
| Maintenance | Completion'da explicit bounded gider | Şimdilik depreciation/repair simülasyonu değildir; ileride bakım zamanı separate design. |
| Periodic payroll/interest | DEFERRED | Background cron/daily drain ve offline soft-lock yaratılmaz. |

Offline ana aday `fuel_tolls_upfront`: start öncesi fuel+tolls nakdi bulunmalı; model tek başarılı iş adımında toplam neti işler. Ara start debit journal'ını veya aktif teslimat state'ini simüle etmez. `all_upfront` ve `net_completion` alternatifleri desteklenir. Net-completion ile nakit0 fakat pozitif net iş yürüyebilir; bu otomatik para üretimi değil giderin aynı reward'dan tahsili varsayımıdır, üretim politikası onayı gerektirir.

BACKEND-09A'da daha küçük ilk adım tüm approved costs'u tek completion transaction'ında netlemek olabilir; bununla prefunding tercihi ayrıca kararlaştırılmalı. Start'ta tahsil seçilirse prior-cost source/receipt delivery'ye pin edilmeli; completion tekrar aynı gideri düşmemeli. Henüz hiçbir yöntem runtime'a konulmadı.

## PROPOSED — kredi ve muhasebe

Tek aktif company loan, principal20000 LGC, fixed noncompounding charge800bps (%8)=1600; liability21600. Unlock5 başarılı delivery; repay3500bps (%35) positive operating profit, reserve floor5000. Model loan beşinci iş tamamlandıktan sonra öder; kesinti altıncı işte başlar. “Earliest permitted” aynı beşinci iştir. Kredi0. işte mevcut aday kurala aykırıdır; ikinci senaryo gibi gösterilmez.

Kredi revenue/XP/completion değildir. Fixed finance charge bir kez oluşur; günlük faiz, gecikme cezası, starter haczi veya debt nedeniyle delivery lockout yoktur. Loan liability bakiye değildir. Tekrar borçlanma modelde ilk krediden sonra tamamen kapalıdır; ikinci kredi için earned-history/cooldown/limit politikası ayrı onay bekler.

Per-delivery exact minor-unit denklemleri:
```text
operatingProfit = grossReward - authorizedOperatingCosts
positiveProfit = max(0, operatingProfit)
targetRepayment = floor(positiveProfit * repaymentBasisPoints / 10000)
cashAfterOperations = cashBefore + operatingProfit
availableForRepayment = max(0, cashAfterOperations - cashReserveFloor)
actualRepayment = min(remainingLiability, targetRepayment, availableForRepayment)
cashAfterSettlement = cashAfterOperations - actualRepayment
remainingLiabilityAfter = remainingLiability - actualRepayment
```

BigInt ara çarpım/bölüm, ardından safe integer kontrolü; wire JSON integer safe range. Fee `floor(principal*chargeBps/10000)`; küsurat minor unit aşağı yuvarlanır, gizli float yok. Fee-first repayment allocation adaydır; early repayment fazla ödeme veya ek ücret yaratmaz, kalan fee için rebate modelde yoktur. Manual pay `min(request, liability, max(0,cash-reserve))` ve tek state transition.

Modelde gerçekleşen şirket kârı=cumulative operating profit−paid fee; taahhüt edilmiş ücret sonrası ekonomik kâr=operating profit−full fixed fee. Finans charge oluştuğu an liability/net position'a yansır; paid fee ölçüsü tek muhasebe standardı iddiası değildir. Principal repayment expense değildir. Fleet acquisition capital spending olarak ayrı izlenir, depreciation/resale UNKNOWN. Net financial position=cash−liability; fleet valuation içermez. Yardımcı acquired-cost-inclusive position=cash+acquisitionCost−liability; starter varlıklara değer uydurmaz.

Conservation:
`cash = initialCash + operatingProfit + principalReceived − principalRepaid − feesRepaid − capitalSpent`.
`debt = principalReceived + fixedCharge − principalRepaid − feesRepaid`.
Bu yüzden `cash + acquiredCost − debt = initialCash + operatingProfit − fixedCharge`. Repayment cash/debt'i eşit azaltır; yeni zarar gibi iki kez sayılmaz.

## PROPOSED — likidite ve soft-lock kararları

Base borçsuz satın alma13. işte cash5300; krediyle8. işte6545, kalan borç18345. Düşük/yüksek krediyle10/6. işte edinim ve30/22. işte borç kapanışı; base25. işte. Aynı30 tamamlanan iş ve aynı asset acquisition için kredinin son nakit maliyeti1600'dür. Ek gelir ancak ek kapasiteyle gerçekten daha fazla iş tamamlanırsa doğar.

Reserve ödeme kesintisinin alt sınırıdır, gelir garantisi değildir. İş zarar ederse cash reserve altına düşebilir; repayment0 olur. Base fuel+tolls800, iki eşzamanlı start için1600 upfront gerektirir;5000 yaklaşık6 tek start veya3 çift dalga fuel+tolls tamponudur, ücret/maintenance/offer shock dahil garanti değildir. AutoAcquirefalse ile satın alma kararları ayrıca modellenebilir; base purchaseReserve0 halinde12. işte edinim cash2200 kalır. Primary horizon30, zarar senaryosu100 işe uzatılınca36 başarı sonrası cash600 ile sonraki800 upfront ödenemez; model BLOCKED raporlar, para yaratmaz.

Önleme adayları: teklif gösterirken gerekli upfront+reserve uyarısı; server'da incelemeyle purchase reserve kuralı; starter-compatible pozitif-net teklif arzı; bounded pinned costs; açık kalan yükümlülükleri dikkate alan max-safe repayment. Basic deliveries sırf unpaid loan yüzünden yasaklanmamalıdır; ancak gideri finanse edememek ayrı liquidity problemi olabilir. Emergency grant/borç affı/bailout uygulanmadı; bu soft-lock riskinin ürün çözümü halen UNKNOWN, production readiness engelidir.

## PROPOSED — kapasite ve süre

Model her başarılı delivery'yi bir adet sayar; fleet2 olduğunda gross'u otomatik2 ile çarpmaz. Aynı30 iş için base no-credit vs kredi ve iki uygun paralel teklif, eşit60 dakika varsayımıyla22 vs19 dalga (1320 vs1140 dakika) kapasite alt sınırıdır. Bir teklif varsa ikisi de30 dalga. Gerçek duration, player session, offline time, claim gecikmesi, new offers ve contract acceptedCount/epoch quota beklemeyi değiştirir. Gerçek waiting-time veya retention tahmini yoktur.

Mevcut BACKEND-06 offer candidate assets'a pinlidir. İkinci truck+driver ancak onların IDs/version/capabilities'ine uygun teklif sunulursa kullanılabilir; kabul edilmiş işleri otomatik yeni truck'a aktaramaz. Trailers/warehouses özel contract unlock'ları final ürün kararı bekler. XP/level placeholder UNKNOWN; simulator XP value/threshold uydurmaz. Loan unlock delivery count=5 yalnız aday, player-level gereği seçilmedi.

## PROPOSED / DEFERRED — sürümleme ve fazlar

Offline scenario version/hash ayrı, approval unapproved_design_candidate; v2WorldConfig'e yüklenmez. Gelecekte approval/review provenance, economy/cost/bank catalog version/hash, accepted term snapshots, rollout barriers ve explicit migration gerekir. Eski config in-place edit veya historical ledger rewrite yok. ROAD temelinde gelir/gider kalemleri extensible; RAIL/SEA/AIR için schema/price/vehicle implementation yapılmadı, mode-specific adapters daha sonra ayrı review.

Resmî roadmap korunur: ECONOMY-00 → BACKEND-09A basic operating cost/net settlement → BANK-00 reviewed domain contract → BANK-01 emulator canonical lending → BACKEND-09B actual dynamic economy → BACKEND-10 LiveOps/admin. Bu çalışma BANK_CONTRACT_DRAFT hazırlar, BANK-00 onayını veya BANK-01'i tamamlamaz. Gelecek fazlar otomatik başlamaz.

BACKEND-09A için: ürün onaylı cost timing/bounds, pinned quote, liquidity/failure policy, v2 journal reader/writer compatibility, old delivery drain policy ve atomic settlement/concurrency tests. BANK-01 için ayrıca BANK-00 review, loan eligibility/history anchor, versioned offers, credit limits/reborrow decision, loan/repayment state, manual-vs-auto conflict ve lost-response tests gerekir. Güvenlik general HOLD, production BLOCK; mevcut trusted local istisna30 Ekim2026'da sona erer.
