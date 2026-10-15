# LOGISTICORE — BACKEND-00
## Yeni Backend Mimarisi ve Üretim Verisi Uyumluluk Denetimi

Tarih: 16 Ekim 2026 (Europe/Istanbul). Kapsam: salt okuma depo denetimi ve mimari belgelendirme. Uygulama, kurulum, dağıtım ve veri geçişi yapılmamıştır.

**Sonuç: BACKEND-00 belgesi incelemeye hazır; BACKEND-01: HOLD.** Ayrı geliştirme ortamının kurulması için kapsam ve kaynak oluşturma onayı gerekir. Eski React Native kaynaklarının bu depoda bulunmaması engel değildir. Üretime geçiş ise veri anlamları, hesap sürekliliği ve geri dönüş kanıtları tamamlanana kadar BLOCK durumundadır.

### Kanıt sözlüğü

- **VERIFIED:** Bu oturumda yerel dosya veya salt okuma Git çıktısı ile doğrulandı.
- **OBSERVED:** Kullanıcının aktardığı dış gözlem veya ürün referansı; üretim ortamında bağımsız doğrulanmadı.
- **INFERRED:** Kanıttan çıkarılan, doğrulanması gereken sonuç.
- **PROPOSED:** Yeni sistem için öneri; uygulanmış özellik değildir.
- **UNKNOWN:** Yeterli kanıt yoktur.

Tablo başlığındaki veya paragraf başındaki etiket, aksi belirtilmedikçe tüm satırlara uygulanır. Firebase platform belgeleri teknik tasarımın dayanağıdır; üretim ayarlarının kanıtı değildir. Dosya referansları depo köküne göredir. Gelecek dosya yolları açıkça öneri olarak belirtilmiştir.

## 01 — Yönetici özeti

**VERIFIED:** Depo Unity istemcisinin başlangıç temelini içeriyor. `ARCHITECTURE.md:3` kapsamı UNITY-00 olarak tanımlar; `ARCHITECTURE.md:7–11` istemciyi sunum/komut gönderme, backend'i kanonik oyun durumu sahibi olarak ayırır ve mevcut aşamada Firebase/backend arayüzü bulunmadığını açıklar. C# çalışma zamanı başlangıcı yalnızca kare hızı ve log ayarını çağırır (`Assets/Game/Core/RuntimeFoundation.cs:8–11`).

**PROPOSED:** Aynı depoda, `Assets/` dışında, TypeScript ile modüler bir Cloud Functions backend'i kurulmalı. Firebase Authentication kimliği korumalı; Firestore kanonik durumu tutmalı; sunucu komutları sahiplik, ekonomi, zaman ve durum geçişlerini doğrulamalı. İlk sürümde ayrı REST gateway, özel Cloud Run servisi ve sürekli dünya simülasyonu gerekli değildir. Runtime/SDK sürümleri BACKEND-01'de destek matrisiyle sabitlenecek; burada sürüm veya bağımlılık kurulmamıştır.

**OBSERVED:** Kullanıcı mevcut üretimde kayıtlı hesaplar ve oyun verileri bulunduğunu bildiriyor. Aktarılan tek örnekte `money=10780`, `cash=20000`, `level=3`, `companyLevel=1`. **UNKNOWN:** Bunların farklı kavramlar mı, eski projeksiyonlar mı, farklı tarihli kayıtlar mı olduğu bilinmiyor. Büyük/küçük olanı, daha yeni görüneni veya `serverState` adını gerekçe göstererek seçmek veri kaybına yol açabilir.

**PROPOSED:** Öncelikli yol mevcut üretim Firebase projesini ve UID'leri koruyup yeni sürümlü ad alanına kontrollü geçiştir. Bu bir üretim onayı değildir. Yeni backend tasarımı eski şemayı kopyalamaz; geçiş adaptörü yalnızca doğrulanmış alan eşlemelerini kullanır. İlk geliştirme sentetik verili ayrı DEV ve Emulator Suite üzerinde ilerleyebilir.

## 02 — Doğrulanmış depo durumu ve inceleme sınırları

| Denetim | Sonuç / kanıt |
|---|---|
| Kök | **VERIFIED:** `D:/Projects/unitylogisticore`, `git rev-parse --show-toplevel` |
| Aktif dal | **VERIFIED:** `feature/backend-00`, `git branch --show-current` |
| Başlangıç çalışma ağacı | **VERIFIED:** `git status --short` boş |
| İncelenen başlangıç commit'i | **VERIFIED:** `9403b0d`; sonraki dal kontrolünde aynı çalışma ağacı temizdi |
| Depo kimliği | **OBSERVED:** Kullanıcı `hansaglam/unitylogisticore` belirtti; GitHub'a bağlanılmadı, uzaktaki repo/dal eşitliği **UNKNOWN** |
| Yerel yapı | **VERIFIED:** `Assets/`, `Packages/`, `ProjectSettings/`, `ARCHITECTURE.md`, `.gitignore` |
| Dosya envanteri | **VERIFIED:** Rapor öncesi `.git` dışındaki yerel taramada 179 dosya; 16 C# kaynak dosyası |
| Unity sürümü | **VERIFIED:** `6000.3.23f1`, `ProjectSettings/ProjectVersion.txt:1` |
| Manifest | **VERIFIED:** Localization `1.5.13`, URP `17.3.0`, Test Framework `1.6.0`, Input System `1.20.0`; `Packages/manifest.json:14–18` |
| Backend paket/runtime | **NOT FOUND** bu çalışma ağacında; mevcut üretim Node/runtime/Functions nesli **UNKNOWN** |
| Firebase yapılandırması | **NOT FOUND:** Yerel Firebase/Firestore dosyası, Functions kaynağı, Node `package.json`, rules/index tanımı veya emulator yapılandırması |
| Gizli dosya göstergeleri | **VERIFIED:** Dosya adı taramasında `.env`, credential/service-account, `.pem`/`.key` eşleşmesi bulunmadı; bu tam gizli bilgi taraması veya geçmiş denetimi değildir |
| Repo yönergeleri | **NOT FOUND:** Depo ve kontrol edilen üst dizinlerde `AGENTS.md` |

**VERIFIED — incelenen kod ve belgeler:** `ARCHITECTURE.md`, manifest, lock dosyasındaki ilgili paket referansları, Unity sürüm dosyası, `.gitignore`, runtime asmdef, Core sınıfları, LocaleService/LocaleCodes/LocalizedFormatting/LocalizationContent, ekran ve safe-area sınıfları, üç test kaynak dosyası ve test çalıştırıcı okundu. `FoundationSetup` ve `FoundationVerificationView` üzerinde fonksiyon/çağrı taraması yapıldı; tüm Unity asset içerikleri, shader'lar veya sahne YAML'ları kapsamlı incelenmedi.

| Alt sistem | Durum | Somut kanıt / sınır |
|---|---|---|
| Yerelleştirme ve biçimleme | IMPLEMENTED | `LocaleService.TrySelect`, `LocaleCodes.NormalizeSelection`, `LocalizedFormatting.Currency`; kaynak mevcut, bu denetimde çalıştırılmadı |
| Mobil kare hızı / ekran düzeni | IMPLEMENTED | `MobileFrameRate.Apply`, `ScreenLayouts.Apply`, `SafeAreaLayout.CalculateAnchors` |
| Doğrulama ekranı | PROTOTYPE ONLY | `FoundationVerificationView.cs:66–67` teslimat sayıları 1 ve 5 ile metin gösteriyor; teslimat motoru değildir |
| Yerel testler | IMPLEMENTED | `FoundationLocalizationTests`, `LocalizedFormattingTests`, `LocaleSelectionTests`, `SafeAreaLayoutTests`; geçme durumu UNKNOWN |
| Backend otoritesi | DOCUMENTED ONLY | `ARCHITECTURE.md:7–11` |
| Auth, Firestore okuma/yazma, şirket, finans, ilerleme | NOT FOUND | İncelenen repo envanteri ve kaynak taraması |
| Araç/sürücü/römork, depo, kontrat, teslimat, piyasa | NOT FOUND | Aynı tarama; metin anahtarları uygulama kanıtı değildir |
| LiveOps, ekonomi worker, scheduled job, migration | NOT FOUND | Yerel kaynak/config/script bulunmadı; üretimde bulunmadıkları sonucu çıkarılamaz |
| ROAD/RAIL/SEA/AIR veri ve oyun kuralları | NOT FOUND | Kullanıcının referansları dışındaki kanonik veri seti yok |

**VERIFIED:** Unity/test çalıştırılmadı. `FoundationLocalizationTests.cs:18,49,72` setup çağrıları içerir; `FoundationSetup.cs:103,125` asset kaydeder. Salt okuma sınırında bu testleri çalıştırmak uygun değildir. Firebase CLI, npm, deploy, migration ve üretim çağrısı yapılmadı. İlk Git komutu sahiplik kontrolünde reddedildi; sonraki salt okuma komutları yalnızca süreç kapsamlı `-c safe.directory=D:/Projects/unitylogisticore` ile çalıştırıldı; Git config dosyası değiştirilmedi.

## 03 — Hedef mimari ve modül sınırları

**PROPOSED:** Bir backend kod tabanı, bir Functions dağıtım grubu, küçük ve açık modüller. Ayrı dağıtım sınırı ancak ayrı IAM veya ölçülmüş ölçek ihtiyacı doğurursa açılmalı. Kullanıcıya açık her komut tek bir iş kuralı uygulamasına ulaşmalı; Unity'deki tahmini fiyat/uygunluk kontrolleri sadece UX olmalı.

```text
Unity sunum + cache + intent
          | Auth kimliği / Callable / App Check
          v
Komut doğrulama → sahiplik → domain kuralı → Firestore transaction
                                                    |
                               kanonik durum + makbuz + finans kaydı
Unity <---- yapılandırılmış sonuç / izinli okumalar --+

Yetkili operasyoncu → ayrı admin yetkisi → sürümlü config + audit
Ürün analitiği <---- türetilmiş olaylar; oyun otoritesi değil
```

| Modül | Sorumluluk ve kanonik veri | Çağıran / bağımlılık | Atomiklik ve güvenlik |
|---|---|---|---|
| Authentication | UID ve sağlayıcı kimliği Firebase Auth'ta | İstemci SDK; command auth context | İstemci payload UID'sine güvenme; linking sahiplik kanıtlı |
| Player/Company | UID–şirket bağlama, şirket durumu | Auth, command | Şirket+üyelik+başlangıç durumu birlikte; varsayılan tek şirket politikası henüz PROPOSED |
| Finance | Tek bakiye, para hareketi kayıtları | Satın alma, ödül, market | Bakiye ve ekonomik etki aynı transaction; negatif/overflow reddi |
| Progression | XP, seviye, itibar, unlock | Ödül ve config | Tek sürümlü formül; ödül ile birlikte ilerleme güncelle |
| Fleet/Assets | Araç, sürücü, römork ve rezervasyon | Satın alma, teslimat | Owner doğrulama; aynı asset iki aktif işe atanamaz |
| Warehouses | Şehirde tesis sahipliği/seviyesi | Finance, Progression, config | Benzersizlik, seviye sırası, fiyat ve ödeme atomik |
| Contracts | Teklif üretimi, kabul, ödül koşulları | Progression, fleet, economy/config | Teklif tüketimi atomik; immutable kabul koşulları |
| Deliveries | Başlama/bitiş zamanı, yük, sonuç | Contract, fleet, finance | Rezervasyon, tamamlanma, reward claim tutarlı |
| Market | Teklif/alışveriş ve varlık devri | Finance, fleet, snapshot | Fiyatı sunucu seçer; P2P iki taraf ve listing tek transaction |
| World economy | Config ve gelecekte snapshot | Admin; gelecekte scheduler | İstemci yazamaz; immutable epoch yayınla |
| LiveOps | Etkinlik sürümü ve yaşam döngüsü | Admin, economy, contract | Sunucu zamanı, bounded modifiers, uyumlu içerik |
| Administration | Yayın, bakım, erişim kontrolü | Operasyon IAM/ayrı admin endpoint | Oyuncu yolu admin rolü veremez; audit zorunlu |
| Analytics | Türetilmiş ürün olayları | Başarılı komut sonrası | Kanonik etkisi yok; tekrar teslimata tolerans |
| Command handling | Protokol, şema, idempotency, hata | Callable | Auth+ownership+rate limit; tek transaction koordinasyonu |
| Persistence | Firestore erişimi ve transaction sınırı | Domain komutları | Küçük doğrudan yardımcılar; genel repository/factory/DI çatısı yok |

**PROPOSED:** Cloud Functions callable ilk API olsun. Unity desteği ve token taşıma davranışı resmi [Callable belgesinde](https://firebase.google.com/docs/functions/callable?hl=en) yer alır. Token taşınması, iş yetkisi kontrolünü ortadan kaldırmaz. HTTPS/REST ancak harici ortak veya uyumsuz istemci gereksinimi oluşursa değerlendirilir. Gen 2 Functions adaydır; seçilecek Unity SDK, bölge ve runtime uyumu DEV'de test edilmelidir.

**PROPOSED:** Cloud Run, ağır/batch ekonomi işinin Functions sınırlarına veya maliyet/işletim hedeflerine uymadığı ölçülünce; Scheduler periyodik epoch/cleanup ihtiyacı oluşunca; Tasks ise kesin zamanda işlem başlatmanın ürün açısından zorunlu olduğu ölçülünce eklenmeli. Remote Config sunum ve istemci bayrakları için kullanılabilir; para/ödül yetkisi vermez.

## 04 — Depo yapısı ve yeniden kullanım

**PROPOSED:** Kullanıcının önerdiği yapı mevcut Unity köküyle uyumludur. Backend `Assets/` altında olmayacağı için Unity asset import akışına dahil edilmez. Şimdi yalnız bu belge oluşturulur; aşağıdaki yapı gelecektir:

```text
Assets/                   Unity geliştiricisi
Packages/                 Unity bağımlılıkları
ProjectSettings/          Unity ayarları
backend/
  functions/              package.json, src/, test/ (gelecek)
  firestore.rules         gelecek
  firestore.indexes.json  gelecek
  firebase.json           gelecek; functions kaynağı buna göre göreli
docs/
  backend/                mimari / geçiş kararları
  api-contracts/          gelecek JSON schema ve örnekler
ARCHITECTURE.md            mevcut, bu aşamada değişmedi
.gitignore                mevcut, bu aşamada değişmedi
```

**PROPOSED:** Şimdilik monorepo araç zinciri, paylaşımlı runtime paketi, admin uygulaması ve ayrı microservice klasörleri gerekmez. Backend komutları açık çalışma dizini `backend/` ve açık ortam seçimiyle çalıştırılmalı; kökten yanlış config bulmaya güvenilmemeli. Testler `functions/test/` altında tutulabilir; her domain için katman klasörü üretmek zorunlu değildir.

**VERIFIED:** Mevcut `.gitignore` Unity/IDE çıktıları için düzenlenmiş; backend bağımlılıkları ve ortam dosyaları için özel kurallar yok. **PROPOSED:** BACKEND-01'de `backend/**/node_modules/`, derleme çıktıları, coverage, emulator verisi/logları, `.env*` (yalnız secretsiz `.env.example` istisnası), credential/key dosyaları ve yerel ortam override'ları hariç tutulmalı. Lock dosyası, rules, indexes ve secretsiz config sürümlenmeli. `.gitignore` tek başına sır koruması değildir; PR ve secret scanning gerekir. Hiçbir ignore değişikliği burada yapılmadı.

| Bileşen | Karar | Gerekçe |
|---|---|---|
| Unity locale/format/safe-area temeli | PROPOSED: REUSE AS IS | Doğru istemci sorumluluğu; backend'e taşınmaz; kaynak kanıtları §02 |
| `GameLog.Error` | PROPOSED: REUSE WITH REFACTORING, ihtiyaçta | `GameLog.cs:19–29` exception mesajını yazar; gelecekte ağ hataları eklenirken hassas veri redaksiyonu gerekir; mevcut sızıntı kanıtı yok |
| Eski backend modülleri | UNKNOWN | Bu repo bilinçli olarak içermez; mimari bunlara bağımlı tasarlanmaz |
| Yeni domain backend | PROPOSED: yeni uygulama | Kullanıcının yeni backend hedefi; eski mimariyi yeniden üretme gereği yok |
| Auth kimlikleri / onaylı gameplay içerikleri | PROPOSED: koru | Platform değişikliği kullanıcı reseti gerekçesi değildir |
| Eski save/state uyarlama | PROPOSED: geçiş adaptörü | Yalnız semantik eşleme onayından sonra; legacy kalıcı otorite olarak kopyalanmaz |
| İleri dünya simülasyonu / admin UI | PROPOSED: DEFER | İlk sürüm doğruluğu ve test edilebilirlik öncelikli |

## 05 — Firestore veri modeli

**PROPOSED — geçici model:** Eski koleksiyon adlarıyla çakışmayı azaltmak için `v2Players` ve `v2Companies` gibi sürümlü kökler. `schemaVersion:2` şema neslini gösterir; API `protocolVersion:1` ile karıştırılmaz. Alan isimleri ve tek şirket ilişkisi tasarım önerisidir, mevcut üretim eşlemesi değildir.

**Kanonik finans kaynağı:** `v2Companies/{companyId}/state/finance.balanceMinor`. Aynı ekonomik bakiye başka yerde otorite olamaz. Firestore integer, sunucuda güvenli tamsayı sınırı; wire'da ondalık tamsayı string (`"1078000"` gibi yalnız sentetik örnek). Ölçek `currencyScale` ve `currencyCode` ile config'te açık seçilir. Mevcut `10780` değerinin para birimi veya minor-unit dönüşümü **UNKNOWN**; örnek bir geçiş hesabı değildir. Float ile para hesabı yapılmaz; maksimum değer, yuvarlama ve gerekiyorsa borç politikası ayrıca onaylanır.

Tablo kısaltmaları: `s` string, `i` integer, `b` boolean, `t` Firestore Timestamp, `m` sınırlı map. Wire zaman biçimi UTC ISO-8601. Tüm yollar **PROPOSED**. Varsayılan güvenlik: oyuncu yalnız kendi şirket verisini okuyabilir; tüm kanonik yazılar backend'e ait. Public snapshot'lar ancak yayımlanmış ve hassas veriden ayrılmışsa okunabilir. Liste sorguları limit/cursor içerir.

| Yol | Ana alanlar / tip | Sahip ve erişim kalıbı | Transaction / index | Saklama / hotspot |
|---|---|---|---|---|
| `v2Players/{uid}` | `companyId:s?`, `schemaVersion:i`, `status:s`, `createdAt:t` | Bootstrap modülü; login'de nokta okuma; nadir yazı; yalnız ilgili UID okur | Company bootstrap ile atomik bağ; doc ID lookup yeterli | Hesap ömrü; global hotspot yok; email/token kopyalama yok |
| `v2Companies/{cid}` | `ownerUid:s`, `name:s`, `schemaVersion:i`, `stateVersion:i`, `migrationStatus:s`, `createdAt/updatedAt:t` | Company modülü; açılış ve özet listener; her kanonik komutta version artışı | Etkilenen state ile atomik; UID lookup player bağından; başlangıçta owner sorgusu gerekmez | Hesap ömrü; şirket içi serialization noktası, ölçülmeden shard edilmez |
| `v2Companies/{cid}/state/finance` | `balanceMinor:i`, `currencyCode:s`, `updatedAt:t` | Finance; açılış/ekonomi ekranı okuması; ekonomik komutta yazı | Asset/claim/ledger/receipt ile atomik; alan indexleri gereksiz | Kalıcı; aynı şirkette eşzamanlı harcama contention'ı bilinçli |
| `v2Companies/{cid}/state/progression` | `xp:i`, `level:i`, `reputation:i`, `unlockedCityIds:array<s>`, `rulesVersion:s` | Progression; açılış/ödül; city listesi sınırlı | Reward ile atomik; listeler indeks dışı adayı | Kalıcı; sayısal sınırlar onaylanmalı; level formülünün sürümlü materialization'ı |
| `v2Companies/{cid}/assets/{aid}` | `kind:s` vehicle/driver/trailer, `catalogId:s`, `status:s`, `assignmentId:s?`, `attributes:m`, `acquiredAt:t` | Fleet; ekran bazlı sayfalama; satın alma/atama yazısı | Finance ve delivery ile atomik; `kind,status` birleşik sorgu adayı; assetsiz genel tarama yok | Kalıcı ownership; emekli varlık tombstone; sıcak nokta yalnız aynı asset |
| `v2Companies/{cid}/warehouses/{wid}` | `cityId:s`, `type:s`, `level:i`, `status:s`, `configVersion:s` | Warehouse; liste küçük, ekran açılışında oku; seyrek upgrade | Finance/progression ile atomik; `wid` onaylı tekillik kapsamından türetilir; city/type sorgusu gerekirse index | Kalıcı; bir şehirde kaç tesis olacağı UNKNOWN; global hotspot yok |
| `v2Companies/{cid}/contracts/{id}` | `status:s`, `offerEpoch:s`, `routeId:s`, `cargo:m`, `reward:m`, `requirements:m`, `expiresAt:t`, `acceptedAt:t?`, `rulesVersion:s` | Contract engine; mevcut offers/accepted sayfalı okuma; sınırlı generation, accept | Status+expiresAt index adayı; kabul ve kota/rezervasyon atomik | Süresi dolmuş kabul edilmemiş teklifler: öneri 7 gün sonra temizle; accepted kayıt teslimat/uyuşmazlık boyunca kalır |
| `v2Companies/{cid}/deliveries/{id}` | `contractId:s`, `vehicleId:s`, `driverId:s?`, `trailerId:s?`, `routeId:s`, `originCityId/destinationCityId:s`, `startedAt/expectedArrivalAt:t`, `status:s`, `cargo:m`, `reward:m`, `claimedAt:t?` | Delivery; aktif ekran sorgusu, start/finalize/claim yazısı | Status+expectedArrivalAt sorgu indexi; asset+contract+claim+finance atomik geçiş | Bitmiş ayrıntı için öneri 90 gün; tekil claim izi silinmez; aktif delivery TTL ile silinmez |
| `v2Players/{uid}/commands/{key}` | `command:s`, `requestHash:s`, `companyId:s`, `result:m`, `stateVersion:i`, `committedAt:t` | Command modülü; yalnız backend; retry nokta okuma; başarı başına tek kayıt | İş etkisiyle aynı transaction; key UID kapsamlı; response/hash index dışı | İlk sürüm ekonomik makbuzlar kalıcı; hacim ölçülür; rastgele key, tek global sayaç yok |
| `v2Companies/{cid}/ledger/{operationId}` | `deltaMinor:i`, `balanceAfterMinor:i`, `reason:s`, `referenceId:s`, `correlationId:s`, `createdAt:t` | Finance; operasyon/audit, oyuncuya gerekiyorsa sınırlı projeksiyon; append | Finance ile atomik; createdAt sayfalı sorgu gerektiğinde index | Öneri 1 yıl ayrıntı, nihai süre onaylı; snapshot bakiyeye alternatif otorite değil; claim tekilliği ayrı kalıcı izde |
| `v2Companies/{cid}/claims/{entitlementId}` | `kind:s`, `sourceId:s`, `operationId:s`, `claimedAt:t` | Reward domain; backend nokta erişimi | Reward ve finance ile atomik create-if-absent; index gerekmez | Tekrar ödülü engellemek için kalıcı tombstone; TTL yok |
| `v2WorldConfig/{version}` + `v2WorldConfig/active` | Sürümde `prices/progression/catalogs:m`, `routeVersion:s`; pointer'da `version:s`, `publishedAt:t`, `maintenance:b`, `minProtocol:i` | Admin yazar; komutlar sürüm kontrollü okur; client yalnız ayrılmış güvenli alanlar | Immutable versiyon + pointer; satın alma transaction'ı aktif sürümü doğrular; büyük map index dışı | Önceki sürümler rollback/audit boyunca saklanır; pointer çok okunur, nadir yazılır |
| `v2EconomySnapshots/{epoch}` | `configVersion:s`, `generatedAt:t`, `validUntil:t`, `cityPrices:m`, `eventVersions:array<s>` | Economy publisher; oyuncular ortak snapshot okur; B aşamasında periyodik yazı | Yeni snapshot tamamlanınca aktif pointer atomik değişir; büyük map index dışı | Aşama A'da zorunlu değil; B'de öneri 30 gün sıcak + gerektiğinde arşiv; büyük dünya büyürse bölgesel doküman |
| `v2Events/{id}` | `version:i`, `status:s`, `startsAt/endsAt:t`, `priority:i`, `regionIds:array<s>`, `modifiers:m`, `content:m`, `minProtocol:i` | LiveOps; admin yayınlar; client yayımlananı okur | Status+startsAt sorgu adayı, endsAt sunucuda kontrol; config versiyonuna bağla | Etkinlik+audit ömrü; ilk aşamada config yeterli, kampanya geldiğinde gerekli |
| `v2AdminAudit/{id}` | `actorId:s`, `action:s`, `target:s`, `beforeVersion/afterVersion:s`, `reason:s`, `createdAt:t` | Yalnız admin backend; oyuncu erişimi yok | Config pointer yayını ile audit atomik; tarih/hedef sorguları kadar index | Öneri 1 yıl; kişisel bilgi minimum; rastgele belge ID |
| `v2MigrationRecords/{uid}` | `mappingVersion:s`, `sourceFingerprint:s`, `status:s`, `validatedAt:t`, `targetCompanyId:s`, `cutoverAt:t?` | Gelecek kontrollü migration servisi; admin-only | Hesap cutover ve geçiş marker'ı tutarlı; phase/cursor kontrollü | Geçiş izi kalıcı; mevcut `migrationCompleted` yerine geçmez |

**PROPOSED:** P2P market ilk sürüme alınırsa `v2Listings/{id}` (`sellerCompanyId`, `assetId`, `priceMinor`, `status`, `expiresAt`) ve `v2MarketTransactions/{id}` (`buyer`, `seller`, `listingId`, `operationId`, `createdAt`) eklenir. Market modülü yazar; yalnız sanitized aktif listing public okunur, işlem yalnız taraflara görünür. `status+assetKind+priceMinor` gibi gerçekten kullanılan sayfalı sorgular indekslenir. Listing tüketimi, asset transferi, iki finance ve ledger aynı transaction; kendi kendine satın alma ve busy asset satışı yasak. Listing expiry iş kuralıyla kontrol edilir, temizleme gecikebilir. Ayrıntı retention önerisi 1 yıl, consumed tekilliği kalıcı. Popüler tek listing contention'ı beklenir; global market lock tutulmaz. Sistemden katalog alışverişiyle başlanırsa bu iki koleksiyon ertelenir.

**PROPOSED:** Tek büyük player belgesi yerine sıcak finance/progression ve büyüyen asset/contract/delivery alt koleksiyonları ayrıldı. Her XP/para alanını ayrı dokümana parçalamak gereksiz. Limitli özet cache/projeksiyonları `sourceStateVersion` taşımalı; komut kararlarında kullanılmamalı. `companyScore` ve leaderboard gibi alanlar türetilmiş model olabilir, ancak legacy anlamları UNKNOWN. Büyük map ve TTL alanlarının index maliyeti ayrıca değerlendirilir ([Firestore best practices](https://firebase.google.com/docs/firestore/best-practices?hl=en)). Doküman büyüklüğü için öneri 256 KiB iç uyarı eşiği; bu Firebase ürün limiti iddiası değildir.

## 06 — Komut protokolü, atomiklik ve tekrarlar

**PROPOSED ortak istek:** Callable endpoint adı komutu belirler; `protocolVersion:i`, `idempotencyKey:s`, `payload:object`. Key rastgele en az 128-bit kimlik, uzunluk sınırı 128 karakter, path ayırıcıları reddedilir. `uid/ownerUid`, ödül, tamamlanma zamanı veya nihai fiyat istemciden kabul edilmez. `expectedStateVersion` yalnız eski ekran kararının reddedilmesinin gerekli olduğu komutlarda optional; tüm komutlarda zorunlu yapmak bağımsız cihaz işlemlerini gereksiz durdurur.

```json
{
  "protocolVersion": 1,
  "idempotencyKey": "synthetic-request-0001",
  "payload": {"cityId": "izmir", "warehouseType": "standard"}
}
```

`standard` burada yalnız mock katalog örneğidir; onaylı depo tipi değildir.

```json
{
  "protocolVersion": 1,
  "serverTime": "2026-10-16T12:00:00Z",
  "stateVersion": 42,
  "result": {"warehouseId": "synthetic-warehouse-01", "level": 1},
  "error": null
}
```

**PROPOSED yanıt:** Başarıda result ve transaction sonrası company stateVersion; beklenen domain hatasında `result:null`, `error:{code,messageKey,params,retryable}`. Geçerli state okumadan hata oluştuysa stateVersion yoktur; 0 uydurulmaz. Auth/transport/framework hataları Callable native error olarak gelebilir; Unity adaptörü aynı yerel hata modeline çevirir. Tarih örnekleri sentetiktir. Para yanıtları minor-unit string, enum'lar string, bilinmeyen response alanları ileri uyum için yok sayılır; request alanları strict allowlist ile kontrol edilir.

**Ortak güvenlik ve yürütme:** Auth context zorunlu → protokol/payload boyutu/alan tipi doğrula → UID bazlı rate limit → company mapping ve owner doğrula → transaction'da idempotency/config/state oku → domain önkoşullarını doğrula → tüm kanonik etkileri, version artışını, makbuzu ve gerekiyorsa ledger/claim kaydını commit et. Bakım/cutover kilidi her mutasyonda kontrol edilir. Sahiplik ve değişebilir önkoşullar yalnız transaction dışındaki önceki okumaya dayandırılmaz.

| Komut | Payload → result | Özel doğrulama / transaction kapsamı | Temel domain hataları |
|---|---|---|---|
| `createCompany` | `name:s` → `companyId` | Auth UID'ye mevcut şirket yok; ad uzunluğu/normalizasyon; player+company+finance+progression+receipt birlikte. Başlangıç parası client'tan değil sürümlü config'ten; yeni key ile tekrar da ikinci şirket açamaz | COMPANY_EXISTS, INVALID_NAME, MIGRATION_PENDING |
| `buyVehicle` | `catalogId:s`, isteğe bağlı `quoteVersion:s` → `assetId,chargedMinor` | Owner, catalog açık, level/kapasite, fiyat ve bakiye; finance+asset+ledger+receipt+version | INSUFFICIENT_FUNDS, CAPACITY_REACHED, PRICE_CHANGED |
| `hireDriver` | `candidateId:s` → `driverId,chargedMinor` | Aday sunucuda geçerli, daha önce alınmamış, kapasite/ücret; aday tüketimi+asset+finance+ledger | OFFER_EXPIRED, CANDIDATE_UNAVAILABLE |
| `openWarehouse` | `cityId:s,warehouseType:s` → `warehouseId,level` | Şehir unlock, tür, tekillik kapsamı, fiyat; warehouse+finance+ledger | CITY_LOCKED, ALREADY_OWNED, INSUFFICIENT_FUNDS |
| `upgradeWarehouse` | `warehouseId:s,expectedLevel:i` → `level,chargedMinor` | Owner, beklenen seviye, max seviye, progression ve maliyet; warehouse+finance+ledger | STALE_STATE, MAX_LEVEL, REQUIREMENT_NOT_MET |
| `acceptContract` | `contractId:s` → `contractId,status` | Teklif oyuncuya ait, status offered, serverNow<expiresAt, quota/progression; contract+company version+receipt; gerekiyorsa kabul kotası aynı transaction | CONTRACT_EXPIRED, ALREADY_ACCEPTED, QUOTA_EXCEEDED |
| `startDelivery` | `contractId:s,vehicleId:s,driverId?:s,trailerId?:s` → `deliveryId,expectedArrivalAt` | Accepted contract, gereksinim/rota/yük kapasitesi, tüm asset owner ve idle; delivery+contract+asset assignment+receipt | ASSET_BUSY, EQUIPMENT_MISMATCH, CONTRACT_NOT_READY |
| `claimDeliveryReward` | `deliveryId:s` → `reward,deliveryStatus` | Owner, sunucu varış zamanı, kalıcı entitlement tekilliği; gerekiyorsa finalize+asset release, claim+finance+progression+ledger+receipt tek transaction | DELIVERY_NOT_READY, ALREADY_CLAIMED |
| `claimReward` | `entitlementId:s` → `reward` | Gelecek günlük/görev/event hakkı sunucuda mevcut, zamanı ve kapsamı uygun; entitlement+claim+finance+progression+ledger | REWARD_NOT_ELIGIBLE, ALREADY_CLAIMED |
| `marketTransaction` | `listingId:s` veya `catalogOfferId:s,quantity:i` → `transactionId,chargedMinor,assetIds` | İki mod ayrık şema; pozitif sınırlı miktar, server fiyatı, owner ve stock. P2P ise §05'te iki taraf atomik | LISTING_UNAVAILABLE, PRICE_CHANGED, SELF_TRADE, INSUFFICIENT_FUNDS |

Her satırda ortak auth/sahiplik ve idempotency zorunlu; generic ödül komutu açık bir miktar aktarım endpoint'i değildir. İsim/başlangıç parası, aday üretimi, kapasite, ücret ve formül değerleri bu depoda **UNKNOWN**; config değerlerini geliştirici tek başına icat etmemeli.

### Dayanıklı idempotency

**PROPOSED:** Key kapsamı Auth UID + key; hash komut adı, protokol, şirket ve normalize edilmiş payload'ı kapsar. Aynı key+aynı hash başarılı kayıttaki sonucu döndürür. Aynı key+farklı hash `IDEMPOTENCY_CONFLICT` ile reddedilir. İki eşzamanlı çağrı aynı makbuz dokümanını create ettiğinden yalnız bir ekonomik etki commit olur. Makbuz önce, işlem sonra veya tersi ayrı commit yapılmaz.

İlk sürümde ekonomik makbuzlar TTL ile silinmez. Böylece eski key'in kaydı temizlendi diye yeni satın alma olması önlenir. Daha sonra büyük response arşivlenip küçük fingerprint/result reference tombstone kalıcı tutulabilir. Süreli idempotency ancak açık retry penceresi, sunucu tarafından doğrulanabilir operation expiry ve kalıcı domain claim/uniqueness ile tasarlanır. Client timestamp tek başına eski replay'i engellemez. [TTL](https://firebase.google.com/docs/firestore/ttl) iş kuralı zamanlayıcısı olarak kullanılmamalıdır.

Başarılı makbuzda `serverTime` ilk commit zamanıdır; replay yanıtı aynı sonucu taşır, yeniden zaman senkronizasyonu için taze read gerekir. Başarısız transaction etki bırakmaz; terminal domain hataları v1'de kalıcı makbuzlanmaz. Bu yüzden hata almış bir key'in sonraki denemesi değişen koşullarda başarılı olabilir; bu protokol sözleşmesinde açık olmalı. Kesin kullanıcı iptalinin semantiği gerekiyorsa ayrıca cancelable server intent tasarlanır.

### Üç farklı retry

**PROPOSED:** (1) Ağ/transport timeout: sonuç bilinmiyor, aynı key+payload ile sınırlı exponential backoff+jitter; yeni key üretme. (2) Uygulama retry: UNAVAILABLE/RETRY_LATER retry edilebilir; PRICE_CHANGED/STALE_STATE fresh state ve yeni kullanıcı kararı ister; yeni karar yeni key'dir. (3) Firestore transaction retry: callback birden fazla çalışabilir; ID'ler/rasgele teklif hesapları retry'da değişmemeli, aynı ekonomik intent korunmalı. E-posta, dış ödeme, analytics gönderimi callback içinde yapılmaz. Firestore transaction davranışı için [resmi belge](https://firebase.google.com/docs/firestore/manage-data/transactions).

Ortak hata kodları: UNAUTHENTICATED, PERMISSION_DENIED, INVALID_ARGUMENT, UNSUPPORTED_PROTOCOL, IDEMPOTENCY_CONFLICT, STALE_STATE, RATE_LIMITED, MAINTENANCE, MIGRATION_PENDING, RETRY_LATER, INTERNAL. Hatalar başka oyuncunun kaydının varlığını veya token/stack trace'i açığa çıkarmaz. Rate limit işlem anahtarının yerine geçmez; meşru retries başarı makbuzuna erişebilmelidir.

## 07 — Yazma sahipliği, okumalar ve analitik

**PROPOSED:** Kanonik v2 koleksiyonlarında mobil istemci yazısı tamamen reddedilir; ad/tercih değişiklikleri bile başlangıçta küçük komutlar üzerinden yürür. Oyuncu kendi sınırlı verisini doğrudan Rules korumalı Firestore okumalarıyla alabilir; karmaşık bootstrap ve mutation sonrası tutarlı sonuç Callable ile sunulur. Liste okuması company owner koşuluna bağlıdır; rules bir sorgu filtresi gibi düşünülmez.

| Veri | Unity | Oyuncu komutu | Admin / job |
|---|---|---|---|
| Auth kimliği | SDK login/link | Auth context doğrular | Yetkili operasyon dışında değiştiremez |
| Company/player mapping | Kendi kaydını okur | Bootstrap/izinli profil komutu yazar | Yalnız kontrollü migration |
| Finance, progression, sahiplik | Kendi verisini okur | Domain transaction yazar | Gerekçeli düzeltme komutu+ledger; serbest edit yok |
| Contract/delivery | İzinli sorgu ve animasyon | Üretme/kabul/start/claim | Gelecekte aynı kuralları kullanan job |
| World config, events | Sanitized published okuma | Salt okur | Yetkili sürüm yayını |
| Receipt, migration marker | Komut sonucu üzerinden | Backend yazar | Denetim için minimum yetki |
| Ledger/admin audit | Gerektiğinde sınırlı geçmiş | Atomik append | Yetkili denetim; oyuncu değiştiremez |
| Cache, animasyon, dil tercihi | Yerel yazar | Oyun otoritesi değil | Gereksiz sunucu yazısı yok |

**PROPOSED:** Dört veri sınıfı ayrıdır: kanonik oyun durumu, finans/güvenlik denetim kaydı, ürün analitiği, operasyon metrikleri. Minimum analitik olaylar `company_created`, `vehicle_purchased`, `driver_hired`, `contract_accepted`, `delivery_started`, `delivery_completed`, `reward_claimed`, `warehouse_opened`, `warehouse_upgraded`, `market_transaction_completed`, ileride `liveops_participation`. `eventId=operationId+eventType` tüketicide dedupe sağlar; correlationId komut ile logu bağlar. Analitik kaybı para kaybına veya ikinci ödüle dönüşmez. İlk aşamada ayrı event bus/warehouse şart değil. Güvenilir dış yayın zorunlu hale gelirse aynı transaction'a outbox kaydı eklenir; tekrar teslimat kabul edilir. Token, email, ham payload ve gerçek isim loglanmaz; analitik saklama önerisi 90 gün, finans kayıtlarından ayrı onaylanır.

## 08 — Güvenlik mimarisi ve tehdit modeli

**UNKNOWN:** Üretimdeki Rules, IAM, App Check enforcement, Auth sağlayıcı ayarları ve Functions yetkilendirmesi incelenmedi. Bu nedenle doğrulanmış üretim açığı raporlanmıyor. Aşağıdakiler tasarım tehditleri ve bilgi eksikleridir.

| Öncelik | Durum | Risk | PROPOSED kontrol / kanıt testi |
|---|---|---|---|
| CRITICAL | UNKNOWN semantik | Yanlış bakiye/level eşlemesiyle oyuncu kaybı | Onaylı mapping + dry-run diff + karantina; money/cash farklı örnek test |
| CRITICAL | Hipotetik | Client'ın para/ownership yazması | v2 deny-write rules; Auth UID/owner backend kontrolü; başka UID testi |
| HIGH | Hipotetik | Retry veya iki cihazla çift harcama/ödül | Atomik receipt+finance+claim; farklı key ile aynı reward ve aynı key yarış testleri |
| HIGH | Hipotetik | Admin SDK ile Rules'ın aşılmasının unutulması | Sunucuda auth/authorization; ayrı admin IAM ve audit |
| HIGH | UNKNOWN | Eski istemcinin cutover sonrası legacy state'e yazması | Hesap bazlı tek yazar bariyeri; eski endpoint/rules davranışı onaylı test |
| HIGH | Hipotetik | Client saatini ileri alıp erken ödül | ServerNow ve expectedArrivalAt; saat manipülasyonu testleri |
| HIGH | Hipotetik | UID/provider uyuşmazlığı veya yanlış hesap birleştirme | Linking credential sahipliği, hata çakışmalarında duruş; synthetic provider testleri |
| HIGH | Hipotetik | DEV testinin production'a düşmesi | Açık project allowlist, demo project, emulator kapalıyken fail-closed test |
| MEDIUM | Hipotetik | Abuse / maliyet saldırısı | UID/komut limitleri, max payload, max query, max instances, App Check ölçüm+enforcement |
| MEDIUM | Hipotetik | Config veya event reward patlaması | Bounded modifier, version review, canary, rollback pointer |
| MEDIUM | VERIFIED yerel davranış, sızıntı UNKNOWN | Error log exception mesajı içeriyor | `GameLog.cs:19–29`; gelecekte backend adapter redaksiyonu; log örneği testi |
| LOW | Hipotetik | Lokalize metinlerde ham HTML / eksik dil | Plain text allowlist, boyut limiti, İngilizce fallback |

**PROPOSED:** App Check cihaz/uygulama kaynaklı abuse'u azaltan ek katmandır, kimlik veya şirket sahipliği değildir. İlgili Firebase [enforcement belgesi](https://firebase.google.com/docs/app-check/cloud-functions) seçilecek Functions/SDK kombinasyonunda DEV testiyle uygulanmalı. Anonymous kullanıcı da UID sahibi olabilir; satın alma/market gibi özelliklerde gereken hesap güven seviyesi ürün kararıdır.

**PROPOSED:** Least privilege runtime service account, ayrı deploy kimliği ve admin kimliği; indirilen service-account JSON'ları geliştirici makinelerine dağıtılmaz. Rules Admin SDK erişimini sınırlandırmaz; bu ayrım [Firebase Rules belgesinde](https://firebase.google.com/docs/firestore/security/rules-conditions) açıklanır. İzin kapsamı ve yönetici rol ataması oyuncu yazılabilir dokümanlardan türetilmez. Maintenance flag sunucuda uygulanır. Tüm query/array/string boyutları bounded; non-finite, negatif miktar ve integer overflow reddedilir.

## 09 — Ekonomi, kontratlar, LiveOps ve kanonik içerik

### Aşamalı ekonomi

| Aşama | PROPOSED değer / model | Maliyet ve risk | Geçiş koşulu |
|---|---|---|---|
| A: sunucu kontrollü statik/config | Fiyat, ödül ve unlock tabloları immutable sürüm; komut server config okur | Düşük operasyon; hatalı config için review/canary; scheduler gerekmez | İlk sürüm önerisi |
| B: periyodik snapshot | Production/consumption/stock/supply/demand girdileri, bounded fiyatlar, epoch | Periyodik hesap ve snapshot okumaları; gecikmiş epoch fallback; input abuse kontrolü | Oyuncu davranışıyla değişen pazarın ürün değeri ve sentetik stabilite testi |
| C: gelişmiş simülasyon | Bölgesel etkileşim, damping, decay, normalizasyon, olay etkileri | Daha fazla CPU, state, scheduling ve operasyon; dengesizlik riski | Ölçülmüş yük ve tasarım ihtiyacı; gerekiyorsa ayrı Cloud Run worker |

**PROPOSED:** A aşamasında gelecekteki alanlar için boş koleksiyonlar yaratılmaz. B'de öneri 15 dakikalık cadence bir başlangıç varsayımıdır; gerçek ihtiyaç ölçülür. Fiyat taban/tavanı, değişim sınırı, damping katsayıları henüz UNKNOWN ürün kararlarıdır. Her epoch tekil ID taşır; retry aynı epoch'u iki kez ilerletmez; tam snapshot hazır olmadan aktif pointer değişmez. Geçmiş kontrat ödülleri yeni fiyatlarla geriye dönük hesaplanmaz.

### Kontrat motoru

**PROPOSED:** İlk model oyuncuya özel sınırlı teklif havuzu; ekran her açıldığında yeniden üretim yok. Sunucu pencere/epoch başına sınırlı batch üretir ve persist eder. İlk kapasite/teklif sayıları konfigürasyon ve performans deneyiyle seçilir. Global paylaşımlı sınırlı stok, ilk sürümde contention ve yarış maliyeti nedeniyle ertelenir; dünya kampanyaları kişisel teklifler için ortak template olabilir.

Girdiler önce progression, uygun rota/yük, fleet capability ve config; ileride economy epoch, rarity, yakın tarihli contract türleri, bölge ve LiveOps. Backend randomness kullanabilir; tam deterministik dünya zorunlu değil. Ancak aynı generation isteğinin retry'ı yeni havuz açmamalı; generation epoch/key ve kayıtlı çıktı kullanılır. Yakın tarih listesi sınırlı tutulur, anti-repetition oyuncunun refresh spam'iyle aşılmaz.

Lifecycle: `offered → accepted → in_progress → completed/failed`, teklif tarafında `expired`. Teklif süresi kabul anında serverNow ile kontrol edilir. Kabul edilmiş işin teslim deadline'ı teklif expiry'sinden ayrı kavramdır; geç teslim/ceza formülü UNKNOWN ve onay gerektirir. Accept asset ayırmazsa başlatma anında yeniden uygunluk kontrolü yapılır; UI kabulün araç rezervasyonu garantilemediğini bilir. Erken rezervasyon ürün gereği olursa accept komutu asset ID'leriyle sözleşme değişikliğine tabi olur.

### LiveOps / yerelleştirme

**PROPOSED:** Event `draft → scheduled → active → ended/cancelled`; etkinlik zaman durumu server timestamp aralığıyla hesaplanabilir, ayrı status job zorunlu değil. Yayınlanmış versiyon immutable; düzeltme yeni version. Çakışmada önce kapsam, sonra priority ve deterministic ID tie-break; multiplier stacking toplama/çarpma ve tavanı config'te açık tanımlanır. Kabul edilmiş kontrat event/config sürümünü saklar. Maintenance, announcements, seasons ve feature flags aynı yetkili yayın disiplinini kullanır; yönetim paneli yapılmaz.

**VERIFIED:** EN/TR/DE ve İngilizce fallback mevcut istemci temelinde tanımlı (`ARCHITECTURE.md:23–40`, `LocaleCodes.cs:5–7`, `LocalizationContent.cs:7–23`). **PROPOSED:** Backend `eventKey`, `cityId`, `productId`, `percent` gibi parametre gönderir. Yeni uzaktan içerik için `contentVersion`, `texts:{en:{title,body},tr:{...},de:{...}}`, parametre şeması, `minProtocol`, fallback key tutulur. Düz metin ve desteklenen placeholder'lar; uzaktan executable kod yok. İngilizce zorunlu, TR/DE yayın kontrolü; yeni gameplay davranışı gerekiyorsa Unity güncellemesi gerekir, sırf yeni metin için gerekmez.

### Ürün referanslarının korunması

**OBSERVED:** Şehir ID'leri `izmir`, `istanbul`, `ankara`, `bursa`, `antalya`, `adana`, `diyarbakir`, `trabzon`; canonical ID'ler çevrilmez. RAIL doğrudan çift yön koridorları yalnız Ankara–İstanbul, Ankara–İzmir, Adana–Ankara, Adana–Diyarbakır, İstanbul–İzmir. Bursa/Antalya/Trabzon için doğrudan RAIL bağlantısı varsayılmaz.

**UNKNOWN:** ROAD veri seti ve hash üretim prosedürü yok. Kullanıcı referansı `sha256:b506587dfd7e390794a448a0a855b773af640ebfeba316a4ffc86bacf4d45882` doğrulanmadı. Gelecekte tam girdi, sıralama, encoding, newline ve serialization prosedürü sağlanırsa salt okuma hash kontrolü yapılır. SEA/AIR ve bu şehirlerin oyun kodu bu depoda uygulanmış değildir. Başlangıç para/XP formülü, reputation, fleet capacity, ekipman/şehir unlock, günlük reset kuralları UNKNOWN; burada değer uydurulmamıştır.

## 10 — Teslimat, sunucu zamanı ve offline davranış

| Yöntem | PROPOSED değerlendirme |
|---|---|
| Lazy completion | İlk sürüm önerisi; claim/start/bootstrap gibi yetkili çağrıda süre kontrolü. Oyuncu yokken yazı yapmaz; bitiş görünürlüğü bir sonraki çağrıya kadar gecikebilir |
| Scheduled sweep | Gecikmeli batch tamamlar; bounded query/cursor, retry ve index gerektirir; anında background sonuç isteyen sistemler için |
| Teslimat başına Tasks | Zamanında tetikleme ihtiyacında yararlı; task yaratma/yeniden deneme/duplicate maliyeti ve operasyonu ekler; correctness task'a bırakılmaz |
| Hybrid | Lazy ana doğruluk yolu + daha sonra seyrek bakım sweep; yalnız ölçülmüş gecikme ihtiyacı varsa |

**PROPOSED:** Start transaction'ı asset assignment'ları kilitler, contract'ı in_progress yapar ve sunucunun seçtiği `startedAt`, rota/config süre hesabıyla `expectedArrivalAt` yazar. Route geometry Unity sunumu içindir. Arrival zamanının geçmesi ile DB status yazısının oluşması ayrı olaylardır. Yetkili finalize, `serverNow >= expectedArrivalAt` ve assignmentId eşleşmesini kontrol edip delivery'yi completed, asset'leri idle yapar. Ödül hakkı completed+claim marker yok koşuludur. Claim çağrısı finalize gerekiyorsa aynı transaction'da finalize+ödül+progression+ledger+claim+receipt yapar. Start çağrısı süresi dolmuş eski assignment'ı çözüyorsa eski delivery completion ile yeni assignment aynı transaction'da olmalı, ödül hakkı korunmalı. Başka delivery'nin assignment'ı körlemesine temizlenmez.

Beklenen varış geçmiş olsa da ödülün bir kez alınması kalıcı entitlement ID ile korunur; farklı key'le retry ikinci ödül üretmez. Finalize ayrı gerçekleşmişse reward claim asset'i yeniden serbestleştirmeye çalışmaz. Zaman hesapları server clock ile yapılır; transaction retry'da zaman şartı yeniden değerlendirilir. Başlangıç/varış süresi ve late/failure kuralları sürümlenir; client timer sonucu yazamaz.

**PROPOSED soft-online:** Unity cached şirket/fleet/harita/delivery gösterir; cache'in `fetchedAt`, `serverTime`, `stateVersion` bilgisi görünür freshness sağlar. İki refresh arasında monotonic süre ile animasyon yapılır; cihaz saat değişikliği ödül yaratmaz. Offline purchase/accept/claim çalıştırılmaz; optimistic UI pending olarak kalır, sahiplik/bakiye kesinleşmiş gösterilmez. Reconnect önce auth ve environment doğrular, taze özet okur, belirsiz komutları aynı key ile uzlaştırır. Eski response stateVersion mevcut cache'ten küçükse snapshot üzerine yazılmaz; özgün operasyon sonucunun geldiği ayrıca gösterilir. Birkaç Firestore listener'ın ara durumunu atomik ekran snapshot'ı saymak yerine command response veya version eşleşmeli refresh kullanılır.

Günlük reset, cooldown, contract expiry, event window ve market geçerliliği backend UTC politikasıyla tanımlanır; yerel gece yarısı otorite değildir. Reset saatinin ürün tanımı UNKNOWN. Per-second position yazısı, sürekli polling ve otomatik offline kanonik Firestore write queue yoktur.

## 11 — Üretim oyuncularının korunması ve strateji karşılaştırması

**OBSERVED:** Kayıtlı oyuncular ve gameplay verisi var. **PROPOSED:** Yeni sistem geliştirilirken üretim değişmeden kalır. Auth UID yeni verinin kimlik bağlantısıdır; Unity'ye geçiş yeni hesap zorunluluğu doğurmamalı. Kayıp veri tahmini default ile doldurulmaz; anlaşılmayan kayıt migration quarantine durumuna alınır, eski erişim politikası kontrollü korunur.

| Boyut | A — Mevcut üretim projesi | B — Yeni üretim projesi |
|---|---|---|
| UID / login | Auth kimliğini yerinde tutar; sağlayıcı ve app kayıt uyumu yine doğrulanır | Kimlik taşıma, UID mapping/koruma ve provider konfigürasyonu ayrıca gerekir |
| Veri sürekliliği | Aynı ortamda ayrı v2 namespace + kontrollü mapping | Projeler arası taşıma ve auth/veri referans tutarlılığı gerekir |
| Risk | Legacy writer, eski rules ve trigger etkileşimi | Bunlara ek hesap erişimi, iki proje routing, provider/oturum karmaşası |
| Downtime | Hesap bazlı kısa freeze/cutover mümkün; kanıtlanmadan sıfır kesinti denmez | Dondurma/taşıma/yeniden login penceresi genellikle daha karmaşık |
| Rollback | Eski veriyi koruma yararlı; yeni işlemler sonrası kör geri dönüş yine güvenli değil | İki projedeki ilerlemeyi uzlaştırma daha zor |
| Operasyon/maliyet | Geçici çift schema ve audit maliyeti | Çift altyapı ve veri/kimlik geçiş işletimi |
| Ne zaman seçilir? | **PROPOSED varsayılan**, orta güven | Mevcut projenin teknik/IAM/bölge/organizasyon engeli kanıtlanırsa |

Yeni backend yazılması yeni production projesi gerektirmez. A seçeneği daha az kimlik hareketi gerektirdiği için önerilir; mevcut projenin güvenli, uygun veya doğru yapılandırılmış olduğu iddiası değildir. Fresh start/reset önerilmez.

### Gelecekteki geçiş kapıları — bu denetimde uygulanmadı

1. Semantik schema discovery ve mapping spec onayı (§12). Kişisel kayıtlar geliştirmeye taşınmaz.
2. Sentetik migration fixture'ları; dönüşüm tekrar çalıştırıldığında aynı hedef, aynı fingerprint ve aynı validation sonucu. SchemaVersion ve mappingVersion ayrı.
3. Yetkili operasyon ekibinin backup/PITR uygunluğunu, retention, erişim ve maliyeti değerlendirmesi; ayrı onayla backup ve izole restore provası. Backup almak restore kanıtı değildir.
4. Eski istemci, tüm legacy Functions/triggers/jobs ve doğrudan yazıların envanteri; hesap bazlı tek yazar bariyerinin testi. Çift kanonik para kaynağına kontrolsüz dual-write yok.
5. Ayrı üretim onayıyla dar canary: ilgili hesabın mutasyonlarını dondur, aktif işleri/saves/state'i sabit kesitte eşleştir, v2 dönüşümünü doğrula, routing'i v2'ye al. Legacy kayıtları silme.
6. Kabul şartları: UID/owner bütünlüğü, onaylı bakiyeler, asset tekilliği, progression mapping, warehouse seviyeleri, kontrat/delivery bağları ve toplamlar. Açık teslimatın elapsed time ve claimed marker'ı korunmalı; resetleyip ikinci ödül verme.
7. Canary izleme ve kademeli genişleme; failure threshold aşılırsa yeni cutover'ları durdur. Kod/config rollback, şema backward compatibility ve ekonomi düzeltmesi ayrı süreçler.
8. V2'de yeni finans işlemi oluşmuşsa eski snapshot'a geri dönme oyuncunun ilerlemesini siler. Önce mutasyonları durdur, audit/ledger'den onaylı ters dönüş veya forward-fix planı üret. Bu plan test edilmemişse rollback yerine HOLD ve bakım modu.

**PROPOSED kurtarma hedefi:** Önce finans/asset kaybını önle; RPO ≤15 dakika ve RTO ≤4 saat yalnız başlangıç iş hedefi adaylarıdır, sağlanmış garanti değildir. Hedeflerin platform özellikleri, Auth kurtarması ve bütçeyle karşılanması ayrıca doğrulanmalı. Hatalı config pointer eski sürüme dönebilir; önceki hatalı ödemeler kayıtlı düzeltme komutu gerektirir. Transaction atomikliği kısmi Firestore ekonomik yazısını önler; dış yan etkiler bu güvenceye dahil değildir.

## 12 — Legacy schema belirsizlikleri ve güvenli discovery

**OBSERVED — kullanıcının aktardığı kök koleksiyonlar:** `users`, `usernames`, `publicProfiles`, `globalEconomy`, `globalEconomySnapshots`, `globalMarketHistory`, `leaderboards`, `leaderboardIdempotency`, `seasons`, `vehicleMarketplaceListings`, `vehicleMarketplaceListingLocks`, `vehicleMarketplaceRateLimits`, `vehicleMarketplaceTransactions`, `weeklyMissionRotations`, `weeklyMissionTemplates`. Bu liste eksiksiz şema veya çalışan özellik kanıtı değildir.

**OBSERVED:** Player alanları `uid`, `ownerUid`, `companyName`, `companyScore`, `level`, `money`, `provider`, `authProviders`, `createdAt`, `updatedAt`, `appVersion`; alt koleksiyonlar `marketplaceState`, `meta`, `saves`, `serverState`. `serverState/current`: `cash`, `companyLevel`, `companyName`, `completedDeliveries`, `failedDeliveries`, `lateDeliveries`, `initialized`, `leaderboardScore`, `migrationCompleted`, `migrationSource`, `ownedTrailerIds`, `ownedTruckIds`, `ownedTrucks`.

**OBSERVED:** `globalEconomy/current` içinde `configVersion`, `epoch`, `generatedAt`, `serverTimeMs`, `snapshot`, `activeEvents`, `cityMarketPrices` görüldüğü aktarıldı. **UNKNOWN:** Hangi writer'ın ne sıklıkta yazdığı, snapshot'ın güncel olup olmadığı veya client ödüllerini belirleyip belirlemediği bilinmiyor. `globalEconomy` adı dinamik ekonominin çalıştığını kanıtlamaz.

| Belirsizlik | Yasak varsayım | Geçişe etkisi / gereken doğrulama |
|---|---|---|
| `users/{uid}.money=10780` / `serverState/current.cash=20000` | Aynı cüzdan, max/min doğru, serverState daha yetkili | Kavram, birim, kapsam, writer, timestamp semantiği, harcama/ödül read path ve audit ilişkisi gerekir |
| `level=3` / `companyLevel=1` | Aynı progression boyutu | Oyuncu/şirket/legacy seviye ayrımı, XP ve unlock bağlantıları belirlenmeli; ayrı boyutlarsa ayrı hedef alan |
| `migrationCompleted=true`, `migrationSource="legacy-save"` | Yeni v2 migration tamamlanmış | Eski marker'ın hangi mapping sürümünü ve kaynağı kapsadığı bilinmiyor; v2 marker ayrı tutulur |
| `uid`, `ownerUid`, doc ID | Her zaman eşit ve tek şirket | İlişki cardinality, orphan/çakışma ve hesap linking kuralı doğrulanmalı |
| `ownedTruckIds` / `ownedTrucks` / trailer ID listeleri | Tüm varlıklar eksiksiz ve eşsiz | Katalog referansı, runtime özellik, busy/satış/transfer ve duplication uzlaştırması |
| `saves`, `meta`, `serverState` | Birini güvenle yok sayabiliriz | Save sürümü, son otoriter writer, güncellik ve merge semantiği |
| Drivers/warehouses/contracts/deliveries/reputation | Yoklar veya sıfır varsayılabilir | Aktarılan örnek sınırlı; tüm domain'ler discovery kapsamına alınmalı |
| `provider`, `authProviders` | Gerçek Auth provider yapılandırmasıyla aynı | Oyuncu dokümanı alanı yerine yetkili Auth yapılandırması ve linking akışı doğrulanmalı |

**PROPOSED gelecek salt okuma discovery prosedürü:** Önce açık insan onayıyla proje, sorumlu kişi, saat aralığı, minimum yetkiler ve çıktı sınırı belirlenir. Yetkili operasyoncu schema/alan türleri ve ilgili writer/reader bilgilerini, deploy sürümlerini, rules/index ve sağlayıcı konfigürasyonunu kişisel veri çıkarmadan raporlar. Bu denetim söz konusu erişimi yetkilendirmez. İlk tercih secretsiz mevcut dokümanlar ve sentetik şekil örnekleridir; gerçek kayıt ihtiyacı varsa ayrıca kapsam onayı gerekir. Toplu kullanıcı export'u yapılmaz, gerçek oyuncu kayıtları DEV'e kopyalanmaz, token/credential paylaşılmaz.

Her aday alan için sözlük: `sourcePath`, `semanticMeaning`, `unit`, `writer`, `reader`, `schemaVersion`, `timeMeaning`, `authorityEvidence`, `targetPath`, `conversion`, `validation`, `conflictPolicy`. Semantik onaysız satır UNKNOWN kalır. Çelişkili kayıt otomatik büyük değere birleştirilmez; finans uzlaştırma kararı audit gerekçesi taşır.

**PROPOSED sentetik test matrisi:** Yukarıdaki 10780/20000 ve 3/1 farkları; farklı para birimleri; eksik serverState; marker true ama eksik asset; duplicate truck ID; iki UID'nin aynı şirketi göstermesi; expired/accepted/ongoing/claimed kontratlar; geçiş sırasında varan delivery; geçmişte ödenmiş reward; anonim linking çakışması; iki cihaz; yarıda kesilen dönüşüm ve tekrar çalışma. PASS: aynı girdi aynı hedef, para/asset kaybı veya artışı yok (yalnız onaylı dönüşüm), bilinmeyen kayıt karantinada, rollback provası başarılı. Hiçbir migration testi bu denetimde çalıştırılmadı.

## 13 — Authentication ve UID sürekliliği

**PROPOSED:** Mevcut Firebase Auth identity realm'i korumak önceliklidir. Unity aynı üretim projesinde onaylı uygulama kayıtları/sağlayıcılarla mevcut hesaba giriş yapar; token yenileme ve gerektiğinde tekrar login beklenir. Aynı email görüldü diye yeni UID'den eski hesabın varlıkları taşınmaz. UID eşitliği, sahiplik ve provider credential kanıtı önemlidir.

Anonymous-to-permanent geçiş, mevcut anonim kullanıcıya credential linking üzerinden yapılmalı; yanlışlıkla ayrı hesap yaratma akışı tasarlanmamalı. Sağlayıcı başka hesaba bağlıysa otomatik para/asset merge yapılmaz; kullanıcı iki hesabın sahipliğini kanıtlayan onaylı kurtarma akışına yönlendirilir. Firebase'in [Unity account linking belgesi](https://firebase.google.com/docs/auth/unity/account-linking) UID sürekliliğini açıklar; aynı belgede bazı projeler için linking sorunu uyarısı bulunduğundan seçilecek SDK/provider kombinasyonu DEV'de doğrulanmalıdır. Mevcut projenin bu sorundan etkilendiği UNKNOWN.

**UNKNOWN:** Etkin sağlayıcılar, anonim kullanıcı oranı, mevcut linking akışı, app/package kimlikleri, provider redirect/sertifika gereksinimleri ve bir UID'nin kaç şirkete sahip olabildiği. Bunlar production account continuity kapısını etkiler; sentetik DEV bootstrap tasarımını durdurmaz. Auth silme/yeniden yaratma veya UID reset bu raporla yetkilendirilmez.

## 14 — DEV ve LOCAL ortam stratejisi

**PROPOSED:** PROD mevcut ve korunan proje; DEV ayrı proje, yalnız sentetik kullanıcılar; LOCAL demo project ID kullanan Emulator Suite. Ortamlar kod yolu kopyalayarak değil açık config ile seçilir. Runtime project ID ve emulator host beklentisi allowlist ile doğrulanır; test hedefi belirsizse fail-closed. Emulator çalışmıyorsa production'a fallback olmaz. Resmi [Emulator Suite bağlantı rehberi](https://firebase.google.com/docs/emulator-suite/connect_and_prototype) sonraki uygulamaya temel alınmalı.

DEV service account/CI kimliği PROD yetkisi almamalı. Secret yönetimi deploy/runtime katmanında kalmalı; Unity build'e service-account veya admin sırrı gömülmez. Firebase app config kimlikleri yetki sınırı yerine geçmez. DEV uygulama kimlikleri ve App Check debug ayarları PROD build'inden ayrılır. CI testleri ağ/kimlik açısından yalnız emulator veya DEV'e erişebilir; yanlış proje testinin kasıtlı olarak başarısız olması acceptance testidir.

**PROPOSED açık onay gerektiren kararlar:** DEV cloud projesi oluşturma ve billing, bölge, erişim sahipleri/IAM, app kayıtları ve sağlayıcı ayarları, App Check enforcement, her production deployment/discovery/backup/restore/migration. BACKEND-00 bunların hiçbirini yapmaz. Bölge seçimi mevcut üretim Firestore bölgesi UNKNOWN olduğundan finalize edilemez; gecikme, veri yerleşimi ve maliyet birlikte değerlendirilir.

## 15 — Unity/backend paralel çalışma

**PROPOSED:** `Assets/`, `Packages/`, `ProjectSettings/` Unity geliştiricisi; `backend/` backend geliştiricisi; `docs/api-contracts/` ortak onay. Ortak `ARCHITECTURE.md` ve root `.gitignore` değişiklikleri iki tarafın etkisini gözetir. CODEOWNERS ileride eklenebilir, bu aşamada eklenmedi.

Dal adlandırma önerisi `feature/unity-XX-*`, `feature/backend-XX-*`; mevcut `feature/backend-00` korunur. Küçük PR, bağımsız CI kontrolleri, sözleşme etkisinin PR'da açıklanması; ortak dosyalarda kısa koordinasyon. Bu denetimde branch oluşturulmadı, commit/push/merge yapılmadı.

Entegrasyondan önce `protocolVersion:1` JSON şemaları ve sentetik success/error fixture'ları ortak incelenir. Önce bootstrap/state DTO, createCompany, sonra purchase, accept/start/claim sözleşmeleri sabitlenir. Unity aynı arayüzün mock adaptörüyle loading/pending/offline/stale/error/success ekranlarını geliştirir. Backend fixture'lara karşı bağımsız handler testleri yazar. Paylaşılan iş kuralı TypeScript ve C#'ta iki kez otoriter uygulanmaz; paylaşılan şey veri kontratı ve örneklerdir.

Breaking change yeni protocol sürümü gerektirir; optional response alanı eklemek eski istemciyi bozmamalı. Request şemaları strict, response okuyucuları ileri uyumlu. Destek penceresi önerisi mevcut ve önceki protokol; legacy RN bu pencereye otomatik dahil değildir, ayrı cutover politikası gerekir. Önce backend uyumlu yayın, sonra Unity; eski sürüm kaldırma yalnız kullanım ve migration ölçümleriyle. `UNSUPPORTED_PROTOCOL` yerelleştirilmiş güncelleme ekranına gider.

Checkpoint'ler: (1) schema/fixture onayı, (2) Unity mock akışı+backend emulator handler, (3) sentetik DEV uçtan uca Auth/Callable/App Check, (4) iki cihaz+timeout/replay+offline, (5) Android/iOS seçilen SDK ile integration, (6) üretim compatibility canary için ayrı onay. Finance wire türü, timestamp parsing ve hata eşleme ilk checkpoint'te test edilir.

## 16 — Performans, ölçek ve maliyet

**PROPOSED tahmin; ölçüm değil:** DAU başına 2 oturum/gün, oturum başına 10 mutasyon komutu, 20 foreground belge okuması (5 teklif dahil), komut başına ortalama 6 transaction okuması ve 6 yazı (state, receipt, ledger dahil ortalama), oyuncu başına 3 delivery/gün (start/claim komutları 20 komutun içinde), günde 10 yeni teklif dokümanı. Teklif generation için oturum başına 1 çağrı, 4 ek kontrol okuması ve 1 kontrol yazısı varsayılır. Epoch tekrar çağrıları yeni teklif üretmez. Ortak market snapshot oturum başına 1 okuma foreground 20'ye dahil. Ortalama aktif event 2, bunların okumaları da foreground 20 bütçesine dahil; yayın haftada 1 varsayımıyla mutasyon hacmine etkisi küçük.

Formüller: `commands=20D`, `functionCalls=22D`, `reads=(40+120+8)D=168D`, `writes=(120+10+2)D=132D`. Bootstrap/admin/retry/index-entry/Rules dependent reads/TTL delete ve bandwidth ek maliyetleri hariçtir; bunlar faturalama teklifi değil belge işlem modelidir. Server-side rate limit Firestore yazısı kullanırsa bunun ek işlemleri ayrıca eklenmelidir.

| DAU | Komut/gün | Function çağrısı/gün | Belge read/gün | Belge write/gün | Ortalama komut/s | 10× peak komut/s |
|---:|---:|---:|---:|---:|---:|---:|
| 1.000 | 20.000 | 22.000 | 168.000 | 132.000 | 0,23 | 2,31 |
| 10.000 | 200.000 | 220.000 | 1.680.000 | 1.320.000 | 2,31 | 23,15 |
| 100.000 | 2.000.000 | 2.200.000 | 16.800.000 | 13.200.000 | 23,15 | 231,48 |

B aşaması 15 dakikalık cadence için 96 job/gün; 8 şehir × epoch başına 1 şehir belgesi seçilirse 768 yazı + 96 pointer yazısı/gün, ayrıca input okumaları. Tek küçük snapshot seçilirse 96 snapshot+96 pointer yazısı; şema seçimine bağlıdır, ikisi birlikte varsayılmaz. Per-delivery Tasks seçilirse 3D task/gün ilave olur; ilk öneride sıfırdır.

**PROPOSED:** Kalıcı makbuz büyümesi 100k DAU'da yılda yaklaşık 730 milyon komut kaydıdır (20×100k×365); ayrıntı+index boyutu maliyetin önemli sürücüsüdür. Hacim yükselmeden response compaction/tombstone ve retention tasarımı test edilmelidir; güvenli replay koruması sırf depolama için silinmez. Ledger hacmi ekonomik komut payına bağlı, UNKNOWN. 1 yıl üstteki ledger süre önerisi ürün/operasyon onayı gerektirir.

**UNKNOWN:** Kesin parasal fiyat, bölge, ücretsiz kota, bellek/runtime süresi, egress, backup/PITR, index-entry ücretleri ve gerçek traffic. Para tutarı tahmini yapılmadı. Toplamları tüm komutların 6 yazı yaptığı garantisi olarak yorumlamayın; market ve finalize kombinasyonları daha pahalı olabilir.

**PROPOSED maliyet kontrolleri:** Ekran dışı listener'ı kapat; açılışta bounded page, offset yerine cursor; full-world refresh yok; immutable route/catalog cache; command response'u kullanıp gereksiz reread yapma; index yalnız gerçek query; global write counter yok. Company version/finance yerel contention üretir, correctness için kabul edilir. Ölçüm olmadan sharding veya mikroservis uygulanmaz. [Firestore contention rehberi](https://firebase.google.com/docs/firestore/transaction-data-contention) compute bölgesinin yakınlığı ve transaction davranışını açıklamaktadır.

**PROPOSED başlangıç hedefleri:** Sıcak komut p95 <1 saniye, hata oranı < %1 (beklenen domain redleri hariç), transaction conflict/retry <%5, ortalama read/write komut bütçesi 6/6 ve sapma incelemesi; bunlar ölçülmüş sonuç veya SLA değildir. Cold-start ayrı ölçülür. İzlenecekler: duplicate hit/conflict, claim reject, finance reconciliation farkı, input deny, latency p50/p95/p99, DB contention, snapshot age, Function invocation/duration, active listener, index/storage artışı. Parasal alarm eşiği bütçe sahibiyle belirlenmeli; budget alert otomatik harcama engeli değildir. MEASURE → VERIFY → OPTIMIZE.

## 17 — Revize geliştirme yol haritası

**PROPOSED sıra:** 01 → 02 → 03 → 04 → 05 → 08'in asgari fleet/warehouse temeli → 06 → 07 → 08'in kalan özellikleri → 09 → 10. Faz numaraları korunur, bağımlılıklar düzeltilir: kontrat/teslimat gerçek asset ownership ve assignment invariant'larına dayanır. Salt mock asset'le production teslimatına geçilmez. API sözleşmesi taslağı 01 öncesinde ortak incelenir; tam fixture'lar 03'te.

Tüm fazlarda ortak üretim kapısı: sentetik LOCAL/DEV testleri, hedef proje doğrulaması, insan incelemesi; üretim değişikliği için ayrıca açık onay. PASS sonraki faza teknik hazır olmayı gösterir, otomatik deploy yetkisi vermez. HOLD karar/kanıt eksikliği; BLOCK veri/güvenlik invariant ihlali anlamındadır.

| Faz / hedef | Bağımlılık ve çıktı | Test / acceptance / güvenlik | PASS / HOLD / BLOCK ve üretim kapısı |
|---|---|---|---|
| 01 Environment Foundation | Audit review; backend iskeleti, pinli runtime/lock, emulator, ayrı DEV ve secretsiz ortam seçimi | Wrong-project fail-closed; emulator yokken prod'a düşmeme; CI isolation, minimum IAM | PASS: onaylı DEV+izolasyon; HOLD: bölge/billing/onay eksik; BLOCK: prod credentials/test bağlantısı. Cloud kaynak için açık onay |
| 02 Auth / continuity | 01; UID context, sentetik login/link ve bootstrap identity sözleşmesi | Provider ve linking collision, expired token; UID korunması, owner spoof reddi | PASS: DEV auth akışları; HOLD: provider seçimi; BLOCK: yanlış hesap merge/reset. Production provider değişimi ayrıca onaylı |
| 03 Command infrastructure | 02; versioned schema/fixture, errors, durable receipts, transaction wrapper | Aynı key farklı payload, yarış, commit sonrası timeout, callback retry | PASS: tek ekonomik etki; HOLD: protocol review; BLOCK: çift etki/unauthorized erişim. Prod deployment yok |
| 04 Player / company | 03; player mapping, company bootstrap, state read, migration status | Eşzamanlı createCompany, başka owner, migrationPending, schema version | PASS: tek company ve tutarlı state; HOLD: cardinality/name kararı; BLOCK: orphan/owner ihlali. Legacy mapping onaysız prod bootstrap yok |
| 05 Finance / progression | 04; integer money, ledger, config authority, XP/level/reputation | Negatif/overflow, iki cihaz harcama, formül sınırları ve reward atomikliği | PASS: invariant ve config review; HOLD: unit/formül eksik; BLOCK: para çoğaltma. Legacy balance onayı prod şartı |
| 08A Asgari fleet / warehouses | 05; vehicle/driver/trailer ownership, capacity, şehir/depo ve assignment altyapısı | Çift purchase, busy asset, ekipman uyumu, upgrade sıra/tekillik | PASS: güvenli ownership; HOLD: katalog/şehir kuralı; BLOCK: aynı asset iki işe. Legacy assets mapping ayrı gate |
| 06 Contracts | 05+08A; bounded offer engine, expiry, kabul ve immutable terms | Refresh spam, expiry boundary, concurrent accept, capability, config version | PASS: bir teklif bir kabul; HOLD: ödül/expiry onayı; BLOCK: client ödülü. Eski active contract mapping prod gate |
| 07 Deliveries | 06+08A; start/lazy finalize/claim ve server time | Saat manipülasyonu, multi-device, finalize/start yarışı, reward retry | PASS: bir hak bir ödeme, doğru assignment; HOLD: late/failure kuralı; BLOCK: erken/çift ödeme. In-flight migration provası gerekir |
| 08B Fleet / warehouses tamamlama | 07; ek upgrade, satış/transfer gerekirse market, gelişmiş kapasite | Inventory conservation, P2P iki finance atomikliği, rollback | PASS: kapsamdaki transfer doğru; HOLD: market kapsamı; BLOCK: varlık kaybı/çoğaltma. Marketplace legacy mapping ayrı |
| 09 Dynamic economy | 05–08 stabil ve ölçümlü; B snapshot cadence/epoch | Repeat epoch, stale snapshot, bounded price, event etkileşimi | PASS: ürün değeri+stabilite+maliyet; HOLD: ihtiyaç yok/ölçüm yok; BLOCK: kontrolsüz fiyat/ödül. Canary+config rollback onayı |
| 10 LiveOps / Admin | 03 audit+05 config+09 gerekiyorsa; güvenli yayın, localized event, maintenance | Unauthorized publish, overlap, expiry, TR/DE fallback, rollback | PASS: audit ve client compatibility; HOLD: içerik/yetki kararı; BLOCK: oyuncu admin olabilir. Admin panel ayrı kapsam |

**PROPOSED yatay compatibility işi:** Production discovery, mapping ve restore provası fazlarla paralel ayrı onaylı iş olarak yürür; 01–03'ün sentetik geliştirmesine eski kaynakları önkoşul yapmaz. Production launch öncesinde her domain için karşılığı tamamlanmalıdır.

## 18 — Karar kaydı, riskler ve açık kararlar

Tüm öneriler **PROPOSED**; güven uygulanmış sistem garantisi değildir.

| Karar | Kanıt / alternatif / tradeoff | Güven / bağımlılık / şimdi kesin mi? |
|---|---|---|
| Yeni modüler backend | Kullanıcı yeni backend hedefi + §02 backend yok; eski mimariyi kopyalama yerine bağımsız domain | Yüksek; uygulama sonraki faz; evet yön olarak |
| Aynı repo, Assets dışında | Yerel Unity yapısı; ayrı repo koordinasyon maliyeti | Yüksek; ownership/CI; evet |
| Mevcut PROD'u koru | OBSERVED kayıtlı kullanıcı; yeni proje daha çok kimlik hareketi | Orta; region/IAM/provider discovery; koşullu |
| Ayrı DEV + emulator | Üretim koruma şartı; tek proje namespace izolasyonundan daha güçlü sınır | Yüksek; billing/erişim onayı; yön evet, kaynak oluşturma hayır |
| Callable API | Unity desteği platform belgesi; REST gateway ekstra işletim | Orta-yüksek; SDK cihaz testleri; koşullu |
| Tek finance belgesi | Para tutarlılığı ve cross-device transaction ihtiyacı; çoklu kanonik bakiye riskli | Yüksek; birim ve cüzdan boyutları; konum önerisi evet, legacy mapping hayır |
| Versioned v2 namespace | OBSERVED legacy users/serverState; eski yolları yerinde dönüştürmek compatibility riskli | Orta; tüm legacy writers/rules; isimler geçici |
| Kalıcı idempotency + claim izi | Retry/çift claim tehdidi; kısa TTL eski replay'e açık | Yüksek doğruluk, orta retention; ölçek ölçümü; v1 için evet |
| Şirket transaction/version | İki cihaz; global lock veya client merge gereksiz | Orta-yüksek; tek-company varsayımı ve contention ölçümü; koşullu |
| Lazy delivery | Soft-online ürün hedefi; Tasks/sweep ek maliyet | Orta-yüksek; background kesin zaman ihtiyacı; koşullu |
| A ekonomi + kişisel teklifler | Yeni backend, formüller UNKNOWN; B/C erken karmaşıklık | Orta; ürün onayı; ilk sürüm yönü |
| Cloud Run / P2P / ileri admin ertele | Mevcut yük kanıtı yok; modüler Functions yeterli başlangıç | Orta; ölçüm ve ürün kapsamı; evet şimdi |
| Structured localization | VERIFIED EN/TR/DE temeli; backend cümlesi istemci bağımlılığı yaratır | Yüksek; remote content şeması; evet |

**Açık kararlar — yalnız mimari/geçişi etkileyenler:**

1. DEV projesi sahibi, billing, bölge ve erişim rolleri kimde; kaynak oluşturma kapsamı onaylı mı?
2. Bir UID yalnız bir şirkete mi bağlı; ortak şirket veya çoklu şirket gereksinimi var mı?
3. Para birimi/ölçeği, izinli borç, başlangıç bakiyesi, XP/level/reputation ve unlock formülleri nedir?
4. Mevcut Auth sağlayıcıları ve anonymous linking ihtiyaçları; seçilecek Unity SDK/provider cihaz test matrisi nedir?
5. `money/cash`, `level/companyLevel`, save/serverState ve migration marker semantiği nasıl doğrulanacak; yetkili discovery sahibi kim?
6. Legacy client cutover/uyumluluk penceresi; eski yazıları durduracak mekanizma ve aktif teslimat/kontrat mapping politikası nedir?
7. İlk sürüm P2P market, arka planda anlık delivery tamamlanması veya dinamik ekonomi gerçekten gerektiriyor mu?
8. API v1/fixture onayı, RPO/RTO, audit/receipt retention ve maliyet bütçesinin sahipleri kim?

Eski React Native kodunu bu depoya getirmek zorunlu açık karar değildir. Veri anlamını başka güvenilir, yetkili ve secretsiz kaynaklarla kanıtlamak mümkündür.

## 19 — BACKEND-01 hazırlığı

**PROPOSED — mevcut durum HOLD:** Depo ve dal doğrulaması PASS; audit belgesi üretilebilir. Backend ortam kurulumuna geçmek için bu mimarinin incelenmesi, ayrı DEV kaynak kapsamının açık onayı, bölge/billing/erişim sahipliği ve test izolasyonu planı gerekir. Backend-01 onayı bu raporun oluşturulmasından çıkarılamaz.

Başlamadan netleşmesi gereken minimumlar: `backend/` sınırı, Callable/protocol v1 yönü, DEV/LOCAL config yaklaşımı, sentetik test kuralı, IAM sahibi, onaylı runtime/SDK seçme görevi ve CI güvenlik kapıları. Production schema'nın eksiksiz çözümü BACKEND-01'in salt DEV kuruluşuna şart değildir; buna karşılık herhangi bir production schema değişikliği veya migration için zorunludur.

**PASS:** Bu minimumlar ve ayrı kullanıcı onayı sağlanır, production dışı uygulama planı kabul edilir. **HOLD:** Bölge, erişim, kaynak onayı veya sözleşme sorumlusu eksik. **BLOCK:** Yanlış dal, dosya üzerine yazma gereği, production credential/test bağlantısı, onaysız veri hareketi veya güvenlik invariant ihlali. Mevcut oturumda uygulama başlatılmadı.

## 20 — Nihai denetim kararı ve yapılan işlemler

**BACKEND-00: PASS (belge/kapsam tamamlandı, üretim doğrulaması yapılmadı). BACKEND-01: HOLD (inceleme ve açık uygulama/kaynak onayı bekleniyor). Üretim geçişi: BLOCK (semantik mapping, legacy writer kontrolü, identity ve restore/rollback kanıtları eksik).** Bu üç karar farklı kapılardır.

**VERIFIED:** Yalnız `docs/backend/BACKEND-00_AUDIT.md` yeni dokümantasyon olarak oluşturuldu. Unity gameplay/sunum kodu, Firebase config, uygulama ayarları, `.gitignore`, dependencies veya ortam dosyaları değiştirilmedi. Firebase üretim hizmetlerine bağlanılmadı; gerçek oyuncu kaydı okunmadı/export edilmedi; credentials kullanılmadı. Auth/Firestore yazısı, deploy, cloud resource oluşturma, migration, backup/restore, branch oluşturma, commit, push ve merge yapılmadı.

Bu denetimin kanıtı yerel kaynak ve kullanıcı gözlemleriyle sınırlıdır. Resmi açık Firebase belgeleri yalnız platform davranışını desteklemek için okundu; üretim Console/API erişimi değildir. Oyun veya backend testi çalıştırıldığı iddia edilmez. Sonraki adım yalnız rapor incelemesidir; BACKEND-01 otomatik başlatılmaz.
