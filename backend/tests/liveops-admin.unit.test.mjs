import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {getApps} from 'firebase-admin/app';
import {digest} from '../functions/lib/logisticsModel.js';
import {liveOpsFixtures} from './liveops.fixtures.mjs';
import {validateLiveOpsCatalog} from '../functions/lib/liveOpsModel.js';
import {ADMIN_STATES, validateAdminContext, requireAdminCapability, adminContentHash,
 validateAdminProposal, validatePublicationApproval, planDraftEdit, planProposalTransition,
 planPublicationIntent, adminOperationIdentity, assertSameAdminOperation} from '../functions/lib/liveOpsAdminModel.js';

const copy=x=>structuredClone(x), h=x=>digest({synthetic:x});
const env={projectId:'demo-logisticore-local',namespace:'unit'};
const pins=Object.fromEntries(['economy','logistics','cost','market'].map(k=>[k,{version:`${k}_v1`,hash:h(k)}]));
const target={contractSchemaVersion:5,liveOpsVersion:'events_v2',contractVersion:'contracts_v5_2'};
const editor={principalRef:'editor',enabled:true,capabilities:['EDITOR'],environment:env,grantRevision:1,validFrom:0,validUntil:1000};
const releaser={...editor,principalRef:'reviewer',capabilities:['RELEASER'],grantRevision:2};
const draft=()=>({schemaVersion:1,proposalId:'proposal_1',revision:0,state:'DRAFT',authorRef:'editor',environment:env,
 contentHash:h('content'),pins,target,evidence:null,approval:null});
const ev=p=>({proposalId:p.proposalId,revision:p.revision,contentHash:p.contentHash,pinsHash:digest(p.pins),
 validationHash:h('validation'),previewHash:h('preview'),validatedAt:10});
const approval=p=>({proposalId:p.proposalId,revision:p.revision,contentHash:p.contentHash,
 validationHash:p.evidence.validationHash,previewHash:p.evidence.previewHash,pins:p.pins,target:p.target,action:'PUBLISH',
 actorRef:p.authorRef,approverRef:'reviewer',approverGrantRevision:2,environment:env,approvedAt:20,notBefore:20,expiresAt:100});
const pending=()=>{const p=draft();return {...p,state:'PENDING_APPROVAL',evidence:ev(p)};};
const approved=()=>{const p=pending();return {...p,state:'APPROVED',approval:approval(p)};};
const req=(p,to,extra={})=>({expectedRevision:p.revision,expectedState:p.state,to,evidence:null,approval:null,...extra});
const operation=(patch={})=>({domain:'logisticore_liveops_admin_v1',environment:env,action:'PUBLISH',actorRef:'reviewer',idempotencyKey:'operation_1',body:{a:1,b:2},...patch});
const fails=fn=>assert.throws(fn);

test('admin proposal validates and normalizes detached data without authenticating identities',()=>{
 const p=draft(),result=validateAdminProposal(p);assert.deepEqual(result,p);assert.notEqual(result.pins,p.pins);
 result.pins.market.version='changed';assert.equal(p.pins.market.version,'market_v1');
 assert.deepEqual(validateAdminProposal(approved()),approved());
});

test('admin proposal rejects missing/extra fields at every metadata object boundary',()=>{
 const p=approved();
 const paths=[[],['environment'],['pins'],...Object.keys(pins).map(k=>['pins',k]),['target'],['evidence'],['approval'],
 ['approval','environment'],['approval','target'],['approval','pins'],...Object.keys(pins).map(k=>['approval','pins',k])];
 for(const path of paths){const node=path.reduce((v,k)=>v[k],p);
  for(const key of Object.keys(node)){const bad=copy(p);const n=path.reduce((v,k)=>v[k],bad);delete n[key];fails(()=>validateAdminProposal(bad));}
  const bad=copy(p);path.reduce((v,k)=>v[k],bad).unexpected=true;fails(()=>validateAdminProposal(bad));
 }
});

test('admin metadata rejects malformed primitives, states, IDs, times, hashes and pins',()=>{
 assert.equal(Object.isFrozen(ADMIN_STATES),true);fails(()=>ADMIN_STATES.push('ACTIVE'));
 for(const patch of [{schemaVersion:2},{schemaVersion:'1'},{revision:-1},{revision:0.5},{revision:NaN},{revision:Infinity},{revision:Number.MAX_SAFE_INTEGER+1},
 {proposalId:''},{proposalId:'a/b'},{proposalId:'a'.repeat(65)},{authorRef:5},{contentHash:'F'.repeat(64)},
 ...['VALIDATED','ACTIVE','SUPERSEDED','ROLLED_BACK',null,0].map(state=>({state})),{environment:{...env,projectId:'real-project'}},
 {pins:[]},{pins:{...pins,market:{version:'bad/path',hash:h('market')}}},{target:{...target,contractSchemaVersion:4}},{evidence:{}},{approval:{}}])fails(()=>validateAdminProposal({...draft(),...patch}));
 for(const n of [-1,0.1,NaN,Infinity,253402300800000])fails(()=>validateAdminProposal({...pending(),evidence:{...pending().evidence,validatedAt:n}}));
});

test('admin boundary rejects unsafe prototypes, hidden keys, symbols, getters, sparse/extended arrays and cycles',()=>{
 const bads=[Object.assign(Object.create({admin:true}),draft()),Object.assign(Object.create(null),draft())];
 const hidden=draft();Object.defineProperty(hidden,'isAdmin',{value:true});bads.push(hidden);
 const symbol=draft();symbol[Symbol('admin')]=true;bads.push(symbol);
 let called=false;const getter=draft();Object.defineProperty(getter,'contentHash',{enumerable:true,get(){called=true;return h('content');}});bads.push(getter);
 for(const p of bads)fails(()=>validateAdminProposal(p));assert.equal(called,false);
 for(const body of [new Date(),new Map(),{x:undefined},{x:()=>0},JSON.parse('{"__proto__":{}}')])fails(()=>adminContentHash(body));
 const sparse=Array(2),extended=[1];extended.admin=true;const cycle={};cycle.self=cycle;
 for(const v of [sparse,extended,cycle])fails(()=>adminContentHash(v));
});

test('admin canonical limits are not raised for administrative payloads',()=>{
 for(const value of [{text:'x'.repeat(32769)},Array(2049).fill(1),{text:'é'.repeat(17000)}])fails(()=>adminContentHash(value));
 let deep=1;for(let i=0;i<14;i++)deep={nested:deep};fails(()=>adminContentHash(deep));
 fails(()=>adminOperationIdentity(operation({body:{text:'x'.repeat(32700)}}))); // Envelope also consumes bytes.
 assert.equal(adminContentHash({array:[1,2]}),digest({array:[1,2]}));
});

test('admin policy requires exact trusted context; player UID, roles and self-grants confer no authority',()=>{
 for(const c of [undefined,{uid:'editor'},{...editor,role:'RELEASER'},{...editor,isAdmin:true},{...editor,capabilities:['ADMIN']},
 {...editor,capabilities:['EDITOR','EDITOR']},{...editor,capabilities:['EDITOR','RELEASER','EDITOR']},{...editor,enabled:'true'},
 {...editor,validUntil:0},{...editor,grantRevision:-1}])fails(()=>validateAdminContext(c));
 for(const key of Object.keys(editor)){const c=copy(editor);delete c[key];fails(()=>validateAdminContext(c));}
 fails(()=>requireAdminCapability(editor,'RELEASER',env,20));
 fails(()=>requireAdminCapability({...editor,enabled:false},'EDITOR',env,20));
 fails(()=>requireAdminCapability({...editor,capabilities:[]},'EDITOR',env,20));
 fails(()=>requireAdminCapability(editor,'EDITOR',{...env,namespace:'foreign'},20));
 fails(()=>requireAdminCapability(editor,'ADMIN',env,20));
 for(const now of [-1,1001,NaN])fails(()=>requireAdminCapability(editor,'EDITOR',env,now));
 assert.equal(requireAdminCapability(editor,'EDITOR',env,0).principalRef,'editor');
 assert.equal(requireAdminCapability(editor,'EDITOR',env,1000).principalRef,'editor');
});

const allowed={DRAFT:['PENDING_APPROVAL','CANCELLED'],PENDING_APPROVAL:['APPROVED','REJECTED','CANCELLED'],APPROVED:['PUBLISHED','CANCELLED'],PUBLISHED:[],REJECTED:[],CANCELLED:[]};
function inState(s){if(s==='DRAFT')return draft();if(s==='PENDING_APPROVAL')return pending();if(s==='REJECTED')return {...pending(),state:s};if(s==='CANCELLED')return {...draft(),state:s};return {...approved(),state:s};}
for(const from of ADMIN_STATES)for(const to of ADMIN_STATES)test(`admin transition matrix ${from} -> ${to}`,()=>{
 const p=inState(from),r=req(p,to),actor=to==='PENDING_APPROVAL'?editor:releaser;
 if(to==='PENDING_APPROVAL')r.evidence=ev(p);if(to==='APPROVED')r.approval=approval({...p,evidence:p.evidence??ev(p)});
 const act=()=>planProposalTransition(p,r,actor,20,releaser);
 if(allowed[from].includes(to)){const next=act();assert.equal(next.state,to);assert.equal(next.revision,p.revision);assert.equal(next.contentHash,p.contentHash);}
 else fails(act);
});

test('admin transition authorization is separate from structurally valid states',()=>{
 const p=pending();fails(()=>planProposalTransition(p,req(p,'APPROVED',{approval:approval(p)}),editor,20));
 fails(()=>planProposalTransition(p,req(p,'REJECTED'),editor,20));
 fails(()=>planProposalTransition(draft(),req(draft(),'PENDING_APPROVAL',{evidence:ev(draft())}),{...editor,principalRef:'other'},20));
 assert.equal(planProposalTransition(p,req(p,'CANCELLED'),editor,20).state,'CANCELLED');
 fails(()=>planProposalTransition(p,req(p,'CANCELLED'),{...editor,principalRef:'other'},20));
 fails(()=>planProposalTransition(draft(),{...req(draft(),'CANCELLED'),role:'RELEASER'},editor,20));
});

test('admin content revisions and expected lifecycle state reject stale mutation',()=>{
 const p=draft();for(const patch of [{expectedRevision:1},{expectedRevision:-1},{expectedState:'APPROVED'}])fails(()=>planProposalTransition(p,{...req(p,'CANCELLED'),...patch},editor,20));
 const r={expectedRevision:0,contentHash:h('edited'),pins,target};
 const withEvidence={...p,evidence:ev(p)};const before=copy(withEvidence);
 const next=planDraftEdit(withEvidence,r,editor,20);assert.equal(next.revision,1);assert.equal(next.evidence,null);assert.equal(next.approval,null);assert.deepEqual(withEvidence,before);
 fails(()=>planDraftEdit(next,r,editor,20));fails(()=>planDraftEdit(p,{...r,expectedRevision:1},editor,20));
 fails(()=>planDraftEdit(p,r,releaser,20));
 for(const s of ADMIN_STATES.filter(s=>s!=='DRAFT'))fails(()=>planDraftEdit(inState(s),r,editor,20));
 fails(()=>planDraftEdit({...p,revision:Number.MAX_SAFE_INTEGER},{...r,expectedRevision:Number.MAX_SAFE_INTEGER},editor,20));
 fails(()=>planProposalTransition(next,req(next,'PENDING_APPROVAL',{evidence:ev(p)}),editor,20));
});

test('admin submit binds evidence and rejects future, missing or altered validation metadata',()=>{
 const p=draft(),e=ev(p);
 for(const patch of [{revision:1},{proposalId:'other'},{contentHash:h('other')},{pinsHash:h('other')},{validatedAt:21},{previewHash:null},{validationHash:null}])fails(()=>planProposalTransition(p,req(p,'PENDING_APPROVAL',{evidence:{...e,...patch}}),editor,20));
 fails(()=>planProposalTransition(p,req(p,'PENDING_APPROVAL'),editor,20));
 fails(()=>planProposalTransition(p,req(p,'CANCELLED',{evidence:e}),editor,20));
});

test('admin approval binds two principals, exact evidence, pins, target, environment and action',()=>{
 const p=pending(),a=approval(p);assert.deepEqual(validatePublicationApproval(a,p,releaser,20),a);
 const changes=[{approverRef:'editor'},{actorRef:'other'},{approverRef:'unknown'},{revision:1},{proposalId:'other'},
 {contentHash:h('changed')},{validationHash:h('changed')},{previewHash:h('changed')},{pins:{...pins,market:{...pins.market,version:'market_v2'}}},
 {target:{...target,liveOpsVersion:'events_other'}},{environment:{...env,namespace:'other'}},{action:'ACTIVATE'},{action:'ROLLBACK'},{extra:1}];
 for(const patch of changes)fails(()=>validatePublicationApproval({...a,...patch},p,releaser,20));
 fails(()=>validatePublicationApproval(a,p,releaser,20,'ACTIVATE'));
 const self={...releaser,principalRef:'editor'};fails(()=>planProposalTransition(p,req(p,'APPROVED',{approval:{...a,approverRef:'editor'}}),self,20));
});

test('admin publication checks inclusive approval edges, invalid ranges, grant expiry and revocation',()=>{
 const p=pending(),a=approval(p);
 for(const now of [20,100])assert.deepEqual(validatePublicationApproval(a,p,releaser,now),a);
 for(const now of [19,101,NaN,Infinity])fails(()=>validatePublicationApproval(a,p,releaser,now));
 for(const patch of [{notBefore:100},{expiresAt:20},{approvedAt:21},{approvedAt:9},{expiresAt:1001},{notBefore:-1},{expiresAt:253402300800000}])fails(()=>validatePublicationApproval({...a,...patch},p,releaser,20));
 for(const context of [null,{...releaser,enabled:false},{...releaser,capabilities:[]},{...releaser,grantRevision:3},{...releaser,validFrom:21},{...releaser,validUntil:50}])fails(()=>validatePublicationApproval(a,p,context,20));
 fails(()=>planProposalTransition(p,req(p,'APPROVED',{approval:a}),releaser,21));
});

test('admin immutable publication intent is a detached plan, not catalog validation or a database event',()=>{
 const p=approved(),before=copy(p),intent=planPublicationIntent(p,0,releaser,releaser,30);
 assert.equal(intent.plannedProposal.state,'PUBLISHED');assert.deepEqual(intent.target,target);assert.deepEqual(p,before);
 assert.equal(intent.approvalHash,digest(p.approval));
 fails(()=>planPublicationIntent(p,1,releaser,releaser,30));fails(()=>planPublicationIntent(p,0,editor,releaser,30));
 fails(()=>planPublicationIntent(p,0,releaser,null,30));fails(()=>planPublicationIntent(p,0,releaser,releaser,101));
 for(const patch of [{contentHash:h('changed')},{revision:1},{approval:null},{target:{...target,contractVersion:'other'}},{pins:{...pins,cost:{...pins.cost,hash:h('changed')}}}])fails(()=>planPublicationIntent({...p,...patch},0,releaser,releaser,30));
});

test('admin canonical hashes preserve ordered arrays, ignore object key order and do not mutate',()=>{
 const body={b:[1,2],a:{y:2,x:1}},before=copy(body);
 assert.equal(adminContentHash(body),adminContentHash({a:{x:1,y:2},b:[1,2]}));
 assert.notEqual(adminContentHash(body),adminContentHash({...body,b:[2,1]}));assert.deepEqual(body,before);
 assert.deepEqual(validateAdminProposal(draft()),validateAdminProposal(draft()));
 assert.deepEqual(planPublicationIntent(approved(),0,releaser,releaser,30),planPublicationIntent(approved(),0,releaser,releaser,30));
});

test('admin operation identities separate namespaces and detect changed-body replay conflicts',()=>{
 const r=operation(),a=adminOperationIdentity(r);assert.deepEqual(a,adminOperationIdentity(r));
 assert.deepEqual(a,adminOperationIdentity(operation({body:{b:2,a:1}})));assertSameAdminOperation(a,a);
 const changed=adminOperationIdentity(operation({body:{a:2,b:2}}));assert.equal(a.operationId,changed.operationId);assert.notEqual(a.fingerprint,changed.fingerprint);
 assert.throws(()=>assertSameAdminOperation(a,changed),e=>e.details.code==='IDEMPOTENCY_CONFLICT');
 for(const patch of [{environment:{...env,namespace:'other'}},{actorRef:'other'},{action:'ACTIVATE'},{idempotencyKey:'other'}]){
  const b=adminOperationIdentity(operation(patch));assert.notEqual(a.operationId,b.operationId);assert.notEqual(a.fingerprint,b.fingerprint);
 }
});

test('admin operation identities reject invalid key/domain/action/record schema and never grant authority',()=>{
 for(const key of ['',1,'x'.repeat(129),'a/b','__reserved__'])fails(()=>adminOperationIdentity(operation({idempotencyKey:key})));
 for(const patch of [{domain:'player_commands'},{action:'GRANT_ADMIN'},{actorRef:'a/b'},{body:[]},{body:null},{isAdmin:true},{environment:{...env,projectId:'production'}}])fails(()=>adminOperationIdentity(operation(patch)));
 for(const key of Object.keys(operation())){const r=operation();delete r[key];fails(()=>adminOperationIdentity(r));}
 const a=adminOperationIdentity(operation());for(const patch of [{operationId:'invalid'},{fingerprint:'bad'},{extra:1}])fails(()=>assertSameAdminOperation({...a,...patch},a));
});

test('admin pure module requires no SDK client, credentials, clock or environment initialization',()=>{
 const apps=getApps().length;
 const source=readFileSync(new URL('../functions/src/liveOpsAdminModel.ts',import.meta.url),'utf8');
 assert.doesNotMatch(source,/Date\.now\(|process\.env|initializeApp\(|new Firestore|\.collection\(|runTransaction\(|onCall\(/);
 validateAdminProposal(draft());planPublicationIntent(approved(),0,releaser,releaser,30);
 assert.equal(getApps().length,apps);
});

test('admin metadata and hashing preserve existing LiveOps fixture content and validator results',()=>{
 const f=liveOpsFixtures('admin_compat'),before=copy(f),hashBefore=digest(f.liveOps);
 const validBefore=validateLiveOpsCatalog(f.liveOps,f.liveOps.version,f.economy,f.logistics,f.market);
 assert.equal(adminContentHash(f.liveOps),hashBefore);
 planPublicationIntent({...approved(),contentHash:hashBefore,evidence:{...approved().evidence,contentHash:hashBefore},approval:{...approved().approval,contentHash:hashBefore}},0,releaser,releaser,30);
 assert.deepEqual(f,before);assert.equal(digest(f.liveOps),hashBefore);
 assert.deepEqual(validateLiveOpsCatalog(f.liveOps,f.liveOps.version,f.economy,f.logistics,f.market),validBefore);
});
