(function () {
    const konfigurace = window.dashboardKonfigurace ?? {};
    const form = document.getElementById('formular-filtru');
    const inputOd = document.getElementById('filter-od');
    const inputDo = document.getElementById('filter-do');
    const selectNormy = document.getElementById('filter-normy');
    const selectMesta = document.getElementById('filter-mesta');
    const tlacitkoReset = document.getElementById('vynulovat-filtry');
    const souhrnPrvky = {
        trzby: document.querySelector('[data-souhrn="trzby"]'),
        objednavky: document.querySelector('[data-souhrn="objednavky"]'),
        prumer: document.querySelector('[data-souhrn="prumer"]'),
        zakaznici: document.querySelector('[data-souhrn="zakaznici"]'),
        rozsah: document.querySelector('[data-rozsah]')
    };
    const realtimePrvky = {
        online: document.querySelector('[data-realtime="online"]'),
        kosiky: document.querySelector('[data-realtime="kosiky"]'),
        hodnota: document.querySelector('[data-realtime="hodnota"]')
    };
    const tabulkaTopKurzy = document.getElementById('top-kurzy-tabulka');
    const seznamKonverzi = document.getElementById('konverzni-souhrn');
    const heatmapaWrapper = document.getElementById('heatmapa');
    const heatmapaPopisek = document.getElementById('heatmapa-popisek');
    const exportTlacitko = document.getElementById('export-do-excelu');

    const mena = new Intl.NumberFormat('cs-CZ', { style: 'currency', currency: 'CZK' });
    const celeCislo = new Intl.NumberFormat('cs-CZ');
    const procenta = new Intl.NumberFormat('cs-CZ', { style: 'percent', minimumFractionDigits: 1, maximumFractionDigits: 1 });

    let grafProdeju;
    let grafTopKurzu;
    let grafKonverzi;
    let hubConnection;
    let realtimeIntervalId;

    inicializovatFormular();
    pripojitUdalosti();
    nactiPrehled();
    inicializovatRealtime();

    function inicializovatFormular() {
        if (inputOd && konfigurace.vychoziOd) {
            inputOd.value = konfigurace.vychoziOd;
        }
        if (inputDo && konfigurace.vychoziDo) {
            inputDo.value = konfigurace.vychoziDo;
        }

        naplnitSelect(selectNormy, konfigurace.normy ?? []);
        naplnitSelect(selectMesta, konfigurace.mesta ?? []);
    }

    function pripojitUdalosti() {
        if (form) {
            form.addEventListener('submit', function (event) {
                event.preventDefault();
                nactiPrehled();
            });
        }

        if (tlacitkoReset) {
            tlacitkoReset.addEventListener('click', function () {
                if (inputOd && konfigurace.vychoziOd) {
                    inputOd.value = konfigurace.vychoziOd;
                }
                if (inputDo && konfigurace.vychoziDo) {
                    inputDo.value = konfigurace.vychoziDo;
                }
                vynulovatVyber(selectNormy);
                vynulovatVyber(selectMesta);
            });
        }

        if (exportTlacitko) {
            exportTlacitko.addEventListener('click', function () {
                const url = vytvorUrl(konfigurace.endpointy?.export);
                if (url) {
                    window.location.href = url;
                }
            });
        }
    }

    function naplnitSelect(select, volby) {
        if (!select) {
            return;
        }

        select.innerHTML = '';
        volby.forEach(function (volba) {
            const option = document.createElement('option');
            option.value = String(volba.id);
            option.textContent = volba.nazev;
            select.appendChild(option);
        });
    }

    function vynulovatVyber(select) {
        if (!select) {
            return;
        }
        Array.from(select.options).forEach(function (opt) {
            opt.selected = false;
        });
    }

    async function nactiPrehled() {
        const url = vytvorUrl(konfigurace.endpointy?.prehled);
        if (!url) {
            return;
        }

        try {
            nastavStavNacitani(true);
            const response = await fetch(url);
            if (!response.ok) {
                throw new Error('Nepodařilo se načíst data analytiky.');
            }

            const data = await response.json();
            vykreslitSouhrn(data);
            vykreslitGrafProdeju(data);
            vykreslitTopKurzy(data);
            vykreslitKonverze(data);
            vykreslitHeatmapu(data);
            obnovitRealtime();
        } catch (error) {
            console.error(error);
            upozorneni('Nastala chyba při načítání dat. Zkuste to prosím znovu.');
        } finally {
            nastavStavNacitani(false);
        }
    }

    function nastavStavNacitani(prubeh) {
        if (prubeh) {
            document.body.classList.add('dashboard-loading');
        } else {
            document.body.classList.remove('dashboard-loading');
        }
    }

    function vykreslitSouhrn(data) {
        if (!data || !data.summary) {
            return;
        }

        const souhrn = data.summary;
        if (souhrnPrvky.trzby) {
            souhrnPrvky.trzby.textContent = mena.format(souhrn.celkoveTrzby ?? 0);
        }
        if (souhrnPrvky.objednavky) {
            souhrnPrvky.objednavky.textContent = celeCislo.format(souhrn.pocetObjednavek ?? 0);
        }
        if (souhrnPrvky.prumer) {
            souhrnPrvky.prumer.textContent = mena.format(souhrn.prumernaObjednavka ?? 0);
        }
        if (souhrnPrvky.zakaznici) {
            souhrnPrvky.zakaznici.textContent = celeCislo.format(souhrn.unikatniZakaznici ?? 0);
        }
        if (souhrnPrvky.rozsah) {
            const od = inputOd?.value ?? '';
            const doDatum = inputDo?.value ?? '';
            souhrnPrvky.rozsah.textContent = od && doDatum ? `${od} – ${doDatum}` : '';
        }
    }

    function vykreslitGrafProdeju(data) {
        const platnaData = Array.isArray(data?.labels) && Array.isArray(data?.revenue);
        const platnaObjednavky = Array.isArray(data?.orders);
        if (!platnaData || !platnaObjednavky) {
            return;
        }

        const context = document.getElementById('graf-prodeju');
        if (!context) {
            return;
        }

        const datasetTrzby = {
            type: 'line',
            label: 'Tržby',
            data: data.revenue,
            borderColor: 'rgba(75, 192, 192, 1)',
            backgroundColor: 'rgba(75, 192, 192, 0.25)',
            tension: 0.25,
            fill: true,
            yAxisID: 'y1'
        };

        const datasetObjednavky = {
            type: 'bar',
            label: 'Objednávky',
            data: data.orders,
            backgroundColor: 'rgba(54, 162, 235, 0.6)',
            borderColor: 'rgba(54, 162, 235, 1)',
            yAxisID: 'y2'
        };

        const datasetPrumer = Array.isArray(data?.averageOrder)
            ? {
                type: 'line',
                label: 'Průměrná objednávka',
                data: data.averageOrder,
                borderColor: 'rgba(255, 159, 64, 1)',
                backgroundColor: 'rgba(255, 159, 64, 0.2)',
                borderDash: [6, 4],
                tension: 0.3,
                yAxisID: 'y1'
            }
            : null;

        const datasets = datasetPrumer ? [datasetObjednavky, datasetTrzby, datasetPrumer] : [datasetObjednavky, datasetTrzby];

        if (grafProdeju) {
            grafProdeju.data.labels = data.labels;
            grafProdeju.data.datasets = datasets;
            grafProdeju.update();
            return;
        }

        grafProdeju = new Chart(context, {
            data: {
                labels: data.labels,
                datasets
            },
            options: {
                responsive: true,
                interaction: { mode: 'index', intersect: false },
                scales: {
                    y1: {
                        position: 'left',
                        beginAtZero: true,
                        ticks: {
                            callback: hodnot => mena.format(hodnot ?? 0)
                        }
                    },
                    y2: {
                        position: 'right',
                        beginAtZero: true,
                        grid: { drawOnChartArea: false }
                    }
                },
                plugins: {
                    legend: { position: 'top' }
                }
            }
        });
    }

    function vykreslitTopKurzy(data) {
        const seznam = Array.isArray(data?.topCourses) ? data.topCourses : [];
        const context = document.getElementById('graf-top-kurzy');

        if (tabulkaTopKurzy) {
            tabulkaTopKurzy.innerHTML = '';
            if (seznam.length === 0) {
                const radek = document.createElement('tr');
                const bunka = document.createElement('td');
                bunka.colSpan = 3;
                bunka.className = 'text-center text-muted';
                bunka.textContent = 'Žádná data pro daný filtr.';
                radek.appendChild(bunka);
                tabulkaTopKurzy.appendChild(radek);
            } else {
                seznam.forEach(function (polozka) {
                    const radek = document.createElement('tr');
                    const nazev = document.createElement('td');
                    nazev.textContent = polozka.nazev;
                    const trzba = document.createElement('td');
                    trzba.className = 'text-end';
                    trzba.textContent = mena.format(polozka.trzba ?? 0);
                    const pocet = document.createElement('td');
                    pocet.className = 'text-end';
                    pocet.textContent = celeCislo.format(polozka.pocet ?? 0);
                    radek.appendChild(nazev);
                    radek.appendChild(trzba);
                    radek.appendChild(pocet);
                    tabulkaTopKurzy.appendChild(radek);
                });
            }
        }

        if (!context) {
            return;
        }

        const labels = seznam.map(item => item.nazev);
        const hodnoty = seznam.map(item => item.trzba ?? 0);

        if (grafTopKurzu) {
            grafTopKurzu.data.labels = labels;
            grafTopKurzu.data.datasets[0].data = hodnoty;
            grafTopKurzu.update();
            return;
        }

        grafTopKurzu = new Chart(context, {
            type: 'bar',
            data: {
                labels,
                datasets: [{
                    label: 'Tržby',
                    data: hodnoty,
                    backgroundColor: 'rgba(99, 102, 241, 0.6)',
                    borderRadius: 6
                }]
            },
            options: {
                indexAxis: 'y',
                plugins: { legend: { display: false } },
                responsive: true,
                scales: {
                    x: {
                        ticks: {
                            callback: hodnot => mena.format(hodnot ?? 0)
                        }
                    }
                }
            }
        });
    }

    function vykreslitKonverze(data) {
        const konverze = data?.conversion;
        const context = document.getElementById('graf-konverzi');
        if (!konverze || !context) {
            return;
        }

        const hodnoty = [konverze.navstevy ?? 0, konverze.registrace ?? 0, konverze.platby ?? 0];
        const popisky = ['Návštěvy', 'Registrace', 'Platby'];

        if (grafKonverzi) {
            grafKonverzi.data.labels = popisky;
            grafKonverzi.data.datasets[0].data = hodnoty;
            grafKonverzi.update();
        } else {
            grafKonverzi = new Chart(context, {
                type: 'bar',
                data: {
                    labels: popisky,
                    datasets: [{
                        data: hodnoty,
                        backgroundColor: ['#38bdf8', '#fb923c', '#22c55e'],
                        borderRadius: 8
                    }]
                },
                options: {
                    plugins: { legend: { display: false } },
                    responsive: true,
                    scales: {
                        y: { beginAtZero: true }
                    }
                }
            });
        }

        if (seznamKonverzi) {
            seznamKonverzi.innerHTML = '';
            const polozky = [
                `Míra návštěva → registrace: ${procenta.format((konverze.mieraNavstevaRegistrace ?? 0) / 100)}`,
                `Míra registrace → platba: ${procenta.format((konverze.mieraRegistracePlatba ?? 0) / 100)}`,
                `Celková míra dokončení: ${procenta.format((konverze.celkovaMiera ?? 0) / 100)}`
            ];

            polozky.forEach(function (text) {
                const li = document.createElement('li');
                li.textContent = text;
                seznamKonverzi.appendChild(li);
            });
        }
    }

    function vykreslitHeatmapu(data) {
        const heatmapa = data?.heatmap;
        if (!heatmapaWrapper || !heatmapa) {
            return;
        }

        heatmapaWrapper.innerHTML = '';
        const dny = Array.isArray(heatmapa.dny) ? heatmapa.dny : [];
        const hodiny = Array.isArray(heatmapa.hodiny) ? heatmapa.hodiny : [];
        const bunky = Array.isArray(heatmapa.bunky) ? heatmapa.bunky : [];
        const maximum = typeof heatmapa.maximum === 'number' ? heatmapa.maximum : 0;

        if (bunky.length === 0 || dny.length === 0 || hodiny.length === 0) {
            heatmapaPopisek?.classList.remove('d-none');
            return;
        }

        heatmapaPopisek?.classList.add('d-none');

        heatmapaWrapper.style.gridTemplateColumns = `repeat(${hodiny.length + 1}, minmax(60px, auto))`;

        const hlavicka = document.createElement('div');
        hlavicka.className = 'heatmapa-hodina';
        heatmapaWrapper.appendChild(hlavicka);

        hodiny.forEach(function (hodina) {
            const bunka = document.createElement('div');
            bunka.className = 'heatmapa-hodina fw-semibold';
            bunka.textContent = `${String(hodina).padStart(2, '0')}:00`;
            heatmapaWrapper.appendChild(bunka);
        });

        dny.forEach(function (den, indexDne) {
            const popisekDne = document.createElement('div');
            popisekDne.className = 'heatmapa-den';
            popisekDne.textContent = den;
            heatmapaWrapper.appendChild(popisekDne);

            hodiny.forEach(function (hodina) {
                const bunka = document.createElement('div');
                bunka.className = 'heatmapa-bunka';
                const nalezena = bunky.find(cell => cell.den === indexDne && cell.hodina === hodina);
                const hodnota = nalezena ? nalezena.hodnota : 0;
                bunka.textContent = `${Math.round(hodnota)}%`;
                bunka.style.background = vypocitatBarvu(hodnota, maximum);
                heatmapaWrapper.appendChild(bunka);
            });
        });
    }

    function vypocitatBarvu(hodnota, maximum) {
        if (!maximum || maximum <= 0) {
            return 'linear-gradient(135deg, #f8f9fa, #e9ecef)';
        }

        const pomer = Math.min(hodnota / maximum, 1);
        const sytost = 50 + pomer * 40;
        const svetlost = 92 - pomer * 40;
        return `hsl(${120 - pomer * 40}, ${sytost}%, ${svetlost}%)`;
    }

    function vytvorUrl(zaklad) {
        if (!zaklad) {
            return null;
        }
        const url = new URL(zaklad, window.location.origin);
        const params = url.searchParams;
        const vybrany = ziskejFiltr();

        if (vybrany.od) {
            params.set('od', vybrany.od);
        }
        if (vybrany.do) {
            params.set('do', vybrany.do);
        }

        vybrany.normy.forEach(id => params.append('normy', String(id)));
        vybrany.mesta.forEach(id => params.append('mesta', String(id)));

        return url.toString();
    }

    function ziskejFiltr() {
        const vybraneNormy = selectNormy ? Array.from(selectNormy.selectedOptions).map(opt => Number(opt.value)) : [];
        const vybranaMesta = selectMesta ? Array.from(selectMesta.selectedOptions).map(opt => Number(opt.value)) : [];

        return {
            od: inputOd?.value ?? '',
            do: inputDo?.value ?? '',
            normy: vybraneNormy.filter(Number.isFinite),
            mesta: vybranaMesta.filter(Number.isFinite)
        };
    }

    function upozorneni(zprava) {
        window.alert(zprava);
    }

    function inicializovatRealtime() {
        if (typeof signalR === 'undefined' || !konfigurace.hub) {
            console.warn('SignalR není dostupný, realtime statistiky budou aktualizovány pouze ručně.');
            return;
        }

        hubConnection = new signalR.HubConnectionBuilder()
            .withUrl(konfigurace.hub)
            .withAutomaticReconnect()
            .build();

        hubConnection.onreconnected(obnovitRealtime);
        hubConnection.onreconnecting(() => nastavitRealtimeText('…'));

        hubConnection
            .start()
            .then(obnovitRealtime)
            .catch(err => {
                console.error('Nepodařilo se připojit k SignalR hubu.', err);
            });

        const interval = Number(konfigurace.intervalAktualizace) || 15000;
        realtimeIntervalId = window.setInterval(() => {
            if (hubConnection && hubConnection.state === signalR.HubConnectionState.Connected) {
                obnovitRealtime();
            }
        }, interval);
    }

    function nastavitRealtimeText(text) {
        if (realtimePrvky.online) {
            realtimePrvky.online.textContent = text;
        }
        if (realtimePrvky.kosiky) {
            realtimePrvky.kosiky.textContent = text;
        }
        if (realtimePrvky.hodnota) {
            realtimePrvky.hodnota.textContent = text;
        }
    }

    async function obnovitRealtime() {
        if (!hubConnection || hubConnection.state !== signalR.HubConnectionState.Connected) {
            return;
        }

        try {
            const filtr = ziskejFiltr();
            const data = await hubConnection.invoke('ZiskejOkamziteStatistiky', filtr);
            aktualizovatRealtime(data);
        } catch (error) {
            console.error('Nepodařilo se aktualizovat realtime statistiky.', error);
        }
    }

    function aktualizovatRealtime(data) {
        if (!data) {
            return;
        }
        if (realtimePrvky.online) {
            realtimePrvky.online.textContent = celeCislo.format(data.onlineUzivatele ?? 0);
        }
        if (realtimePrvky.kosiky) {
            realtimePrvky.kosiky.textContent = celeCislo.format(data.aktivniKosiky ?? 0);
        }
        if (realtimePrvky.hodnota) {
            realtimePrvky.hodnota.textContent = mena.format(data.hodnotaKosiku ?? 0);
        }
    }

    window.addEventListener('beforeunload', function () {
        if (realtimeIntervalId) {
            window.clearInterval(realtimeIntervalId);
        }
        if (hubConnection) {
            hubConnection.stop();
        }
    });
})();
