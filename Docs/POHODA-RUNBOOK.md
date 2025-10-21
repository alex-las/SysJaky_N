# Pohoda mServer Runbook

Tento dokument popisuje rutinní operační kroky a postupy řešení incidentů pro integraci
SysJaky s Pohoda mServerem. Používejte jej při plánovaných zásazích i během incidentů.

## Restart mServeru
1. Ověř na [monitoringu](#monitorovani-a-eskalace), že aktuálně neprobíhá export (poslední
   záznam v logu starší než 5 minut, fronty prázdné).
2. V administrační konzoli Pohody přejdi na **Služby → Pohoda mServer**.
3. Informuj uživatele o krátké nedostupnosti (cca 1–2 min).
4. Zvol `Restartovat službu` a potvrď.
5. Sleduj `/status` endpoint aplikace, dokud nezačne vracet `200 OK` a sekce `pohoda` nehlásí
   chybu.
6. Zkontroluj logy aplikace (`PohodaXmlClient` a background worker), že se exporty obnovily.

## Řešení locku účetní jednotky
1. V logu nebo chybové hlášce identifikuj název uzamčené jednotky.
2. V Pohoda mServer konzoli otevři **Účetní jednotky** a ověř stav.
3. Pokud je účetní jednotka ve stavu `Locked`, komunikuj s účetním – ujisti se, že neprobíhá
   manuální práce.
4. Ukonči případné běžící sezení (`Odstranit spojení`).
5. Pokud se lock neodstraní, restartuj mServer dle výše uvedeného postupu.
6. Po odemčení spusť ruční re-run exportu přes administrační UI aplikace.

## Rotace credentialů
1. Vygeneruj nové heslo u technického účtu v Pohoda mServeru (správce Pohody).
2. Aktualizuj tajemství v používaném správci (Azure Key Vault / Kubernetes secret / App Service
   konfigurace).
3. Nasazení/konfiguraci prováděj mimo provozní špičku; informuj tým.
4. Po aktualizaci proveď `POST /api/pohoda/test-connection` (pokud dostupné) nebo manuální export
   testovací objednávky.
5. Sleduj logy na chyby `401 Unauthorized` – pokud se objeví, vrať konfiguraci na předchozí hodnotu
   a proveď revizi hesla.

## Bezpečné zvýšení timeoutu
1. Ověř v logách, že chyby jsou způsobeny `TaskCanceledException` nebo `TimeoutRejectedException`.
2. Změň hodnotu `PohodaXml:TimeoutSeconds` v konfiguraci (Infrastructure-as-Code nebo secret).
3. Zvyšuj po 10 sekundách, maximálně na 120 sekund, a zaznamenej změnu do provozního deníku.
4. Restartuj aplikaci, aby načetla novou hodnotu (podle typu deploymentu).
5. Monitoruj `/status` a logy – pokud timeouty přetrvávají, zvaž zvýšení `RetryCount` místo dalšího
   prodlužování.

## Monitorování a eskalace
- **/status endpoint** – dostupný bez autentizace pro interní monitoring. Kontroluje
  konektivitu na Pohoda mServer (`pohoda` sekce) a databázi.
- **Health check** – využívá ASP.NET Core health checks (`/health/live`, `/health/ready`).
  Zahrň do cloud monitoringu (Azure Monitor, Prometheus).
- **Logy** – centralizované v Application Insights / Elastic Stack. Hledej zdroje `PohodaXml*` a
  `ExportWorker`.

### Eskalační kontakty
1. **On-call vývojář** – #team-sysjaky (Slack) nebo tel. +420 111 222 333.
2. **Správce Pohody** – pohoda-admin@example.com, tel. +420 444 555 666.
3. **IT Operations** – operations@example.com, tel. +420 777 888 999.

Eskaluj nejprve on-call vývojáři; pokud do 15 minut nereaguje, zapoj správce Pohody. Kritické
incidenty (SLA 1 hod) hlásit současně IT Operations.
