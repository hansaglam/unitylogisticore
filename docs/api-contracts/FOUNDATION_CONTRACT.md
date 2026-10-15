# LogistiCore — Foundation Contract

Durum: BACKEND-01. Yalnız `localHealth` uygulanmıştır; oyun komutları için aşağıdaki kurallar geçicidir. Unity dosyaları değiştirilmemiştir.

## Yetki ve veri

İstemci intent/payload gönderir; backend fiyatı, ödülü, bakiyeyi, sahipliği ve zamanı belirler. Firebase Auth kimliği callable'ın trusted auth context'inden gelir. Payload'daki UID/owner bilgisi kimlik kanıtı değildir. Şirket yetkisi ayrıca doğrulanacaktır. Cache, animasyon, dil ve görsel interpolasyon istemcidedir; ekonomik otorite değildir.

## Uygulanan yerel callable

Endpoint: `http://127.0.0.1:5001/demo-logisticore-local/us-central1/localHealth`.

Yalnız Functions Emulator, onaylı demo proje ve zorunlu loopback endpoint'leri ile çalışır. Auth Emulator kimliği gerektirir. App Check enforcement bu yerel fazda yoktur. Hesap linking ve şirket erişimi uygulanmamıştır.

Callable SDK'ya verilen payload:

```json
{ "protocolVersion": 1 }
```

SDK'nin çözdüğü başarılı cevap (zaman sentetik örnek):

```json
{
  "protocolVersion": 1,
  "serverTime": "2026-10-16T12:00:00.000Z",
  "result": { "status": "ok" }
}
```

HTTP üzerinden testte istek `{ "data": { "protocolVersion": 1 } }`, cevap ise callable wire protokolü nedeniyle yukarıdaki cevabı dış `result` alanında taşır. Unity SDK kullanıcıları dış wire zarfını kendileri eklememelidir.

Kimlik yoksa `unauthenticated` (HTTP 401); doğrulanamayan token da reddedilir. Desteklenmeyen protokol, null/array payload veya ek UID gibi alanlar `invalid-argument` (HTTP 400). Handler'ın HttpsError `details` alanı `{code:"UNAUTHENTICATED",messageKey:"errors.unauthenticated"}` veya `{code:"INVALID_ARGUMENT",messageKey:"errors.invalid_argument"}` taşır. Framework kaynaklı token/network hatası aynı details alanına sahip olmayabilir; Unity adapter canonical code'a map etmeli ve fallback metin kullanmalı. Token veya stack trace kullanıcıya gösterilmez.

Health read-only olduğundan `idempotencyKey`, stateVersion, receipt ve gameplay payload yoktur. ServerTime UTC ISO-8601, server tarafından üretilir; yerel UI saati otorite olmaz. Yanıt secret, UID, ortam credential veya oyuncu verisi taşımaz.

## Gelecek komutlar — öneri, uygulanmadı

- `protocolVersion`: backend'in desteklediği API şekli; schemaVersion ile aynı kavram değildir.
- `idempotencyKey`: mutasyon için istemcinin ürettiği, retry boyunca sabit tutulan rastgele işlem anahtarı. Sunucu UID+key+normalized fingerprint ile doğrular. Health'e eklenmez.
- `payload`: sınırlandırılmış intent. Server-generated fiyat/ödül/owner kabul edilmez.
- `result`: yapılandırılmış sunucu sonucu; yerelleştirilmiş cümleye bağlı olmamalı.
- `error.code` ve `messageKey`: gelecekteki ortak domain hata modeli; callable native transport/auth hatalarından ayrılmalı. Henüz bütün oyun endpoint'leri için zarf kesinleşmedi.
- `serverTime`: UTC; client'ın gönderdiği saat ödül veya expiry kararını belirlemez.
- State version: gerekiyorsa domain bazlı (`financeVersion`, `fleetVersion` vb.) veya entity version. Merkezi company belgesini her komutta artırmak bu fazın gereksinimi değildir. İsimler ve kapsam gelecekte onaylanacak.

Gelecek mutasyonların network retry'ı aynı key+payload ile yapılmalı; aynı key farklı payload reddedilmeli. Receipt ile ekonomik etki aynı transaction'da; kalıcı claim tekilliği ve ledger ayrıca tasarlanmalı. Bu garantilerin hiçbiri sağlık callable'ının varlığıyla uygulanmış sayılmaz.

Para wire biçimi minor-unit string adayıdır; currency/ölçek/formül henüz kesin değildir. Legacy `money/cash`, `level/companyLevel` alanlarının eşdeğerliği bilinmez; değer dönüşümü yoktur. Başlangıç bakiyesi veya reward değerleri burada tanımlanmaz.

## Unity/backend koordinasyonu

API değişikliği iki geliştiricinin incelemesine açık olmalı. Yeni breaking shape yeni protocol sürümü gerektirir. Request alanları strict allowlist, response'ta bilinmeyen ek alanlar ileri uyum için tolere edilir. EN/TR/DE metinler messageKey ve parametrelerden üretilebilir; backend TypeScript business logic C#'a kopyalanmaz. Sonraki fazlarda aynı sentetik success/error fixtures hem Unity mock hem backend contract testinde kullanılmalı. Production rollout, minimum client version ve backward compatibility penceresi henüz açık karardır.
