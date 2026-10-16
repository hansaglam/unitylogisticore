# ECONOMY_CONTRACT — BACKEND-05

16 Ekim 2026. **IMPLEMENTED, yalnız `demo-logisticore-local` emülatörleri. Production BLOCK.**

## Yetki ve etkinleştirme

Client intent gönderir; Auth UID, canonical company mapping, owner ve migration kontrolleri transaction içinde tekrar doğrulanır. `runEconomyCommand` yalnız backend içi mekanizmadır; hiçbir callable bu mutation mekanizmasını çağırmaz. Trusted server selector `{kind,id}` request payload'ından alınamaz. Request payload kesinlikle `{}` olmalıdır; executor protocolVersion/idempotencyKey kullanır. Yeni public mutation/admin/config writer yoktur.

Gerçek ürün konfigürasyonu **DEFERRED**. Yalnız `approval: synthetic_emulator_only` işaretli konfigürasyon/entitlement kabul edilir. Ortam guard'ı her girişte local project ve loopback uçlarını zorunlu tutar. Kaynak kodunda para, başlangıç XP'si veya threshold varsayılanı yoktur. Test fixture sayıları ürün kararı değildir.

## Yapılandırma ve canonical state

`v2WorldConfig/{version}` tam alan kümesi: `schemaVersion:1`, `version`, `approval`, `currency` (üç büyük ASCII harf), `scale` (0–6), `maxBalance`, `startingBalance`, `maxXp`, `initialXp`, `thresholds`, `rulesVersion`. ID/version 1–64 alfanümerik/alt çizgi/tire, ilk karakter alfanümerik. Config açık version ile seçilir; aktif pointer veya otomatik publish yoktur. Currency biçimi doğrulaması, gerçek ürün para birimi onayı değildir.

Bütün miktarlar Firestore'da `Number.isSafeInteger` ile doğrulanan integer sayılardır. Bakiye 0..config.maxBalance, XP 0..config.maxXp; üst sınırlar en fazla Number.MAX_SAFE_INTEGER. Delta signed safe integer; toplama sonucu yeniden sınanır. Negatif bakiye, float, NaN, Infinity, overflow, string persistent miktar reddedilir. Wire bakiye decimal integer string'dir; XP ve level bounded integer'dır. Double kullanımı yalnız exact safe integer aralığındadır; yuvarlama yapılmaz.

Config canonical JSON SHA-256 hash'i iki state kaydına pin edilir. Aynı version içeriğini sonradan değiştirmek ready state'i fail-closed yapar; sessiz kural güncellemesi yoktur. Sürüm geçişi ayrı migration tasarımı gerektirir. Threshold listesi 1–1000 eleman, ilk değer 0, strictly increasing safe integer ve maxXp içinde. Level, XP'nin ulaştığı threshold sayısıdır (1-based). Reputation **DEFERRED**, alanı eklenmedi.

Tek wallet: `v2Companies/{companyId}/state/finance`:
`schemaVersion, balance, currency, scale, configVersion, configHash, updatedAt`.
Progression: `v2Companies/{companyId}/state/progression`:
`schemaVersion, xp, level, rulesVersion, configVersion, configHash, updatedAt`.
Timestamp alanları server timestamp; bozuk/eksik/ek alan, yanlış para birimi/scale, yanlış level/config hash onarılmadan reddedilir. Player/company root'ta ikinci balance yoktur.

## Initialization ve domain kaynakları

`initialize` trusted selector ID'si config version'dır. Foundation-only company, legacy/migration engeli olmaması ve finance/progression'ın ikisinin de yokluğu gerekir. Transaction company lifecycle'ı `economy_ready` yapar, finance/progression, initialization claim, initial ledger (delta sıfır olsa da) ve receipt oluşturur. Eksik/onaysız config hiçbir kayıt yaratmaz. Aynı key historical replay; farklı key state veya kalıcı initialization claim nedeniyle reddedilir.

**PROPOSED:** ayrı onaylanmış onboarding backend orchestration adımı, gelecekteki `completeCompanyOnboarding` domain handler'ı içinde ürün/migration/rollout eligibility doğrulandıktan sonra internal initializer çağrılabilir. Bu handler mevcut değildir. `createCompany` initializer çağırmaz; gerçek initialization kapalıdır.

`entitlement` selector ID'si sunucunun verdiği gerçek `v2Companies/{companyId}/entitlements/{id}` kaydını arar. String ID tek başına ödül kanıtı değildir. Tam şema: `schemaVersion:1, approval, ownerUid, companyId, configVersion, configHash, currency, scale, delta, xpDelta, reason`. Owner/scope/config hash doğrulanır. Bu aşamadaki reason'lar yalnız `synthetic_debit` (negatif delta, sıfır XP), `synthetic_credit` veya `synthetic_reward` (nonnegative delta/XP). Ürün purchase/reward katalogları, entitlement üreticisi ve gerçek ekonomik rule set **DEFERRED**. Test dışında entitlement writer yoktur. Güvenilir Admin erişimi config/entitlement authority sınırıdır; default-deny Rules Admin'i kısıtlamaz.

Kalıcı claim path'i `.../claims/entitlement_{id}`; initialization için `.../claims/initialization`. Şema `schemaVersion, source, operationId, configVersion, committedAt`. Kaynak başına aynı/farklı key tek consumption; TTL/silme yoktur. Kaynak ID tekrar kullanılmaz.

Ledger path `.../ledger/{operationId}`; ID server tarafından SHA-256(companyId + ':' + source) türetilir. Alanlar `schemaVersion, operationId, reason, delta, balanceAfter, currency, scale, source, configVersion, rulesVersion, committedAt`. Create-only; mevcut ledger/claim reddedilir; global sayaç veya TTL yoktur. Ledger tarihsel kanıttır, wallet değildir. XP-only işlemde de zero-delta kanıt kaydı tutulur. Claim, finance, progression, ledger ve receipt aynı transaction'da commit edilir. Yetersiz bakiye veya XP overflow hiçbir kısmi yazı bırakmaz.

## Replay, retry ve compatibility

BACKEND-03 executor kullanılır. Receipt `v2Players/{uid}/commands/{key}`; fingerprint server selector, command ve canonical company scope içerir. Transaction callback yeniden çalışabilir; dış side effect yoktur. Aynı key ilk result/serverTime'ı korur. Commit sonrası yanıt kaybolursa aynı key ile tekrar denenir. Farklı key domain claim'i aşamaz. Eşzamanlı spends Firestore transaction conflict/retry ile güncel bakiyeye göre değerlendirilir.

BACKEND-04 historical createCompany receipt'i pending sonucu ve eski zamanı korur; canlı ready state'i değiştirmez. Yeni key ikinci company oluşturamaz. `getMyCompanyState` hazır olduğunu yalnız geçerli config/finance/progression ile bildirir. Pending veya companiesiz durumda orphan state varsa fail-closed. Replay dahil güncel ownership/migration/state uygunluğu aranır; bozuk güncel state, historical receipt varlığıyla bypass edilemez.

## getMyEconomyState

IMPLEMENTED local callable, `us-central1`, maxInstances 1. Auth zorunlu. Tek request: `{"protocolVersion":1}`; UID/companyId/amount veya ek alan reddedilir. Read-only transaction; create/repair/receipt/ledger query yoktur.

Pending sentetik response:
```json
{"protocolVersion":1,"serverTime":"2026-10-16T12:00:00.000Z","result":{"hasCompany":true,"economyReady":false}}
```
Companiesiz: `hasCompany:false,economyReady:false`. Pending durumda balance/XP/level alanı yoktur.
Ready sentetik result (sayılar yalnız test örneği):
```json
{"hasCompany":true,"economyReady":true,"companyId":"co_<sha256>","balance":"100","currency":"TST","scale":2,"xp":0,"level":1,"configVersion":"synthetic_fixture","rulesVersion":"synthetic_v1"}
```
Read `serverTime` response anıdır; command replay ilk commit zamanıdır. Unity EN/TR/DE sunum ve yerelleştirmeyi yapar, wire değerlerini otorite veya fiyat kaynağı olarak kullanmaz. balance string'i float para aritmetiğine çevrilmez. Sabit `details.code/messageKey`: mevcut `UNAUTHENTICATED/errors.unauthenticated`, `INVALID_ARGUMENT/errors.invalid_argument`, `UNSUPPORTED_PROTOCOL`, `IDEMPOTENCY_CONFLICT`, `COMPANY_EXISTS`, `COMPANY_STATE_INVALID`, `MIGRATION_REQUIRED`, `FAILED_PRECONDITION`, `RETRY_LATER`, `INTERNAL` (diğer key'ler `errors.` + lowercase code). Ekonomi/config/claim/bounds hataları generic FAILED_PRECONDITION; özel veri sızdırmaz. Receipt retry yalnız aynı key ve aynı intent ile yapılır.

## Maliyet ve operasyon sınırı

Sabit direct document okumaları: company/economy read pending 7, ready 8; createCompany yeni işlem 8 read/3 write; init 11 read/6 write; entitlement 12 read/5 write. Receipt replay pending 8/ready 9 read, write 0. Başarılı yeni command timestamp çözmek için ayrıca 1 receipt read yapar. Retry okumaları tekrarlar; bunlar SDK logical read sayıları, fiyat garantisi değildir. Liste taraması, timer, Pub/Sub, global lock, scheduler yoktur. Tek company wallet üzerinde contention ileride yük testi gerektirir.

## Karar bekleyenler

**UNKNOWN:** gerçek currency/scale, başlangıç fonu/XP, XP thresholds/maxima, reputation, legacy money/cash ve level/companyLevel anlamları, migration eligibility, rollout policy ve gerçek entitlement kuralları. **DEFERRED:** üretim onayı/dağıtımı, gerçek DEV, CI güvenlik onayı, version migration, purchases/contracts/deliveries/market. Emulator PASS üretim güvenliği veya ekonomi dengesi onayı değildir.
