# LogistiCore — Company Contract

Durum: BACKEND-04, **IMPLEMENTED / LOCAL-ONLY**. `createCompany` ve `getMyCompanyState` Gen 2 callable'ları `us-central1` bölgesindedir. Tek proje `demo-logisticore-local`; Functions `127.0.0.1:5001`, Auth `127.0.0.1:9099`, Firestore `127.0.0.1:8080`. Production API veya migration izni değildir.

## Kimlik, şirket ID ve şema

Trusted callable Auth UID zorunludur. BACKEND-02 `v2Players/{uid}` belgesinin schemaVersion=1, authUid=uid, identityStatus=active ve createdAt=Firestore Timestamp invariant'ları korunur. Eksik kimlik oluşturulmaz; önce ensurePlayerIdentity gerekir. Bootstrap yalnız `companyId` ekler; diğer metadata ve createdAt değiştirilmez. Identity schemaVersion yükseltilmez.

Şirket ID politikası: `co_` + trusted UID UTF-8 baytlarının tam SHA-256 küçük harf hex özeti (67 ASCII karakter). Aynı UID her anahtarda aynı ID; farklı UID farklı digest. Bu bir yetki veya gizlilik kanıtı değildir; güvenlik server ownership kontrolünden gelir. UID hash çakışması halinde owner/mapping invariant'ı fail-closed olur; belge overwrite edilmez.

Canonical yollar:

- `v2Players/{uid}.companyId` → deterministic ID.
- `v2Companies/{companyId}` → yalnız schemaVersion=1, ownerUid=uid, name, bootstrapStatus=`foundation_pending_economy`, createdAt=serverTimestamp.
- `v2Players/{uid}/commands/{key}` → BACKEND-03 durable receipt.

updatedAt/stateVersion eklenmedi; henüz metadata mutation yok. Şirketin finans/progression/inventory alt belgesi oluşturulmaz. `economyReady:false` API'nin derived sabit bilgisidir; finansın sıfırlandığı anlamına gelmez. Tanınmayan şirket status/schema, ek alanlar veya uyumsuz name/owner/createdAt güvenli hata verir.

## createCompany — IMPLEMENTED

Unity Callable SDK payload:

```json
{"protocolVersion":1,"idempotencyKey":"synthetic-company-request-001","payload":{"name":"Synthetic Logistics"}}
```

Sentetik SDK başarı cevabı:

```json
{
  "protocolVersion":1,
  "serverTime":"2026-10-16T12:00:00.000Z",
  "result":{
    "companyId":"co_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "name":"Synthetic Logistics",
    "schemaVersion":1,
    "bootstrapStatus":"foundation_pending_economy",
    "economyReady":false
  }
}
```

ID örneği sentetik biçim fixture'ıdır; gerçek ID server tarafından trusted UID'den üretilir, client input değildir. HTTP test zarfı request `data`, response dış `result`; Unity SDK bunları elle eklemez.

Strict envelope BACKEND-03 ile aynı; payload yalnız name. uid, ownerUid, companyId, balance, cash, money, xp, level, reputation, isAdmin, role, startingFunds ve diğer ek alanlar hem üst seviyede hem payload'da reddedilir. Protokol yalnız numeric 1; anahtar ve 8192-byte request sınırları COMMAND_CONTRACT ile aynıdır.

Ad normalizasyonu: raw string en fazla 512 UTF-8 byte; Unicode NFC; Zs space-separator dizileri tek ASCII boşluk; baş/son boşluklar temizlenir. Sonuç 2–64 Unicode code point ve en az bir Unicode harf/rakam içerir. Cc/Cf/Cs/Zl/Zp kategorileri ve Default_Ignorable_Code_Point reddedilir (newline/tab, yönlendirme/zero-width/soft-hyphen/variation-selector dahil). TR/EN/DE harfleri, umlaut/ß/dotted-I korunur; case folding yok. Örnek `  İstanbul  Lojistik ` → `İstanbul Lojistik`, decomposed `Müller` → `Müller`. Global ad tekilliği yok. Ad literal display verisidir; Unity markup olarak yorumlamamalı, rich-text'i kapatmalı/escape etmelidir.

## Atomiklik, concurrency ve replay

`executeCommand` tekrar kullanılır; ayrı idempotency mekanizması yok. Transaction receipt'i okur; scope resolver aynı transaction'da beş sabit belgeyi okur: player, deterministic company, users/{uid}, users/{uid}/serverState/current, v2MigrationRecords/{uid}. Kimlik/eligibility/mapping/company invariant'ları doğrulanır. Bu snapshot yalnız çağrı/transaction-attempt kapsamındaki WeakMap'te tutulur; SDK retry'ında resolver tekrar okur, stale process cache yoktur. Apply aynı snapshot'ı kullanır; yinelenen DB okuması yok.

Geçerli kimliğin companyId alanı hiç yok ve deterministic şirket yoksa create uygundur. Null/boş/farklı companyId, mapping var/company yok veya mapping yok/company var hata verir. Foreign mapping takip edilmez; başka UID şirketi okunmaz. Geçerli mevcut şirket farklı key'de COMPANY_EXISTS döndürür. İlk bootstrap company create + yalnız player.companyId update + receipt create olarak aynı commit'te tamamlanır. Bütün okumalar yazılardan öncedir. Aynı UID+farklı key yarışı da player/company contention sayesinde yalnız tek şirket ve tek başarılı receipt üretir.

Aynı key + aynı normalize name, **güncel kimlik/eligibility/kayıt tutarlılığı geçerliyken**, original committed result ve original serverTime'ı replay eder. Farklı name aynı key'de IDEMPOTENCY_CONFLICT; normalizasyonda eşit ad aynı istektir. Yeni key ad değiştirmez. Replay yazı yapmaz. Daha sonra suspension, migration marker veya kayıt bozulması oluşursa eski receipt bu kontrolleri aşamaz; receipt silinmez/onarılmaz. Receipt geçmiş sonucu gösterir, güncel state yerine geçmez. Kayıp cevapta aynı key/payload ile retry; farklı key ile ikinci bootstrap yok.

## getMyCompanyState — IMPLEMENTED

Request yalnız:

```json
{"protocolVersion":1}
```

Şirketsiz geçerli oyuncu:

```json
{"protocolVersion":1,"serverTime":"2026-10-16T12:00:00.000Z","result":{"hasCompany":false}}
```

Şirketi olan oyuncu `hasCompany:true` ile createCompany result alanlarını alır. Ad/ID yalnız canonical mapping ve ownerUid aynı trusted UID için tutarlıysa döner. Read-only Firestore transaction beş sabit belgeyi tutarlı snapshot'tan okur; create/update/receipt yazısı yapmaz. Taze response serverTime üretir; create replay zamanından farklıdır. Arbitrary UID/companyId kabul etmez; list/query scan yoktur. Foreign/missing/malformed kayıtlar ortak COMPANY_STATE_INVALID ile reddedilir; başka şirket adı/owner/veri dönmez.

## Migration ve progress koruması

Legacy `users/{uid}` veya parent yokken bile `users/{uid}/serverState/current` varlığı; `v2MigrationRecords/{uid}` belgesinin herhangi bir varlığı; player veya company üzerindeki `migration` ile başlayan herhangi bir field (case-insensitive, false/completed dahil) MIGRATION_REQUIRED üretir. Bu konservatif yerel gate'tir: marker anlamını yorumlamaz, tamamlandığını varsaymaz. Unknown legacy money/cash, level/companyLevel değerleri dönüştürülmez. Mapping inconsistency otomatik repair edilmez. Missing/suspended/malformed identity FAILED_PRECONDITION'dır.

Bu sabit yollar dışında üretimde kalmış orphan şirketleri veya bilinmeyen legacy şemaları keşfetmez. Gerçek kullanıcı eligibility kanıtı, UID/owner mapping, authorized discovery, backup/restore ve cutover/migration tasarımı tamamlanmadan production bootstrap **BLOCK**. Sentetik fixture başarısı production discovery/migration kanıtı değildir.

## Hatalar ve localization

| details.code | Callable code | messageKey |
|---|---|---|
| UNAUTHENTICATED | unauthenticated | errors.unauthenticated |
| INVALID_ARGUMENT | invalid-argument | errors.invalid_argument |
| UNSUPPORTED_PROTOCOL | failed-precondition | errors.unsupported_protocol |
| IDEMPOTENCY_CONFLICT | already-exists | errors.idempotency_conflict |
| COMPANY_EXISTS | already-exists | errors.company_exists |
| COMPANY_STATE_INVALID | failed-precondition | errors.company_state_invalid |
| MIGRATION_REQUIRED | failed-precondition | errors.migration_required |
| FAILED_PRECONDITION | failed-precondition | errors.failed_precondition |
| RETRY_LATER | unavailable | errors.retry_later |
| INTERNAL | internal | errors.internal |

Create protokol hatası UNSUPPORTED_PROTOCOL; read'in tek-alan validator'ı BACKEND-02 ile uyumlu biçimde INVALID_ARGUMENT üretir. Read beklenmeyen altyapı hatası INTERNAL; create transient eşlemesi BACKEND-03 ile aynı. Framework token/network hatası details taşımayabilir. EN/TR/DE client key mapping ve güvenli fallback kullanmalı; raw error text/stack gösterilmez.

Sentetik details fixture:

```json
{"code":"MIGRATION_REQUIRED","messageKey":"errors.migration_required"}
```

Company exists/invalid/migration hatasında yeni key ile otomatik bootstrap döngüsü açılmaz. Geçici network hatasında aynı intent/key, bounded backoff. Başarısız işlemler successful receipt oluşturmaz.

## Unity başlangıç akışı ve gelecek sınırı

IMPLEMENTED local akış: SDK Auth → ensurePlayerIdentity → getMyCompanyState → hasCompany=false ise kullanıcı ad intent'i ve yeni key ile createCompany. MIGRATION_REQUIRED/invalid durumda dur ve destek/review göster. Foundation şirketine gameplay satın alma/ödül ekranı hazır kabul edilmez. Cache UID'ye bağlı, non-authoritative; sign-out/UID değişiminde temizlenir. Direct Firestore read/write default-deny, client owner/finans hesaplamaz. Aynı Auth user'a email linking UID'yi/şirket bağını korur; ayrı UID sign-in transfer değildir.

PROPOSED BACKEND-05 transition: versioned, ürünce onaylı finans/progression kuralları ile ayrı authoritative transaction; yalnız uygun foundation şirketini initialize edip explicit economy-ready lifecycle'a geçirir. Bugünkü read strict status/schema nedeniyle bu değişim öncesinde uyumlu sözleşme/review gerekir. Zero balance/XP veya başlangıç currency icat edilmedi.

DEFERRED: name-change ayrı authorized command; company transfer deterministic UID-ID politikasının ayrı migration/ownership tasarımını gerektirir, mevcut create tekrar kullanılamaz. Account merge, ekonomi, oyun varlıkları ve production deployment yok. UNKNOWN: legacy marker anlamı ve production canonical mappings.

## Okuma/yazma maliyeti

Contention retry ve index maliyeti hariç yaklaşık belge sayıları:

| Akış | Reads | Writes |
|---|---:|---:|
| İlk create | 6 transaction (receipt + 5 state) + 1 post-commit receipt | 3 (company/player/receipt) |
| Aynı key replay | 6 | 0 |
| Farklı key duplicate | 6 | 0 |
| getMyCompanyState | 5 | 0 |

Ek read'ler legacy ve replay eligibility koruması içindir. Transaction retry'ları ek read maliyeti getirir; UID başına contention vardır, global counter/hotspot yok. Heartbeat/job/index değişikliği yok. Ölçülmüş production latency veya parasal bütçe iddiası değildir.

## BACKEND-05 lifecycle eki (16 Ekim 2026)

Yukarıdaki BACKEND-04 pending davranışı korunur. IMPLEMENTED: internal economy initializer `economy_ready` durumuna geçebilir; yalnız açık synthetic emulator config ile, public mutation API olmadan. `getMyCompanyState` bu durumda `bootstrapStatus:economy_ready,economyReady:true` döndürür; finance/progression/config kaydı ve pinned hash doğrulanmadan ready kabul edilmez. Pending/companiesiz durumda kısmi ekonomi kaydı fail-closed olur.

`createCompany` ilk receipt replay'i eski pending result/serverTime'ı korur; şirketin güncel durumunu geri almaz. Güncel durum için read API kullanılmalıdır. Farklı key ikinci şirket yaratamaz. Owner/mapping/legacy/migration kontrolleri devam eder. Ayrıntılar: [ECONOMY_CONTRACT](ECONOMY_CONTRACT.md). Gerçek başlangıç değerleri ve üretim initialization hâlâ onaysız ve kapalıdır.
