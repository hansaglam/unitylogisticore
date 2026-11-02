# BACKEND-11A — Saf LiveOps yönetim domain temeli

28 Ekim 2026, Europe/Istanbul. **BACKEND-11A: PASS — yalnız pure model/unit kapsamı.** Operasyonel admin sistemi, gerçek yönetici doğrulaması veya yayınlama yetkisi oluşturulmadı. Worker reliability ve general security audit **HOLD**; production/deployment/migration **BLOCK**.

## 1. Yetki, baseline ve koruma

Proje sahibi 28 Ekim konuşmasındaki BACKEND-11A talimatıyla DESIGN-00 varsayılanlarını ve yalnız saf model/unit uygulamasını açıkça onayladı. Bu izin 11B/11C/11D, gerçek principal/grant oluşturma, Firestore writer veya yeni callable yetkisi değildir.

Başlangıç repo `D:/Projects/unitylogisticore`; origin `https://github.com/hansaglam/unitylogisticore.git`; branch main. Fetch sonrası origin/main ve yerel main aynı: `d58e8a134347723a147b56a06ab397caa602d781`. Başlangıç tracked/staged değişiklik yoktu; yalnız beklenen iki untracked rapor vardı. Hedef branch önceden yoktu; bu baseline'dan `feature/backend-11a-admin-domain` oluşturuldu. HEAD hâlâ aynı; commit/push/stage yok.

Başlangıçta kaydedilen ve son kontrolde korunan SHA256:

| Dosya | SHA256 |
| --- | --- |
| BACKEND-10_POST_MERGE_REPORT.md | `292144E1779CF1854452C6CA2B1A12ADC765255A84096383E0155654B089DFFB` |
| BACKEND-11_DESIGN-00.md | `440D4D46623F9EB6CB2499FE4F427E21B8DC9A1997B6680243D04972E7A748E6` |
| backend/package-lock.json | `AA1F6BA0D7A5CCAC1C9551A3941B8F1A2FD66B7482FD487C5C4D354ACBC5CC9F` |

Mevcut ignored `.local/`, debug logları, node_modules ve build çıktıları korundu. Yeni test/audit logları ignored `.local/backend-11a-*` altında; build yalnız olağan ignored `functions/lib` çıktısını üretir. Emulator/helper çalıştırılmadı.

## 2. Kaynak dayanağı ve uygulanan sınır

DESIGN-00, POST_MERGE_REPORT, LOCAL_RISK_ACCEPTANCE, LIVEOPS_CONTRACT ve EVENT_PRICING_CONTRACT incelendi. Mevcut `liveOpsModel`, `contractModel`, `marketModel`, `economyModel`, `logisticsModel`, `commands`, `identity`, `localEnvironment`, unit entry/LiveOps unit fixture'ları, package scripts ve strict tsconfig esas alındı.

`commands.canonicalJson`, `CommandError`, `economyModel.safe/versionId` ve `logisticsModel.digest` yeniden kullanılır; canonical format, 32 KiB digest limiti ve player command limitleri değiştirilmedi. Bu utility modülleri mevcut Firebase SDK türleri/HttpsError sınıflarını transitif yükleyebilir; model SDK client oluşturmaz, SDK initialization veya database işlemi gerektirmez. İzole test dosyası ortam kurulumu, credential veya emulator olmadan geçti. Bu durum SDK paketlerine hiç import bağımlılığı bulunmadığı anlamına gelmez.

İzin verilen tek yeni source `liveOpsAdminModel.ts` dosyasıdır. Finansal katalog içeriği bu modelde yeniden doğrulanmaz/hesaplanmaz: contentHash ve evidence birer taahhüttür. Gelecek trusted adapter existing immutable catalog validators ile gerçek içeriği doğrulayıp bu hash'lerle eşleştirmek zorundadır. Hash'li metadata kendi başına doğrulanmış yayın veya imza değildir.

## 3. Saf API'ler

| API | Uygulanan davranış |
| --- | --- |
| `validateAdminContext` / `requireAdminCapability` | Exact principalRef/enabled/capabilities/environment/grantRevision/validFrom/validUntil; EDITOR/RELEASER allowlist, duplicate capability ret, güncel explicit zaman ve scope kontrolü |
| `adminContentHash` | Mevcut canonical SHA-256; bounded JSON, ordered arrays, key-order bağımsızlığı; finansal validasyon değildir |
| `validateAdminProposal` | Schema1, altı state, author/content revision/hash, exact dependency pins, V5 target, evidence/approval yapısal bağları |
| `planDraftEdit` | Sahip EDITOR, expected revision, DRAFT zorunlu; revision +1, evidence ve approval null; detached yeni plan |
| `planProposalTransition` | Strict matrix + expected state/revision, action'a göre trusted capability ve evidence kontrolleri; yalnız yeni değer döndürür |
| `validatePublicationApproval` | Farklı author/approver, exact revision/content/evidence/pins/target/action/environment/time ve approver grant revision kontrolü |
| `planPublicationIntent` | APPROVED proposal → PUBLISHED plan, immutable hedef ID'leri ve approval hash; target varlığı veya Firestore immutability kontrolü değildir |
| `adminOperationIdentity` | Domain/environment/action/actor/key namespace hash'i ve aynı kapsam + canonical body fingerprint'i |
| `assertSameAdminOperation` | İki bounded identity/fingerprint eşleşmesi; farklı body/key/scope durumunda IDEMPOTENCY_CONFLICT |

Admin operation action isimleri future ACTIVATE/ROLLBACK namespace'lerini de tanımlar; bu işlemler için planner/writer/approval evaluator yoktur. 11A approval action yalnız PUBLISH'tir. Environment yalnız `demo-logisticore-local`, bounded namespace ile sentetik test kapsamlarını ayırır; gerçek proje veya production seçeneği yoktur.

Trusted context gelecekte adapter tarafından güvenilir kaynaktan sağlanmalıdır; request body'den alınamaz. **Simüle actor/approver değerleri doğrulanmış yönetici kimlikleri değildir.** Pure evaluator bir UID'yi veya `role/isAdmin` alanını gerçek yetkiye dönüştürmez; context provenance/authentication bu model tarafından ispatlanamaz. Self-grant/provisioning API'si yoktur.

## 4. Lifecycle, revision ve evidence

| Başlangıç | İzinli hedef | Yetki/ön koşul |
| --- | --- | --- |
| DRAFT | PENDING_APPROVAL | Sahip EDITOR; eşleşen validation/preview evidence, future olmayan validatedAt |
| DRAFT | CANCELLED | Sahip EDITOR veya RELEASER |
| PENDING_APPROVAL | APPROVED | Farklı RELEASER; PUBLISH approval bütün bağları ve grant scope'u geçerli |
| PENDING_APPROVAL | REJECTED | RELEASER |
| PENDING_APPROVAL | CANCELLED | Sahip EDITOR veya RELEASER |
| APPROVED | PUBLISHED | RELEASER requester + güncel trusted approver context ve stored approval |
| APPROVED | CANCELLED | Sahip EDITOR veya RELEASER |
| PUBLISHED / REJECTED / CANCELLED | Hiçbiri | Terminal; draft'a dönüş veya published cancellation yok |

VALIDATED/ACTIVE/SUPERSEDED/ROLLED_BACK proposal state olarak reddedilir. DRAFT→DRAFT yalnız ayrı edit planner'ıdır; submit sonrası in-place edit yok. Publication/activation ayrı kalır; activation uygulanmadı.

Revision **content revision** olarak tanımlandı: draft edit artırır; lifecycle geçişleri content revision'ı değiştirmez. Böylece validation ve approval aynı dondurulmuş içeriği pinler. Lifecycle concurrency ayrıca `expectedState` ile bağlanır; future adapter hem revision hem state'i transaction içinde CAS etmelidir. Pure planner yarışları kalıcı olarak önleyemez. Content revision overflow, stale revision ve stale expectedState fail-closed; otomatik conflict merging yoktur.

Evidence: proposalId/revision/contentHash/pinsHash/validationHash/previewHash/validatedAt. Approval: aynı content ve evidence hash'leri, tam economy/logistics/cost/market pinleri, V5 liveOps/contract target sürümleri, PUBLISH action, author actorRef, farklı approverRef/grantRevision, environment ve approvedAt/notBefore/expiresAt. Draft edit evidence'ı temizler; eski evidence yeni revision'a takılamaz. PUBLISHED yapısal kayıt geçerliliği, geçmiş approval süresi sonradan bitince tarihçeyi geçersiz yapmaz; yeni publication planı ise mevcut zaman/yetkiyi yeniden denetler.

## 5. Approval ve zaman

Zaman yalnız açık `now` parametresidir; Date.now/env kullanımı yok. Safe integer Unix ms, en fazla `253402300799999`. `approvedAt <= notBefore < expiresAt`; validatedAt <= approvedAt; approval author/approver farklı; approval grantRevision güncel context ile eşit olmalıdır. Enabled=false, capability boş, farklı environment/principal veya daraltılmış grant ret üretir.

Publication yetkisi `notBefore <= now <= expiresAt` aralığındadır; bitişten sonraki ilk ms reddedilir. Approval expiry approver context validUntil değerini aşamaz; context'in kendisi de zaman aralığında olmalıdır. APPROVED geçişi için approvedAt explicit now ile aynı olmalıdır. İlk 11A planner'ı gelecekte notBefore başlayan preapproval oluşturmaz: approval değerlendirmesi geçerli pencere içinde yapılır. Time-gated advance approval gelecekte ayrıca ele alınacaktır; scheduler veya aktivasyon burada yoktur.

Sabit gerçek risk süresi model içine genel bir yetki kaynağı olarak gömülmedi. Future adapter trusted context sınırlarını owner yetkisini aşmayacak biçimde üretmelidir. Mevcut çalışma onayı 28 Ekim–6 Kasım 2026 saat 23:59 Europe/Istanbul; bu görev süresini uzatmadı. Self-approval, changed content/revision/pins/target, wrong action, expired/not-yet-valid approval, missing evidence ve revoked authority testlerle reddedildi.

## 6. Fingerprint, input sınırları ve hatalar

OperationId = mevcut digest ile domain `logisticore_liveops_admin_v1`, strict environment, action, actorRef, idempotencyKey. Fingerprint aynı namespace + canonical body'dir. Same key/different body aynı operationId/farklı fingerprint üretir; conflict helper bunu reddeder. Ayrı actor/environment/action namespace'leri çakışmaz. Persistent receipt yazılmaz; future adapter identity'yi gerçek request'ten yeniden üretip atomik storage'da uygulamalıdır.

Object key sırası önemsiz, array sırası korunur. Yeni parallel canonical format yoktur. Guard, canonicalization öncesi non-plain prototype, hidden/nonenumerable/symbol/getter, sparse/extended arrays ve cycle/depth/node taşmalarını reddeder. Getter çalıştırılmaz. Bütün authority-bearing alanlarda exact key kontrolü; nullable alanlar açık null olmak zorundadır. Hash lowercase 64 hex; IDs mevcut versionId sınırında; key 1..128 güvenli ASCII ve reserved-pattern reddi. NaN/Infinity/unsafe/fractional/negative revision/time reddedilir.

Digest/canonical input toplam 32 KiB, depth12/node2048 sınırları korunur; operation envelope da bütçeden yer tüketir. DESIGN-00'daki 96 KiB proposal önerisi **11A'da uygulanmadı**; büyük intent reject edilir. Hash metadata actual body'nin gerçekten validated/published olduğunu kanıtlamaz. Untrusted JS Proxy çalıştırılması için sandbox sağlanmaz; adapter deserialized JSON ve kendi trusted context'ini vermelidir.

Mevcut `CommandError` ile FAILED_PRECONDITION/PERMISSION_DENIED/IDEMPOTENCY_CONFLICT ve utility'den INVALID_ARGUMENT kullanılabilir. Bunlar yeni player API değildir; gelecekte ayrı admin error vocabulary gerekirse ayrı tasarım yapılmalı, player command altyapısı değiştirilmemelidir. Yeni player-visible/localized metin eklenmedi; TR/EN/DE anahtar sözleşmesi korunur.

## 7. Gerçek test sonuçları

| Çalıştırılan kontrol | Sonuç |
| --- | --- |
| `npm run check` | PASS, exit0 |
| TypeScript build | PASS |
| Strict typecheck | PASS |
| Standart unit workflow | **96/96 PASS**, fail/skip/cancelled0 |
| `node --test tests/liveops-admin.unit.test.mjs` | **53/53 PASS**, fail/skip/cancelled0, exit0 |
| Mevcut testler | Önceki 43 unit testi aynı workflow'da PASS |
| Full emulator / npm run validate | NOT RUN — yetki kapsamında değil |

53 yeni testin 36'sı altı-state matrisinin bütün çiftlerini ayrı doğrular; kalanlar recursive missing/extra alanlar, malformed primitive/prototype/size, trusted context, revision/evidence invalidation, author/approver ayrımı, grant revocation, time edges, publication intent, determinism, idempotency ve fixture compatibility içerir. Runtime model testleri Firebase client veya emulator oluşturmaz. Unit entry'ye yalnız bir import satırı eklendi; financial concurrency/emulator testleri değiştirilmedi.

Ara başarısızlıklar gizlenmedi: ilk build'de `never` arrow helper'ın TypeScript control-flow narrowing'i nedeniyle derleme hataları çıktı; normal never-returning function declaration ile yalnız yeni modelde düzeltildi. İkinci check build/typecheck geçti, unit94 PASS/2 FAIL: illegal geçiş testleri approval fixture hazırlarken null evidence'a erişiyordu. Fixture artık geçerli evidence ile yasadışı geçişin gerçekten model tarafından reddedildiğini test ediyor. Son check96/96 ve standalone53/53 geçti; assertion/timeout/test skip gevşetilmedi.

Ignored kanıtlar: `backend-11a-check.log`, `backend-11a-check-final.log` (ara 94/2), `backend-11a-check-pass.log`, `backend-11a-isolated.log`; audit JSON ve exit kaydı aynı önekte. Tarihsel emulator134/134 sonucu yeni admin entegrasyon kanıtı diye sunulmaz.

## 8. Güvenlik ve finansal uyumluluk

Fresh read-only runtime audit **0/exit0**; full audit **5 moderate affected package / aynı 2 advisory**, high0/critical0, exit1. Vulnerabilities JSON önceki post-merge audit ile birebir aynı. GHSA-8988-4f7v-96qf ve GHSA-w5hq-g745-h8pq görünür; general audit HOLD. Kurulu 773 paket kaydı, workspace junction hedefi dahil lock sürümleriyle eşleşti. Install/update/override/downgrade yok; kilit hash'i aynı.

LOCAL security CONDITIONAL PASS mevcut fixed dependency onayı ve ayrı 11A narrow-scope yetkisi altında; yeni operational trust boundary yok. Worker startup root cause çözülmedi, HOLD kalır. Real DEV/PROD, migration ve deployment BLOCK.

Event selection, combined bps, single BigInt rounding, contract generation, V1–V5, operating costs, V3 bank repayment, wallet/XP, receipts/journals ve historical catalog validator kaynakları değişmedi. Fixture compatibility testi yeni hashing/model kullanımından önce ve sonra mevcut LiveOps fixture hash/validasyon sonucunun aynı olduğunu doğruladı. İkinci finansal hesaplayıcı yok. Source integrity + mevcut unit geçişi korunur; yeni full financial emulator geçişi iddia edilmez.

## 9. Dosyalar ve Git durumu

Yeni dosyalar:

- `backend/functions/src/liveOpsAdminModel.ts`
- `backend/tests/liveops-admin.unit.test.mjs`
- `docs/backend/BACKEND-11A_REPORT.md`

Tek mevcut tracked değişiklik: `backend/tests/unit.test.mjs` sonuna minimal import. Önceki POST_MERGE_REPORT ve DESIGN-00 hâlâ untracked ve byte-for-byte aynı. Unit entry dışındaki tracked source, Unity Assets/Packages/ProjectSettings, Rules, Firebase config, index exports, guards, emulator launcher ve dependency dosyalarının diff'i boştur. Yeni dosyalar stage edilmedi. Strict UTF-8/whitespace/credential-pattern ve izinli path kontrolü yapıldı; pattern kontrolü tüm repo secret audit iddiası değildir.

## 10. Ertelenenler ve insan incelemesi

11B: gerçek authenticated context provenance, principal provisioning/revocation registry, persisted draft/approval ve read-only ekonomik preview; hiçbiri uygulanmadı. 11C: immutable target existence/creation, selector/control CAS, replay receipts ve generation-selector entegrasyonu; hiçbiri uygulanmadı. 11D: rollback/history/retention ve gerçek emulator concurrency entegrasyonu; hiçbiri uygulanmadı. Modeldeki PUBLISHED sonucu **persisted publication event değildir**.

Açık teknik kararlar: future adapter'ın content revision + expectedState CAS'ini transactional tutması; grant expiry'nin gerçek owner yetkisine bağlanması; validator/preview evidence provenance; admin error vocabulary; 32 KiB üstü payload için ayrı review; preapproved future time window; publication idempotency'nin persistent namespace/approval tüketimine bağlanması. Yapısal olarak geçerli context/approval model çağrısına verilmesi gerçek kimlik/onay doğrulaması değildir.

Önerilen sonraki işlem: bu dört dosyalık 11A değişikliğini insan kod/güvenlik incelemesine sunmak; kabul sonrası ayrı commit/push veya 11B kapsam talimatı beklemek. Admin hesabı oluşturulmadı, Firebase'e erişilmedi, yeni endpoint/Function/scheduler eklenmedi, source branch dışında bir Git mutation yapılmadı. Commit, push, PR, merge, deploy ve migration yapılmadı.
