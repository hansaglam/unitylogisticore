# ECONOMY-00 offline simulator

IMPLEMENTED: dependency-free Node22 ESM model; all monetary values are UNAPPROVED DESIGN CANDIDATES. No Firebase, credentials, network, runtime config activation or XP simulation.

From repository root:
```powershell
node backend/tools/economy-sim/run.mjs
node --test backend/tools/economy-sim/model.test.mjs
node backend/tools/economy-sim/run.mjs --input path/to/local-scenarios.json
```

Default output is the deterministic Markdown comparison report. --input accepts one strict scenario object or1..32 objects and emits JSON with every delivery and accounting state. Build an input from candidate() in scenarios.mjs; monetary inputs are safe integer minor units. money('15000.00') yields1500000. No installation or package script change needed.

model.mjs contains pure immutable transitions, exact BigInt intermediate arithmetic, one-loan/no-reborrow rules, reserve caps and conservation checks. Scenario hash pins every model state; do not change config mid-run. Early repayment is available via earlyRepay(state,config,requestedMinorUnits); default reports simulate only automatic repayment. Rejected changes leave input state intact. Only operating cash shortage becomes an explicit blocked result; malformed config/overflow throws.

costTiming: net_completion, fuel_tolls_upfront, all_upfront. Upfront modes check cash at start but each model step aggregates one successful delivery; they do not implement a start/debit/refund lifecycle. A fleet acquisition purchases one extra truck+driver pair; no automatic income multiplier or warehouse simulation. waitingBound is an optimistic equal-duration capacity bound, not a scheduler.

Tests include an explicit Git diff against reviewed base186f8bcc203c5e39409ae4818e1b1d47938c352d for protected runtime/Unity paths, and generated-report equality. In a shallow clone missing that base, fetch the reviewed history; do not interpret an unavailable baseline as PASS. This scope test is phase-specific and will intentionally need review when a later phase changes runtime.

To regenerate the checked-in report explicitly:
```powershell
node --input-type=module -e "import {writeFileSync} from 'node:fs';import {comparisonReport} from './backend/tools/economy-sim/report.mjs';writeFileSync('docs/backend/ECONOMY-00_SIMULATION_REPORT.md',comparisonReport());"
```

See docs/backend/ECONOMY-00_DESIGN.md and docs/api-contracts/BANK_CONTRACT_DRAFT.md. Production and future banking implementation remain blocked pending separate reviews.

## BANK-00 — ayrı policy model

`bank-model.mjs`, `bank-report.mjs`, `bank-model.test.mjs` BANK-00'ın ayrı offline sürümüdür. Yukarıdaki ECONOMY-00 model ve tarihsel rapor değişmedi; onun beş teslimat/lifetime-loan kısıtları yeni banka politikası değildir.

Repository kökünden:
```powershell
node --test backend/tools/economy-sim/bank-model.test.mjs backend/tools/economy-sim/model.test.mjs
node backend/tools/economy-sim/bank-report.mjs
node --input-type=module -e "import {writeFileSync} from 'node:fs'; import {render} from './backend/tools/economy-sim/bank-report.mjs'; writeFileSync('docs/backend/BANK-00_SIMULATION_REPORT.md',render());"
```

BANK-00 CLI yalnız checked-in adayları raporlar; yeni input dosyası veya network kullanmaz. Model fonksiyonları strict bank config ve canonical integer amount intent'i doğrular. Tüm rakamlar UNAPPROVED DESIGN CANDIDATES. `simulate` seviyesindeki job planı trusted test fixture'ıdır; Firebase request validator değildir. `initial/borrow/repay/settle/levelUp/acquire` pure offline transitions, quote/receipt/transaction endpoint değildir. Gerçek level-up XP thresholds, loan quote concurrency ve rollout ayrı BANK-01 kapsamındadır. Ayrıntılar docs/backend/BANK-00_POLICY.md ve BANK-00_SIMULATION_REPORT.md.
