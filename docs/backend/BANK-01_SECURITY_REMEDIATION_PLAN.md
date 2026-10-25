# BANK-01 SECURITY-01 — Dependency remediation plan

24 Ekim 2026. **SECURITY-01: HOLD (uygulama onayı bekliyor).** Araştırma tamamlandı; hiçbir dependency düzeltilmedi. BANK-01 functional/local security HOLD, general security HOLD; production, real DEV, migration, deploy ve untrusted CI BLOCK.

## 1. Kapsam ve çalışma ağacının korunması

Doğrulanan root `D:/Projects/unitylogisticore`, branch `feature/bank-01`, HEAD `bbacf04af40f1973787f40e9c7524defaf62b322`. Başlangıçta14 tracked modification +8 untracked BANK-01 dosyası vardı. Bunlar ve iki manifest/lockfile için SHA256 başlangıç kaydı ignored `backend/.local/security-01-preservation-before.json` içine alındı. Önceki test/audit raporları üzerine yazılmadı; yeni kanıtlar ayrı `security-01-*` adları kullanır. Unity değişikliği yapılmadı. Branch switch, stash/reset/clean, commit/push/PR yok.

Bu fazın tek yeni reviewable belgesi bu dosyadır. BANK-01_REPORT veya feature source/test dosyaları değiştirilmez. `npm install`, `npm update`, `npm ci`, `npm audit fix` ve emulator çalıştırılmadı. Aşağıdaki mutasyon komutları yalnız gelecekteki açık onay içindir.

Son preservation kontrolü:22 mevcut BANK-01 dosyası +2 manifest +lockfile toplam25 dosyanın SHA256 değeri başlangıçla aynı;0 değişim. Git durumuna yalnız bu yeni belge eklendi (toplam23 modified/untracked). Protected Unity/manifests/lock diff boş; git diff --check geçti. Lock SHA256 `3C2CC424D6651A69181A15A5D6ADDD8E4994983E23DB4B8745B794E4968C97D6`. Son status kanıtı ignored security-01-preservation-after.txt.

## 2. Gerçek inventory ve audit

Node22.23.2 / npm10.9.8. Root engine22.x; Functions22. Direct sürümler: firebase-functions7.4.0, firebase-admin14.5.0, firebase-tools15.34.0, firebase12.19.0, rules-unit-testing5.0.2, TypeScript5.9.3. Mevcut tek override `@firebase/firestore -> @grpc/grpc-js:1.13.6`; değiştirilmedi.

24 Ekim yeni registry taramaları başarıyla sonuçlandı; iki audit'in exit1 olması advisory bulgularındandır, registry erişim hatası değildir:

| Komut | Gerçek sonuç |
| --- | --- |
| npm audit --omit=dev --json --fetch-retries=0 --fetch-timeout=20000 | **1 HIGH**, body-parser runtime |
| npm audit --json --fetch-retries=0 --fetch-timeout=20000 | **1 HIGH +6 MODERATE** affected package;0 critical |

Yedi affected package: body-parser, fast-uri, @opentelemetry/core, @google-cloud/pubsub, uuid, gaxios, firebase-tools. Distinct aktif advisory7; fast-uri4 advisory'si bir moderate package kaydına toplanır. Aynı paket farklı parent'larda kullanılıyor diye bağımsız exploit ispatı sayılmaz. `npm ls`/`npm explain`, disk/lock sürümleri ve parent ranges birlikte kullanıldı; yalnız manifest aralığına dayanılmadı.

Kanıt dosyaları (ignored backend/.local): security-01-{runtime-audit,full-audit,tree,explain-body,explain-uri,residual-tree}.json; security-01-parent-ranges.jsonl; security-01-residual-ranges.txt; security-01-metadata-{body-parser-1.20.9,body-parser-2.4.0,fast-uri-3.1.9,firebase-tools-15.34.1}.json. Patch metadata için `npm view <exact-version> version engines dependencies peerDependencies dist.integrity --json` kullanıldı.

## 3. Advisory durumu ve erişilebilirlik

### body-parser — öncelikli HIGH

[GHSA-27r3-jxmh-p828](https://github.com/advisories/GHSA-27r3-jxmh-p828): upstream22 Ekim; GitHub database23 Ekim2026, aktif HIGH. Etkilenen1.x<1.20.9 ve2.x>=2.0.0<2.4.0; patch1.20.9/2.4.0. Sıkıştırılmış HTTP body ve trailing bytes, decoder'ın yakalanmayan hatasıyla process DoS oluşturabilir. Parser routing/auth öncesinde çalışıyorsa auth koruması yeterli değildir. Exploit veya crash testi yapılmadı.

Kesin kurulu yollar:

| Sınıf | Zincir | Fiziksel body-parser kaydı |
| --- | --- | --- |
| Runtime | functions workspace → firebase-functions7.4.0 → express5.3.0 → body-parser2.3.0 | node_modules/firebase-functions/node_modules/body-parser |
| Dev | firebase-tools15.34.0 → body-parser1.20.8 | node_modules/body-parser |
| Dev | firebase-tools15.34.0 → express4.22.3 → body-parser1.20.8 | Aynı1.x kayıt |
| Dev | firebase-tools15.34.0 → exegesis4.3.0 → body-parser1.20.8 | Aynı1.x kayıt |
| Dev | firebase-tools15.34.0 → exegesis-express4.0.0 → exegesis4.3.0 → body-parser1.20.8 | Aynı1.x kayıt |
| Dev | firebase-tools15.34.0 → @modelcontextprotocol/sdk1.32.1 → express5.3.0 → body-parser2.3.0 | node_modules/@modelcontextprotocol/sdk/node_modules/body-parser |

Kaynak incelemesi: CLI `lib/emulator/functionsEmulatorRuntime.js:544–561` json/text/urlencoded/raw parser'larını32mb limit ve rawBodySaver ile **handler öncesinde** kuruyor; inflate:false yok. CLI1.20.8 parser'a desteklenen Functions emulator yolundan erişim kod düzeyinde doğrulandı. Auth `lib/emulator/auth/server.js:335` json parser kullanır; ExpressBasedEmulator:27 de parser kurar. Loopback erişebilen saldırganın encoded body göndermesi gerekir. Loopback exposure'ı sınırlar, trusted machine varsayımını otomatik olarak yeni risk onayına dönüştürmez.

SDK express5.3.0'ın `lib/express.js` parser export'ları body-parser2.3.0'a gider. SDK discovery server express kullanır; ancak sırf paket mevcut diye discovery'de parser'ın kurulduğu veya deployed runtime exploit yolu kesinleşmiş sayılmaz. Gerçek deployment incelenmedi/bağlanılmadı. Runtime HIGH audit bulgusu ve CLI'nin doğrudan parser yolu birlikte remediation gerektirir. Uygulama callable'ına eklenen middleware, daha erken çalışan CLI parser'ını kontrol etmez.

### fast-uri — dört farklı advisory, dev dependency

Hepsi upstream19 Ekim / GitHub database23 Ekim2026, aktif. Kurulu3.1.8 için ortak patched hedef **3.1.9**; diğer major hatların patched sürümleri2.4.8 ve4.2.3'tür, bu repo için major geçiş önerilmez.

| Advisory | Severity | Savunmasız davranış / gerekli girdi |
| --- | --- | --- |
| [GHSA-w74x-v6f9-j3xg](https://github.com/advisories/GHSA-w74x-v6f9-j3xg) | Moderate | Percent-encoded scheme normalizasyonu; normalize edilen URI'nin uygulama tarafından güvenilir/renderable sayılması gerekir |
| [GHSA-wjr9-j795-jxcr](https://github.com/advisories/GHSA-wjr9-j795-jxcr) | Moderate | WHATWG host canonicalization farkı; saldırgan URI'sinin host izin/karşılaştırma kararında kullanılması gerekir |
| [GHSA-333x-w4r6-6crp](https://github.com/advisories/GHSA-333x-w4r6-6crp) | Low | Suffix reference ilk segmentindeki colon üzerinden scheme injection; saldırgan reference ve consumer güven kararı gerekir |
| [GHSA-xhq7-xmqj-769h](https://github.com/advisories/GHSA-xhq7-xmqj-769h) | Low | Relative path normalizasyonunda yanlış equivalence; eşitliği yetki/kimlik olarak kullanan consumer gerekir |

`firebase-tools15.34.0 → ajv8.20.0 → fast-uri3.1.8` tek fiziksel fast-uri kaydıdır. Ek yollar CLI → exegesis4.3.0 → ajv8.20.0; CLI → exegesis-express4.0.0 → exegesis4.3.0 → ajv; CLI → @modelcontextprotocol/sdk1.32.1 → ajv; exegesis → ajv-formats2.1.1 peer ajv8.20.0. `npm explain` hepsini aynı dev kaydına bağlar.

AJV `dist/runtime/uri.js:3` fast-uri kullanır; `dist/compile/resolve.js` schemaId/ref için resolver çağırır. Bu kullanım kodda doğrulandı. BANK-01 trusted local config/workflow'da saldırgan URI'nin security-sensitive consumer'a ulaştığı **doğrulanmadı**; XSS/SSRF sömürülebilirliği varsayılmaz. Storage/MCP/untrusted schema akışlarını açmak mevcut güven sınırını genişletir. Patch mevcut aralıkta olduğundan workaround yerine yükseltme önerilir.

### Önceki istisnaya ait kalan bulgular

[GHSA-8988-4f7v-96qf](https://github.com/advisories/GHSA-8988-4f7v-96qf): upstream12 Haziran/database15 Haziran2026,1 Eylül güncelleme, aktif Moderate. CLI15.34.0 → @google-cloud/pubsub5.3.1 → @opentelemetry/core1.30.1 (`^1.30.1`). Patch2.8.0 mevcut major aralığının dışındadır. Büyük attacker-controlled W3C baggage extractor girdisi memory exhaustion riski taşır. Pubsub telemetry-tracing.js:68,536,577 TraceContext propagator kullanıyor; desteklenen launcher Pub/Sub açmıyor. Baggage extractor'a supported workflow yolu doğrulanmadı. Global core major override önerilmez.

[GHSA-w5hq-g745-h8pq](https://github.com/advisories/GHSA-w5hq-g745-h8pq): upstream19 Nisan/database22 Nisan2026,21 Mayıs güncelleme, aktif Moderate. CLI15.34.0 → gaxios6.7.1 → uuid9.0.1; CLI → google-auth-library9.15.1 → gaxios6.7.1 ve auth → gcp-metadata6.1.1/gtoken7.1.0 → gaxios6.7.1 ek yolları aynı dev uuid'ye gider. `^9.0.1` patch11.1.1/12.0.1/13.0.1'i kabul etmez. Caller-provided buffer/offset ile v3/v5/v6 gerekir; gaxios.js:417 argümansız v4 çağırır. Bu consumer'dan savunmasız yola erişim bulunmadı. Native RCE iddiası yapılmaz. Admin'in gaxios8 yolları bu uuid9 bulgusuna dahil değildir.

[GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm):18 Eylül yayın; **23 Ekim2026 withdrawn**, pratikte exploit edilebilir olmadığı gerekçesiyle. CLI15.34.0 → chokidar3.6.0 → braces3.0.3 kurulu kalmıştır. Current audit'te yokluğu suppression veya installed upgrade sonucu değildir. Tarihsel kayıt korunur.

## 4. Option A — önerilen en küçük değişiklik

**Yalnız backend/package-lock.json hedefli refresh; hiçbir direct dependency/override değişikliği yok.** Semver karşılaştırmaları kurulu parent manifest/lock verisiyle doğrulandı:

| Parent | Mevcut range | Hedef | Range uyumu |
| --- | --- | --- | --- |
| express5.3.0 (SDK ve MCP kopyaları) | body-parser ^2.3.0 | 2.4.0 | Evet |
| firebase-tools15.34.0 | body-parser ^1.19.0 | 1.20.9 | Evet |
| express4.22.3 | body-parser ~1.20.5 | 1.20.9 | Evet |
| exegesis4.3.0 | body-parser ^1.18.3 | 1.20.9 | Evet |
| ajv8.20.0 | fast-uri ^3.0.1 | 3.1.9 | Evet |

Node22.23.2, body-parser2.4.0'ın >=18 ve1.20.9'un >=0.8 engine'ini karşılar. fast-uri3.1.9 metadata'sında engine kısıtı/dependency yok; bu runtime uyumluluk testi yerine geçmez. Functions7.4.0 >=18; CLI15.34.0/15.34.1 Node22'yi destekler. Admin ve client/testing SDK sabit tutulur; TypeScript public interface değişikliği hedeflenmez.

**Kesin hedef sürüm haritası (uygulanmadı):**

```text
node_modules/body-parser: 1.20.8 -> 1.20.9
node_modules/firebase-functions/node_modules/body-parser: 2.3.0 -> 2.4.0
node_modules/@modelcontextprotocol/sdk/node_modules/body-parser: 2.3.0 -> 2.4.0
node_modules/fast-uri: 3.1.8 -> 3.1.9
```

Her kayıtta version/resolved/integrity ve yayımlanmış dependency metadata güncellenir. Metadata2.4.0, qs aralığını^6.16.1, iconv-lite^0.7.3, content-type^2.1.0 yapar; lock'ta zaten qs6.16.1, iconv-lite0.7.3 ve ilgili content-type2.1.0 kayıtları vardır. Bu nedenle yeni alt paket sürümü zorunluluğu görünmüyor; npm hoisting/dedup sonucu yol düzeni değişebilir. 1.20.9'un alt dependency aralıkları aynı; fast-uri dependency'sizdir. Sadece version/integrity'yi elle değiştirip2.4.0'ın metadata'sını eski bırakmak uygun değildir.

Yama integrity'leri registry'den doğrulandı:

```text
body-parser1.20.9 sha512-FISIQxV5u91Y9gvuFq3q/tA/qTqLJn3AhRcEvyozyhJTh5OBFFtv827AdHU7Rbyct4r+Ba1/99oFa1Y3yuFsdw==
body-parser2.4.0  sha512-Ifk1KcLrxaGt1occQagQNFNNOqRt1LFuehEnHHzB8Z6cRgIxRU1TBaiIsUiCdA+gy5PL7Y1IX31DNKkRmxtXjQ==
fast-uri3.1.9     sha512-hS/0Wt3Ya043oKXs0sIxrMpKQITXhMLIrVAKKXi4gozGC9v7YRWVnXIZzjNPTO02M9BdxLCWqK1yRXgNziVyJQ==
```

**Yalnız ayrı onay SONRASI**, backend workspace root'ta:

```powershell
Set-Location D:\Projects\unitylogisticore\backend
npm update body-parser fast-uri --package-lock-only --ignore-scripts
git -c safe.directory=D:/Projects/unitylogisticore diff -- package-lock.json package.json functions/package.json
```

Bu komut izin verilen aralıklardaki paketleri resolve eder, exact sürüm sabitlemez. Bu nedenle sonucu doğrudan kabul etmek yasaktır: yukarıdaki dört hedef ve gerekli metadata/hoisting dışındaki version değişimi, yeni parent/direct upgrade veya yeni advisory görülürse durup diff ayrı incelenir. Registry daha yeni sürüm verirse burada onaylanan minimum yama sayılmaz. Bütün 1.x/2.x kopyalar ve resolver parent edges doğrulanmalıdır. Bu araştırmada resolver çalıştırılmadığı için byte-exact unified lock diff veya başarılı npm resolution **iddia edilmez**; sürüm/metadata hedefi kesin, gerçek npm diff'i onay sonrası üretilecek ve kontrol edilecektir. Önceki BANK-01 kaynak çalışması otomatik geri alınmaz.

## 5. Alternatifler

**B — dar overrides:** Şu anda gerekli değil; bütün ranges uyumlu. Resolver bir kopyayı güncellemezse önce neden incelenir. Ayrı onaylı fallback örneği, mevcut grpc override korunarak `express@5.3.0 -> body-parser:2.4.0`, `express@4.22.3 -> body-parser:1.20.9`, `firebase-tools@15.34.0 -> body-parser:1.20.9`, `exegesis@4.3.0 -> body-parser:1.20.9`, `ajv@8.20.0 -> fast-uri:3.1.9` scope'larıdır. Bu, manifest+lock değiştirir ve bakım yükü yaratır. Tek global body-parser2 override1.x consumer'ları bozabilir; önerilmez. OpenTelemetry/uuid major override kapsam dışı.

**C — parent updates:** Yeni bulgular için gereken minimum parent sürümü zaten kurulu parent'tır. Functions7.4.0/CLI15.34.0/express5.3.0/4.22.3/ajv8.20.0 korunabilir. Registry'den ayrıca CLI15.34.1 kontrol edildi: body-parser^1.19.0, ajv^8.17.1, gaxios^6.7.0, pubsub^5.2.0 aralıkları değişmemiş; yalnız CLI patch yükseltmek lock refresh ihtiyacını veya eski iki advisory'yi ortadan kaldırmaz. SDK/CLI/Admin/client major upgrade/downgrade gerekmez. En düşük riskli aday A'dır; parent davranış değişikliklerini eklemek gereksizdir.

**D — workaround:** Upstream body-parser `inflate:false` önerir; yalnız ilgili parser'ı gerçekten kuran kod üzerinde etkili olur. Bu projede Firebase CLI parser'ı uygulama kontrolünde değildir. User callable middleware'i parsing sonrası çalışır; node_modules patch, launcher fork veya sıkıştırılmış body proxy filtresi bu minimum çözüm değildir. URI girişlerini filtrelemek AJV'nin tüm internal kullanımlarını uygulamadan düzeltmez. Güvenli patched path mevcut olduğundan workaround/risk widening önerilmez.

## 6. Beklenen kalan risk ve karar

A başarılı uygulanırsa beklenen runtime audit0, yeni body-parser+fast-uri advisory'lerinin yokluğu; full audit'te eski OpenTelemetry+uuid ile bunların parent'ları kalabilir (mevcut graph için yaklaşık5 moderate affected package). Bu **tahmin**, son audit sonucu değildir. Current full audit temiz değildir. Patch sonrası yeni risk bulunursa HOLD devam eder; kalan legacy iki advisory eski trusted local değerlendirmesiyle tek tek eşleştirilmelidir.

Yeni advisory'ler gerçekten giderilir, güven sınırı değişmez ve gerekli testler geçerse yeni body-parser/fast-uri risk istisnası gerekmez; mevcut16–30 Ekim yerel istisnası yalnız eski kalan riskler için değerlendirilebilir, süresi uzatılmaz. Dependency değişimi için bu planın açık uygulama onayı gerekir. Yeni açık bırakılırsa eski istisna kendiliğinden genişlemez; proje sahibi ayrı risk kararı vermelidir. Production/CI/migration ayrı BLOCK kararlarıdır.

## 7. Onay sonrası doğrulama prosedürü — henüz yapılmadı

1. Branch/root/HEAD/status, mevcut22 BANK-01 dosyası, manifest ve lock hash'lerini yeniden kaydet; Unity ve eski .local kanıtlarını koru. Node22/npm10 kullan; yalnız backend workspace.
2. Option A lock-only komutunu çalıştır; JSON package-entry diff ile her değişen resolved/version/integrity/dependency'yi incele. Direct deps/override aynı, scope yalnız lock olmalı. Yeni paket/major/unrelated drift olursa devam etme.
3. Onaylanan lock hash'ini kaydet; **npm ci** çalıştır. Lock değişmeden başarılı ci reproducibility kanıtıdır; install başarısızsa sebep araştırılır, temiz kabul edilmez. Bu normal workspace installation'dır, Unity altında çalıştırılmaz. ADC/production credential kullanılmaz; mevcut documented isolation korunur.
4. `npm ls --all --json`, `npm ls body-parser fast-uri --all`, `npm explain body-parser`, `npm explain fast-uri`; tüm fiziksel kopyalar+resolved parent edges kontrol edilir. Minimum hedef1.20.9/2.4.0/3.1.9, eski1.20.8/2.3.0/3.1.8 kalmamalı. Bir patched kopya öteki vulnerable branch'i gizleyemez.
5. Ayrı taramalar: `npm audit --omit=dev --json --fetch-retries=0 --fetch-timeout=20000` ve `npm audit --json --fetch-retries=0 --fetch-timeout=20000`. Runtime0 ve beş yeni advisory'nin tüm graph'tan kalkması gerekli; full findings saklanır. Eski iki dev advisory dışında risk varsa emulator öncesi HOLD. Registry failure temiz audit sayılmaz.
6. `npm run validate`: build, typecheck, bütün unit, gerçek Auth9099/Firestore8080/Functions5001 emülatör testleri; yalnız demo-logisticore-local, loopback. Yeni güvenlik kararı uygun olmadan başlatılmaz. Önceki30/30 bu yeni lock'ın testi değildir.
7. `node --test tools/economy-sim/model.test.mjs tools/economy-sim/bank-model.test.mjs`:75 offline regression. Mevcut ECONOMY-00 testinde eski base'e göre manifest/lock freeze vardır; yetkili remediation lock değiştirince bu test beklenen şekilde FAIL verebilir. Testi sessizce kapatmak/başarılı saymak yasaktır. **Option A dependency yamasından ayrı, açık test-maintenance onayı** ile koruma kontrolünün onaylı lock baseline/hash'e bağlanması gerekebilir; testin diğer Unity/isolation korumaları korunur. Bu faz o dosyaya dokunmaz. Bu bilinen çakışma giderilmeden75/75 sonrası final gate PASS verilemez.
8. BANK-01 suite sonuçlarında özellikle sekizli origination/quote/claim, manual-v-auto, final payoff/reborrow, iki delivery, V1/V2 muafiyeti+V3 atomicity, rollback, SDK retry, postcommit lost-response replay, default-deny bank paths ve guards ayrı doğrulanır. Yazılmış test gerçek concurrency'dir diye koşmadan PASS yazılmaz; eksik coverage/failed test ayrı BANK-01 onaylı çalışmada giderilir.
9. Emulator exit/cleanup doğrula;9099,8080,9150,5001,4400,4500,8257 dinleyen süreç olmamalı. `git diff --check`, izinli scope, Assets/Packages/ProjectSettings değişmezliği, manifest/guard/launcher kontrolü ve limited secret scan. Eski unstaged/untracked BANK-01 içerikleri korunmalı; yalnız açıkça yetkilendirilmiş ek değişiklikler bulunmalı.
10. Sonuçları gerçek exit codes/counts ile kaydet. Runtime audit0 tek başına bankanın finansal invariant'larını kanıtlamaz. Feature/functionality HOLD ancak tam BANK-01 doğrulamasıyla kaldırılabilir. Commit/push/PR/deploy bu SECURITY-01 onayının parçası değildir.

## 8. İstenen onay

**Onay önerisi:** “Option A kapsamında yalnız backend/package-lock.json içindeki body-parser1.x→1.20.9, iki2.x kopya→2.4.0 ve fast-uri→3.1.9 minimum refresh'ini, gerekli paket metadata güncellemelerini, ardından npm ci/inventory/audit doğrulamasını onaylıyorum. Beklenmeyen paket değişikliği veya yeni riskte dur. Güvenlik kontrolü uygun bulunursa yalnız demo-logisticore-local tam validation'ı çalıştır. Eski offline lock-baseline testinin onaylı yeni lock'a uyarlanmasını ayrı ve görünür test değişikliği olarak kabul ediyorum. Unity/feature davranışı değişmesin; commit/push/PR/deploy yapılmasın.”

Bu metin henüz kullanıcı onayı değildir. Test-baseline düzeltmesi onaylanmazsa dependency remediation ayrı tamamlanabilir, ancak offline regression/final functional gate HOLD kalır. Kullanıcının §8 talimatı nedeniyle açık onay gelmeden düzeltme uygulanmaz; bu bir araç otomatik-approval reddi değildir.
