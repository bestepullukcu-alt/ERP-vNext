// Node 16 compatibility for the repository's Vite/Vitest version. Test bootstrap only; no production effect.
const nodeCrypto = require('crypto');
const { webcrypto } = nodeCrypto;
if (!globalThis.crypto?.getRandomValues && webcrypto) globalThis.crypto = webcrypto;
if (!nodeCrypto.getRandomValues && webcrypto) nodeCrypto.getRandomValues = webcrypto.getRandomValues.bind(webcrypto);
