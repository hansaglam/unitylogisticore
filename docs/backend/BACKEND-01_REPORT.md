# BACKEND-01 — Güvenli Yerel Backend Temeli

Tarih: 16 Ekim 2026, Europe/Istanbul. Depo: `D:/Projects/unitylogisticore`. Dal: `feature/backend-01`.

**Güncel takip:** BACKEND-01-S1 güvenlik incelemesi §18'de bulunur. §01–17 ilk uygulamanın tarihsel sonuçlarıdır; S1'de `npm ci` yeniden üretilebilirliği ayrıca doğrulanmıştır. Proje sahibinin 16 Ekim 2026 tarihli dar kapsamlı risk onayı §19'da kayıtlıdır: LOCAL SECURITY GATE CONDITIONAL PASS, GENERAL SECURITY AUDIT HOLD. Önceki test sonuçları değişmemiştir.

## 01 — Yönetici özeti

Yerel TypeScript/Node 22 Firebase Functions Gen 2 temeli kuruldu. Auth, Firestore ve Functions emülatörleri `demo-logisticore-local` üzerinde birlikte çalıştı. `npm run validate` son çalışması exit 0: build, typecheck, dört unit ve iki integration testi PASS; skip yok. Gameplay, production erişimi ve migration yapılmadı.

**İşlevsel BACKEND-01 kabul kriterleri: PASS. Genel güvenlik/sonraki faz kapısı: HOLD.** Runtime bağımlılık audit'i sıfır bulgu; development Firebase CLI zincirinde 3 high + 4 moderate paket bulgusu kaldı. Bunlar üç temel advisory'nin parent paketlere yayılımını içerir; yedi ayrı exploit olduğu iddia edilmez. Sürüm düzeltmesi veya belgeli yerel araç riski değerlendirmesi olmadan tamamen temiz güvenlik sonucu verilmez. BACKEND-02 başlamadı.

## 02 — Ortam sürümleri

| Bileşen | Gözlenen/seçilen sürüm |
|---|---|
| Node | `v22.23.2`; target `22` |
| npm | `10.9.8` |
| Java | Temurin OpenJDK `21.0.12+8-LTS` |
| TypeScript | `5.9.3` |
| Firebase CLI | `15.34.0`, yalnız local devDependency |
| firebase-functions | `7.4.0`, `firebase-functions/v2/https` |
| firebase-admin | `14.5.0` |
| Rules test SDK | `5.0.2` |
| Firebase test client | `12.19.0` |
| Node types | `22.19.15` |
| Firestore Emulator | Resmi indirilen `cloud-firestore-emulator-v1.22.0.jar` |
| Test framework | Node yerleşik `node:test` |

Paketlerin npm engine/peer bilgileri kontrol edildi. En yeni Rules test SDK 6.0.0 Node >=24.12 istediğinden seçilmedi; 5.0.2 Node >=20 ve Firebase ^12 peer ile Node 22'ye uyumlu. Admin 14.5.0 Node >=22, Functions 7.4.0 Admin ^14 destekler; optional GraphQL/Apollo framework'leri eklenmedi. TypeScript 5.9.3 destekli, kararlı bir sürüm olarak seçildi. Node 22 desteği [resmi Functions belgesinde](https://firebase.google.com/docs/functions/manage-functions) doğrulandı. Exact sürümler ve lockfile v3 kullanıldı.

## 03 — Oluşturulan dosyalar

```text
backend/README.md
backend/package.json
backend/package-lock.json
backend/firebase.json
backend/firestore.rules
backend/firestore.indexes.json
backend/functions/package.json
backend/functions/tsconfig.json
backend/functions/src/index.ts
backend/functions/src/localEnvironment.ts
backend/scripts/emulators.mjs
backend/tests/unit.test.mjs
backend/tests/emulators.test.mjs
docs/api-contracts/FOUNDATION_CONTRACT.md
docs/backend/BACKEND-01_REPORT.md
```

Yerelde üretilen `node_modules`, `functions/lib`, `.local` config/binary cache ve emulator debug logları Git dışında. Emulator export'u veya gameplay seed'i oluşturulmadı.

## 04 — Mevcut dosya değişiklikleri

Yalnız root `.gitignore` sonuna backend'e dar kapsamlı kurallar eklendi. Önceki Unity kuralları korundu. Dependencies, compiled output, coverage, binary/config cache, emulator log/export, `.env*`, credentials/service-account/private key ve local override kalıpları ignore edildi. `package-lock.json`, Rules, index ve secretsiz config ignore edilmedi.

`Assets/`, `Packages/`, `ProjectSettings/`, `ARCHITECTURE.md` ve `docs/backend/BACKEND-00_AUDIT.md` değişmedi. Repo dalı değiştirilmedi; commit/push/merge/PR yapılmadı.

## 05 — Mimari kararlar

- Tek root npm workspace + tek lockfile: functions runtime bağımlılıkları ayrı manifestte, test/CLI/TS tooling root devDependencies'te. İki kurulum/lockfile yönetimi yok.
- TS kaynak `functions/src`, çıktı `functions/lib`. Strict ve unused kontroller açık; ayrı lint framework'ü gerekmedi.
- Tek callable `localHealth`: emulator yükleme ve Auth entegrasyonu için gereklidir; oyun/read-model/finans işlemi yapmaz. Production dışındaki import guard export öncesinde çalışır.
- Admin SDK açık yerel credential ile initialize edilir; ADC veya gerçek hizmet hesabı kullanılmaz. Sağlık çağrısı Admin üzerinden hiçbir kayıt okumaz/yazmaz.
- Testler Node'un built-in runner'ında; production SDK testleri değil, gerçek yerel Rules/Auth/Functions entegrasyonlarıdır.
- BACKEND-00'daki her komutta company `stateVersion` artırma önerisi uygulanmadı. Merkezi contention noktası varsayılan kabul edilmez; domain/entity version ileride transaction sınırına göre seçilecek.
- Bir authoritative balance, atomic purchases, durable idempotency, ledger ve reward tekilliği sonraki faz gereksinimleri olarak kaldı; finans kodu oluşturulmadı.

## 06 — Emulator konfigürasyonu

Proje: yalnız `demo-logisticore-local`. `.firebaserc` yok. CLI, açık project/config/only bayrakları ile çalışır. Config'teki tüm listener host'ları `127.0.0.1`; wildcard bind yok.

| Emulator | Port |
|---|---:|
| Auth | 9099 |
| Firestore | 8080 |
| Firestore websocket | 9150 |
| Functions | 5001 |
| Hub | 4400 |
| Logging | 4500 |
| UI | Kapalı |

Başlatıcı tüm sabit portlarda bind probe yapar; dolu portta hata verir, başka süreç kapatmaz. CLI Functions discovery sırasında geçici internal port da kullanır; bu health istemci endpoint'i değildir. `us-central1` yalnız yerel test route'udur, production bölge kararı değildir.

`emulators:exec` başarılı ve başarısız test koşularından sonra kendi başlattığı suite'i kapattı. CLI kapanış çıktısında eventarc/tasks yardımcılarının stop satırları görülebilir; job/Task sistemi kurulmadı. Firestore'un kapanırken SIGINT bildirmesi CLI'nin normal cleanup yolunda gözlendi, test failure sayılmadı.

## 07 — Rules davranışı

`rules_version = '2'` ve recursive wildcard altında `allow read, write: if false`. Unauthenticated ve authenticated context için get, list, create, update, delete reddedildi. `arbitrary/synthetic`, `v2Players/synthetic-user`, `v2Companies/synthetic-company/state/finance` yolları sadece denied erişim hedefleridir; bu yollara veri seed edilmedi.

Index config `indexes:[]`, `fieldOverrides:[]`; hiçbir composite index veya TTL icat edilmedi. Admin SDK Rules'ı bypass eder; sonraki domain endpoint'lerinde Auth + company ownership + invariant kontrolleri zorunlu kalır. Health callable'ın varlığı gameplay authorization uygulandığı anlamına gelmez.

## 08 — Ortam izolasyonu

`assertSafeOverrides` yanlış project ID, farklı emulator endpoint, credential/token env ve yanlış FIREBASE_CONFIG project'ini reddeder; değerleri çıktıya dökmez. `assertLocalEnvironment` zorunlu endpoint eksikliğinde live fallback yapmadan hata verir.

Launcher child environment allowlist kullanır; proxy, credential ve rastgele SDK override aktarılmaz. Her çalışmada yeni `.local/config/run-*` XDG/APPDATA diziniyle CLI login state ayrılır; metadata keşfi kapalıdır. Emulator binary cache `.local/emulators` altındadır. Firebase CLI'nin configstore/credentials kaynakları okunarak bu davranışın hangi env değişkenlerini kullandığı kontrol edildi; gerçek credential dosyası okunmadı.

Functions discovery subprocess özel `FUNCTIONS_EMULATOR_HOST` değişkenini aktarmadığından functions runtime yalnız gerekli Auth/Firestore endpoint'lerini ve `FUNCTIONS_EMULATOR=true` bayrağını zorunlu tutar; outbound Functions çağrısı yapmaz. Test runner üç endpoint'i zorunlu tutar. Yanlış proje/remote endpoint guard'ları tüm modlarda korunur.

Health trusted `request.auth.uid` alanının dolu string olmasını doğrular; istemciden UID kabul etmez. Emulator token çözümlemesi production signature güvenliğini taklit etmez; bu testler production Auth/IAM/App Check doğrulaması değildir. Yerel process'i değiştirebilen kötü niyetli kullanıcıya karşı OS sandbox iddiası yoktur; desteklenen developer akışının yanlışlıkla live hizmete bağlanması engellenir.

## 09 — TypeScript/build sonuçları

`Set-Location 'D:\Projects\unitylogisticore\backend'` altında:

- `npm run build` → PASS (`tsc -p tsconfig.json`, output yalnız functions/lib).
- `npm run typecheck` → PASS (`--noEmit`).
- `npm ls --depth=0` → PASS, workspace/runtime/tooling manifestleri çözüldü.
- `node --check backend/scripts/emulators.mjs`, `node --check backend/tests/unit.test.mjs`, `node --check backend/tests/emulators.test.mjs` repo kökünden → PASS.

Derleme Unity import'u veya credential gerektirmedi. Runtime entrypoint'in local olmayan env ile yüklenmesi beklenen fail-closed testidir; production'da çalışması istenmez.

## 10 — Test ve komut sonuçları

Tarih kullanıcı saat dilimine göre 16 Ekim 2026. Bazı tool/SDK logları UTC 15 Ekim gösterir; tarih uyuşmazlığı değil timezone farkıdır.

| Kontrol / komut | Son durum | Kanıt ve kapsam |
|---|---|---|
| root / branch / status | PASS | Doğru kök, feature/backend-01, başlangıç temiz |
| node/npm/java version | PASS | §02; eksik runtime yok |
| `npm view ... version engines peerDependencies --json` | PASS, ağ erişimli tekrar | İlk sandbox çağrıları DNS ENOTFOUND; kayıt defterine izinli erişimde metadata alındı |
| `npm install --ignore-scripts --no-fund --no-audit --fetch-retries=0 --fetch-timeout=30000` | PASS | Yerel install + lockfile; global kurulum yok; lifecycle script'leri çalıştırılmadı |
| `npm run check` | PASS | Build/typecheck + 4 unit, sıfır skip |
| `npm run test:emulators`, ilk deneme | FAIL, düzeltildi | Rules geçti; Functions discovery özel client endpoint env'ini taşımadığı için entrypoint yüklenmedi |
| `npm run validate`, ikinci deneme | FAIL, düzeltildi | Functions yüklendi; malformed token emulator'da boş UID'li auth object üretti, yalnız object varlığı kontrolü yetmedi |
| `npm run validate`, son deneme | PASS / exit 0 | Build/typecheck, 4 unit, 2 emulator integration; toplam 6 üst seviye test, 0 fail/skip |
| Rules integration | PASS | 2 auth context × 3 path × 4 doc operation + 2 list = 26 denied assertion |
| Auth/Functions integration | PASS | Kimliksiz ve bozuk token reddi; Auth Emulator signup; authenticated health; yanlış protokol/ek UID/null payload reddi; sentetik kullanıcının cleanup'ı |
| Isolation unit | PASS | Yanlış demo/real-looking project, missing/remote endpoints, credential env, conflicting FIREBASE_CONFIG, gerçek entrypoint wrong env yükleme reddi |
| `npm audit --omit=dev --audit-level=high --json` | PASS | Runtime setinde 0 advisory |
| `npm audit --audit-level=high --json` | FAIL / kalan araç bulguları | Son sorgu: 3 high, 4 moderate, 0 critical; §14 |
| `git diff --check` | PASS | Whitespace sorunu yok |
| Protected paths `git diff --exit-code -- ...` | PASS | Unity + ARCHITECTURE + BACKEND-00 değişmedi |
| Production/Auth migration/App Check enforcement | NOT RUN | Bu faz kapsam dışı ve yetkisiz |

Test çıktısındaki Firestore PERMISSION_DENIED satırları beklenen negative-test sonuçlarıdır. İlk başarısız koşular gizlenmedi; son düzeltmelerden sonra aynı entegrasyonlar başarıyla çalıştırıldı. `npm ci` ile sıfırdan ikinci kurulum yapılmadı; kurulum `npm install` ve lockfile ile doğrulandı. Manuel uzun süreli startup/Ctrl+C kullanıcı akışı ayrı test edilmedi; otomatik exec startup/cleanup çalıştı.

## 11 — Eksik önkoşullar

Node/Java/npm eksikliği yok. Sandbox varsayılan ağ erişiminde registry DNS çözümü engellendi; gerekli npm metadata/kurulum/audit ve resmi binary indirmesi izinli ağ çağrılarıyla tamamlandı. Kullanıcıya global yazılım kurulumu gerekmiyor. İlk kurulumda registry ve resmi binary indirme ağı gerekir; emulator testleri gerçek Firebase projesi gerektirmez.

## 12 — Firebase production güvenliği

Gerçek Firebase projesine bağlanılmadı, gerçek kimlik/provider kullanılmadı, oyuncu verisi okunmadı veya değişmedi. Firebase login/use/deploy/init, cloud resource oluşturma, Rules deploy, migration, export/import veya backup/restore yapılmadı. Dış ağ kullanımı npm metadata/paket/advisory, açık teknik belgeler ve resmi emulator binary indirmesi içindi; production Firebase API erişimi değildi.

Generated config yalnız demo ID içerir. Testlerde yalnız geçici yerel synthetic Auth kimliği kullanıldı. Hiçbir service-account credential oluşturulmadı. Sırlar commit edilmedi; zaten commit yapılmadı. Yerel emulator loglarında sentetik test bilgileri bulunabilir, bunlar ignore edilir.

## 13 — Git diff özeti

Başlangıç dalı `feature/backend-01`, çalışma ağacı temiz. Son değişiklik kapsamı: 15 yeni kaynak/config/test/belge dosyası ve yalnız `.gitignore` mevcut dosya değişikliği. `backend/node_modules`, `backend/functions/lib`, `backend/.local`, emulator loglarının ignore edildiği `git check-ignore` ile doğrulandı. Tracked Unity/önceki mimari dosyalarının diff'i boş. Stage/commit/push/merge/PR/branch switch yok.

## 14 — Bilinen sınırlamalar ve güvenlik advisories

İlk tam audit 12 paket kaydı (8 high, 4 moderate) buldu. Firebase istemcisinin `@grpc/grpc-js ~1.9.0` bağımlılığı için scoped override `@firebase/firestore → 1.13.6` uygulandı; Rules testleri bununla geçti. Düzeltme [gRPC advisory](https://github.com/advisories/GHSA-m9gg-hp2v-232j) sınırını karşılar. Upstream dependency güncellendiğinde override yeniden incelenmeli.

Kalan bulgular yalnız devDependency Firebase CLI ağacında:

| Temel advisory | Zincir / sayı etkisi | Değerlendirme |
|---|---|---|
| [braces stack exhaustion](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm) | braces → chokidar → firebase-tools; 3 high paket kaydı | Registry'de braces son sürümü 3.0.3 hâlâ etkilenen aralıkta. Launcher sabit yerel kaynak/config kullanır; untrusted glob/repo girdisi alınmamalı |
| [OpenTelemetry Baggage allocation](https://github.com/advisories/GHSA-8988-4f7v-96qf) | @opentelemetry/core → @google-cloud/pubsub; 2 moderate | CLI Pub/Sub dependency; bu faz Pub/Sub başlatmaz; önerilen yeni major'ı uyumsuz şekilde zorlamak yapılmadı |
| [uuid buffer bounds](https://github.com/advisories/GHSA-w5hq-g745-h8pq) | uuid → gaxios; 2 moderate | CLI transitive; runtime dependency audit'i temiz; gerçek auth/cloud işlemleri kullanılmıyor |

`npm audit fix --force` veya geniş major override uygulanmadı. Audit'in eski major CLI'ye dönme önerisi tek başına uyumluluk/güvenlik kanıtı sayılmadı. Latest supported CLI'nin tamamının vulnerabilities-free olduğu iddia edilmez. Sonraki faz öncesinde CLI sürüm düzeltmesi veya bu yalnız yerel araç kapsamının gerekçeli risk kabulü gereklidir; bu rapor kullanıcı adına risk kabul etmez.

Diğer sınırlar: emulator IAM/production Auth/App Check davranışını tam modellemez; JavaScript test/launcher dosyaları TypeScript typecheck kapsamı dışındadır fakat syntax ve gerçek integration ile kontrol edildi. Endpoint guard egress firewall değildir. Local health production endpoint'i değildir. Performans/ölçek ve Android/iOS Firebase SDK entegrasyonu ölçülmedi.

## 15 — Ertelenen sistemler

Company bootstrap, para/XP/level/reputation, asset/warehouse, kontrat/delivery/reward, receipts/ledger/claims, world economy, LiveOps/admin, market, scheduler/tasks, Cloud Run, DEV/PROD cloud projeleri ve migration yoktur. `v2Players/v2Companies` şemaları oluşturulmadı. Legacy money/cash ve level/companyLevel eşdeğerliği UNKNOWN; BACKEND-00 korunur, burada mapping yapılmaz. Full Auth linking BACKEND-02; canonical command altyapısı BACKEND-03.

## 16 — BACKEND-02 hazırlığı

Teknik yerel altyapı hazır: derleme, Auth, Firestore Rules ve Functions testi çalışıyor. **HOLD:** CLI toolchain advisory kararı/düzeltmesi ve kullanıcı incelemesi bekleniyor. BACKEND-02 için ayrıca provider/linking ve account continuity test kapsamı netleşmeli. Üretim sağlayıcı/UID geçişi bu hazır oluşun parçası değildir. Eksik Node/Java engeli yoktur.

## 17 — PASS / HOLD / BLOCK kararı

- **PASS:** BACKEND-01'in işlevsel yerel kabul testleri, repo izolasyonu ve production koruması.
- **HOLD:** Genel güvenlik incelemesi ve BACKEND-02 geçişi; kalan CLI development advisories değerlendirilmeden temiz güvenlik PASS verilmedi.
- **BLOCK / NOT AUTHORIZED:** Production deployment, gerçek proje/kimlik değişikliği ve migration; bu faz bunları yetkilendirmiyor.

Yapılan çalışma incelemeye bırakıldı. Commit, push, deployment veya BACKEND-02 başlangıcı yoktur.

## 18 — BACKEND-01-S1: Dependency Security Review & Foundation Verification

### 18.1 Kapsam, başlangıç ve kanıt yöntemi

16 Ekim 2026 tarihinde doğru kök ve `feature/backend-01` dalı yeniden doğrulandı. Başlangıçtaki `.gitignore` değişikliği ve untracked `backend/`, `docs/api-contracts/`, bu rapor önceki BACKEND-01 çalışmasıydı; korunmuştur. S1 başında tracked ve ignore edilmeyen untracked toplam 195 dosyanın SHA-256 özeti alınmıştır. Hiçbir branch değişimi, reset, commit, push, merge, deploy veya cloud kaynağı oluşturma yoktur.

Manifestler, lockfile, launcher, guard, health kaynakları, Rules, testler ve API sözleşmesi okundu. `npm ls`, lockfile `dev` işaretleri, kurulu paket kaynakları ve güncel npm registry metadata birlikte değerlendirildi. Advisory bilgileri yayıncı/GitHub kayıtlarından okundu. Buradaki **VERIFIED** kaynak/komut kanıtını; **INFERRED** kaynak akışından risk yorumunu; **UNKNOWN** kanıtlanmamış davranışı belirtir. Yıkıcı PoC, stress/OOM testi veya production erişimi yapılmadı. Statik erişilebilirlik incelemesi kapsamlı dinamik coverage iddiası değildir.

### 18.2 Kesin dependency zincirleri ve classification

Tüm aşağıdaki savunmasız paketlerin lockfile kaydı `dev:true`; Functions workspace runtime zincirinde aynı savunmasız sürümler bulunmadı. Runtime audit ayrıca sıfır bulgu döndürdü. Bu sonuç production dağıtımının güvenlik onayı değildir.

| Advisory | Kurulu savunmasız sürüm ve kesin ana zincir | Diğer yollar / audit sayısı |
|---|---|---|
| GHSA-vfj7-8cjw-p6xm | root devDependency → firebase-tools **15.34.0** → chokidar **3.6.0** → braces **3.0.3** | braces/chokidar/CLI için 3 high paket kaydı |
| GHSA-8988-4f7v-96qf | root devDependency → firebase-tools **15.34.0** → @google-cloud/pubsub **5.3.1** → @opentelemetry/core **1.30.1** | core/pubsub için 2 moderate; CLI birleşik kaydı zaten high |
| GHSA-w5hq-g745-h8pq | root devDependency → firebase-tools **15.34.0** → gaxios **6.7.1** → uuid **9.0.1** | gaxios/uuid için 2 moderate |

UUID'ye ek aynı dev yolları: `firebase-tools@15.34.0 → google-auth-library@9.15.1 → gaxios@6.7.1 → uuid@9.0.1`; aynı Auth library üzerinden `gcp-metadata@6.1.1 → gaxios@6.7.1` ve `gtoken@7.1.0 → gaxios@6.7.1` de aynı kurulu UUID paketine ulaşır. `npm ls ... --all` ile doğrulandı. Admin tarafındaki gaxios 7/8 yolları bu UUID 9 zinciri değildir.

**Son tam audit: 7 paket kaydı = 3 high + 4 moderate, 0 critical. Üç temel advisory vardır; yedi bağımsız saldırı yolu veya yedi doğrulanmış exploit değildir.** npm ve GitHub sayfalarının CVSS sayıları farklı gösterilebildiğinden keyfi tek skor seçilmedi; advisory ID, sürüm ve davranış esas alındı.

### 18.3 Braces — erişilebilirlik, etki ve düzeltme

[Yayımlanmış advisory](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm), <=3.0.3 için derin iç içe brace pattern işlenmesinde stack exhaustion bildirir; patched version yoktur. Saldırganın parser'a ulaşan pattern'i kontrol etmesi gerekir. Olası sonuç yerel CLI/izleyicinin RangeError ile durmasıdır; bu kayıt kendi başına veri sızıntısı veya uzaktan kod yürütme kanıtı değildir.

**VERIFIED kaynak:** `backend/node_modules/firebase-tools/lib/emulator/functionsEmulator.js:300` Functions dizinini `chokidar.watch` ile izler; satır 306 config ignore kalıplarını ekler. `backend/node_modules/chokidar/index.js:255–258` yalnız glob koşulunda brace içeren path'i `braces.expand` yoluna verir. Mevcut kaynak dizini `D:/Projects/unitylogisticore/backend/functions`; config ignore listesi kısa, denetlenen `node_modules`, `test`, `src`, `*.log`, `.env*`, `tsconfig.json` girdileridir. Launcher project/endpoint'leri korur fakat her glob girdisinin derinliğini doğrulayan bir sanitizer değildir.

**INFERRED:** Watcher desteklenen akışta çalışır; mevcut girdilerden savunmasız recursive davranışın tetiklendiği doğrulanmadı. Saldırgan kontrollü kaynak yolu, config ignore/glob, checkout veya farklı CLI kullanımının parse noktasına erişmesi değerlendirmeyi geçersiz kılabilir. Dosya adından tek başına bu spesifik exploit'e uçtan uca yol doğrulanmadı; untrusted repository güvenli ilan edilmez.

**Güncel metadata:** braces latest 3.0.3; chokidar 3.x latest 3.6.0; CLI latest 15.34.0 ve `chokidar:^3.6.0`. Mevcut aralıkta yayımlanmış yama yok. Chokidar major değiştirmek glob/watch semantiğini etkileyebilir; uyumluluk kanıtı olmadan override yapılmadı. Öneri: trusted checkout ve sabit kısa yollar/config, loopback suite, fork/untrusted glob çalıştırmama; upstream yamasını takip etme. Artık risk düşük maruziyetli fakat sıfır olmayan yerel kullanılabilirlik riskidir.

### 18.4 OpenTelemetry — erişilebilirlik, etki ve düzeltme

[Yayımlanmış advisory](https://github.com/advisories/GHSA-8988-4f7v-96qf), `W3CBaggagePropagator.extract()` için inbound baggage sınırlarının eksikliğini ve 2.8.0 düzeltmesini bildirir. Saldırgan kontrollü büyük baggage header/mesajı bu extractor'a verilirse bellek baskısı oluşabilir. HTTP header sınırları azaltıcıdır; messaging/custom getter üzerinde otomatik güvence değildir.

**VERIFIED kaynak:** `backend/node_modules/@opentelemetry/core/build/src/baggage/propagation/W3CBaggagePropagator.js:44,55` extract/split kodunu içerir. Buna karşılık `backend/node_modules/@google-cloud/pubsub/build/src/telemetry-tracing.js:68,536,577` **W3CTraceContextPropagator** kullanır; Baggage propagator değildir. `backend/node_modules/firebase-tools/lib/emulator/controller.js:578` Pub/Sub'u koşullu başlatır. `pubsubEmulator.js:16–22` client'ı lazy oluşturur. Desteklenen launcher yalnız auth,firestore,functions seçer. Health payload'ı telemetry extractor'a iletmez.

**INFERRED:** Paket yüklenebilmesi, savunmasız extractor çağrısı kanıtı değildir. İncelenen yerel akışta saldırgan girdisinden Baggage extractor'a yol bulunmadı; gelecekte Pub/Sub, tracing veya custom propagator eklenmesi yeniden inceleme ister. Olası etki geliştirici/CI prosesinde kaynak tüketimidir; mevcut LogistiCore oyuncu API'sinde doğrulanmış etki yoktur.

**Güncel metadata:** core 1.x latest 1.30.1; pubsub 5.x latest 5.3.1 ve `core:^1.30.1`; CLI `pubsub:^5.2.0`. Pubsub 6.3.0 Node >=22 ve core ^2.8.0 kullanır, ancak CLI'nin major aralığı dışındadır. Core 2.8.0 da Node 22'de çalışabilir fakat bu, Pub/Sub 5 ile API uyumu kanıtı değildir. Zorunlu major override yerine upstream güncellemesi beklenmesi önerilir. Kontrol: Pub/Sub/tracing açmama, dış telemetry input almama. Artık risk bu dar kapsamda düşük ve koşulludur.

### 18.5 UUID — erişilebilirlik, etki ve düzeltme

[Yayımlanmış advisory](https://github.com/advisories/GHSA-w5hq-g745-h8pq), v3/v5/v6 API'lerinde dış output buffer/offset sınırı eksikliğini bildirir; 11.1.1, 12.0.1, 13.0.1 düzeltmeleri vardır. v4 API'si bu davranıştan etkilenmez. Gerekli girdi saldırganın buffer boyutu/offset veya bunları belirleyen veri üzerinde etkisidir. Sonuç kısmi/bozuk identifier yazımı olabilir; bu JavaScript davranışını native bellek bozulması/RCE olarak sunmak doğru değildir.

**VERIFIED kaynak:** `backend/node_modules/gaxios/build/src/gaxios.js:63,417` UUID import eder ve multipart boundary için yalnız argümansız `v4()` çağırır. Gaxios JS kaynak taramasında UUID v3/v5/v6 veya dış output buffer çağrısı bulunmadı. Kurulu UUID 9'un v3/v5 ortak `dist/v35.js` kodu advisory davranışını taşır; yüklü olmak mevcut gaxios çağrısından tetiklenebilir olmak değildir.

**INFERRED:** İncelenen tüm savunmasız UUID yolları gaxios 6.7.1'e yakınsar; kullanılan API'den bu advisory'ye erişim saptanmadı. Uygulamanın doğrudan UUID 9 import etmesi veya başka consumer eklenmesi bu değerlendirmeyi geçersiz kılar.

**Güncel metadata:** UUID 9.x latest 9.0.1; gaxios 6.x latest 6.7.1 ve `uuid:^9.0.1`. UUID 11.1.1 CommonJS `require` export'u sunar; bu yararlı bir adaydır, fakat mevcut peer/dependency aralığı dışı iki major geçişin tamamı için tek başına uyumluluk kanıtı değildir. Gaxios major değiştirmek Auth/CLI consumer'larını da etkileyebilir. Savunmasız API'nin desteklenen akışta kullanılmaması karşısında yeni out-of-range override eklenmedi. Gelecekte scoped override düşünülürse multipart boundary ve CommonJS/CLI regression testleriyle ayrıca onaylanmalı. Artık risk mevcut kullanımda düşük; paket yine savunmasız ve taramada görünür bırakılmıştır.

### 18.6 Gerçek remediation sonucu ve compatibility

**Bu S1 turunda dependency manifest veya lockfile değişmedi; yeni override yoktur.** Yayınlanmış, üst paketlerin mevcut aralıklarına uyan güvenli güncelleme bulunamadı. Bu, tüm olası major geçişlerin imkânsız olduğu iddiası değildir; kapsamlı major uyumluluk kanıtı bu çalışma sırasında üretilmedi. Sadece audit sayısını azaltmak için downgrade, zorlanan major yükseltme, `npm audit fix --force`, node_modules patch'i veya advisory suppression yapılmadı.

Önceki gRPC scoped override (`@firebase/firestore → @grpc/grpc-js:1.13.6`) korundu; yeni bir S1 düzeltmesi gibi sayılmadı. Node 22, Functions 7.4.0, Admin 14.5.0, CLI 15.34.0 ve tek workspace lockfile korunmuştur. README komutları/bağımlılıkları değişmediğinden README düzenlenmedi. S1'in somut çıktısı yeniden üretilebilir kurulum kanıtı, tekrar testleri ve aşağıdaki kapsamlı risk istisnası taslağıdır; bağımlılık açığının yamalandığı iddia edilmez.

### 18.7 Yeniden üretilebilir kurulum ve son doğrulamalar

Çalışma dizini `D:\Projects\unitylogisticore\backend`:

```powershell
npm ci --ignore-scripts --no-fund --no-audit
npm run validate
npm ls --depth=0
npm audit --omit=dev --audit-level=high --json
npm audit --audit-level=high --json
```

`--no-audit` yalnız belgelenmiş kurulum komutundaki otomatik audit'i ayırır; hemen ardından runtime ve full audit açıkça çalıştırıldı. Scanning kapatılmadı veya findings gizlenmedi. `npm ci` mevcut lockfile'dan 773 paket kurdu, exit 0. Deprecated transitive paket uyarıları gözlendi; başarı diye uyarısız olduğu söylenmez. Install öncesi/sonrası lockfile SHA-256 aynı:

`84735D1B295F7B0FA86FA05AFB13A32C9A3ECF9085C19E8A208D383843EF50BB`

Bu makine/Node/npm ortamında yeniden üretilebilirlik test edildi; tüm işletim sistemleri veya offline registry için garanti verilmez.

| S1 kontrolü | Sonuç | Kanıt |
|---|---|---|
| npm ci / aynı lockfile | PASS | Exit 0, hash değişmedi |
| TypeScript build | PASS | npm run validate içindeki tsc |
| Typecheck | PASS | --noEmit |
| Unit / environment isolation | PASS | 4/4; yanlış project/endpoint, missing endpoint, credential env, gerçek entrypoint fail-closed |
| Firestore default-deny | PASS | Integration suite; get/list/write ve provisional canonical path negatif testleri |
| Auth / Functions | PASS | Sentetik signup, health success, kimliksiz/bozuk token/invalid payload reddi, kullanıcı cleanup |
| Tüm suite | PASS | 4 unit + 2 integration, 0 fail/skip; validate exit 0 |
| Runtime audit | PASS | 0 advisory |
| Full audit | FAIL | 3 high + 4 moderate, aynı üç temel advisory |
| Emulator shutdown | PASS | CLI cleanup exit 0; 4400,4500,5001,8080,9099,9150 ve bu koşudaki discovery 8422 portlarında sonradan loopback bind başarılı |
| Foundation / korunan dizinler | PASS | Kaynak incelemesi ve Git diff; Unity/BACKEND-00/ARCHITECTURE değişmedi |
| Sır göstergesi kontrolü | PASS, sınırlı | Git'e aday backend/belgelerde private-key/API-key/token kalıpları eşleşmedi; tam secret scanner veya geçmiş incelemesi değildir |
| Production / CI / saldırı PoC | NOT RUN | Yetkilendirilmedi; yerel review/test kapsamı |

İzolasyon guard'ları bağımlılıklar değişmediği halde `npm ci` sonrası tekrar sınandı. Emulator başarısızlığı live Firebase'e fallback yolu oluşturmadı. Yalnız `demo-logisticore-local` kullanıldı. Functions src, launcher, Rules ve API foundation sözleşmesi korunmuştur. Varsayımsal domain modülü, oyuncu ekonomisi veya gameplay mutasyonu yoktur.

### 18.8 Dar kapsamlı risk istisnası — S1 değerlendirmesi ve onay kapsamı

Bu bölüm S1 incelemesindeki koşulları ve o tarihteki önerileri korur. Başlangıçta taslak olan istisna, proje sahibi tarafından 16 Ekim 2026 tarihinde yalnız §19'daki kapsam ve süreyle açıkça onaylanmıştır. Aşağıdaki onay bekleyen ifadeler S1'in tarihsel değerlendirmesidir; güncel onay durumu §19'dadır.

**Kapsam:** §18.2'deki kesin sürümler/zincirler, bu lockfile, Node 22, yalnız belgelenmiş launcher ile developer workstation üzerinde sentetik LOCAL/EMULATOR geliştirme. Üç advisory için davranış, input, reachability, düzeltme durumu ve residual risk §18.3–18.5'te ayrı verilmiştir. Advisory'ler kaldırılmış/accepted sayılmamıştır.

**Güvenli yerel devam koşulları:** Güvenilen ve incelenmiş checkout; kısa sabit source/ignore kalıpları; untrusted fork/PR veya saldırgan kontrollü glob yok; loopback ve tek demo ID; cloud credentials/token yok; mevcut launcher/config/guard değişmeden kullanım; dış ağdan emulator erişimi/port forwarding yok; Pub/Sub ve Baggage propagator eklenmemesi; UUID'nin doğrudan yeni consumer'ının eklenmemesi; npm ci ve ayrı full/runtime audit'in sürmesi. Aynı makinedeki güvenilmeyen süreçlere karşı emülatörler güvenlik sınırı değildir; bu durumda ayrı izolasyon gerekir.

**Geçersiz kılan değişiklikler:** Dependency/lockfile veya Node major değişimi; farklı checkout path/glob/config; yeni emülatör/telemetry/multipart consumer; auth credentials ile CLI; gerçek proje; açık network bind; CI/fork kodu; yeni advisory erişilebilirlik kanıtı. Böyle bir durumda istisna yeniden değerlendirilmeden uygulanamaz.

**Süre:** Onay verilirse en fazla 14 gün geçerli önerilir; haftalık ve her dependency/config değişikliğinde yeniden kontrol. İlk recheck en geç **23 Ekim 2026**, yeniden onay/sonlandırma en geç **30 Ekim 2026**. Bu belge otomatik hatırlatıcı/automation kurmaz. Onaylayan insan/proje sahibi, tarih, kapsam ve sona erme kararı ayrıca kaydedilmelidir.

**A — LOCAL/EMULATOR önerisi:** Mevcut dar akışta adversarial input yolu doğrulanmadığı ve tüm kontroller geçtiği için koşullu yerel devam makuldür. Ancak kullanıcı/proje sahibinin üç advisory'yi, kapsamı ve 14 günlük süreyi açıkça kabul etmesi gerekir. S1 testlerini çalıştırma yetkisi gelecekteki riski kabul yetkisi sayılmadı.

**B — CI / untrusted ortam önerisi:** Yerel istisna CI'ye taşınmaz. Güvenilir branch CI bile ayrı review gerektirir. Fork/untrusted PR çalıştırma için mevcut değerlendirme **BLOCK**: secretsiz ephemeral runner, kısıtlı egress, kaynak/time limitleri, güven sınırından ayrılmış job ve ayrı onay sağlanmadan güvenli kabul edilmez. CI testi yapılmadı; sadece loopback binding untrusted kodu güvenli yapmaz.

**C — Production deployment önerisi:** **BLOCK / yetkisiz.** Runtime audit'in temizliği deployment tooling açığını, production IAM/App Check/authorization veya mevcut emulator-only entrypoint'i onaylamaz. Ayrı release-toolchain ve production mimari güvenlik kapısı gerekir; localHealth bu haliyle production'a yüklenmez.

### 18.9 S1 incelemesi sonundaki tarihsel karar ve değişiklik sınırı

**BACKEND-01 security gate: HOLD. BACKEND-02 readiness: HOLD.** Foundation ve yeniden üretilebilirlik PASS; full audit FAIL ve insan risk kabulü bekleniyor. Dar LOCAL devam için onay taslağı §18.8'de hazırdır; kullanıcı adına kabul edilmedi.

S1 sırasında yalnız bu rapora güvenlik follow-up bölümü eklendi. Önceden uncommitted olan .gitignore/backend/API dosyaları korunmuştur; kaynak, manifest, lockfile, README, guard veya API kontratı değiştirilmedi. npm ci/build/emulator yalnız ignore edilmiş yerel kurulum/çıktı/log/config dosyalarını yeniden üretmiştir. Üretim Firebase'e bağlanılmadı; credentials, gerçek oyuncu kayıtları veya cloud kaynakları kullanılmadı. Commit/push/merge/deploy ve BACKEND-02 başlangıcı yoktur.

## 19 — BACKEND-01 Risk Approval Record

**Onaylayan:** Proje sahibi; bu sohbet üzerinden verilen açık insan onayı.

**Onay tarihi:** 16 Ekim 2026 (Europe/Istanbul).

**Geçerlilik:** 16–30 Ekim 2026. **İlk güvenlik yeniden kontrolü:** 23 Ekim 2026. Bu kayıt yeniden kontrolün yapıldığı anlamına gelmez ve otomatik hatırlatıcı oluşturmaz.

Proje sahibi, BACKEND-01-S1 §18.8'de tanımlanan dar kapsamlı güvenlik risk istisnasını aşağıdaki şartlarla açıkça onaylamıştır:

- Yalnız güvenilir yerel geliştirme bilgisayarında LOCAL/EMULATOR kullanımı.
- Mevcut sabit bağımlılıklar, lockfile ve yalnız `demo-logisticore-local` proje kimliği.
- Rapordaki güvenlik ve izolasyon koşullarının korunması.
- Kalan üç advisory'nin güvenlik taramalarında görünür kalması: GHSA-vfj7-8cjw-p6xm, GHSA-8988-4f7v-96qf ve GHSA-w5hq-g745-h8pq. Bulgular gizlenmez veya bastırılmaz.
- CI, güvenilmeyen kod, gerçek Firebase DEV/PROD, deployment ve migration kapsam dışıdır.
- Bağımlılık veya güven sınırı değişirse onay yeniden değerlendirilir; §18.8'deki geçersiz kılma koşulları korunur.

### 19.1 Onay sonrası geçerli kararlar

| Kapı | Karar |
|---|---|
| BACKEND-01 FUNCTIONAL GATE | **PASS** |
| BACKEND-01 LOCAL SECURITY GATE | **CONDITIONAL PASS** |
| GENERAL SECURITY AUDIT | **HOLD — unresolved advisories** |
| PRODUCTION DEPLOYMENT | **BLOCK** |
| BACKEND-02 LOCAL DEVELOPMENT | **ELIGIBLE, subject to separate phase review** |

Bu onay açıkların düzeltildiği, tam güvenlik taramasının temiz olduğu veya production'ın güvenli olduğu şeklinde yorumlanamaz. Yalnız belirtilen yerel kullanım için kalan riskin süreli ve koşullu kabulüdür. BACKEND-02 uygunluğu ayrı faz incelemesine tabidir; bu kayıt BACKEND-02'yi başlatmaz veya uygulama yetkisi vermez.

### 19.2 Bu kayıt sırasında yapılan işlem

Yalnız `docs/backend/BACKEND-01_REPORT.md` güncellendi. Önceki test sonuçları korunmuştur; yeni test, kurulum veya güvenlik taraması yapılmamıştır. Başka dosya değiştirilmedi; commit, push, merge veya deploy yapılmadı.
