(function () {
    const konfigurace = window.dashboardKonfigurace;
    if (!konfigurace) {
        console.warn('Chybí konfigurace dashboardu.');
        return;
    }

    const prvky = {
        filterForm: document.getElementById('dashboardFilters'),
        filterFrom: document.getElementById('filterFrom'),
        filterTo: document.getElementById('filterTo'),
        filterNorms: document.getElementById('filterNorms'),
        filterCities: document.getElementById('filterCities'),
        summaryRevenue: document.getElementById('summaryRevenue'),
        summaryRevenueChange: document.getElementById('summaryRevenueChange'),
        summaryOrders: document.getElementById('summaryOrders'),
        summaryAverageOrder: document.getElementById('summaryAverageOrder'),
        summarySeats: document.getElementById('summarySeats'),
        summaryOccupancy: document.getElementById('summaryOccupancy'),
        summaryCustomers: document.getElementById('summaryCustomers'),
        summaryNewCustomers: document.getElementById('summaryNewCustomers'),
        salesRangeLabel: document.getElementById('salesRangeLabel'),
        salesAggregateLabel: document.getElementById('salesAggregateLabel'),
        salesEmpty: document.getElementById('salesEmpty'),
        topCoursesTable: document.getElementById('topCoursesTable'),
        topCoursesTotal: document.getElementById('topCoursesTotal'),
        conversionRates: document.getElementById('conversionRates'),
        conversionDetails: document.getElementById('conversionDetails'),
        heatmapEmpty: document.getElementById('heatmapEmpty'),
        filtersSpinner: document.getElementById('filtersSpinner'),
        filtersStatusText: document.getElementById('filtersStatusText'),
        exportButton: document.getElementById('exportReport'),
        presetButtons: Array.from(document.querySelectorAll('[data-range]')),
        realtimeUsers: document.getElementById('realtimeUsers'),
        realtimeCarts: document.getElementById('realtimeCarts'),
        realtimeCartValue: document.getElementById('realtimeCartValue'),
        realtimeUpdated: document.getElementById('realtimeUpdated')
    };

    const stav = {
        salesChart: null,
        topCoursesChart: null,
        conversionChart: null,
        heatmapChart: null,
        hub: null,
        hubTimer: null,
        posledniFiltry: null
    };

    const denniNazvy = ['Neděle', 'Pondělí', 'Úterý', 'Středa', 'Čtvrtek', 'Pátek', 'Sobota'];
    const formatMena = new Intl.NumberFormat('cs-CZ', { style: 'currency', currency: 'CZK' });
    const formatCislo = new Intl.NumberFormat('cs-CZ');
    const formatDatum = new Intl.DateTimeFormat('cs-CZ', { dateStyle: 'medium' });

    function nastavStavNačítání(nacitani, text) {
        if (!prvky.filtersSpinner || !prvky.filtersStatusText) {
            return;
        }

        if (nacitani) {
            prvky.filtersSpinner.classList.remove('d-none');
            prvky.filtersStatusText.textContent = text ?? 'Načítám data…';
        } else {
            prvky.filtersSpinner.classList.add('d-none');
            prvky.filtersStatusText.textContent = text ?? 'Připraveno';
        }
    }

    function smazatMožnosti(select) {
        while (select.firstChild) {
            select.removeChild(select.firstChild);
        }
    }

    function naplnitSelect(select, možnosti) {
        smazatMožnosti(select);
        možnosti.forEach(možnost => {
            const option = document.createElement('option');
            option.value = možnost.id;
            option.textContent = možnost.name;
            select.appendChild(option);
        });
    }

    async function načtiFiltry() {
        nastavStavNačítání(true, 'Načítám filtry…');
        try {
            const odpověď = await fetch(konfigurace.api.filtry, { credentials: 'include' });
            if (!odpověď.ok) {
                throw new Error('Nepodařilo se načíst filtry');
            }
            const data = await odpověď.json();
            naplnitSelect(prvky.filterNorms, data.normy ?? []);
            naplnitSelect(prvky.filterCities, data.mesta ?? []);
            if (data.vychoziOd) {
                prvky.filterFrom.value = data.vychoziOd;
            }
            if (data.vychoziDo) {
                prvky.filterTo.value = data.vychoziDo;
            }
            nastavStavNačítání(false, 'Filtry načteny');
            await načtiPřehled();
        } catch (chyba) {
            console.error(chyba);
            nastavStavNačítání(false, 'Chyba při načítání filtrů');
        }
    }

    function získejHodnotyFiltru() {
        const vybranéNormy = Array.from(prvky.filterNorms.selectedOptions).map(opt => opt.value);
        const vybranáMěsta = Array.from(prvky.filterCities.selectedOptions).map(opt => opt.value);
        return {
            from: prvky.filterFrom.value,
            to: prvky.filterTo.value,
            normy: vybranéNormy,
            mesta: vybranáMěsta
        };
    }

    function sestavUrl(filters) {
        const params = new URLSearchParams();
        if (filters.from) {
            params.set('from', filters.from);
        }
        if (filters.to) {
            params.set('to', filters.to);
        }
        filters.normy.forEach(id => params.append('normy', id));
        filters.mesta.forEach(id => params.append('mesta', id));
        return `${konfigurace.api.prehled}?${params.toString()}`;
    }

    async function načtiPřehled(event) {
        if (event) {
            event.preventDefault();
        }

        const filtry = získejHodnotyFiltru();
        if (!filtry.from || !filtry.to) {
            nastavStavNačítání(false, 'Vyberte platné období');
            return;
        }

        stav.posledniFiltry = filtry;
        nastavStavNačítání(true);

        try {
            const odpověď = await fetch(sestavUrl(filtry), { credentials: 'include' });
            if (!odpověď.ok) {
                throw new Error('Načtení přehledu selhalo');
            }
            const data = await odpověď.json();
            aktualizujPřehled(data);
            nastavStavNačítání(false, 'Data aktualizována');
        } catch (chyba) {
            console.error(chyba);
            nastavStavNačítání(false, 'Chyba při načítání dat');
        }
    }

    function aktualizujPřehled(data) {
        if (!data || !data.souhrn) {
            return;
        }

        aktualizujSouhrn(data);
        aktualizujGrafTržeb(data);
        aktualizujTopKurzy(data);
        aktualizujKonverze(data.konverze);
        aktualizujHeatmapu(data.heatmap);
    }

    function aktualizujSouhrn(data) {
        const souhrn = data.souhrn;
        const intervalText = `${formatDatum(new Date(`${data.obdobiOd}T00:00:00`))} – ${formatDatum(new Date(`${data.obdobiDo}T00:00:00`))}`;
        prvky.salesRangeLabel.textContent = intervalText;

        prvky.summaryRevenue.textContent = formatMena.format(souhrn.celkoveTrzby);
        const změna = souhrn.zmenaTrzebProcenta ?? 0;
        let změnaText = "";
        if (změna > 0) {
            změnaText = `▲ ${změna.toFixed(2)} % vs. předchozí období`;
            prvky.summaryRevenueChange.classList.remove("text-danger");
            prvky.summaryRevenueChange.classList.add("text-success");
        } else if (změna < 0) {
            změnaText = `▼ ${Math.abs(změna).toFixed(2)} % vs. předchozí období`;
            prvky.summaryRevenueChange.classList.remove("text-success");
            prvky.summaryRevenueChange.classList.add("text-danger");
        } else {
            změnaText = "Žádná změna oproti předchozímu období";
            prvky.summaryRevenueChange.classList.remove("text-success", "text-danger");
        }
        prvky.summaryRevenueChange.textContent = změnaText;

        prvky.summaryOrders.textContent = formatCislo.format(souhrn.objednavky);
        prvky.summaryAverageOrder.textContent = `Průměrná objednávka ${formatMena.format(souhrn.prumernaObjednavka)}`;
        prvky.summarySeats.textContent = formatCislo.format(souhrn.prodanaMista);
        prvky.summaryOccupancy.textContent = `Průměrná obsazenost ${(souhrn.prumernaObsazenost ?? 0).toFixed(1)} %`;
        prvky.summaryCustomers.textContent = formatCislo.format(souhrn.aktivniZakaznici);
        prvky.summaryNewCustomers.textContent = `${formatCislo.format(souhrn.noviZakaznici)} nových zákazníků`;

        const prumerDen = souhrn.delkaObdobiDni > 0 ? souhrn.celkoveTrzby / souhrn.delkaObdobiDni : 0;
        prvky.salesAggregateLabel.textContent = `Denní průměr ${formatMena.format(prumerDen)}`;
    }

    function vytvořNeboAktualizujGraf(nazev, canvas, config) {
        if (!canvas) {
            return;
        }
        if (stav[nazev]) {
            stav[nazev].data = config.data;
            stav[nazev].options = config.options;
            stav[nazev].update();
        } else {
            stav[nazev] = new Chart(canvas, config);
        }
    }

    function aktualizujGrafTržeb(data) {
        const body = data.trend ?? [];
        const canvas = document.getElementById('salesTrendChart');
        if (!canvas) {
            return;
        }

        const labels = body.map(b => b.datum);
        const objednavky = body.map(b => b.objednavky);
        const tržby = body.map(b => b.trzba);
        const průměr = body.map(b => b.prumernaObjednavka);

        prvky.salesEmpty.hidden = body.length > 0;

        const config = {
            type: 'bar',
            data: {
                labels,
                datasets: [
                    {
                        type: 'bar',
                        label: 'Objednávky',
                        data: objednavky,
                        backgroundColor: 'rgba(13,110,253,0.4)',
                        borderColor: 'rgba(13,110,253,1)',
                        yAxisID: 'objednavky'
                    },
                    {
                        type: 'line',
                        label: 'Tržby',
                        data: tržby,
                        borderColor: 'rgba(25,135,84,1)',
                        backgroundColor: 'rgba(25,135,84,0.2)',
                        fill: true,
                        tension: 0.3,
                        yAxisID: 'trzby'
                    },
                    {
                        type: 'line',
                        label: 'Průměrná objednávka',
                        data: průměr,
                        borderColor: 'rgba(255,193,7,1)',
                        backgroundColor: 'rgba(255,193,7,0.2)',
                        borderDash: [6, 4],
                        yAxisID: 'trzby'
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                scales: {
                    objednavky: {
                        type: 'linear',
                        position: 'left',
                        beginAtZero: true,
                        grid: {
                            drawOnChartArea: false
                        }
                    },
                    trzby: {
                        type: 'linear',
                        position: 'right',
                        beginAtZero: true,
                        ticks: {
                            callback: hodnota => formatMena.format(hodnota)
                        }
                    }
                },
                plugins: {
                    tooltip: {
                        callbacks: {
                            label: context => {
                                if (context.datasetIndex === 0) {
                                    return `Objednávky: ${formatCislo.format(context.parsed.y)}`;
                                }
                                return `${context.dataset.label}: ${formatMena.format(context.parsed.y)}`;
                            }
                        }
                    }
                }
            }
        };

        vytvořNeboAktualizujGraf("salesChart", canvas, config);
    }

    function aktualizujTopKurzy(data) {
        const kurzy = data.topKurzy ?? [];
        const canvas = document.getElementById('topCoursesChart');
        if (!canvas) {
            return;
        }

        const labels = kurzy.map(k => k.nazev);
        const hodnoty = kurzy.map(k => k.trzba);

        const config = {
            type: 'bar',
            data: {
                labels,
                datasets: [
                    {
                        label: 'Tržby',
                        data: hodnoty,
                        backgroundColor: 'rgba(102,16,242,0.5)',
                        borderColor: 'rgba(102,16,242,1)',
                        borderWidth: 1
                    }
                ]
            },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    x: {
                        ticks: {
                            callback: hodnot => formatMena.format(hodnot)
                        }
                    }
                },
                plugins: {
                    tooltip: {
                        callbacks: {
                            label: context => `${context.label}: ${formatMena.format(context.parsed.x)}`
                        }
                    }
                }
            }
        };

        vytvořNeboAktualizujGraf("topCoursesChart", canvas, config);

        if (prvky.topCoursesTotal) {
            const součet = hodnoty.reduce((sum, val) => sum + val, 0);
            prvky.topCoursesTotal.textContent = kurzy.length > 0
                ? `Součet tržeb ${formatMena.format(součet)}`
                : 'Žádné kurzy nesplňují filtr';
        }

        if (prvky.topCoursesTable) {
            smazatMožnosti(prvky.topCoursesTable);
            if (kurzy.length === 0) {
                const row = document.createElement('tr');
                const cell = document.createElement('td');
                cell.colSpan = 3;
                cell.classList.add('text-muted', 'text-center', 'py-3');
                cell.textContent = konfigurace.texty.zadnaData;
                row.appendChild(cell);
                prvky.topCoursesTable.appendChild(row);
            } else {
                kurzy.forEach(kurz => {
                    const row = document.createElement('tr');
                    const název = document.createElement('td');
                    název.textContent = kurz.nazev;
                    const trzby = document.createElement('td');
                    trzby.classList.add('text-end');
                    trzby.textContent = formatMena.format(kurz.trzba);
                    const množství = document.createElement('td');
                    množství.classList.add('text-end');
                    množství.textContent = formatCislo.format(kurz.mnozstvi);
                    row.append(název, trzby, množství);
                    prvky.topCoursesTable.appendChild(row);
                });
            }
        }
    }

    function aktualizujKonverze(konverze) {
        if (!konverze) {
            return;
        }

        const canvas = document.getElementById('conversionChart');
        if (!canvas) {
            return;
        }

        const hodnoty = [konverze.navstevy, konverze.registrace, konverze.platby];
        const popisky = ['Návštěvy', 'Registrace', 'Platby'];

        const config = {
            type: 'bar',
            data: {
                labels: popisky,
                datasets: [
                    {
                        label: 'Počet',
                        data: hodnoty,
                        backgroundColor: [
                            'rgba(13,110,253,0.6)',
                            'rgba(255,193,7,0.6)',
                            'rgba(25,135,84,0.6)'
                        ],
                        borderColor: [
                            'rgba(13,110,253,1)',
                            'rgba(255,193,7,1)',
                            'rgba(25,135,84,1)'
                        ],
                        borderWidth: 1
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                },
                plugins: {
                    tooltip: {
                        callbacks: {
                            label: context => `${context.label}: ${formatCislo.format(context.parsed.y)}`
                        }
                    }
                }
            }
        };

        vytvořNeboAktualizujGraf("conversionChart", canvas, config);

        if (prvky.conversionRates) {
            prvky.conversionRates.textContent = `Registrace z návštěv ${konverze.navstevyNaRegistraci.toFixed(1)} %, platby z registrací ${konverze.registraceNaPlatbu.toFixed(1)} %`;
        }

        if (prvky.conversionDetails) {
            prvky.conversionDetails.innerHTML = '';
            const položky = [
                `Návštěvy: ${formatCislo.format(konverze.navstevy)}`,
                `Registrace: ${formatCislo.format(konverze.registrace)} (${konverze.navstevyNaRegistraci.toFixed(1)} % z návštěv)`,
                `Platby: ${formatCislo.format(konverze.platby)} (${konverze.navstevyNaPlatbu.toFixed(1)} % z návštěv)`
            ];
            položky.forEach(text => {
                const li = document.createElement('li');
                li.textContent = text;
                prvky.conversionDetails.appendChild(li);
            });
        }
    }

    function barvaProObsazenost(podíl) {
        const clamped = Math.max(0, Math.min(1, podíl));
        const start = [13, 110, 253];
        const end = [25, 135, 84];
        const r = Math.round(start[0] + (end[0] - start[0]) * clamped);
        const g = Math.round(start[1] + (end[1] - start[1]) * clamped);
        const b = Math.round(start[2] + (end[2] - start[2]) * clamped);
        const alfa = 0.35 + clamped * 0.45;
        return `rgba(${r}, ${g}, ${b}, ${alfa.toFixed(2)})`;
    }

    function aktualizujHeatmapu(heatmap) {
        const canvas = document.getElementById('heatmapChart');
        if (!canvas) {
            return;
        }

        const bunky = (heatmap && heatmap.bunky) ? heatmap.bunky : [];
        prvky.heatmapEmpty.hidden = bunky.length > 0;

        const dataBody = bunky.map(bunka => {
            const obsazenost = bunka.obsazenost ?? 0;
            return {
                x: bunka.den,
                y: bunka.hodina,
                r: Math.max(4, Math.round(obsazenost * 18) + 4),
                backgroundColor: barvaProObsazenost(obsazenost),
                borderColor: barvaProObsazenost(obsazenost),
                occupancy: obsazenost,
                terms: bunka.pocetTerminu,
                seats: bunka.obsazenaMista,
                capacity: bunka.kapacitaCelkem
            };
        });

        const config = {
            type: 'bubble',
            data: {
                datasets: [
                    {
                        label: 'Obsazenost',
                        data: dataBody,
                        parsing: false,
                        backgroundColor: context => context.raw.backgroundColor,
                        borderColor: context => context.raw.borderColor,
                        borderWidth: 1
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    x: {
                        type: 'linear',
                        min: -0.5,
                        max: 6.5,
                        ticks: {
                            callback: hodnota => denniNazvy[((hodnota % 7) + 7) % 7]
                        }
                    },
                    y: {
                        type: 'linear',
                        min: -0.5,
                        max: 23.5,
                        ticks: {
                            stepSize: 2,
                            callback: hodnota => `${Math.round(hodnota)}:00`
                        }
                    }
                },
                plugins: {
                    tooltip: {
                        callbacks: {
                            title: context => {
                                const den = denniNazvy[((context[0].raw.x % 7) + 7) % 7];
                                const hodina = context[0].raw.y;
                                return `${den}, ${hodina}:00`;
                            },
                            label: context => {
                                const raw = context.raw;
                                const procenta = (raw.occupancy * 100).toFixed(1);
                                return [
                                    `Obsazenost ${procenta} %`,
                                    `Termíny: ${raw.terms}`,
                                    `Obsazená místa: ${formatCislo.format(raw.seats)} / ${formatCislo.format(raw.capacity)}`
                                ];
                            }
                        }
                    }
                }
            }
        };

        vytvořNeboAktualizujGraf("heatmapChart", canvas, config);
    }

    function nastavPředvolbu(range) {
        const dny = parseInt(range, 10);
        if (!Number.isFinite(dny)) {
            return;
        }

        const konec = prvky.filterTo.value ? new Date(`${prvky.filterTo.value}T00:00:00`) : new Date();
        const začátek = new Date(konec);
        začátek.setDate(konec.getDate() - dny + 1);
        prvky.filterFrom.value = začátek.toISOString().slice(0, 10);
        prvky.filterTo.value = konec.toISOString().slice(0, 10);
        načtiPřehled();
    }

    function inicializujPředvolby() {
        prvky.presetButtons.forEach(tlačítko => {
            tlačítko.addEventListener('click', () => nastavPředvolbu(tlačítko.dataset.range));
        });
    }

    function aktualizujRealTime(data) {
        if (!data) {
            return;
        }
        prvky.realtimeUsers.textContent = formatCislo.format(data.onlineUzivatelu ?? 0);
        prvky.realtimeCarts.textContent = formatCislo.format(data.aktivniKosiky ?? 0);
        prvky.realtimeCartValue.textContent = formatMena.format(data.celkovaHodnota ?? 0);
        const čas = data.vytvorenoUtc ? new Date(data.vytvorenoUtc) : new Date();
        prvky.realtimeUpdated.textContent = `Aktualizováno ${čas.toLocaleTimeString('cs-CZ')}`;
    }

    function inicializujSignalR() {
        if (!window.signalR || !konfigurace.api.hub) {
            prvky.realtimeUpdated.textContent = 'SignalR není dostupný.';
            return;
        }

        const připojení = new signalR.HubConnectionBuilder()
            .withUrl(konfigurace.api.hub)
            .withAutomaticReconnect()
            .build();

        připojení.onreconnected(() => {
            prvky.realtimeUpdated.textContent = 'Znovu připojeno – načítám aktuální data…';
            požádejOStatistiky();
        });

        připojení.onclose(() => {
            prvky.realtimeUpdated.textContent = 'Odpojeno, čekám na opětovné připojení…';
        });

        async function požádejOStatistiky() {
            try {
                const data = await připojení.invoke('RequestRealtimeStats');
                aktualizujRealTime(data);
            } catch (chyba) {
                console.error('Nepodařilo se získat real-time data', chyba);
            }
        }

        připojení.start()
            .then(() => {
                prvky.realtimeUpdated.textContent = 'Připojeno k real-time streamu';
                požádejOStatistiky();
                stav.hubTimer = setInterval(požádejOStatistiky, 15000);
            })
            .catch(chyba => {
                console.error('SignalR připojení selhalo', chyba);
                prvky.realtimeUpdated.textContent = 'Nepodařilo se připojit k real-time streamu';
            });

        stav.hub = připojení;
    }

    function inicializujExport() {
        if (!prvky.exportButton) {
            return;
        }
        prvky.exportButton.addEventListener('click', () => {
            if (!stav.posledniFiltry) {
                return;
            }
            const url = sestavUrl(stav.posledniFiltry).replace(konfigurace.api.prehled, konfigurace.api.export);
            const link = document.createElement('a');
            link.href = url;
            link.setAttribute('download', 'report.xlsx');
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
        });
    }

    function inicializujFormulář() {
        if (prvky.filterForm) {
            prvky.filterForm.addEventListener('submit', načtiPřehled);
        }
    }

    document.addEventListener('DOMContentLoaded', () => {
        inicializujFormulář();
        inicializujPředvolby();
        inicializujExport();
        načtiFiltry();
        inicializujSignalR();
    });
})();
