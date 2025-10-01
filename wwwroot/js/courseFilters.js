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

    function createCourseCard(course) {
        const wrapper = document.createElement('div');
        const id = Number(course.id);
        const checkboxId = `cmp_${id}`;
        const description = escapeHtml(course.description ?? '');
        const title = escapeHtml(course.title ?? '');
        const level = escapeHtml(course.level ?? '');
        const mode = escapeHtml(course.mode ?? '');
        const type = escapeHtml(course.type ?? '');
        const durationDisplay = escapeHtml(course.durationDisplay ?? '');
        const dateDisplay = escapeHtml(course.dateDisplay ?? '');
        const priceDisplay = escapeHtml(course.priceDisplay ?? '');
        const detailsUrl = escapeAttribute(course.detailsUrl ?? `#/course/${id}`);
        const addToCartUrl = escapeAttribute(course.addToCartUrl ?? '/Courses/Index?handler=AddToCart');
        const coverImageUrl = course.coverImageUrl ? escapeAttribute(course.coverImageUrl) : null;
        const popoverHtml = course.popoverHtml ? escapeAttribute(course.popoverHtml) : null;

        const coverHtml = coverImageUrl
            ? `<img src="${coverImageUrl}" alt="${title}" class="img-fluid rounded mb-2" loading="lazy" decoding="async">`
            : '';

        const popoverLink = popoverHtml
            ? `<a tabindex="0" role="button" class="ms-1 text-decoration-dotted" data-bs-toggle="popover" data-bs-html="true" data-bs-content="${popoverHtml}">
                    <i class="bi bi-info-circle"></i>
               </a>`
            : '';

        wrapper.innerHTML = `
        <div class="feature-card h-100 p-3 d-flex flex-column justify-content-between">
            <div class="d-flex flex-column gap-2">
                ${coverHtml}
                <h3 class="h5 mb-1">${title}</h3>
                ${description ? `<p class="text-muted small mb-2">${description}</p>` : ''}
                <div class="d-flex flex-wrap gap-2 align-items-center small">
                    <span class="badge badge-soft-primary"><i class="bi bi-bar-chart me-1"></i>${level}</span>
                    <span class="badge badge-soft-primary"><i class="bi bi-laptop me-1"></i>${mode}</span>
                    <span class="badge badge-soft-accent"><i class="bi bi-geo-alt me-1"></i>${type}</span>
                    <span class="badge badge-soft-accent"><i class="bi bi-clock me-1"></i>${durationDisplay}</span>
                </div>
            </div>
            <div class="d-flex justify-content-between align-items-end mt-3">
                <div class="small text-muted">
                    <div class="d-flex align-items-center">
                        <i class="bi bi-calendar2 me-2"></i>
                        <small class="text-muted">${dateDisplay}${popoverLink}</small>
                    </div>
                    <div class="fw-semibold">${priceDisplay}</div>
                </div>
                <div class="d-flex flex-column align-items-end gap-2">
                    <div class="form-check form-check-inline">
                        <input class="form-check-input cmp-check" type="checkbox" value="${id}" id="${checkboxId}">
                        <label class="form-check-label small" for="${checkboxId}">${resources.compareLabel ?? 'Porovnat'}</label>
                    </div>
                    <div class="d-flex gap-2">
                        <a class="btn btn-outline-secondary" href="${detailsUrl}">${resources.detailsLabel ?? 'Detail'}</a>
                        <form method="post" action="${addToCartUrl}" class="d-inline">
                            <input type="hidden" name="courseId" value="${id}">
                            <button type="submit" class="btn btn-primary">${resources.enrollLabel ?? 'Přihlásit'}</button>
                        </form>
                    </div>
                </div>
            </div>
        </div>`;

        const card = wrapper.firstElementChild;
        if (card) {
            coursesGrid?.appendChild(card);
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

        const handleSelectChange = (set, values) => {
            set.clear();
            values.forEach(value => set.add(Number(value)));
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
                handleSelectChange(state.norms, selected);
            });
        });

        controls.cities.forEach(select => {
            select.addEventListener('change', event => {
                if (syncing) {
                    return;
                }
                const selected = Array.from(event.target.selectedOptions).map(opt => opt.value);
                handleSelectChange(state.cities, selected);
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
