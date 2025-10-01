# Accessibility audit prompt

Níže je návrh promptu, který lze použít při zadávání manuálního nebo kombinovaného (manuální + nástroje) auditu přístupnosti pro aplikaci SysJaky.

---

```
Jednáš jako seniorní specialista na přístupnost webových aplikací. Proveď detailní audit UI aplikace SysJaky se zaměřením na WCAG 2.2 (úroveň AA).

Kontext k aktuální implementaci:
- K dispozici jsou skip linky (`class="skip-link"`).
- Formuláře mají doplněna vhodná `aria-label` nebo napojení přes `aria-describedby`.
- Aktivní prvky mají definované `:focus-visible` styly; outline se nezakrývá.
- Stránka obsahuje live regiony (`#accessibility-live-region` a `#accessibility-assertive-region`) pro oznamování změn.

Na co se soustředit v auditu:
1. Zkontroluj hierarchii nadpisů (maximálně jeden `h1` na stránku, zachovat logickou posloupnost `h1`–`h6`).
2. Ověř klávesovou navigaci u všech modálních dialogů: zachycení fokusu, možnost zavření pomocí Escape, správné `aria-modal` a `aria-labelledby`.
3. Projdi všechny obrázky a grafické komponenty a potvrď přítomnost adekvátních `alt` textů nebo `aria-hidden="true"` u dekorativních prvků.
4. Zapiš nalezené problémy včetně doporučené opravy a odkazu na konkrétní komponentu/URL.
5. Navrhni rychlé manuální testy (např. tabulátor, screen reader) a případné automatizované nástroje (axe DevTools, Lighthouse) s konkrétní konfigurací.

Na závěr vytvoř shrnutí v češtině: hlavní nalezené chyby, jejich závažnost, doporučené priority oprav a seznam nástrojů/testů, které byly použity.
```

---

Prompt lze podle potřeby rozšířit o další specifika konkrétních stránek (např. odkazy na testovací účty nebo uživatelské role). Pokud bude audit probíhat jen nad vybranou sekcí, doporučujeme doplnit přesné URL.
