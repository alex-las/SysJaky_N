# Content prompts for Systémy jakosti web

Tyto dílčí prompty jsou připravené pro práci v projektu **SysJaky_N** (ASP.NET Razor Pages). Odkazují na konkrétní soubory a resource klíče, aby bylo možné rychle nasadit konzistentní obsah vycházející z webu [systemy-jakosti.cz](https://www.systemy-jakosti.cz/).

## 1. Globální navigace (`Pages/Shared/_Layout.cshtml` + `Resources/Pages.Shared._Layout.cshtml.resx`)
- **Prompt:** „V souboru `Resources/Pages.Shared._Layout.cshtml.resx` přepiš texty `NavHome`, `NavAbout`, `NavCourses`, `NavArticles`, `NavCorporateInquiry` tak, aby respektovaly terminologii poradenství pro systémy jakosti. Například `NavAbout` → „O společnosti Systémy jakosti“, `NavCourses` → „Kurzy a školení ISO“, `NavArticles` → „Články a případové studie“, `NavCorporateInquiry` → „Firemní poptávka / audit na míru“.“
- **Prompt:** „Do `Resources/Pages.Shared._Layout.cshtml.resx` doplň klíč `NavServices` s hodnotou „Poradenství a audity“, následně v `_Layout.cshtml` přidej novou položku menu odkazující na stránku `/About` s tímto textem.“
- **Prompt:** „Aktualizuj `BrandName` v `Pages.Shared._Layout.cshtml.resx` na „Systémy jakosti – poradenství ISO“, aby byl brand popisnější v kontextu kvality.“

## 2. Hero sekce domovské stránky (`Pages/Index.cshtml` + `Resources/Pages.Index.cshtml.resx`)
- **Prompt:** „V `Pages.Index.cshtml.resx` uprav `HeroMainHeading` na „Implementace, audity a školení systémů jakosti“ a `HeroSubheading` na větu shrnující služby: „Provázíme organizace celým cyklem ISO 9001, ISO 14001, ISO/IEC 17025, ISO 15189, HACCP, ISO 45001, ISO 27001, IATF 16949 a ISO 13485 – od analýzy po certifikaci.““
- **Prompt:** „Doplň `HeroUSP1`, `HeroUSP2`, `HeroUSP3` v `Pages.Index.cshtml.resx` o benefity z původního webu: dlouholeté zkušenosti auditorů, praktické know-how z auditů a možnost interních workshopů.“
- **Prompt:** „Přeformuluj `HeroPrimaryCTA` na „Prohlédnout nabídku kurzů ISO“ a `HeroSecondaryCTA` na „Nechat si připravit firemní řešení“, aby lépe odpovídaly hlavnímu směru služeb.“

## 3. Filtry a mikrotexty formuláře doporučení kurzů (`Pages/Index.cshtml` + resx)
- **Prompt:** „V `Pages.Index.cshtml.resx` aktualizuj `PersonaLabel` na „Vyberte svou roli v systému jakosti“ a `GoalLabel` na „Jaký cíl chcete plněním norem dosáhnout?“; pomocné texty `PersonaHelp` a `GoalHelp` doplň o příklady (např. manažer kvality, interní auditor, příprava na akreditaci).“
- **Prompt:** „Rozšiř seznam štítků `ChipOnline`, `ChipRetraining`, `ChipBeginner`, `ChipPrague`, `ChipCertificate` o další zaměření související s ISO – přidej nové položky `ChipAccreditation` („Příprava na akreditaci“) a `ChipInternalAudits` („Interní audity“).“

## 4. Sekce „Jak to funguje“ (`Pages/Index.cshtml` + resx)
- **Prompt:** „V `Pages.Index.cshtml.resx` přepiš `StepSelectTitle`, `StepRegisterTitle`, `StepPayTitle` na proces odpovídající implementaci ISO: `StepSelectTitle` → „Analyzujte aktuální stav“, `StepRegisterTitle` → „Naplánujte školení a audity“, `StepPayTitle` → „Získejte certifikaci bez stresu“. Popisy kroků doplň o konkrétní činnosti (gap analýza, interní audit, příprava dokumentace).“

## 5. Sekce důvěry a statistiky (`Pages/Index.cshtml` + resx)
- **Prompt:** „Nahraď `TrustGuarantee` textem „Zaručujeme úspěšnou certifikaci nebo vrátíme kurzovné“, `TrustCertificate` rozšiř o zmínku „Absolventi získají oficiální potvrzení auditora / interního auditora“. Čísla v `TrustGraduates` a `TrustRating` ponech, jen uprav slovník na „certifikovaných odborníků“.“
- **Prompt:** „Doplň `WhyChooseHeading` na „Proč spolupracovat se Systémy jakosti?“ a `WhyChooseDescription` o hlavní argumenty: 20 let praxe, tým externích auditorů, know-how z desítek odvětví.“

## 6. Novinky a doporučené kurzy (`Pages/Index.cshtml` + resx)
- **Prompt:** „Přejmenuj `NewsHeading` na „Aktuality z oblasti ISO“ a `RecommendedForYou` na „Kurzy doporučené pro vaši certifikační cestu“. `StartingSoon` změň na „Brzy startují“ a `RecommendedEmpty` doplň o výzvu, aby si uživatel vybral normu nebo cíl (např. „Vyberte normu ISO nebo cíl, se kterým potřebujete pomoci“).“

## 7. Další obsahové prvky
- **Prompt:** „V `Resources/SharedResources.resx` doplň klíče pro časté texty z původního webu: `ConsultingIntro`, `TrainingIntro`, `AuditSupport`, `DocumentTemplates` – přidej texty popisující poradenství, školení, podporu auditů a poskytované šablony dokumentace.“
- **Prompt:** „Pro stránku `Pages/CorporateInquiry.cshtml` doplň do resx souboru konkrétní popisy balíčků: „Kompletní zavedení ISO 9001“, „Integrovaný systém IMS“, „Akreditační balíček pro laboratoře“ s krátkým vysvětlením rozsahu.“
- **Prompt:** „V šabloně `Pages/Articles/Index.cshtml` doplň úvodní odstavec shrnující typ obsahu: best practices pro ISO 9001/14001, checklisty auditora, změny legislativy.“

## 8. Obecné pokyny pro CODEX
- ResX soubory edituj v UTF-8 a zachovej HTML entity (např. `&amp;`).
- Při úpravě textů vždy aktualizuj českou (`*.resx`) i anglickou mutaci (`*.en.resx`), aby lokalizace zůstala synchronizovaná.
- Pokud doplňuješ nové klíče, nezapomeň je vložit do příslušných Razor šablon přes `@Localizer["Key"]`.

Tyto prompty můžeš zadávat postupně podle toho, jaký blok obsahu chceš přepracovat. Díky tomu zůstane obsah webu konzistentní s nabídkou služeb společnosti Systémy jakosti.
