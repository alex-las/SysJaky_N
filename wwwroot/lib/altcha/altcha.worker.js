self.onmessage = async (e) => {
    const d = e.data || {};
    const seed = d.seed || d.challenge || "";
    const difficulty = Number(d.difficulty || 0);

    let n = 0;
    const enc = new TextEncoder();
    const zeros = "0".repeat(difficulty);

    while (true) {
        // vyzkoušej obě varianty zprávy: "seed+number" i "seed:number"
        const in1 = enc.encode(seed + n);
        const in2 = enc.encode(`${seed}:${n}`);

        const [h1, h2] = await Promise.all([
            crypto.subtle.digest("SHA-256", in1),
            crypto.subtle.digest("SHA-256", in2)
        ]);

        const toHex = (buf) => Array.from(new Uint8Array(buf))
            .map(b => b.toString(16).padStart(2, "0")).join("");

        if (toHex(h1).startsWith(zeros) || toHex(h2).startsWith(zeros)) {
            // Altcha očekává 'number'
            self.postMessage({ number: n, took: n });
            break;
        }
        n++;
    }
};
