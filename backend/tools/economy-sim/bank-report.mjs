import {pathToFileURL} from 'node:url';
import {candidate,initial,borrow,settle,acquire,repay,levelUp,debt,assertState,limit} from './bank-model.mjs';
import {money,display,MAX,integer,checked,ModelError} from './model.mjs';
export function simulate({profit=3100,borrowAt=null,principal=20000,deliveries=30,startingCash=15000,repeat=false}={}){
  integer(profit,-4500,4500);integer(deliveries,1,10000);integer(startingCash);
  if(borrowAt!==null)integer(borrowAt,0,deliveries);integer(principal,1);
  let state=initial(candidate,{cash:money(String(startingCash))}),blocked=null,rows=[],borrowed=false;
  const decide=()=>{
    if(borrowAt!==null&&state.completed===borrowAt&&!borrowed){state=borrow(state,candidate,String(money(String(principal))));borrowed=true;}
    else if(repeat&&borrowed&&state.activeId===null)state=borrow(state,candidate,String(money(String(principal))));
    if(state.purchaseAt===null&&state.cash>=money('55000'))state=acquire(state,candidate);
  };
  decide();
  for(let i=0;i<deliveries;i++){
    try{state=settle(state,candidate,{gross:money('4500'),costs:money(String(4500-profit)),upfront:Math.min(money('800'),money(String(4500-profit)))});}
    catch(e){if(!(e instanceof ModelError)||!['INSUFFICIENT_OPERATING_CASH','INSUFFICIENT_SETTLEMENT_CASH'].includes(e.code))throw e;blocked=e.code;break;}
    decide();rows.push({delivery:state.completed,cash:state.cash,debt:debt(state),loans:state.sequence});
  }
  return {state,rows,blocked};
}
export function waiting(r,offers,duration){
  integer(offers,1,100);integer(duration,1,100000);let done=0,waves=0;
  while(done<r.state.completed){done+=Math.min(offers,r.state.purchaseAt!==null&&done>=r.state.purchaseAt?2:1,r.state.completed-done);waves++;}
  return checked(BigInt(waves)*BigInt(duration));
}
export function cycles(){
  let s=initial(candidate,{cash:money('100000')});const rows=[];
  for(const [level,principal] of [[1,'20000'],[5,'35000'],[10,'60000']]){
    s=levelUp(s,candidate,level);s=borrow(s,candidate,String(money(principal)));s=repay(s,candidate,String(MAX));
    rows.push({sequence:s.sequence,cash:s.cash,debt:debt(s),fees:s.feesCharged});
  }return {state:assertState(s,candidate),rows};
}
export function render(){
  const out=['# BANK-00 — Offline simülasyon sonuçları','',
    '**IMPLEMENTED offline; tüm miktarlar, oranlar ve süreler UNAPPROVED DESIGN CANDIDATES.** Tarihsel ECONOMY-00 sonuçları değiştirilmedi.',
    '', 'Başlangıç 15000 LGC; ikinci truck+driver 50000; edinim rezervi 5000. Gross 4500; low/base/high net 2500/3100/3800. Banka %8 sabit ücret, %35 pozitif net tahsilatı. Kredi belirtilen başarılı teslimatın sonunda, 0 ise onboarding sonrası alınır.',
    '', '| Net | Kredi zamanı | Principal | Edinim teslimatı | 30 iş nakit | Kalan borç |', '|---|---|---|---|---|---|'];
  for(const profit of [2500,3100,3800])for(const at of [null,0,1,5,6,7,8]){
    const r=simulate({profit,borrowAt:at});out.push(`| ${profit} | ${at??'yok'} | ${at===null?0:20000} | ${r.state.purchaseAt??'yok'} | ${display(r.state.cash)} | ${display(debt(r.state))} |`);
  }
  out.push('','## IMPLEMENTED — Kısmi kredi ve tekrar borçlanma','', '| Zaman / tekrar | Principal | Edinim işi | Kredi adedi | Nakit | Borç | Toplam ücret |','|---|---|---|---|---|---|---|');
  for(const args of [{borrowAt:0,principal:10000},{borrowAt:5,principal:10000},{borrowAt:0},{borrowAt:5},{borrowAt:0,repeat:true,deliveries:60}]){
    const r=simulate(args),s=r.state;out.push(`| ${args.borrowAt} / ${args.repeat?'60 iş tekrar':'30 iş'} | ${args.principal??20000} | ${s.purchaseAt} | ${s.sequence} | ${display(s.cash)} | ${display(debt(s))} | ${display(s.feesCharged)} |`);
  }
  out.push('','## IMPLEMENTED — İş yapmadan üç tam manuel kredi döngüsü','', '| Döngü | Nakit | Borç | Birikmiş ücret |','|---|---|---|---|');
  for(const row of cycles().rows)out.push(`| ${row.sequence} | ${display(row.cash)} | ${display(row.debt)} | ${display(row.fees)} |`);
  let leveled=borrow(initial(candidate,{cash:money('100000')}),candidate,String(money('20000')));
  leveled=levelUp(leveled,candidate,5);
  out.push('','Başlangıç 100000; seviyeler 1/5/10, principal 20000/35000/60000. Gelir/XP yaratılmadı. Net servet yalnız ücret kadar azalır; eski paid kayıtlar sonraki kredilerde değişmez.',
    `Aktif 20000 kredi sırasında level1→5: yeni tier limiti ${display(limit(candidate,leveled.level))}, mevcut borç ${display(debt(leveled))}, mevcut fee ${display(leveled.loans[0].terms.fee)} olarak kalır. Yeni limit ancak aktif borç kapandıktan sonra kullanılabilir.`,
    'Manuel sınır testi: 1000 principal +80 fee için 1079.99 ödeme sonrası borç0.01; kalan borçtan büyük sonraki niyet yalnız0.01 tahsil eder. Yeni key ile paid loan tekrar ödenmez. Reserve altındaki nakitten ödeme yapılmaz.',
    '', '## IMPLEMENTED — Kapasite alt sınırı (gerçek bekleme tahmini değil)','', '| Kredi zamanı | Teklif/dalga | İş süresi dakika | 30 iş alt sınır dakika |','|---|---|---|---|');
  for(const at of [null,0,5,8])for(const offers of [1,2])for(const duration of [30,60])out.push(`| ${at??'yok'} | ${offers} | ${duration} | ${waiting(simulate({borrowAt:at}),offers,duration)} |`);
  out.push('','## IMPLEMENTED — Zarar ve nakit yetersizliği','', '| Senaryo | Başarılı iş | Nakit | Borç | Durma nedeni |','|---|---|---|---|');
  for(const [name,args] of [['zarar ve kredi',{profit:-400,borrowAt:0,deliveries:100}],['nakit yok',{startingCash:0}],['çok düşük net',{profit:100,borrowAt:0}]]){
    const r=simulate(args);out.push(`| ${name} | ${r.state.completed} | ${display(r.state.cash)} | ${display(debt(r.state))} | ${r.blocked??'ufuk tamamlandı'} |`);
  }
  out.push('','## PROPOSED — Yorum ve sınırlar','',
    'Borçsuz 10–15 iş hedefi base/high için sağlanır, low 16 ile dışındadır. Erken kredi kesintileri erkenden başlatır: base day-zero edinim 10, beşinci iş kredi 8, ihtiyaç anı yedinci iş kredi 7 olur. Low için ihtiyaç anı 8, high için 6. Bunlar sabit net ve tek satın alma varsayımı altında 20000 krediyle ulaşılabilecek en erken nakit eşikleridir; daha erken borçlanmak kesinti başlatır, daha geç almak edinimi geciktirir.',
    'Aynı principal ve tamamlanan iş sayısı için tam ödenen kredilerin nihai maliyeti aynı sabit ücrettir. Erken kredi serbest harcanırsa ek varlık alınmadan borç ve tahsilat yükü oluşur. İkinci araç geliri otomatik ikiye katlamaz. Teklif arzı tek ise kapasite avantajı yoktur; çift arz tablosu yeterli nakit, uyumlu araç/teklif ve anında claim varsayar.',
    'Model düz sıralı tamamlanmış işler ve tek ek filo çifti kullanır; start/cancel/refund, gerçek kuyruk, level XP eşikleri, bank quote/receipt/concurrency veya Firebase transaction simülasyonu değildir. Upfront yalnız başlangıç likidite kontrolüdür; toplam gider bir kez netlenir. Loan history offline bellekte tutulur ve invariant taraması yapar; backend history taraması önerilmez.',
    'Reserve tahsilatı sınırlar, zararı engellemez. Acil fon/borç silme uygulanmadı. Örnek seviye yükseltmesi trusted fixture olayıdır; client level authority veya XP üretimi değildir.', '');
  return out.join('\n');
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href)process.stdout.write(render());
