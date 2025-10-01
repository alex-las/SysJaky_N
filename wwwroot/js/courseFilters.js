const dataElement = document.getElementById('courseFiltersData');
if (!dataElement) {
    console.warn('courseFilters: missing configuration element');
} else {
    const config = JSON.parse(dataElement.textContent || '{}');
    const priceBounds = {
        min: Number(config.price?.min ?? 0),
        max: Number(config.price?.max ?? 0)
    };
    const resources = config.resources ?? {};
    const culture = config.culture ?? {};
    const initial = config.initial ?? {};
    const filtersConfig = config.filters ?? {};

    const state = {
        pageNumber: Number(initial.pageNumber ?? 1),
        totalPages: Number(initial.totalPages ?? 1),
        totalCount: Number(initial.totalCount ?? 0),
        search: initial.search ?? '',
        norms: new Set((initial.norms ?? []).map(Number)),
        cities: new Set((initial.cities ?? []).map(Number)),
        levels: new Set((initial.levels ?? []).map(String)),
        types: new Set((initial.types ?? []).map(String)),
        categories: new Set((initial.categories ?? []).map(String)),
        minPrice: Number(initial.minPrice ?? priceBounds.min),
        maxPrice: Number(initial.maxPrice ?? priceBounds.max)
    };

    const defaultFormatter = {
        format(value) {
            const symbol = resources.currencySymbol ?? '';
            return `${value.toFixed(0)} ${symbol}`.trim();
        }
    };

    let currencyFormatter = defaultFormatter;
    try {
        currencyFormatter = new Intl.NumberFormat(culture.name || undefined, {
            style: 'currency',
            currency: culture.currencyCode || 'CZK'
        });
    } catch {
        currencyFormatter = defaultFormatter;
    }

    const controls = {
        search: [],
        norms: [],
        cities: [],
        categories: [],
        levels: [],
        types: [],
        priceMin: [],
        priceMax: [],
        priceDisplayMin: [],
        priceDisplayMax: [],
        saveButtons: [],
        resetButtons: [],
        feedback: []
    };

    const compareSelection = new Set();
    let errorTimer = null;

    const resultCountElement = document.getElementById('resultCount');
    const activeFiltersElement = document.getElementById('activeFilters');
    const coursesGrid = document.getElementById('coursesGrid');
    const noCoursesElement = document.getElementById('noCourses');
    const courseErrorElement = document.getElementById('courseError');
    const paginationElement = document.getElementById('coursePagination');
    const paginationStatusElement = document.getElementById('paginationStatus');
    const resetAllLink = document.querySelector('[data-action="reset-all"]');

    const compareBar = document.getElementById('cmpBar');
    const compareCountElement = document.getElementById('cmpCount');
    const compareButton = document.getElementById('cmpGo');
    const compareFormat = compareBar?.dataset.countFormat ?? '{0}';
    const compareCta = compareBar?.dataset.ctaText ?? resources.compareLabel ?? '';
    if (compareButton && compareCta) {
        compareButton.textContent = compareCta;
    }

    const normOptions = new Map((filtersConfig.norms ?? []).map(opt => [String(opt.id), opt.name]));
    const cityOptions = new Map((filtersConfig.cities ?? []).map(opt => [String(opt.id), opt.name]));
    const categoryOptions = filtersConfig.categories ?? [];
    const categoryOptionMap = new Map(categoryOptions.map(opt => [String(opt.id), opt]));
    const levelOptions = filtersConfig.levels ?? [];
    const typeOptions = filtersConfig.types ?? [];

    const containers = document.querySelectorAll('[data-filter-container]');
    containers.forEach(container => {
        const search = container.querySelector('[data-filter="search"]');
        if (search) controls.search.push(search);
        const norms = container.querySelector('[data-filter="norms"]');
        if (norms) controls.norms.push(norms);
        const cities = container.querySelector('[data-filter="cities"]');
        if (cities) controls.cities.push(cities);
        const categories = container.querySelector('[data-filter="categories"]');
        if (categories) controls.categories.push(categories);
        const levels = container.querySelector('[data-filter="levels"]');
        if (levels) controls.levels.push(levels);
        const types = container.querySelector('[data-filter="types"]');
        if (types) controls.types.push(types);
        const priceMin = container.querySelector('[data-filter="price-min"]');
        if (priceMin) controls.priceMin.push(priceMin);
        const priceMax = container.querySelector('[data-filter="price-max"]');
        if (priceMax) controls.priceMax.push(priceMax);
        const priceMinDisplay = container.querySelector('[data-price-display="min"]');
        if (priceMinDisplay) controls.priceDisplayMin.push(priceMinDisplay);
        const priceMaxDisplay = container.querySelector('[data-price-display="max"]');
        if (priceMaxDisplay) controls.priceDisplayMax.push(priceMaxDisplay);
        const saveButton = container.querySelector('[data-action="save-filters"]');
        if (saveButton) controls.saveButtons.push(saveButton);
        const resetButton = container.querySelector('[data-action="reset-filters"]');
        if (resetButton) controls.resetButtons.push(resetButton);
        const feedback = container.querySelector('[data-filter="feedback"]');
        if (feedback) controls.feedback.push(feedback);
    });

    let checkboxIdCounter = 0;

    function createInlineCheckbox(container, group, option) {
        const wrapper = document.createElement('div');
        wrapper.className = 'form-check form-check-inline';
        const input = document.createElement('input');
        input.type = 'checkbox';
        input.className = 'form-check-input';
        const id = `${group}_${checkboxIdCounter++}`;
        input.id = id;
        input.value = option.value;
        input.dataset.filterOption = group;
        const label = document.createElement('label');
        label.className = 'form-check-label small';
        label.htmlFor = id;
        label.textContent = option.label;
        wrapper.appendChild(input);
        wrapper.appendChild(label);
        container.appendChild(wrapper);
        return input;
    }

    const levelInputs = [];
    controls.levels.forEach(container => {
        container.innerHTML = '';
        levelOptions.forEach(option => {
            const input = createInlineCheckbox(container, 'levels', option);
            levelInputs.push(input);
        });
    });

    const typeInputs = [];
    controls.types.forEach(container => {
        container.innerHTML = '';
        typeOptions.forEach(option => {
            const input = createInlineCheckbox(container, 'types', option);
            typeInputs.push(input);
        });
    });

    const priceSpan = priceBounds.max - priceBounds.min;
    const priceStep = priceSpan > 0 ? Math.max(1, Math.round(priceSpan / 50)) : 1;

    let syncing = false;

    function formatCurrency(value) {
        if (!Number.isFinite(value)) {
            return currencyFormatter.format(0);
        }

        try {
            return currencyFormatter.format(value);
        } catch {
            return defaultFormatter.format(value);
        }
    }

    function syncControls() {
        syncing = true;
        controls.search.forEach(input => {
            input.value = state.search ?? '';
        });

        const validCategoryIds = new Set(categoryOptions.map(option => String(option.id)));
        Array.from(state.categories).forEach(id => {
            if (!validCategoryIds.has(String(id))) {
                state.categories.delete(id);
            }
        });

        const categoryTemplate = resources.categoryCountTemplate || '{0} ({1})';
        controls.categories.forEach(select => {
            select.innerHTML = '';
            categoryOptions.forEach(option => {
                const optionElement = document.createElement('option');
                const optionId = String(option.id);
                optionElement.value = optionId;
                const count = Number(option.count ?? 0);
                if (Number.isFinite(count) && count >= 0) {
                    optionElement.textContent = categoryTemplate
                        .replace('{0}', option.name ?? '')
                        .replace('{1}', count.toString());
                } else {
                    optionElement.textContent = option.name ?? '';
                }
                optionElement.selected = state.categories.has(optionId);
                select.appendChild(optionElement);
            });
        });

        controls.norms.forEach(select => {
            select.innerHTML = '';
            normOptions.forEach((name, id) => {
                const option = document.createElement('option');
                option.value = id;
                option.textContent = name;
                option.selected = state.norms.has(Number(id));
                select.appendChild(option);
            });
        });

        controls.cities.forEach(select => {
            select.innerHTML = '';
            cityOptions.forEach((name, id) => {
                const option = document.createElement('option');
                option.value = id;
                option.textContent = name;
                option.selected = state.cities.has(Number(id));
                select.appendChild(option);
            });
        });

        levelInputs.forEach(input => {
            input.checked = state.levels.has(input.value);
        });

        typeInputs.forEach(input => {
            input.checked = state.types.has(input.value);
        });

        controls.priceMin.forEach(input => {
            input.min = priceBounds.min;
            input.max = priceBounds.max;
            input.step = priceStep;
            input.value = Math.max(priceBounds.min, Math.min(state.minPrice, priceBounds.max));
            input.disabled = priceBounds.max <= priceBounds.min;
        });

        controls.priceMax.forEach(input => {
            input.min = priceBounds.min;
            input.max = priceBounds.max;
            input.step = priceStep;
            input.value = Math.max(priceBounds.min, Math.min(state.maxPrice, priceBounds.max));
            input.disabled = priceBounds.max <= priceBounds.min;
        });

        const minDisplayValue = Math.max(priceBounds.min, Math.min(state.minPrice, priceBounds.max));
        const maxDisplayValue = Math.max(priceBounds.min, Math.min(state.maxPrice, priceBounds.max));

        controls.priceDisplayMin.forEach(span => {
            span.textContent = formatCurrency(minDisplayValue);
        });
        controls.priceDisplayMax.forEach(span => {
            span.textContent = formatCurrency(maxDisplayValue);
        });

        syncing = false;
    }

    function clampPrice(value) {
        if (!Number.isFinite(value)) {
            return priceBounds.min;
        }
        return Math.max(priceBounds.min, Math.min(priceBounds.max, value));
    }

    function updateResultCount(count) {
        if (!resultCountElement) {
            return;
        }
        const template = resultCountElement.dataset.countTemplate || resources.resultCountTemplate || '{0}';
        resultCountElement.textContent = template.replace('{0}', count.toString());
    }

    function updatePagination(meta) {
        state.pageNumber = Number(meta.pageNumber ?? state.pageNumber);
        state.totalPages = Number(meta.totalPages ?? state.totalPages);
        state.totalCount = Number(meta.totalCount ?? state.totalCount);

        updateResultCount(state.totalCount);

        if (paginationElement) {
            const shouldHide = !Number.isFinite(state.totalPages) || state.totalPages <= 1;
            paginationElement.classList.toggle('d-none', shouldHide);

            const prevButton = paginationElement.querySelector('[data-action="prev-page"]');
            const nextButton = paginationElement.querySelector('[data-action="next-page"]');
            const prevItem = prevButton?.closest('.page-item');
            const nextItem = nextButton?.closest('.page-item');

            if (prevItem) {
                prevItem.classList.toggle('disabled', state.pageNumber <= 1);
            }
            if (nextItem) {
                nextItem.classList.toggle('disabled', state.pageNumber >= state.totalPages);
            }

            if (paginationStatusElement) {
                const template = resources.pageStatusTemplate || '{0}/{1}';
                paginationStatusElement.textContent = template
                    .replace('{0}', state.pageNumber.toString())
                    .replace('{1}', Math.max(state.totalPages, 1).toString());
            }
        }
    }

    function escapeHtml(value) {
        return (value ?? '').replace(/[&<>"]/g, ch => {
            switch (ch) {
                case '&': return '&amp;';
                case '<': return '&lt;';
                case '>': return '&gt;';
                case '"': return '&quot;';
                default: return ch;
            }
        });
    }

    function escapeAttribute(value) {
        return (value ?? '').replace(/[&"']/g, ch => {
            switch (ch) {
                case '&': return '&amp;';
                case '"': return '&quot;';
                case "'": return '&#39;';
                default: return ch;
            }
        });
    }

    function getIsoColor(code) {
        switch ((code ?? '').toString()) {
            case '9001': return '#0d6efd';
            case '14001': return '#198754';
            case '45001': return '#dc3545';
            case '27001': return '#6f42c1';
            default: return '#0aa2c0';
        }
    }

    function getIsoIconPath(code) {
        switch ((code ?? '').toString()) {
            case '9001':
                return 'M12 2C6.477 2 2 6.477 2 12s4.477 10 10 10 10-4.477 10-10S17.523 2 12 2Zm0 2a8 8 0 1 1 0 16 8 8 0 0 1 0-16Zm-.5 3.5a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3Zm3.5 2.5a2 2 0 0 0-2 2v3.5h1.75a.75.75 0 0 1 0 1.5h-4.5a.75.75 0 1 1 0-1.5H10v-4a3.5 3.5 0 0 1 7 0v.25a.75.75 0 0 1-1.5 0V10a2 2 0 0 0-2-2Z';
            case '14001':
                return 'M12 3c.414 0 .75.336.75.75V6h2.25a.75.75 0 0 1 0 1.5H12a.75.75 0 0 1-.75-.75V3.75c0-.414.336-.75.75-.75Zm-4.5 4A1.5 1.5 0 0 1 9 8.5V18h8.25a.75.75 0 0 1 0 1.5H8.25A.75.75 0 0 1 7.5 18V8.5A1.5 1.5 0 0 1 9 7h1.5ZM6 10.5a.75.75 0 0 1 .75.75V18a3 3 0 0 0 3 3h7.5a.75.75 0 0 1 0 1.5h-7.5A4.5 4.5 0 0 1 5.25 18v-6.75A.75.75 0 0 1 6 10.5Z';
            case '45001':
                return 'M12 2a10 10 0 1 1 0 20 10 10 0 0 1 0-20Zm0 1.5a8.5 8.5 0 1 0 0 17 8.5 8.5 0 0 0 0-17ZM9.5 8a2.5 2.5 0 1 1 0 5h-.75v3.75a.75.75 0 1 1-1.5 0V8.75A.75.75 0 0 1 8 8h1.5Zm5 0A2.5 2.5 0 0 1 17 10.5v.25a.75.75 0 0 1-1.5 0v-.25a1 1 0 0 0-2 0v5.5a.75.75 0 0 1-1.5 0v-5.5A2.5 2.5 0 0 1 14.5 8Z';
            case '27001':
                return 'M11.25 4a3.75 3.75 0 0 1 7.5 0v4.25h.75a1.5 1.5 0 0 1 1.5 1.5v9A1.5 1.5 0 0 1 19.5 20h-15A1.5 1.5 0 0 1 3 18.75v-9A1.5 1.5 0 0 1 4.5 8.25H5.25V4a3.75 3.75 0 0 1 7.5 0v4.25h-1.5V4a2.25 2.25 0 0 0-4.5 0v4.25h9V4a2.25 2.25 0 0 0-4.5 0v4.25h-1.5ZM12 13.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5Zm0 1.5a1 1 0 1 1 0 2 1 1 0 0 1 0-2Z';
            default:
                return 'M12 2a10 10 0 1 1-7.07 2.93A10 10 0 0 1 12 2Zm0 1.5a8.5 8.5 0 1 0 6.01 14.51A8.5 8.5 0 0 0 12 3.5Zm0 4a1.5 1.5 0 1 1 0 3h-.25a.75.75 0 0 0-.75.75V15a.75.75 0 0 0 1.5 0v-2.25H13a2.25 2.25 0 1 0-1-4.25Z';
        }
    }

    function buildCountdown(daysUntilStart) {
        if (typeof daysUntilStart !== 'number' || Number.isNaN(daysUntilStart)) {
            return '';
        }
        if (daysUntilStart > 0) {
            return (resources.countdownTemplate ?? 'Začíná za {0} dní').replace('{0}', daysUntilStart);
        }
        if (daysUntilStart === 0) {
            return resources.countdownToday ?? 'Začíná dnes';
        }
        return resources.countdownPast ?? 'Kurz probíhá';
    }

    function isExternalUrl(url) {
        return /^(?:https?:)?\/\//i.test(url);
    }

    function appendImageParams(url, params) {
        if (!url || isExternalUrl(url) || (!params.width && !params.format)) {
            return url || '';
        }

        const [path, query = ''] = url.split('?', 2);
        const search = new URLSearchParams(query);
        if (params.width && !search.has('w')) {
            search.set('w', String(params.width));
        }
        if (params.format && !search.has('format')) {
            search.set('format', params.format);
        }
        const queryString = search.toString();
        return queryString ? `${path}?${queryString}` : path;
    }

    function buildCourseCardImage(baseUrl, altText) {
        if (!baseUrl) {
            return '';
        }

        const widths = [480, 768, 1200];
        const webpSources = [];
        const jpegSources = [];

        widths.forEach((width) => {
            const webpUrl = appendImageParams(baseUrl, { width, format: 'webp' });
            if (webpUrl) {
                webpSources.push(`${escapeAttribute(webpUrl)} ${width}w`);
            }

            const jpegUrl = appendImageParams(baseUrl, { width, format: 'jpg' });
            if (jpegUrl) {
                jpegSources.push(`${escapeAttribute(jpegUrl)} ${width}w`);
            }
        });

        const fallback = appendImageParams(baseUrl, { width: 768, format: 'jpg' }) || baseUrl;
        const fallbackAttr = escapeAttribute(fallback);
        const altAttr = escapeAttribute(altText ?? '');
        const webpHtml = webpSources.length
            ? `<source type="image/webp" srcset="${webpSources.join(', ')}" sizes="(max-width: 768px) 100vw, 480px">`
            : '';
        const jpegHtml = jpegSources.length
            ? `<source type="image/jpeg" srcset="${jpegSources.join(', ')}" sizes="(max-width: 768px) 100vw, 480px">`
            : '';

        return `<picture class="course-card__picture">
            ${webpHtml}
            ${jpegHtml}
            <img class="course-card__image" src="${fallbackAttr}" alt="${altAttr}" loading="lazy" decoding="async" onload="this.dataset.loaded='true';">
        </picture>`;
    }

    function createCourseCard(course) {
        const wrapper = document.createElement('div');
        const id = Number(course.id);
        const checkboxId = `cmp_${id}`;
        const title = escapeHtml(course.title ?? '');
        const titleAttr = escapeAttribute(course.title ?? '');
        const description = escapeHtml(course.description ?? '');
        const level = escapeHtml(course.level ?? '');
        const mode = escapeHtml(course.mode ?? '');
        const type = escapeHtml(course.type ?? '');
        const durationDisplay = escapeHtml(course.durationDisplay ?? '');
        const dateDisplay = escapeHtml(course.dateDisplay ?? '');
        const priceDisplay = escapeHtml(course.priceDisplay ?? '');
        const detailsUrl = escapeAttribute(course.detailsUrl ?? `#/course/${id}`);
        const addToCartUrl = escapeAttribute(course.addToCartUrl ?? '/Courses/Index?handler=AddToCart');
        const popoverHtml = course.popoverHtml ? escapeAttribute(course.popoverHtml) : null;
        const rawCoverImageUrl = typeof course.coverImageUrl === 'string' ? course.coverImageUrl : null;
        const previewText = escapeHtml(course.previewContent ?? '');
        const isoBadges = Array.isArray(course.isoBadges) ? course.isoBadges : [];
        const daysUntilStart = typeof course.daysUntilStart === 'number' ? course.daysUntilStart : null;
        const capacity = Number(course.capacity ?? 0);
        const seatsTaken = Number(course.seatsTaken ?? 0);
        const occupancyPercent = Math.max(0, Math.min(100, Math.round(Number(course.occupancyPercent ?? (capacity > 0 ? (seatsTaken / capacity) * 100 : 0)))));
        const hasCertificate = Boolean(course.hasCertificate);
        const countdownText = buildCountdown(daysUntilStart);
        const isoList = isoBadges.map((badge) => {
            const label = escapeHtml(badge.label ?? 'ISO');
            const code = escapeHtml(badge.code ?? '');
            const color = getIsoColor(badge.code);
            const iconPath = getIsoIconPath(badge.code);
            const aria = (resources.isoBadgeAria ?? 'Certifikace {0}').replace('{0}', label);
            return `<li class="course-card__iso-badge" aria-label="${escapeAttribute(aria)}">
                <span class="course-card__iso-icon" style="--iso-color:${color}">
                    <svg viewBox="0 0 24 24" role="presentation" aria-hidden="true"><path d="${iconPath}"></path></svg>
                </span>
                <span class="course-card__iso-label">${label}</span>
            </li>`;
        }).join('');

        const coverHtml = rawCoverImageUrl
            ? buildCourseCardImage(rawCoverImageUrl, titleAttr)
            : '<div class="course-card__image course-card__image--placeholder" aria-hidden="true"></div>';

        const popoverLink = popoverHtml
            ? `<a tabindex="0" role="button" class="course-card__info" data-bs-toggle="popover" data-bs-html="true" data-bs-content="${popoverHtml}">
                    <i class="bi bi-info-circle" aria-hidden="true"></i> ${resources.additionalInfo ?? ''}
               </a>`
            : '';

        const occupancyAria = (resources.occupancyAria ?? 'Obsazenost {0}%').replace('{0}', occupancyPercent);
        const occupancyLabel = resources.occupancyLabel ?? 'Obsazenost';
        const typeAria = (resources.typeAria ?? 'Typ: {0}').replace('{0}', type);
        const certificateText = hasCertificate
            ? resources.certificateAvailable ?? 'Certifikát'
            : resources.certificateUnavailable ?? 'Bez certifikátu';
        const modeAria = (resources.modeAria ?? 'Režim: {0}').replace('{0}', mode);
        const levelAria = (resources.levelAria ?? 'Úroveň: {0}').replace('{0}', level);

        wrapper.innerHTML = `
        <article class="course-card card-hover feature-card h-100 d-flex flex-column" role="article" aria-labelledby="course-card-title-${id}">
            <div class="course-card__media position-relative">
                ${coverHtml}
                ${isoList ? `<ul class="course-card__iso-list" aria-label="${escapeAttribute(resources.isoBadgeListAria ?? '')}">${isoList}</ul>` : ''}
                <button type="button"
                        class="course-card__wishlist"
                        data-wishlist-button
                        data-course-id="${id}"
                        data-wishlist-label-add="${escapeAttribute(resources.wishlistAdd ?? '')}"
                        data-wishlist-label-remove="${escapeAttribute(resources.wishlistRemove ?? '')}"
                        aria-pressed="false"
                        aria-label="${escapeAttribute(resources.wishlistAdd ?? '')}">
                    <svg viewBox="0 0 24 24" role="presentation" aria-hidden="true"><path d="M12 21s-6.716-4.418-9.193-7.368C.386 10.74.7 6.54 3.64 4.7 5.49 3.55 7.86 3.87 9.34 5.35L12 8.01l2.66-2.66c1.48-1.48 3.85-1.8 5.7-.65 2.94 1.84 3.25 6.04.83 8.93C18.72 16.58 12 21 12 21Z"></path></svg>
                    <span class="visually-hidden">${escapeHtml(resources.wishlistAdd ?? '')}</span>
                </button>
            </div>
            <div class="course-card__body d-flex flex-column flex-grow-1 gap-3 p-3">
                <header>
                    <h3 id="course-card-title-${id}" class="h5 mb-1 course-card__title">${title}</h3>
                    ${description ? `<p class="course-card__excerpt text-muted mb-0">${description}</p>` : ''}
                </header>
                <div class="course-card__meta d-flex flex-wrap gap-2" aria-label="${escapeAttribute(resources.metaInformation ?? '')}">
                    <span class="course-card__meta-item" aria-label="${escapeAttribute(modeAria)}">
                        <i class="bi bi-broadcast" aria-hidden="true"></i><span>${mode}</span>
                    </span>
                    <span class="course-card__meta-item" aria-label="${escapeAttribute(levelAria)}">
                        <i class="bi bi-bar-chart" aria-hidden="true"></i><span>${level}</span>
                    </span>
                    <span class="course-card__meta-item" aria-label="${escapeAttribute(typeAria)}">
                        <i class="bi bi-geo-alt" aria-hidden="true"></i><span>${type}</span>
                    </span>
                    <span class="course-card__meta-item" aria-label="${escapeAttribute(resources.certificateAria ?? '')}">
                        <i class="bi bi-patch-check" aria-hidden="true"></i><span>${escapeHtml(certificateText)}</span>
                    </span>
                    <button type="button" class="course-card__preview" data-course-preview="${previewText}" aria-label="${escapeAttribute(resources.previewLabel ?? '')}">
                        <i class="bi bi-eye" aria-hidden="true"></i>
                    </button>
                </div>
                <div class="course-card__progress" aria-label="${escapeAttribute(occupancyAria)}">
                    <div class="progress" role="progressbar" aria-valuenow="${occupancyPercent}" aria-valuemin="0" aria-valuemax="100">
                        <div class="progress-bar" style="width:${occupancyPercent}%"></div>
                    </div>
                    <div class="course-card__progress-text">${occupancyLabel}: ${occupancyPercent}%</div>
                </div>
                <div class="course-card__schedule d-flex flex-wrap justify-content-between gap-2 align-items-center">
                    <div class="d-flex flex-column">
                        <span class="course-card__date" aria-label="${escapeAttribute((resources.courseDateAria ?? '').replace('{0}', dateDisplay))}">${dateDisplay}</span>
                        ${countdownText ? `<span class="course-card__countdown">${escapeHtml(countdownText)}</span>` : ''}
                    </div>
                    <div class="course-card__price fw-semibold text-end">${priceDisplay}</div>
                </div>
                ${popoverLink}
                <div class="course-card__footer mt-auto d-flex flex-wrap align-items-center justify-content-between gap-3">
                    <div class="form-check form-check-inline m-0">
                        <input class="form-check-input cmp-check" type="checkbox" value="${id}" id="${checkboxId}">
                        <label class="form-check-label small" for="${checkboxId}">${resources.compareLabel ?? 'Porovnat'}</label>
                    </div>
                    <div class="course-card__actions d-flex flex-wrap gap-2">
                        <a class="btn btn-outline-secondary btn-sm" href="${detailsUrl}">${resources.detailsLabel ?? 'Detail'}</a>
                        <form method="post" action="${addToCartUrl}" class="d-inline">
                            <input type="hidden" name="courseId" value="${id}">
                            <button type="submit" class="btn btn-primary btn-sm">${resources.enrollLabel ?? 'Přihlásit'}</button>
                        </form>
                    </div>
                </div>
            </div>
        </article>`;

        const card = wrapper.firstElementChild;
        if (card) {
            coursesGrid?.appendChild(card);
            window.courseCardWishlist?.syncAll(card);
            window.courseCardPreview?.register(card);
        }
    }

    function initPopovers(scope) {
        if (!window.bootstrap || !scope) {
            return;
        }
        const triggers = scope.querySelectorAll('[data-bs-toggle="popover"]');
        triggers.forEach(trigger => {
            window.bootstrap.Popover.getOrCreateInstance(trigger);
        });
    }

    function showCourseError(message, { autoHide = false } = {}) {
        if (!courseErrorElement) {
            return;
        }

        if (errorTimer) {
            clearTimeout(errorTimer);
            errorTimer = null;
        }

        if (!message) {
            courseErrorElement.classList.add('d-none');
            courseErrorElement.textContent = '';
            return;
        }

        courseErrorElement.textContent = message;
        courseErrorElement.classList.remove('d-none');

        if (autoHide) {
            errorTimer = setTimeout(() => {
                if (courseErrorElement.textContent === message) {
                    courseErrorElement.classList.add('d-none');
                    courseErrorElement.textContent = '';
                }
            }, 4000);
        }
    }

    function renderCourses(courses) {
        if (!coursesGrid) {
            return;
        }
        coursesGrid.innerHTML = '';
        (courses ?? []).forEach(course => createCourseCard(course));

        const checkboxes = coursesGrid.querySelectorAll('.cmp-check');
        checkboxes.forEach(checkbox => {
            const value = String(checkbox.value);
            checkbox.checked = compareSelection.has(value);
            checkbox.addEventListener('change', () => {
                if (checkbox.checked) {
                    compareSelection.add(value);
                    if (compareSelection.size > 3) {
                        compareSelection.delete(value);
                        checkbox.checked = false;
                        showCourseError(resources.compareLimit ?? 'Můžete porovnat maximálně 3 kurzy.', { autoHide: true });
                        updateCompareBar();
                        return;
                    }
                } else {
                    compareSelection.delete(value);
                }
                updateCompareBar();
            });
        });

        initPopovers(coursesGrid);

        if (noCoursesElement) {
            noCoursesElement.classList.toggle('d-none', (courses ?? []).length > 0);
        }
    }

    function updateCompareBar() {
        if (!compareBar || !compareCountElement) {
            return;
        }
        const selected = compareSelection.size;
        compareCountElement.textContent = compareFormat.replace('{0}', selected.toString());

        if (!compareButton) {
            return;
        }

        if (selected >= 2 && selected <= 3) {
            compareBar.classList.remove('d-none');
            compareButton.classList.remove('disabled');
            compareButton.removeAttribute('aria-disabled');
            compareButton.href = `/Courses/Compare?ids=${Array.from(compareSelection).join(',')}`;
        } else {
            compareButton.classList.add('disabled');
            compareButton.setAttribute('aria-disabled', 'true');
            compareButton.removeAttribute('href');
            if (selected === 0) {
                compareBar.classList.add('d-none');
            } else {
                compareBar.classList.remove('d-none');
            }
        }
    }

    function clearChildren(element) {
        while (element?.firstChild) {
            element.removeChild(element.firstChild);
        }
    }

    function createChip(label, type, value) {
        const chip = document.createElement('button');
        chip.type = 'button';
        chip.className = 'chip chip-light';
        chip.dataset.removeType = type;
        if (value !== undefined) {
            chip.dataset.removeValue = String(value);
        }
        chip.innerHTML = `${label} <span class="ms-1" aria-hidden="true">&times;</span>`;
        chip.setAttribute('aria-label', `${resources.removeLabel ?? 'Odebrat filtr'} ${label}`);
        chip.addEventListener('click', () => {
            removeFilter(type, value);
        });
        return chip;
    }

    function updateChips() {
        if (!activeFiltersElement) {
            return;
        }
        clearChildren(activeFiltersElement);

        if (state.search) {
            activeFiltersElement.appendChild(createChip(`${resources.searchLabel ?? 'Hledat'}: ${state.search}`, 'search'));
        }

        state.norms.forEach(id => {
            const name = normOptions.get(String(id));
            if (name) {
                activeFiltersElement.appendChild(createChip(name, 'norms', id));
            }
        });

        state.cities.forEach(id => {
            const name = cityOptions.get(String(id));
            if (name) {
                activeFiltersElement.appendChild(createChip(name, 'cities', id));
            }
        });

        state.categories.forEach(id => {
            const option = categoryOptionMap.get(String(id));
            if (option) {
                activeFiltersElement.appendChild(createChip(option.name ?? String(id), 'categories', id));
            }
        });

        state.levels.forEach(level => {
            const option = levelOptions.find(opt => opt.value === level);
            if (option) {
                activeFiltersElement.appendChild(createChip(option.label, 'levels', level));
            }
        });

        state.types.forEach(type => {
            const option = typeOptions.find(opt => opt.value === type);
            if (option) {
                activeFiltersElement.appendChild(createChip(option.label, 'types', type));
            }
        });

        if (priceBounds.max > priceBounds.min) {
            const minVal = clampPrice(state.minPrice);
            const maxVal = clampPrice(state.maxPrice);
            if (minVal > priceBounds.min || maxVal < priceBounds.max) {
                const label = `${resources.priceLabel ?? 'Cena'}: ${formatCurrency(minVal)} – ${formatCurrency(maxVal)}`;
                activeFiltersElement.appendChild(createChip(label, 'price'));
            }
        }
    }

    function removeFilter(type, value) {
        switch (type) {
            case 'search':
                state.search = '';
                break;
            case 'norms':
                state.norms.delete(Number(value));
                break;
            case 'cities':
                state.cities.delete(Number(value));
                break;
            case 'categories':
                state.categories.delete(String(value));
                break;
            case 'levels':
                state.levels.delete(String(value));
                break;
            case 'types':
                state.types.delete(String(value));
                break;
            case 'price':
                state.minPrice = priceBounds.min;
                state.maxPrice = priceBounds.max;
                break;
        }
        state.pageNumber = 1;
        syncControls();
        updateChips();
        scheduleFetch();
    }

    function showFeedback(message) {
        controls.feedback.forEach(el => {
            el.textContent = message;
        });
        if (message) {
            setTimeout(() => {
                controls.feedback.forEach(el => {
                    el.textContent = '';
                });
            }, 3000);
        }
    }

    function saveFilters() {
        try {
            const payload = {
                search: state.search,
                norms: Array.from(state.norms),
                cities: Array.from(state.cities),
                categories: Array.from(state.categories),
                levels: Array.from(state.levels),
                types: Array.from(state.types),
                minPrice: clampPrice(state.minPrice),
                maxPrice: clampPrice(state.maxPrice)
            };
            window.localStorage?.setItem('sysjaky.courses.filters', JSON.stringify(payload));
            showFeedback(resources.savedMessage ?? 'Filtry byly uloženy.');
        } catch (error) {
            console.warn('courseFilters: unable to save filters', error);
        }
    }

    function loadSavedFilters() {
        try {
            const raw = window.localStorage?.getItem('sysjaky.courses.filters');
            if (!raw) {
                return null;
            }
            return JSON.parse(raw);
        } catch (error) {
            console.warn('courseFilters: unable to load filters', error);
            return null;
        }
    }

    function resetState() {
        state.search = '';
        state.norms.clear();
        state.cities.clear();
        state.categories.clear();
        state.levels.clear();
        state.types.clear();
        state.minPrice = priceBounds.min;
        state.maxPrice = priceBounds.max;
        state.pageNumber = 1;
    }

    function resetFilters(triggerFetch = true) {
        resetState();
        syncControls();
        updateChips();
        if (triggerFetch) {
            scheduleFetch();
        }
    }

    const debounce = (fn, delay) => {
        let timer;
        return (...args) => {
            clearTimeout(timer);
            timer = setTimeout(() => fn(...args), delay);
        };
    };

    const scheduleFetch = debounce(() => fetchCourses(), 250);

    let currentRequest = null;

    function buildQuery() {
        const params = new URLSearchParams();
        params.set('PageNumber', String(state.pageNumber));
        if (state.search) {
            params.set('SearchString', state.search);
        }
        state.norms.forEach(id => params.append('SelectedTagIds', String(id)));
        state.cities.forEach(id => params.append('SelectedCityTagIds', String(id)));
        state.categories.forEach(id => params.append('SelectedCategoryIds', String(id)));
        state.levels.forEach(level => params.append('SelectedLevels', level));
        state.types.forEach(type => params.append('SelectedTypes', type));

        if (priceBounds.max > priceBounds.min) {
            const minPrice = clampPrice(state.minPrice);
            const maxPrice = clampPrice(state.maxPrice);
            const separator = culture.decimalSeparator || '.';
            params.set('MinPrice', minPrice.toString().replace('.', separator));
            params.set('MaxPrice', maxPrice.toString().replace('.', separator));
        }

        return params;
    }

    async function fetchCourses() {
        if (!coursesGrid) {
            return;
        }

        const params = buildQuery();
        if (currentRequest) {
            currentRequest.abort();
        }
        currentRequest = new AbortController();
        const signal = currentRequest.signal;
        const url = `/Courses/Index?handler=Courses&${params.toString()}`;
        try {
            showCourseError('');
            const response = await fetch(url, {
                headers: { 'Accept': 'application/json' },
                signal
            });
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            const data = await response.json();
            renderCourses(data.courses ?? []);
            updatePagination(data.pagination ?? {});
            updateChips();
            updateCompareBar();
        } catch (error) {
            if (error.name === 'AbortError') {
                return;
            }
            console.error('courseFilters: fetch failed', error);
            showCourseError(resources.fetchError ?? resources.noResults ?? 'Nepodařilo se načíst kurzy.');
        } finally {
            currentRequest = null;
        }
    }

    function attachEventHandlers() {
        controls.search.forEach(input => {
            input.addEventListener('input', () => {
                if (syncing) {
                    return;
                }
                state.search = input.value.trim();
                state.pageNumber = 1;
                updateChips();
                scheduleFetch();
            });
        });

        const handleSelectChange = (set, values, transform = Number) => {
            set.clear();
            values.forEach(value => set.add(transform(value)));
            state.pageNumber = 1;
            syncControls();
            updateChips();
            scheduleFetch();
        };

        controls.norms.forEach(select => {
            select.addEventListener('change', event => {
                if (syncing) {
                    return;
                }
                const selected = Array.from(event.target.selectedOptions).map(opt => opt.value);
                handleSelectChange(state.norms, selected, Number);
            });
        });

        controls.cities.forEach(select => {
            select.addEventListener('change', event => {
                if (syncing) {
                    return;
                }
                const selected = Array.from(event.target.selectedOptions).map(opt => opt.value);
                handleSelectChange(state.cities, selected, Number);
            });
        });

        controls.categories.forEach(select => {
            select.addEventListener('change', event => {
                if (syncing) {
                    return;
                }
                const selected = Array.from(event.target.selectedOptions).map(opt => opt.value);
                handleSelectChange(state.categories, selected, value => String(value));
            });
        });

        levelInputs.forEach(input => {
            input.addEventListener('change', () => {
                if (input.checked) {
                    state.levels.add(input.value);
                } else {
                    state.levels.delete(input.value);
                }
                state.pageNumber = 1;
                syncControls();
                updateChips();
                scheduleFetch();
            });
        });

        typeInputs.forEach(input => {
            input.addEventListener('change', () => {
                if (input.checked) {
                    state.types.add(input.value);
                } else {
                    state.types.delete(input.value);
                }
                state.pageNumber = 1;
                syncControls();
                updateChips();
                scheduleFetch();
            });
        });

        const handlePriceInput = (isMin, input) => {
            input.addEventListener('input', () => {
                if (syncing) {
                    return;
                }
                const value = clampPrice(Number(input.value));
                if (isMin) {
                    state.minPrice = Math.min(value, state.maxPrice);
                } else {
                    state.maxPrice = Math.max(value, state.minPrice);
                }
                state.pageNumber = 1;
                syncControls();
                updateChips();
                scheduleFetch();
            });
        };

        controls.priceMin.forEach(input => handlePriceInput(true, input));
        controls.priceMax.forEach(input => handlePriceInput(false, input));

        controls.saveButtons.forEach(button => {
            button.addEventListener('click', () => saveFilters());
        });

        controls.resetButtons.forEach(button => {
            button.addEventListener('click', () => resetFilters());
        });

        if (resetAllLink) {
            resetAllLink.addEventListener('click', event => {
                event.preventDefault();
                resetFilters();
            });
        }

        if (paginationElement) {
            const prev = paginationElement.querySelector('[data-action="prev-page"]');
            const next = paginationElement.querySelector('[data-action="next-page"]');
            prev?.addEventListener('click', () => {
                if (state.pageNumber > 1) {
                    state.pageNumber -= 1;
                    fetchCourses();
                }
            });
            next?.addEventListener('click', () => {
                if (state.pageNumber < state.totalPages) {
                    state.pageNumber += 1;
                    fetchCourses();
                }
            });
        }
    }

    function initialise() {
        syncControls();
        attachEventHandlers();

        const saved = !initial.hasFilters ? loadSavedFilters() : null;
        if (saved) {
            state.search = saved.search ?? state.search;
            state.norms = new Set((saved.norms ?? []).map(Number));
            state.cities = new Set((saved.cities ?? []).map(Number));
            state.categories = new Set((saved.categories ?? [])
                .map(String)
                .filter(id => categoryOptionMap.has(id)));
            state.levels = new Set((saved.levels ?? []).map(String));
            state.types = new Set((saved.types ?? []).map(String));
            state.minPrice = clampPrice(Number(saved.minPrice ?? priceBounds.min));
            state.maxPrice = clampPrice(Number(saved.maxPrice ?? priceBounds.max));
            state.pageNumber = 1;
            syncControls();
            updateChips();
            fetchCourses();
        } else {
            syncControls();
            updateChips();
            renderCourses(initial.courses ?? []);
            updatePagination(initial);
            updateCompareBar();
        }
    }

    initialise();
}
