# LogistiCore — yerel backend temeli

BACKEND-01 yalnız yerel geliştirmedir. Unity dosyalarına bağımlı değildir. Oyun komutu, veri geçişi veya production dağıtımı içermez. `CLIENT SENDS INTENT. BACKEND VALIDATES AND DECIDES.`

## Gereksinimler ve bağımlılıklar

- Node.js **22.x**, npm 10.x; global Firebase CLI gerekmez.
- Java **21 veya üstü**, `java` PATH üzerinde olmalı. Makine araçları otomatik kurulmaz.
- İlk `npm ci` için npm registry; ilk Firestore emulator çalışması için resmi emulator indirme sunucusuna erişim.
- Auth 9099, Firestore 8080/9150, Functions 5001, hub 4400, logging 4500 portları boş olmalı. UI kapalıdır.

Node 22, [Functions runtime belgesinde](https://firebase.google.com/docs/functions/manage-functions) desteklenir. Paketler npm engine/peer metadata kontrolüyle seçildi: Functions 7.4.0, Admin 14.5.0, CLI 15.34.0, Rules test SDK 5.0.2, Firebase test istemcisi 12.19.0, TypeScript 5.9.3. Rules test SDK 6.x Node 24 gerektirdiğinden seçilmedi. Exact manifest sürümleri ve tek kök workspace `package-lock.json` geçişli bağımlılıkları sabitler. Functions altında ikinci lockfile oluşturmayın.

## Yapı

```text
backend/
  package.json / package-lock.json    tooling + npm workspace
  firebase.json                      yalnız demo emulator config
  firestore.rules / firestore.indexes.json
  functions/
    package.json / tsconfig.json
    src/index.ts                     yerel, auth zorunlu localHealth
    src/localEnvironment.ts          fail-closed kontroller
    lib/                             üretilmiş, Git dışında
  scripts/emulators.mjs              kontrollü CLI başlatıcısı
  tests/unit.test.mjs                guard ve entrypoint testleri
  tests/emulators.test.mjs           Rules + Auth + Callable integration
  .local/                            özel CLI config ve binary cache, Git dışında
```

Test çerçevesi Node'un yerleşik `node:test` modülüdür. Ayrı lint framework'ü yerine strict TypeScript, unused kontrolleri ve testler kullanılır. `src` yalnız TS, test/başlatıcılar küçük ESM JavaScript dosyalarıdır. Build çıktısı `functions/lib/` dışına çıkmaz.

## Windows PowerShell

Tüm komutlar açıkça backend kökünde çalışır:

```powershell
Set-Location 'D:\Projects\unitylogisticore\backend'
node --version
npm --version
java -version
npm ci --ignore-scripts --no-fund --no-audit
npm run check
npm run test:emulators
```

`check`: build + typecheck + unit. `test:emulators`: build + port/config/environment guard + CLI `emulators:exec` + integration testleri + emulator kapatma. Tam yerel kontrol `npm run validate` ile yapılır. `npm run test:unit` mevcut derleme çıktısını kullanır; kaynak değişikliğinden sonra `npm run check` tercih edin. Başlatıcı build'i otomatik yapar.

Elle geliştirme:

```powershell
Set-Location 'D:\Projects\unitylogisticore\backend'
npm run emulators
```

Aynı terminalde **Ctrl+C** ile kapatın; CLI emülatörleri sonlandırır. `emulators:exec` testler bittiğinde kendisi kapatır. Başlatıcı dolu portta durur; ilgisiz süreçleri öldürmez. Açık elle çalışan suite varken ikinci `test:emulators` çalıştırmayın; önce kendi suite'inizi kapatın. Java veya Node yoksa runtime testleri BLOCKED olur; derleme tek başına BACKEND-01 PASS sayılmaz.

CLI yerel `node_modules/firebase-tools/lib/bin/firebase.js` dosyasından, açık `--project demo-logisticore-local --config <backend/firebase.json> --only auth,firestore,functions --non-interactive` ile çalışır. Başlatıcı serbest argüman almaz; proje değiştirme veya deploy script'i yoktur. `.firebaserc` gerekmez. `firebase login`, gerçek proje seçimi ve deploy bu fazın parçası değildir.

## İzolasyon ve güvenlik

Tek izinli ID **demo-logisticore-local**. Auth/Firestore/Functions adresleri yalnız `127.0.0.1` ve sabit portlardır. Başlatıcı mevcut proje/endpoint override'larını doğrular; farklı veya eksik runtime config fail-closed sonuç verir. Credentials/token environment değişkeni mevcutsa değerini göstermeden reddeder. Farklı bir çalışma için ayarlanmışsa o işi etkilemeden ayrı temiz terminal kullanın.

CLI child environment allowlist ile kurulur; kullanıcı Firebase login ayarları için ayrı `.local/config` XDG config dizini kullanılır. ADC credential yolu aktarılmaz. Admin SDK uygulaması guard sonrasında açık emulator-only credential ile oluşturulur; ADC keşfi kullanılmaz. `localHealth` dosyası yanlış ortamda export/SDK initialization öncesinde hata verir ve her istekte ortamı tekrar doğrular. Bu entrypoint production için hazır değildir; daha sonraki production backend tasarımı yerel sağlık endpoint'ini kaldırmalı veya ayrı tutmalıdır.

Her başlatmada `.local/config/run-*` altında yeni XDG/APPDATA dizini seçilir; önceki CLI login dosyaları tekrar kullanılmaz. Metadata server keşfi `METADATA_SERVER_DETECTION=none` ile kapalıdır. Functions discovery alt süreci özel Functions client endpoint değişkenini taşımadığından runtime guard Auth/Firestore adreslerini ve `FUNCTIONS_EMULATOR=true` değerini zorunlu tutar; test runner üç endpoint'i de zorunlu tutar. Emulator token doğrulaması production kriptografik doğrulaması değildir; handler ayrıca boş/eksik trusted UID'yi reddeder.

Demo projeler yerel test amacı taşır; [Firebase emulator rehberi](https://firebase.google.com/docs/emulator-suite/connect_functions) proje ID tutarlılığını açıklar. Koruma mekanizmaları desteklenen kod yolunu güvenli kılar; geliştiricinin repo kodunu değiştirip doğrudan CLI çalıştırmasını engelleyen bir işletim sistemi sandbox'ı değildir. Yeni servis SDK'sı eklenirse endpoint ve egress sınırı yeniden incelenmelidir.

Rules tüm belge get/list/create/update/delete işlemlerini hem anonymous hem authenticated istemci için reddeder. Index listesi boştur; TTL veya varsayımsal index yoktur. **Admin SDK Rules'ı aşar**; ileride her komutta trusted Auth UID + ayrı company ownership + domain doğrulaması gerekir. App Check şu anda uygulanmadı; production Auth/authorization yerine geçmez.

Health fonksiyonu veri okumaz/yazmaz, oyuncu kimliğini cevapta döndürmez. Integration testi yalnız Auth Emulator'da geçici anonim sentetik kimlik üretir ve sonunda siler; gerçek provider credential kullanılmaz. Rules testleri yalnız reddedilmesi beklenen sentetik erişimler yapar; kanonik gameplay koleksiyonları seed edilmez. Emulator verisi export edilmez.

## Bağımsız çalışma ve sonraki sınır

Backend geliştiricisi yalnız `backend/` ve ilgili belgelerde çalışır; Unity geliştiricisi API sözleşmesindeki sentetik örnekleri kullanabilir. `Assets/`, `Packages/`, `ProjectSettings/`, mevcut `ARCHITECTURE.md` ve BACKEND-00 değişmez. Bütün ortam/test/derleme dosyaları backend altında kalır.

Auth kimliği trusted request context'ten alınır; payload UID'si kabul edilmez. Company authorization ve anonymous linking BACKEND-02'ye, command idempotency BACKEND-03'e aittir. Başlangıç schema veya financial ledger henüz uygulanmaz. `money/cash` ve `level/companyLevel` eşdeğerliği UNKNOWN; mapping yapılmaz. Her domain mutation'da merkezi company stateVersion yazılması zorunlu değildir; domain bazlı version seçimi gelecekte transaction sınırları ve ölçümle kararlaştırılacak.

Geçici API sözleşmesi: [FOUNDATION_CONTRACT](../docs/api-contracts/FOUNDATION_CONTRACT.md). Gerçek test sonuçları: [BACKEND-01_REPORT](../docs/backend/BACKEND-01_REPORT.md). BACKEND-02 otomatik başlamaz.

## Bilinen bağımlılık sınırlaması

16 Ekim 2026 kontrolünde `npm audit --omit=dev --audit-level=high --json` sıfır bulgu döndürdü. Tam audit, Firebase CLI'nin development bağımlılıklarında 3 high + 4 moderate paket kaydı döndürüyor (üç temel advisory'nin bağımlılık zincirine yayılımı). Bunlar temiz sonuç olarak raporlanmadı; ayrıntılar BACKEND-01 raporunda. Untrusted repo/glob/URL girdileri bu yerel CLI'ye verilmemeli. Otomatik `npm audit fix --force` çalıştırmayın.

Firebase test istemcisinin eski gRPC sürümü için yalnız `@firebase/firestore → @grpc/grpc-js` yolu 1.13.6'ya override edildi; aynı major içindeki güvenlik düzeltmesi Rules emulator testinden geçti. Bu override upstream düzeltildiğinde kaldırılmak üzere belgelenmiştir. CLI'nin kalan bulguları için sürüm düzeltmesi veya kapsamı belgelenmiş risk kabulü gerekir; işlevsel test başarısı genel güvenlik onayı değildir.

## BACKEND-02 — yerel identity eklemesi

`ensurePlayerIdentity` ve sunucu içi `requireCompanyOwner` uygulanmıştır; ayrıntılı sözleşme [IDENTITY_CONTRACT](../docs/api-contracts/IDENTITY_CONTRACT.md), güncel doğrulamalar [BACKEND-02_REPORT](../docs/backend/BACKEND-02_REPORT.md). Yukarıdaki BACKEND-01 kapsam açıklamaları tarihsel başlangıç durumudur. Komutlar, dependency sürümleri, lockfile ve launcher aynıdır.

`src/identity.ts` trusted UID/payload doğrulaması, transaction ile minimal identity ve ownership kontrolünü içerir. Testler yalnız geçici sentetik kimlik/company/legacy fixtures kullanır; anonymous→email linking, ikinci oturum ve collision SDK üzerinden sınanır. Her test yalnız kendi kayıtlarını temizler; genel reset/export yoktur.

Firestore, Admin SDK'nin export ettiği `Firestore` constructor'ından guard sonrasında sabit demo proje/loopback host ve `ssl:false` ile oluşturulur. Kurulu SDK'nin emulator transport'u `Bearer owner` kullanır; ADC veya gerçek sertifika gerekmez. `getFirestore(app)` mevcut özel health credential nesnesini kabul etmediği için kullanılmaz. Guard ve sabit endpoint olmadan bu istemci başka ortama taşınmamalıdır.

Firestore tip zincirindeki CommonJS/ESM uyumu için `module` ve `moduleResolution` NodeNext oldu; strict ve skipLibCheck=false korunur. Doğrulanan ortam Node 22.23.2, TypeScript 5.9.3; eski Node 22 minor sürümleri ayrıca doğrulanmadı. Yeni dependency, Pub/Sub, tracing, glob, telemetry veya remote emulator erişimi eklenmedi. Üç advisory ve BACKEND-01 §19 süreli yerel risk koşulları geçerliliğini korur; production/CI onayı değildir.

## BACKEND-03 — internal command execution

`src/commands.ts` trusted Auth UID, strict protocol/envelope, bounded canonical JSON/SHA-256 fingerprint ve atomik domain-effect/receipt yürütücüsünü içerir. Yeni callable export edilmez; test-only synthetic operasyon yalnız integration test dosyasındadır. Receipt `v2Players/{uid}/commands/{key}` altında kalıcıdır, TTL yoktur. Command replay result ve ilk commit zamanını korur; identity callable davranışı değişmez.

Mevcut `npm run validate` komutuyla bütün eski ve yeni testler çalışır; manifest, dependency, lockfile ve launcher değişmez. Contract: [COMMAND_CONTRACT](../docs/api-contracts/COMMAND_CONTRACT.md); sonuçlar: [BACKEND-03_REPORT](../docs/backend/BACKEND-03_REPORT.md). BACKEND-04/05 uygulanmadı. Mevcut local-only güvenlik istisnası CI/deployment izni değildir.

## BACKEND-04 — company foundation

`createCompany` ve `getMyCompanyState` yalnız mevcut demo emulator ortamında `us-central1` callable'larıdır. İlk API BACKEND-03 executor ile deterministic UID-derived şirket, player mapping ve receipt'i atomik yazar; ikinci API read-only snapshot döndürür. Kimlik invariant'ı ortak helper'a ayrıldı; eski callable davranışı korunur. Lifecycle `foundation_pending_economy`: finans/progression/inventory oluşturulmaz.

Legacy/migration işaretleri ve tutarsız mapping fail-closed; aynı key replay sırasında da güncel eligibility doğrulanır. Contract: [COMPANY_CONTRACT](../docs/api-contracts/COMPANY_CONTRACT.md); kanıt: [BACKEND-04_REPORT](../docs/backend/BACKEND-04_REPORT.md). Aynı `npm run validate`, sabit bağımlılıklar/lockfile/launcher kullanılır. BACKEND-05 başlamaz; production hâlâ BLOCK.

## BACKEND-05 — local authoritative economy

`economyModel.ts` versioned synthetic config, exact bounded integer money ve XP/level doğrulamasını; `economy.ts` internal atomic initialization/entitlement/ledger/claim yürütmesini ekler. Tek yeni callable read-only `getMyEconomyState`; para/XP grant veya init callable yoktur. Gerçek ürün değerleri ve production initialization kapalıdır. [ECONOMY_CONTRACT](../docs/api-contracts/ECONOMY_CONTRACT.md), [BACKEND-05_REPORT](../docs/backend/BACKEND-05_REPORT.md). Yukarıdaki faz kapsamları tarihsel kayıttır.

Aynı `npm run validate` ile tüm testler çalışır. Bağımlılıklar/lockfile/launcher/Rules değişmez; 16–30 Ekim local-only risk istisnası ve 23 Ekim yeniden kontrol koşulu korunur.

## BACKEND-08A — local fleet ve warehouse foundation

`getMyFleetState` ve `getMyWarehouseState` authenticated, salt okunur, document-ID cursor ile en fazla 20 kayıt döndüren local callable'lardır. `starterFleetReady` ekonomiden ayrı doğrulanır. Internal starter provisioning tek truck + tek driver ve permanent marker/receipt'i atomik oluşturur; farklı key veya catalog version ikinci paketi veremez. Public grant/purchase/assignment endpoint yoktur; warehouse/trailer otomatik verilmez.

`fleet.ts` transaction-bound reserve/release, `warehouses.ts` yalnız sentetik warehouse creation, `logisticsModel.ts` strict katalog/asset/capacity/pagination validators içerir. Gerçek katalog/ekonomi/rollout kapalıdır; LGC ve başlangıç bakiyesi önerileri default yapılmadı. Mevcut `npm run validate`, sabit dependency/lockfile/guard/Rules/launcher korunur. Sözleşmeler: [FLEET_CONTRACT](../docs/api-contracts/FLEET_CONTRACT.md), [WAREHOUSE_CONTRACT](../docs/api-contracts/WAREHOUSE_CONTRACT.md); kanıt: [BACKEND-08A_REPORT](../docs/backend/BACKEND-08A_REPORT.md). Genel security HOLD, production BLOCK; 23 Ekim reassessment gereklidir.

## BACKEND-06 — local contract foundation

Internal epoch-bound generator en çok 5 deterministik company offer'ı marker/receipt ile atomik oluşturur. Public generator/refresh yoktur. `acceptContract` strict contractId intent ile ownership/config/eligibility/server expiry/quota doğrular; wallet/XP veya fleet reservation değişmez. `getMyContracts` read-only, max20 document-ID pagination; status filter henüz yoktur. Logistics catalog v2 capabilities destekler, v1 davranışı korunur. Gerçek route/economy/katalog aktivasyonu kapalıdır. [CONTRACT_CONTRACT](../docs/api-contracts/CONTRACT_CONTRACT.md), [BACKEND-06_REPORT](../docs/backend/BACKEND-06_REPORT.md). Aynı npm run validate ve sabit dependencies; BACKEND-07 başlatılmaz.
