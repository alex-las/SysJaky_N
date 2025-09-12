self.onmessage = async (e) => {
  const data = e.data;
  const seed = data.seed || '';
  const difficulty = data.difficulty || 0;
  let nonce = 0;
  const encoder = new TextEncoder();
  while (true) {
    const input = encoder.encode(seed + nonce);
    const hashBuffer = await crypto.subtle.digest('SHA-256', input);
    const hashArray = Array.from(new Uint8Array(hashBuffer));
    const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
    if (hashHex.startsWith('0'.repeat(difficulty))) {
      self.postMessage({ nonce });
      break;
    }
    nonce++;
  }
};
