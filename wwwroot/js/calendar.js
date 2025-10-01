(() => {
    'use strict';

    const REFRESH_INTERVAL_MS = 60_000;
    const TYPE_LABELS = {
        InPerson: 'Prezenčně',
        Online: 'Online',
        Hybrid: 'Hybrid'
    };

    document.addEventListener('DOMContentLoaded', () => {
        const calendarRoot = document.querySelector('[data-calendar]');
        if (!calendarRoot) {
            return;
        }

        const calendar = new CourseCalendar(calendarRoot);
        calendar.init();
    });

    class CourseCalendar {
        constructor(root) {
            this.root = root;
            this.locale = document.documentElement.lang || 'cs';
            this.state = {
                view: 'month',
                currentDate: new Date(),
                events: [],
                eventsById: new Map(),
                allEvents: []
            };

            this.loadingEl = root.querySelector('[data-calendar-loading]');
            this.errorEl = root.querySelector('[data-calendar-error]');
            this.emptyEl = root.querySelector('[data-calendar-empty]');
            this.containerEl = root.querySelector('[data-calendar-container]');
            this.titleEl = root.querySelector('[data-calendar-title]');
            this.viewButtons = Array.from(root.querySelectorAll('[data-calendar-view]'));
            this.navButtons = Array.from(root.querySelectorAll('[data-calendar-nav]'));

            const filterForm = document.getElementById('calendarFilters');
            this.filters = {
                form: filterForm,
                selects: {
                    norms: filterForm?.querySelector('[data-filter="norms"]') ?? null,
                    cities: filterForm?.querySelector('[data-filter="cities"]') ?? null,
                    types: filterForm?.querySelector('[data-filter="types"]') ?? null
                },
                onlyAvailable: filterForm?.querySelector('[data-filter="onlyAvailable"]') ?? null,
                summary: filterForm?.querySelector('[data-filter="summary"]') ?? null,
                reset: filterForm?.querySelector('[data-filter="reset"]') ?? null
            };

            this.filterState = {
                norms: new Set(),
                cities: new Set(),
                types: new Set(),
                onlyAvailable: false
            };

            this.modal = null;
            this.modalElements = null;
            this.refreshTimer = null;
        }

        async init() {
            this.bindViewControls();
            this.bindFilters();
            await this.refresh(true);
            this.scheduleAutoRefresh();
        }

        scheduleAutoRefresh() {
            if (this.refreshTimer) {
                window.clearInterval(this.refreshTimer);
            }

            this.refreshTimer = window.setInterval(() => {
                this.refresh(false, { preserveOptions: true }).catch(() => {
                    /* errors handled in refresh */
                });
            }, REFRESH_INTERVAL_MS);
        }

        bindViewControls() {
            this.viewButtons.forEach(button => {
                button.addEventListener('click', () => {
                    const view = button.getAttribute('data-calendar-view');
                    if (!view || view === this.state.view) {
                        return;
                    }

                    this.state.view = view;
                    if (view === 'day') {
                        this.state.currentDate = new Date(this.state.currentDate);
                    }

                    this.updateViewButtons();
                    this.render();
                });
            });

            this.navButtons.forEach(button => {
                button.addEventListener('click', () => {
                    const action = button.getAttribute('data-calendar-nav');
                    if (!action) {
                        return;
                    }

                    this.navigate(action);
                });
            });
        }

        bindFilters() {
            const { selects, onlyAvailable, reset } = this.filters;

            if (selects.norms) {
                selects.norms.addEventListener('change', () => {
                    this.filterState.norms = new Set(Array.from(selects.norms.selectedOptions).map(o => o.value));
                    this.handleFiltersChanged();
                });
            }

            if (selects.cities) {
                selects.cities.addEventListener('change', () => {
                    this.filterState.cities = new Set(Array.from(selects.cities.selectedOptions).map(o => o.value));
                    this.handleFiltersChanged();
                });
            }

            if (selects.types) {
                selects.types.addEventListener('change', () => {
                    this.filterState.types = new Set(Array.from(selects.types.selectedOptions).map(o => o.value));
                    this.handleFiltersChanged();
                });
            }

            if (onlyAvailable) {
                onlyAvailable.addEventListener('change', () => {
                    this.filterState.onlyAvailable = onlyAvailable.checked;
                    this.handleFiltersChanged();
                });
            }

            if (reset) {
                reset.addEventListener('click', () => {
                    this.resetFilters();
                });
            }
        }

        resetFilters() {
            const { selects, onlyAvailable } = this.filters;

            this.filterState.norms.clear();
            this.filterState.cities.clear();
            this.filterState.types.clear();
            this.filterState.onlyAvailable = false;

            if (selects.norms) {
                Array.from(selects.norms.options).forEach(option => {
                    option.selected = false;
                });
            }

            if (selects.cities) {
                Array.from(selects.cities.options).forEach(option => {
                    option.selected = false;
                });
            }

            if (selects.types) {
                Array.from(selects.types.options).forEach(option => {
                    option.selected = false;
                });
            }

            if (onlyAvailable) {
                onlyAvailable.checked = false;
            }

            this.handleFiltersChanged();
        }

        async handleFiltersChanged() {
            this.updateFilterSummary();
            await this.refresh(false, { preserveOptions: true });
        }

        async refresh(initialLoad = false, { preserveOptions = false } = {}) {
            this.setLoading(true);
            this.showError(false);

            try {
                const data = await this.fetchEvents();
                const events = (data?.events ?? []).map(transformEvent).sort((a, b) => a.startLocal - b.startLocal);

                this.state.events = events;
                this.state.eventsById = new Map(events.map(evt => [evt.termId, evt]));

                if (!preserveOptions && (initialLoad || !this.hasActiveFilters())) {
                    this.state.allEvents = events.slice();
                    this.populateFilterOptions();
                } else if (this.state.allEvents.length === 0) {
                    this.state.allEvents = events.slice();
                    this.populateFilterOptions();
                }

                this.render();
            } catch (error) {
                console.error('Failed to load course calendar', error);
                this.showError(true);
                this.state.events = [];
                this.state.eventsById.clear();
                this.render();
            } finally {
                this.setLoading(false);
            }
        }

        async fetchEvents() {
            const params = new URLSearchParams();
            this.filterState.norms.forEach(value => params.append('norms', value));
            this.filterState.cities.forEach(value => params.append('cities', value));
            this.filterState.types.forEach(value => params.append('types', value));
            if (this.filterState.onlyAvailable) {
                params.append('onlyAvailable', 'true');
            }

            const url = params.size > 0 ? `/api/course-calendar?${params.toString()}` : '/api/course-calendar';
            const response = await fetch(url, { cache: 'no-store', headers: { Accept: 'application/json' } });
            if (!response.ok) {
                throw new Error(`Unexpected status ${response.status}`);
            }

            return response.json();
        }

        render() {
            this.updateTitle();
            this.updateViewButtons();
            this.updateFilterSummary();

            const hasError = this.errorEl && !this.errorEl.classList.contains('d-none');

            if (this.state.events.length === 0) {
                this.showEmpty(!hasError);
                this.containerEl.innerHTML = '';
                return;
            }

            this.showEmpty(false);

            switch (this.state.view) {
                case 'week':
                    this.renderWeekView();
                    break;
                case 'day':
                    this.renderDayView();
                    break;
                case 'month':
                default:
                    this.renderMonthView();
                    break;
            }
        }

        updateTitle() {
            if (!this.titleEl) {
                return;
            }

            const formatter = new Intl.DateTimeFormat(this.locale, { month: 'long', year: 'numeric' });
            if (this.state.view === 'month') {
                this.titleEl.textContent = capitalize(formatter.format(this.state.currentDate));
                return;
            }

            if (this.state.view === 'week') {
                const start = startOfWeek(this.state.currentDate);
                const end = addDays(start, 6);
                const dateFormatter = new Intl.DateTimeFormat(this.locale, { day: 'numeric', month: 'short', year: 'numeric' });
                this.titleEl.textContent = `${dateFormatter.format(start)} – ${dateFormatter.format(end)}`;
                return;
            }

            const dayFormatter = new Intl.DateTimeFormat(this.locale, { dateStyle: 'full' });
            this.titleEl.textContent = capitalize(dayFormatter.format(this.state.currentDate));
        }

        updateViewButtons() {
            this.viewButtons.forEach(button => {
                const view = button.getAttribute('data-calendar-view');
                if (!view) {
                    return;
                }

                if (view === this.state.view) {
                    button.classList.add('active');
                } else {
                    button.classList.remove('active');
                }
            });
        }

        populateFilterOptions() {
            const { selects } = this.filters;
            if (!selects) {
                return;
            }

            const normValues = new Set();
            const cityValues = new Set();
            const typeValues = new Set();

            this.state.allEvents.forEach(event => {
                event.norms.forEach(norm => normValues.add(norm));
                event.cities.forEach(city => cityValues.add(city));
                if (event.deliveryType) {
                    typeValues.add(event.deliveryType);
                }
            });

            if (selects.norms) {
                populateSelect(selects.norms, Array.from(normValues).sort(localeCompareFactory(this.locale)), this.filterState.norms);
            }

            if (selects.cities) {
                populateSelect(selects.cities, Array.from(cityValues).sort(localeCompareFactory(this.locale)), this.filterState.cities);
            }

            if (selects.types) {
                const sortedTypes = Array.from(typeValues).sort();
                populateSelect(selects.types, sortedTypes, this.filterState.types, value => TYPE_LABELS[value] ?? value);
            }
        }

        renderMonthView() {
            const startMonth = new Date(this.state.currentDate.getFullYear(), this.state.currentDate.getMonth(), 1);
            const start = startOfWeek(startMonth);
            const table = document.createElement('table');
            table.className = 'table table-bordered align-middle';

            const thead = document.createElement('thead');
            const headerRow = document.createElement('tr');
            getDayNames(this.locale).forEach(name => {
                const th = document.createElement('th');
                th.scope = 'col';
                th.className = 'text-center small text-uppercase';
                th.textContent = name;
                headerRow.appendChild(th);
            });
            thead.appendChild(headerRow);
            table.appendChild(thead);

            const tbody = document.createElement('tbody');
            let current = new Date(start);

            for (let week = 0; week < 6; week += 1) {
                const row = document.createElement('tr');
                for (let day = 0; day < 7; day += 1) {
                    const cellDate = new Date(current);
                    const cellDateKey = formatDateKey(cellDate);
                    const events = this.eventsOnDay(cellDate);

                    const cell = document.createElement('td');
                    cell.className = 'align-top';
                    if (cellDate.getMonth() !== startMonth.getMonth()) {
                        cell.classList.add('bg-light', 'text-muted');
                    }

                    if (isToday(cellDate)) {
                        cell.classList.add('border-primary', 'border-2');
                    }

                    const header = document.createElement('div');
                    header.className = 'd-flex justify-content-between align-items-start mb-2';
                    const dayNumber = document.createElement('span');
                    dayNumber.className = 'fw-semibold';
                    dayNumber.textContent = String(cellDate.getDate());
                    header.appendChild(dayNumber);
                    cell.appendChild(header);

                    if (events.length > 0) {
                        const list = document.createElement('div');
                        list.className = 'vstack gap-1';
                        events.forEach(event => {
                            list.appendChild(this.createEventButton(event));
                        });
                        cell.appendChild(list);
                    } else {
                        const placeholder = document.createElement('p');
                        placeholder.className = 'small text-muted mb-0';
                        placeholder.textContent = 'Žádné akce';
                        cell.appendChild(placeholder);
                    }

                    cell.dataset.date = cellDateKey;
                    cell.tabIndex = 0;
                    cell.addEventListener('click', () => {
                        this.state.currentDate = new Date(cellDate.getFullYear(), cellDate.getMonth(), cellDate.getDate());
                        this.state.view = 'day';
                        this.updateViewButtons();
                        this.render();
                    });
                    cell.addEventListener('keydown', event => {
                        if (event.key === 'Enter' || event.key === ' ') {
                            event.preventDefault();
                            cell.click();
                        }
                    });

                    row.appendChild(cell);
                    current = addDays(current, 1);
                }
                tbody.appendChild(row);
            }

            table.appendChild(tbody);
            this.containerEl.replaceChildren(table);
        }

        renderWeekView() {
            const start = startOfWeek(this.state.currentDate);
            const row = document.createElement('div');
            row.className = 'row g-3';

            for (let i = 0; i < 7; i += 1) {
                const dayDate = addDays(start, i);
                const events = this.eventsOnDay(dayDate);
                const col = document.createElement('div');
                col.className = 'col-12 col-md-6';

                const card = document.createElement('div');
                card.className = 'border rounded p-3 h-100';

                const heading = document.createElement('div');
                heading.className = 'd-flex justify-content-between align-items-start mb-2';
                const label = document.createElement('span');
                label.className = 'fw-semibold';
                const labelFormatter = new Intl.DateTimeFormat(this.locale, { weekday: 'long', day: 'numeric', month: 'short' });
                label.textContent = capitalize(labelFormatter.format(dayDate));
                heading.appendChild(label);

                if (isToday(dayDate)) {
                    const badge = document.createElement('span');
                    badge.className = 'badge text-bg-primary';
                    badge.textContent = 'Dnes';
                    heading.appendChild(badge);
                }

                card.appendChild(heading);

                if (events.length === 0) {
                    const placeholder = document.createElement('p');
                    placeholder.className = 'text-muted mb-0';
                    placeholder.textContent = 'Žádné akce';
                    card.appendChild(placeholder);
                } else {
                    const list = document.createElement('div');
                    list.className = 'vstack gap-2';
                    events.forEach(event => {
                        list.appendChild(this.createEventSummary(event));
                    });
                    card.appendChild(list);
                }

                col.appendChild(card);
                row.appendChild(col);
            }

            this.containerEl.replaceChildren(row);
        }

        renderDayView() {
            const dayDate = new Date(this.state.currentDate.getFullYear(), this.state.currentDate.getMonth(), this.state.currentDate.getDate());
            const events = this.eventsOnDay(dayDate);

            const container = document.createElement('div');
            container.className = 'vstack gap-3';

            if (events.length === 0) {
                const placeholder = document.createElement('p');
                placeholder.className = 'text-muted mb-0';
                placeholder.textContent = 'Žádné akce pro vybraný den.';
                container.appendChild(placeholder);
            } else {
                events.forEach(event => {
                    container.appendChild(this.createDayCard(event));
                });
            }

            this.containerEl.replaceChildren(container);
        }

        createEventButton(event) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'btn btn-sm text-start text-white w-100 calendar-event';
            button.style.backgroundColor = event.color;
            button.textContent = event.title;
            button.title = event.title;
            button.addEventListener('click', evt => {
                evt.stopPropagation();
                this.openModal(event);
            });
            return button;
        }

        createEventSummary(event) {
            const wrapper = document.createElement('button');
            wrapper.type = 'button';
            wrapper.className = 'btn btn-outline-primary text-start w-100';
            const title = document.createElement('div');
            title.className = 'fw-semibold';
            title.textContent = event.title;

            const timeRange = document.createElement('div');
            timeRange.className = 'small text-muted';
            timeRange.textContent = `${formatTime(event.startLocal, this.locale)} – ${formatTime(event.endLocal, this.locale)}`;

            wrapper.append(title, timeRange);
            wrapper.addEventListener('click', () => {
                this.openModal(event);
            });
            return wrapper;
        }

        createDayCard(event) {
            const card = document.createElement('div');
            card.className = 'card border-0 shadow-sm';
            const body = document.createElement('div');
            body.className = 'card-body';

            const header = document.createElement('div');
            header.className = 'd-flex justify-content-between align-items-center mb-2';
            const heading = document.createElement('h3');
            heading.className = 'h5 mb-0';
            heading.textContent = event.title;
            header.appendChild(heading);

            if (event.category) {
                const badge = document.createElement('span');
                badge.className = 'badge';
                badge.style.backgroundColor = event.color;
                badge.textContent = event.category;
                header.appendChild(badge);
            }

            body.appendChild(header);

            const time = document.createElement('p');
            time.className = 'mb-2';
            time.textContent = `${formatDateTime(event.startLocal, this.locale)} – ${formatDateTime(event.endLocal, this.locale)}`;
            body.appendChild(time);

            const info = document.createElement('p');
            info.className = 'text-muted mb-2';
            const parts = [];
            if (event.primaryCity) {
                parts.push(event.primaryCity);
            }
            if (event.deliveryType) {
                parts.push(TYPE_LABELS[event.deliveryType] ?? event.deliveryType);
            }
            if (parts.length > 0) {
                info.textContent = parts.join(' • ');
                body.appendChild(info);
            }

            const availability = document.createElement('p');
            availability.className = 'mb-0';
            availability.textContent = buildAvailabilityText(event);
            body.appendChild(availability);

            const footer = document.createElement('div');
            footer.className = 'mt-3 d-flex flex-wrap gap-2';

            const detailBtn = document.createElement('a');
            detailBtn.href = event.detailsUrl;
            detailBtn.className = 'btn btn-outline-secondary';
            detailBtn.target = '_blank';
            detailBtn.rel = 'noopener';
            detailBtn.textContent = 'Detail kurzu';
            footer.appendChild(detailBtn);

            const modalBtn = document.createElement('button');
            modalBtn.type = 'button';
            modalBtn.className = 'btn btn-primary';
            modalBtn.textContent = 'Rychlý náhled';
            modalBtn.addEventListener('click', () => this.openModal(event));
            footer.appendChild(modalBtn);

            body.appendChild(footer);
            card.appendChild(body);
            return card;
        }

        eventsOnDay(date) {
            const dayStart = startOfDay(date);
            const dayEnd = endOfDay(date);
            return this.state.events.filter(event => event.startLocal <= dayEnd && event.endLocal >= dayStart);
        }

        openModal(event) {
            const modalEl = document.getElementById('coursePreviewModal');
            if (!modalEl || typeof bootstrap === 'undefined' || !bootstrap.Modal) {
                return;
            }

            if (!this.modal) {
                this.modal = new bootstrap.Modal(modalEl);
                this.modalElements = {
                    title: modalEl.querySelector('[data-course-modal="title"]'),
                    subtitle: modalEl.querySelector('[data-course-modal="subtitle"]'),
                    start: modalEl.querySelector('[data-course-modal="start"]'),
                    end: modalEl.querySelector('[data-course-modal="end"]'),
                    city: modalEl.querySelector('[data-course-modal="city"]'),
                    mode: modalEl.querySelector('[data-course-modal="mode"]'),
                    description: modalEl.querySelector('[data-course-modal="description"]'),
                    availability: modalEl.querySelector('[data-course-modal="availability"]'),
                    badges: modalEl.querySelector('[data-course-modal="badges"]'),
                    detailsLink: modalEl.querySelector('[data-course-modal="details-link"]'),
                    google: modalEl.querySelector('[data-course-modal="google"]'),
                    ics: modalEl.querySelector('[data-course-modal="ics"]')
                };
            }

            const elements = this.modalElements;
            if (!elements) {
                return;
            }

            if (elements.title) {
                elements.title.textContent = event.title;
            }

            if (elements.subtitle) {
                const subtitleParts = [];
                if (event.category) {
                    subtitleParts.push(event.category);
                }
                if (event.deliveryType) {
                    subtitleParts.push(TYPE_LABELS[event.deliveryType] ?? event.deliveryType);
                }
                if (event.instructionMode && event.instructionMode !== event.deliveryType) {
                    subtitleParts.push(event.instructionMode);
                }
                elements.subtitle.textContent = subtitleParts.join(' • ');
            }

            if (elements.start) {
                elements.start.textContent = formatDateTime(event.startLocal, this.locale);
            }

            if (elements.end) {
                elements.end.textContent = formatDateTime(event.endLocal, this.locale);
            }

            if (elements.city) {
                elements.city.textContent = event.primaryCity ?? '—';
            }

            if (elements.mode) {
                const label = TYPE_LABELS[event.deliveryType] ?? event.deliveryType ?? '—';
                elements.mode.textContent = label;
            }

            if (elements.description) {
                elements.description.textContent = event.description || 'Bez popisu';
            }

            if (elements.availability) {
                elements.availability.textContent = buildAvailabilityText(event);
            }

            if (elements.badges) {
                elements.badges.innerHTML = '';
                event.norms.forEach(norm => {
                    const badge = document.createElement('span');
                    badge.className = 'badge rounded-pill text-bg-secondary';
                    badge.textContent = norm;
                    elements.badges.appendChild(badge);
                });
            }

            if (elements.detailsLink) {
                elements.detailsLink.href = event.detailsUrl;
            }

            if (elements.google) {
                elements.google.onclick = () => {
                    const url = buildGoogleCalendarUrl(event);
                    window.open(url, '_blank', 'noopener');
                };
            }

            if (elements.ics) {
                elements.ics.onclick = () => {
                    downloadIcs(event);
                };
            }

            this.modal?.show();
        }

        navigate(action) {
            const current = new Date(this.state.currentDate);
            switch (action) {
                case 'prev':
                    if (this.state.view === 'month') {
                        this.state.currentDate = addMonths(current, -1);
                    } else if (this.state.view === 'week') {
                        this.state.currentDate = addDays(current, -7);
                    } else {
                        this.state.currentDate = addDays(current, -1);
                    }
                    break;
                case 'next':
                    if (this.state.view === 'month') {
                        this.state.currentDate = addMonths(current, 1);
                    } else if (this.state.view === 'week') {
                        this.state.currentDate = addDays(current, 7);
                    } else {
                        this.state.currentDate = addDays(current, 1);
                    }
                    break;
                case 'today':
                    this.state.currentDate = new Date();
                    break;
                default:
                    break;
            }

            this.render();
        }

        setLoading(isLoading) {
            toggleVisibility(this.loadingEl, isLoading);
            if (isLoading) {
                toggleVisibility(this.errorEl, false);
            }
        }

        showError(isVisible) {
            toggleVisibility(this.errorEl, isVisible);
        }

        showEmpty(isEmpty) {
            toggleVisibility(this.emptyEl, isEmpty);
        }

        updateFilterSummary() {
            const summaryEl = this.filters.summary;
            if (!summaryEl) {
                return;
            }

            const count = this.filterState.norms.size + this.filterState.cities.size + this.filterState.types.size + (this.filterState.onlyAvailable ? 1 : 0);
            summaryEl.textContent = count > 0 ? `Aktivní filtry: ${count}` : 'Bez filtrů';
        }

        hasActiveFilters() {
            return this.filterState.norms.size > 0 || this.filterState.cities.size > 0 || this.filterState.types.size > 0 || this.filterState.onlyAvailable;
        }
    }

    function transformEvent(raw) {
        const startUtc = new Date(raw.startUtc);
        const endUtc = new Date(raw.endUtc);
        const startLocal = new Date(startUtc.getTime());
        const endLocal = new Date(endUtc.getTime());
        return {
            termId: raw.termId,
            courseId: raw.courseId,
            title: raw.title,
            category: raw.category,
            color: raw.color || '#0d6efd',
            description: raw.description ?? '',
            deliveryType: raw.deliveryType ?? raw.mode ?? '',
            instructionMode: raw.instructionMode ?? '',
            primaryCity: raw.primaryCity ?? null,
            norms: Array.isArray(raw.norms) ? raw.norms : [],
            cities: Array.isArray(raw.cities) ? raw.cities : [],
            capacity: raw.capacity ?? 0,
            seatsTaken: raw.seatsTaken ?? 0,
            seatsAvailable: raw.seatsAvailable ?? 0,
            detailsUrl: raw.detailsUrl ?? '#',
            startUtc,
            endUtc,
            startLocal,
            endLocal
        };
    }

    function populateSelect(select, values, selectedSet, labelFactory) {
        const fragment = document.createDocumentFragment();
        values.forEach(value => {
            const option = document.createElement('option');
            option.value = value;
            option.textContent = labelFactory ? labelFactory(value) : value;
            option.selected = selectedSet.has(value);
            fragment.appendChild(option);
        });
        select.innerHTML = '';
        select.appendChild(fragment);
    }

    function toggleVisibility(element, isVisible) {
        if (!element) {
            return;
        }

        element.classList.toggle('d-none', !isVisible);
    }

    function startOfWeek(date) {
        const result = startOfDay(date);
        const day = result.getDay();
        const diff = (day + 6) % 7;
        result.setDate(result.getDate() - diff);
        return result;
    }

    function startOfDay(date) {
        const result = new Date(date);
        result.setHours(0, 0, 0, 0);
        return result;
    }

    function endOfDay(date) {
        const result = new Date(date);
        result.setHours(23, 59, 59, 999);
        return result;
    }

    function addDays(date, days) {
        const result = new Date(date);
        result.setDate(result.getDate() + days);
        return result;
    }

    function addMonths(date, months) {
        const result = new Date(date);
        result.setMonth(result.getMonth() + months);
        return result;
    }

    function formatDateKey(date) {
        const copy = new Date(date);
        copy.setHours(0, 0, 0, 0);
        return copy.toISOString().split('T')[0];
    }

    function getDayNames(locale) {
        const formatter = new Intl.DateTimeFormat(locale, { weekday: 'short' });
        const baseDate = new Date(Date.UTC(2021, 5, 14));
        const names = [];
        for (let i = 0; i < 7; i += 1) {
            const date = new Date(baseDate);
            date.setDate(baseDate.getDate() + i);
            names.push(capitalize(formatter.format(date)));
        }
        const sunday = names.shift();
        if (sunday) {
            names.push(sunday);
        }
        return names;
    }

    function formatTime(date, locale) {
        return new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit' }).format(date);
    }

    function formatDateTime(date, locale) {
        return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(date);
    }

    function localeCompareFactory(locale) {
        const collator = new Intl.Collator(locale, { sensitivity: 'base' });
        return (a, b) => collator.compare(a, b);
    }

    function capitalize(text) {
        if (!text) {
            return text;
        }
        return text.charAt(0).toUpperCase() + text.slice(1);
    }

    function isToday(date) {
        const today = new Date();
        return date.getFullYear() === today.getFullYear() && date.getMonth() === today.getMonth() && date.getDate() === today.getDate();
    }

    function buildAvailabilityText(event) {
        const remaining = Math.max(0, event.seatsAvailable ?? event.capacity - event.seatsTaken);
        if (event.capacity > 0) {
            return `Volná místa: ${remaining} / ${event.capacity}`;
        }
        return `Volná místa: ${remaining}`;
    }

    function buildGoogleCalendarUrl(event) {
        const base = 'https://calendar.google.com/calendar/render?action=TEMPLATE';
        const title = encodeURIComponent(event.title);
        const dates = `${formatDateForCalendar(event.startUtc)}/${formatDateForCalendar(event.endUtc)}`;
        const detailsParts = [];
        if (event.description) {
            detailsParts.push(event.description);
        }
        detailsParts.push(buildAvailabilityText(event));
        const details = encodeURIComponent(detailsParts.join('\n\n'));
        const location = encodeURIComponent(event.primaryCity ?? '');
        return `${base}&text=${title}&dates=${dates}&details=${details}&location=${location}`;
    }

    function formatDateForCalendar(date) {
        const year = date.getUTCFullYear();
        const month = String(date.getUTCMonth() + 1).padStart(2, '0');
        const day = String(date.getUTCDate()).padStart(2, '0');
        const hours = String(date.getUTCHours()).padStart(2, '0');
        const minutes = String(date.getUTCMinutes()).padStart(2, '0');
        const seconds = String(date.getUTCSeconds()).padStart(2, '0');
        return `${year}${month}${day}T${hours}${minutes}${seconds}Z`;
    }

    function downloadIcs(event) {
        const lines = [
            'BEGIN:VCALENDAR',
            'VERSION:2.0',
            'PRODID:-//SysJaky//Course Calendar//CZ',
            'BEGIN:VEVENT',
            `UID:course-term-${event.termId}@sysjaky`,
            `DTSTAMP:${formatDateForCalendar(new Date())}`,
            `DTSTART:${formatDateForCalendar(event.startUtc)}`,
            `DTEND:${formatDateForCalendar(event.endUtc)}`,
            `SUMMARY:${escapeIcsText(event.title)}`,
            `DESCRIPTION:${escapeIcsText(event.description || '')}`,
            `LOCATION:${escapeIcsText(event.primaryCity ?? '')}`,
            `STATUS:CONFIRMED`,
            'END:VEVENT',
            'END:VCALENDAR'
        ];

        const blob = new Blob([lines.join('\r\n')], { type: 'text/calendar;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `course-${event.termId}.ics`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    }

    function escapeIcsText(value) {
        return value.replace(/[\\;,\n]/g, char => {
            switch (char) {
                case '\\':
                    return '\\\\';
                case ';':
                    return '\\;';
                case ',':
                    return '\\,';
                case '\n':
                    return '\\n';
                default:
                    return char;
            }
        });
    }
})();
