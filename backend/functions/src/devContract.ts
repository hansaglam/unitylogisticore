import { Firestore } from "firebase-admin/firestore";
import { parseCommandRequest } from "./commands";
import { clockTime, epochFor, ServerClock, serverClock } from "./contractModel";
import { generateContractOffers } from "./contracts";
import { DEV_FIRST_CONTRACT_CANDIDATES, LOCAL_DEV_CONTRACT_VERSION } from "./devContractIds";
import { STARTER_DRIVER, STARTER_TRUCK, validateSelection } from "./fleet";
import { assertLocalEnvironment } from "./localEnvironment";

export { DEV_FIRST_CONTRACT_CANDIDATES, LOCAL_DEV_CONTRACT_VERSION };

/**
 * Narrow LOCAL callable. Empty client payload. Reuses generateContractOffers.
 * Epoch uses the approved candidate duration so it must match a future catalog seed.
 */
export function generateCompanyContractOffersCommand(
  db: Firestore, auth: { uid: string } | undefined, data: unknown, clock: ServerClock = serverClock,
) {
  assertLocalEnvironment(process.env, true);
  parseCommandRequest(data);
  return generateContractOffers(db, auth, data, {
    configVersion: LOCAL_DEV_CONTRACT_VERSION,
    epoch: epochFor(clockTime(clock), DEV_FIRST_CONTRACT_CANDIDATES.epochDurationMs),
    assets: validateSelection([
      { assetId: STARTER_TRUCK, kind: "truck" },
      { assetId: STARTER_DRIVER, kind: "driver" },
    ]),
  }, clock);
}
