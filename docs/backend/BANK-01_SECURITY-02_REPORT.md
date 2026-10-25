# BANK-01 SECURITY-02 — Onaylı minimum dependency remediation

**SECURITY-02 GATE: PASS. LOCAL SECURITY: CONDITIONAL PASS. GENERAL SECURITY: HOLD. PRODUCTION/MIGRATION: BLOCK.** BANK-01 ayrı completion/review aşamasına geçebilir; bu karar otomatik final acceptance, commit veya release onayı değildir.

24 Ekim 2026. Çalışma dalı `feature/bank-01`, HEAD `bbacf04af40f1973787f40e9c7524defaf62b322`, root `D:/Projects/unitylogisticore`. SECURITY-01 planı ve BANK-01 raporu başlangıçta tam okundu; Option A kullanıcının açık SECURITY-02 onayıyla uygulandı.

## Dependency düzeltmesi ve kurulum

`npm update body-parser fast-uri --package-lock-only --ignore-scripts --no-fund` exit0. Bütün lock diff'i incelendi: **yalnız dört package kaydı,18 eklenen/18 çıkarılan satır**; başka package version/metadata değişmedi, hoisting değişmedi.

| Fiziksel kayıt | Önce | Sonra |
| --- | --- | --- |
| node_modules/body-parser | 1.20.8 | **1.20.9** |
| node_modules/firebase-functions/node_modules/body-parser | 2.3.0 | **2.4.0** |
| node_modules/@modelcontextprotocol/sdk/node_modules/body-parser | 2.3.0 | **2.4.0** |
| node_modules/fast-uri | 3.1.8 | **3.1.9** |

Version, registry resolved URL, integrity ve2.4.0 content-type/iconv-lite/qs dependency ranges npm tarafından güncellendi; SECURITY-01 registry metadata ile aynı. Lock elle düzenlenmedi. Root/functions manifestleri, tüm direct Firebase sürümleri ve mevcut grpc override aynı; yeni override yok.

Node22.23.2/npm10.9.8 ile backend kökünde `npm ci --ignore-scripts --no-fund` **exit0**,773 paket kuruldu. Kurulum audit'i görünür bırakıldı; deprecation uyarıları gizlenmedi. Kurulum öncesi/sonrası lock SHA256 aynı:

`AA1F6BA0D7A5CCAC1C9551A3941B8F1A2FD66B7482FD487C5C4D354ACBC5CC9F`

Reproducible installation **PASS**. `npm ls --all --json`: problems boş. npm ls/explain ve fiziksel dizin taraması üç gerçek body-parser ve bir fast-uri kopyasını doğruladı;1.20.8/2.3.0/3.1.8 kalmadı. `@types/body-parser1.19.6` yalnız type package'dır, vulnerable body-parser kopyası değildir.

Parent ilişkileri korunur: Functions7.4.0 → Express5.3.0 →2.4.0 runtime; CLI15.34.0 →1.20.9 direct/Express4.22.3/exegesis4.3.0; CLI → MCP SDK1.32.1 → Express5.3.0 →2.4.0; CLI → AJV8.20.0 → fast-uri3.1.9. Tüm parent semver aralıkları uyumludur.

## Yeni güvenlik taramaları

Registry erişimi başarılı. `npm audit --omit=dev --json --fetch-retries=0 --fetch-timeout=20000` **exit0,0 bulgu**. Ayrı full audit **exit1,5 moderate affected package,0 high/critical**. body-parser GHSA-27r3-jxmh-p828 ve dört fast-uri advisory'si graph'ta yok. Yeni advisory yok; full audit temiz diye sunulmaz.

Kalan iki distinct advisory:

- GHSA-8988-4f7v-96qf: CLI15.34.0 → pubsub5.3.1 → OpenTelemetry core1.30.1. Pub/Sub/baggage yeni workflow'a eklenmedi.
- GHSA-w5hq-g745-h8pq: CLI15.34.0 → gaxios6.7.1 → uuid9.0.1; mevcut google-auth-library/gcp-metadata/gtoken ek yolları değişmedi. Gaxios argümansız v4 consumer'ı korunur.

Affected package sayısı5: @google-cloud/pubsub, @opentelemetry/core, firebase-tools, gaxios, uuid. Bu beş bağımsız exploit değildir. Diğer bütün lock package kayıtlarının byte-equivalent JSON olduğu karşılaştırıldı; eski iki advisory'nin zincirleri genişlemedi. Braces3.0.3 değişmedi; withdrawn advisory'nin audit'ten yokluğu bu remediation'a mal edilmez.

**Yeni dependency bulgularının giderilmesi PASS; LOCAL SECURITY CONDITIONAL PASS** yalnız önceki trusted-local16–30 Ekim istisnası altında. Genel güvenlik HOLD; production/real DEV/untrusted CI/deploy/migration BLOCK. Yeni istisna verilmedi,30 Ekim süre sınırı uzatılmadı.

## Offline test bakımı

Sadece `backend/tools/economy-sim/model.test.mjs` içindeki eski Git-base lock karşılaştırması yerine yeni onaylı lock içeriğinin SHA256 kontrolü eklendi. Checkout CRLF/LF farkını normalize eden hash:

`14d593562413c39bfaff9e74b7490c900ae33e570770cd049e2c77437002ab4a`

Diğer manifest, Unity, guard, scripts, Firebase config ve Rules historical-base kontrolleri aynen korunur. Tüm ekonomik assertion'lar aynı; test silinmedi/skip edilmedi. Ham dosya hash'i ile normalized hash'in farklı olması yalnız satır sonlarındandır. Offline75/75 ilk çalışma PASS; full validate sonrası tekrar koşu da75/75 PASS.

## Doğrulama durumu

Full `npm run validate` güvenlik kapısı sağlandıktan sonra başlatıldı ve **exit0** ile tamamlandı. Hiçbir BANK-01 runtime/integration test logic düzeltmesi yapılmadı.

| Kontrol | Gerçek sonuç |
| --- | --- |
| TypeScript build/typecheck | PASS |
| Unit test | 30/30 PASS,0 fail/skip |
| Auth/Firestore/Functions emulator toplamı | 86/86 PASS,0 fail/cancelled/skip;12 top-level test ve74 alt test |
| BANK-01 yeni suite | 20/20 alt test PASS (+1 parent kaydı); eski65 emulator kayıt da PASS |
| ECONOMY-00/BANK-00 offline son koşu | 75/75 PASS (27+48),exit0 |
| npm run validate | PASS,exit0 |

Gerçek emulator test kanıtları:

- BANK alt test5: sekiz eşzamanlı quote aynı right'ı reuse eder; sekiz origination'dan bir payout,XP artışı yok, replay exact. Alt test12: sekiz V3 claim'den bir repayment/credit.
- Alt test4: replacement/cancel/expiry/replay ve21 concurrent request ile bütçe; replacement/cancel adımları sıralıdır. Ayrı eşzamanlı replacement-vs-consume senaryosu bu suite'te bağımsız olarak ispatlanmadı; final review coverage değerlendirmesinde açıkça incelenmeli.
- Alt test6–8: fee-first partial/capped payoff, reserve, paid-loan reddi, distinct sequence ile hemen reborrow, tarihi replay, çok döngülü nakit korunumu ve canonical level/pinned terms.
- Alt test13–15: gerçek manual-auto/final payoff yarışı, iki farklı delivery'nin aynı borcu cap ederek kapatması, payoff-quote ve yeni origination-delivery yarışları PASS. İzinli serial sonuçlar testte açık; stale loan eski lifecycle'ı yeniden açmıyor.
- Alt test10: aktif loan varken V1 gross/V2 net otomatik repayment yapmıyor; completed reads ve receipts korunuyor. Alt test11–12/16–17: V3 no-loan/current-loan, acceptance sonrası origination, integer fee/principal/journal/XP, intermediate wallet cap ve corrupt journal fail-closed PASS.
- Alt test18: controlled callback rollback bütün şirket kayıtlarını korur; SDK'ya controlled aborted code10 verilerek gerçek retry yürütülür. Bu fault injection bir doğal network arızası olarak sunulmaz. Alt test19: commit sonrası receipt-read kaybı same-key ile ikinci payout olmadan kurtarılır.
- Alt test20: gerçek Auth/Functions HTTP wrapper'larında unauthorized/restricted payload, readonly state, quote/take/repay/cancel PASS. Mevcut suite default-deny Rules altında bank/catalog/offer/loan/repayment/journal yollarını authenticated ve unauthenticated erişime karşı test etti.

Koşuda işlevsel hata bulunmadı; mevcut testlerin tamamı geçti. Bu sonuç bütün75 BANK-01 acceptance maddesinin bağımsız testle eksiksiz kanıtlandığı veya production ölçek/güvenlik onayı olduğu anlamına gelmez. Final BANK-01 completion/review, test coverage ve API sözleşmesi incelemesini ayrıca yapmalıdır; feature geliştirme bu fazda sürdürülmedi.

## Kapanış, izolasyon ve preservation

Log demo-logisticore-local projesini doğrular. Mevcut launcher yalnız auth/firestore/functions, loopback ve credential allowlist ile çalıştı; ADC/real Firebase/production config kullanılmadı. CLI başarılı testten sonra emulator shutdown yaptı; Firestore SIGINT normal kapanış sinyalidir.9099/8080/9150/5001/4400/4500/8257 için dinleyen süreç sayısı0. CLI kapanış mesajındaki eventarc/tasks cleanup satırları bu servislerin test kapsamına eklendiği anlamına gelmez.

Başlangıçta hash kaydı alınan26 mevcut dosyada yalnız onaylı package-lock ve offline test değişti; diğer24 dosya byte olarak aynı. Bunlar BANK-01 kaynaklarını/untracked dosyalarını, SECURITY-01 planını ve manifestleri kapsar. Unity/Packages/ProjectSettings, guard, launcher, Firebase config/Rules diff'i boş. Eski ignored kanıtlar korunur. Limited secret pattern scan0 eşleşme; comprehensive security audit iddiası yok. git diff --check PASS. Lock hash'i son doğrulama sonrası aynı.

Bu fazda değişen **üç dosya**: backend/package-lock.json; backend/tools/economy-sim/model.test.mjs; docs/backend/BANK-01_SECURITY-02_REPORT.md. Yeni dependency, override, BANK-01 runtime değişikliği yok. Çalışma feature/bank-01 üzerinde unstaged korunur; commit/push/PR/merge/deploy/migration yok. BACKEND-09B başlatılmadı. BANK-01 ayrı completion/review için uygun; general audit HOLD ve30 Ekim istisna sonu aynen kalır.

## Kanıtlar ve değişiklik sınırı

Ignored `.local/security-02-*`: before.json (başlangıç hash'leri), before-lock.json (orijinal lock), lock-refresh.log, lock-diff.json, ci.log/ci-result.json, tree.txt, explain-body.json, explain-uri.json, all.json, physical.json, runtime-audit.json, full-audit.json, audit-exits.json, offline.log/offline-final.log, validation.log/validation-exit.json, preservation-after.json. Önceki local raporların üzerine yazılmadı.

SECURITY-02'nin izin verilen dosyaları: backend/package-lock.json, backend/tools/economy-sim/model.test.mjs ve bu rapor. Önceki BANK-0122 dosya+SECURITY-01 planı korunur; yalnız yukarıdaki açıkça onaylı test-baseline eki istisnadır. Commit/push/PR/merge/deploy/migration/BACKEND-09B yok.
