# LogistiCore — Identity Contract

Durum: BACKEND-02, yalnız LOCAL/EMULATOR. FOUNDATION_CONTRACT.md içindeki BACKEND-01 sağlık sözleşmesine ek sözleşmedir; production API değildir.

## IMPLEMENTED — ensurePlayerIdentity

Gen 2 callable; bölge `us-central1`. HTTP test adresi:
`http://127.0.0.1:5001/demo-logisticore-local/us-central1/ensurePlayerIdentity`.
Auth `127.0.0.1:9099`, Firestore `127.0.0.1:8080`; tek proje `demo-logisticore-local`. Uzak cihaz erişimi/port forwarding mevcut onayın dışında.

Unity Callable SDK payload'ı:

```json
{ "protocolVersion": 1 }
```

SDK'nin çözdüğü sentetik başarılı cevap:

```json
{
  "protocolVersion": 1,
  "serverTime": "2026-10-16T12:00:00.000Z",
  "result": {
    "playerId": "synthetic-player",
    "schemaVersion": 1,
    "identityStatus": "active"
  }
}
```

HTTP testleri isteği `data`, cevabı dış `result` zarfıyla taşır. Unity SDK bunları eklemez. `playerId` yalnız çağıranın trusted UID'sidir. E-posta, token, provider claims dönmez. `serverTime` sunucu cevap zamanı; kalıcı `createdAt` Firestore server timestamp'tir ve cevapta dönmez. İlk çağrı ve retry boyunca identity verisi sabit kalır; `serverTime` her yanıt için ayrı üretilir ve yanıtlar arasında farklı olabilir; created flag, receipt veya ekonomi idempotency anahtarı yoktur.

Firebase callable tarafından doğrulanmış `request.auth.uid` zorunlu. UID boş, bozuk veya belge yolu olarak geçersizse reddedilir. Payload yalnız protocolVersion kabul eder; UID, owner, role, finans ve diğer bütün alanlar reddedilir. Anonymous Firebase Auth kullanıcısı authenticated sayılır; kimliksiz istek değildir.

| Callable code | details.code | details.messageKey | İşlem |
|---|---|---|---|
| unauthenticated | UNAUTHENTICATED | errors.unauthenticated | SDK Auth oturumunu doğrula |
| invalid-argument | INVALID_ARGUMENT | errors.invalid_argument | Payload/protokolü düzelt |
| failed-precondition | IDENTITY_CONFLICT | errors.identity_conflict | Otomatik repair/reset yapma; inceleme |

Framework token/network/internal hatalarında details bulunmayabilir. Unity canonical code ile güvenli fallback anahtarına eşler; stack/token veya teknik mesajı oyuncuya göstermez. EN/TR/DE çevirileri client'ta messageKey üzerinden yönetilir. Geçici network hatasında aynı istek güvenle yinelenebilir; her failed-precondition için retry döngüsü açılmaz. Uygulama loglarına token, e-posta veya credentials eklenmez.

## IMPLEMENTED — identity ve ownership sınırı

`v2Players/{trustedUid}`: schemaVersion=1, authUid=trustedUid, identityStatus=active, createdAt=server timestamp. Transaction create-if-absent; mevcut zorunlu invariant'lar yanlışsa hata. Geçerli belgenin ek alanları ve metadata'sı retry'da korunur; istemciye aktarılmaz. Cash/XP/company/gameplay oluşturulmaz.

`requireCompanyOwner` yalnız sunucu içi primitive; public endpoint değildir. `v2Companies/{companyId}.ownerUid` trusted UID ile karşılaştırılır. CompanyId intent'tir, yetki kanıtı değildir. Gelecek mutasyon aynı transaction'ı checker'a geçirip yazıları o transaction'da yapmalıdır; bağımsız check ardından write güvenli değildir.

İç hata sınıfları: unauthenticated; invalid-argument; not-found/COMPANY_NOT_FOUND/errors.company_not_found; permission-denied/COMPANY_ACCESS_DENIED/errors.company_access_denied; failed-precondition/COMPANY_INVALID/errors.company_invalid. Owner veya şirket verisi hatada dönmez. Gelecek public endpoint şirket varlığı bilgisini de saklamak istiyorsa bu iç ayrımı ortak erişim hatasına map etmelidir. Şirket create/transfer/merge yoktur.

Direct Firestore get/list/create/update/delete tüm istemciler için default-deny. Identity response UID'ye bağlı cache edilebilir; sign-out/UID değişiminde temizlenir. Cache sahiplik, bakiye, oyun zamanı veya yetki kaynağı olamaz. Unity finans, ödül, sahiplik veya account transfer kararı vermez.

## IMPLEMENTED test kapsamı / PROPOSED istemci akışı

Firebase SDK interactive sign-in/linking Unity'nin sorumluluğudur. Anonymous hesabın aynı Auth user'ına credential linking başarılı olursa UID korunmalıdır. Backend sağlayıcıdan bağımsız yalnız UID kullanır. Yerel SDK anonymous→email linking, ikinci Auth instance ile email sign-in ve credential collision emülatör testlerinin sonuçları BACKEND-02_REPORT.md'dedir.

Ayrı sign-in farklı UID döndürürse farklı hesap kabul edilir; önceki misafir kimliği/şirketi taşınmaz. Collision'da link başarısız kalır; UI mevcut hesabı kurtarma/sign-in seçeneğini açıkça sunmalıdır. Aynı e-posta veya client'ın claimed UID'si merge kanıtı değildir. Otomatik merge yoktur. SDK işlemi başarılı olmadan client cache'i yeni hesaba taşımayın. Firebase Auth dışı parola sistemi yoktur.

Platform davranışı: [Firebase account linking](https://firebase.google.com/docs/auth/web/account-linking). Emulator kanıtı production token/provider güvenliği veya gerçek oyuncu continuity kanıtı değildir.

## DEFERRED / UNKNOWN

DEFERRED: Apple/Google gerçek entegrasyonu, cihaz testleri, recovery UI, duplicate account çözümü, App Check, production bootstrap/migration, command receipts, şirket oluşturma ve ekonomi. Production deployment BLOCK.

UNKNOWN: mevcut production providers/UID mapping, legacy canonical kayıt, money/cash ve level/companyLevel eşdeğerliği. Migration öncesinde yetkili discovery, legacy kayıt tespiti ve eligibility, UID/ownership kanıtı, link/collision politikası, duplicate çözümü, backup/restore ve rollback provası gerekir. Mevcut callable production'da legacy kullanıcıya boş profil oluşturma yetkisi vermez.

Unity geliştiricisiyle açık sorular: SDK sürümü ve platform matrisi; guest giriş UX/onayı; collision/recovery ekranı; EN/TR/DE key eşlemesi; UID değişiminde cache temizleme; loopback mobil test düzeni (uzak erişim ayrı güvenlik onayı ister). Bu belge C# veya Unity dosyası uygulamaz.
