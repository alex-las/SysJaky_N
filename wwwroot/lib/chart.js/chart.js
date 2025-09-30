/*!
 * Minimal chart rendering utility inspired by Chart.js
 * Provides just enough functionality for the SysJaky dashboard to run offline
 * Supports bar and line charts with multiple Y axes.
 */
(function () {
    'use strict';

    const COLOR_PALETTE = [
        '#4e79a7', '#f28e2b', '#e15759', '#76b7b2', '#59a14f', '#edc948',
        '#b07aa1', '#ff9da7', '#9c755f', '#bab0ab'
    ];

    function deepClone(value) {
        return JSON.parse(JSON.stringify(value));
    }

    function niceNum(range, round) {
        if (!isFinite(range) || range === 0) {
            return 1;
        }
        const exponent = Math.floor(Math.log10(range));
        const fraction = range / Math.pow(10, exponent);
        let niceFraction;
        if (round) {
            if (fraction < 1.5) niceFraction = 1;
            else if (fraction < 3) niceFraction = 2;
            else if (fraction < 7) niceFraction = 5;
            else niceFraction = 10;
        } else {
            if (fraction <= 1) niceFraction = 1;
            else if (fraction <= 2) niceFraction = 2;
            else if (fraction <= 5) niceFraction = 5;
            else niceFraction = 10;
        }
        return niceFraction * Math.pow(10, exponent);
    }

    function buildTicks(min, max, count) {
        if (!isFinite(min) || !isFinite(max) || min === max) {
            const delta = Math.abs(min) || 1;
            min -= delta;
            max += delta;
        }
        const tickCount = Math.max(count || 5, 2);
        const range = niceNum(max - min, false);
        const step = niceNum(range / (tickCount - 1), true);
        const niceMin = Math.floor(min / step) * step;
        const niceMax = Math.ceil(max / step) * step;
        const ticks = [];
        for (let v = niceMin; v <= niceMax + step * 0.5; v += step) {
            const rounded = Math.abs(v) < step * 0.0001 ? 0 : v;
            ticks.push(rounded);
        }
        return { ticks, min: niceMin, max: niceMax };
    }

    function formatTick(value) {
        const abs = Math.abs(value);
        if (abs >= 1e6) {
            return (value / 1e6).toFixed(1).replace(/\.0$/, '') + 'M';
        }
        if (abs >= 1e3) {
            return (value / 1e3).toFixed(1).replace(/\.0$/, '') + 'k';
        }
        if (abs === Math.floor(abs)) {
            return String(value);
        }
        return value.toFixed(2).replace(/\.00$/, '');
    }

    function toNumber(value) {
        if (value === null || value === undefined || value === '') {
            return null;
        }
        const num = Number(value);
        return isFinite(num) ? num : null;
    }

    function resolveColor(dataset, index, fallback) {
        return dataset[fallback] || dataset.backgroundColor || dataset.borderColor || COLOR_PALETTE[index % COLOR_PALETTE.length];
    }

    class Axis {
        constructor(id, config) {
            this.id = id;
            this.config = config || {};
            this.position = (this.config.position || 'left').toLowerCase();
            this.values = [];
            this.min = 0;
            this.max = 1;
            this.ticks = [0, 1];
            this.lineX = 0;
        }

        computeRange() {
            if (this.values.length === 0) {
                this.values = [0, 1];
            }
            let min = Math.min(...this.values);
            let max = Math.max(...this.values);
            const beginAtZero = !!this.config.beginAtZero;
            if (beginAtZero) {
                min = Math.min(0, min);
                max = Math.max(0, max);
            }
            if (min === max) {
                const delta = Math.abs(min) || 1;
                min -= delta / 2;
                max += delta / 2;
            }
            const padding = (max - min) * 0.1 || 1;
            min -= padding;
            max += padding;
            if (beginAtZero && min > 0) {
                min = 0;
            }
            if (beginAtZero && max < 0) {
                max = 0;
            }
            const tickInfo = buildTicks(min, max, 6);
            this.min = tickInfo.min;
            this.max = tickInfo.max;
            this.ticks = tickInfo.ticks;
        }

        scale(value, chartTop, chartBottom) {
            if (value === null || value === undefined) {
                return null;
            }
            const ratio = (value - this.min) / (this.max - this.min || 1);
            const clamped = Math.max(0, Math.min(1, ratio));
            return chartBottom - clamped * (chartBottom - chartTop);
        }
    }

    class ChartInstance {
        constructor(element, config) {
            this.canvas = element instanceof HTMLCanvasElement ? element : element?.canvas || element;
            if (!(this.canvas instanceof HTMLCanvasElement)) {
                throw new Error('Chart requires a canvas element.');
            }
            this.ctx = this.canvas.getContext('2d');
            this.originalConfig = deepClone(config || {});
            this.config = deepClone(config || {});
            this.dpr = window.devicePixelRatio || 1;
            this._resizeHandler = () => this.render();
            window.addEventListener('resize', this._resizeHandler);
            this.render();
        }

        destroy() {
            window.removeEventListener('resize', this._resizeHandler);
            this.ctx && this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
        }

        update(newConfig) {
            if (newConfig) {
                this.config = deepClone(newConfig);
            }
            this.render();
        }

        getDatasets() {
            const datasets = this.config.data?.datasets || [];
            const chartType = (this.config.type || 'bar').toLowerCase();
            return datasets.map((ds, index) => {
                const dsCopy = Object.assign({}, ds);
                dsCopy._index = index;
                dsCopy._resolvedType = (ds.type || chartType || 'bar').toLowerCase();
                dsCopy._yAxis = ds.yAxisID || dsCopy.yAxisId || 'y';
                return dsCopy;
            });
        }

        prepareAxes(datasets) {
            const axesConfig = this.config.options?.scales || {};
            const axes = new Map();
            const ensureAxis = (axisId) => {
                if (!axes.has(axisId)) {
                    const cfg = axesConfig[axisId] || {};
                    axes.set(axisId, new Axis(axisId, cfg));
                }
                return axes.get(axisId);
            };

            if (Object.keys(axesConfig).length === 0) {
                axes.set('y', new Axis('y', { position: 'left', beginAtZero: true }));
            }

            datasets.forEach((ds) => {
                const axis = ensureAxis(ds._yAxis);
                (ds.data || []).forEach((value) => {
                    const num = toNumber(value);
                    if (num !== null) {
                        axis.values.push(num);
                    }
                });
                if (axis.config.beginAtZero) {
                    axis.values.push(0);
                }
            });

            axes.forEach((axis) => axis.computeRange());
            return axes;
        }

        setCanvasSize() {
            const parent = this.canvas.parentElement;
            const cssWidth = (parent?.clientWidth || this.canvas.clientWidth || this.canvas.width || 600);
            const cssHeight = this.canvas.clientHeight || this.canvas.height || 320;
            this.canvas.style.width = cssWidth + 'px';
            this.canvas.style.height = cssHeight + 'px';
            this.canvas.width = cssWidth * this.dpr;
            this.canvas.height = cssHeight * this.dpr;
            return { width: cssWidth, height: cssHeight };
        }

        render() {
            const ctx = this.ctx;
            if (!ctx) return;
            this.config = deepClone(this.config);
            const datasets = this.getDatasets();
            const axes = this.prepareAxes(datasets);
            const { width, height } = this.setCanvasSize();

            ctx.save();
            ctx.scale(this.dpr, this.dpr);
            ctx.clearRect(0, 0, width, height);

            const labels = this.config.data?.labels || [];
            const leftAxes = Array.from(axes.values()).filter(a => a.position !== 'right');
            const rightAxes = Array.from(axes.values()).filter(a => a.position === 'right');

            const legendHeight = datasets.some(ds => ds.label) ? 30 : 0;
            const topPadding = 20 + legendHeight;
            const bottomPadding = 60;
            const leftPadding = leftAxes.length ? 50 + (leftAxes.length - 1) * 40 : 40;
            const rightPadding = rightAxes.length ? 50 + (rightAxes.length - 1) * 40 : 40;

            const chartLeft = leftPadding;
            const chartRight = width - rightPadding;
            const chartTop = topPadding;
            const chartBottom = height - bottomPadding;
            const chartWidth = Math.max(10, chartRight - chartLeft);
            const chartHeight = Math.max(10, chartBottom - chartTop);

            leftAxes.forEach((axis, index) => {
                axis.lineX = chartLeft - index * 40;
            });
            rightAxes.forEach((axis, index) => {
                axis.lineX = chartRight + index * 40;
            });

            const axisById = {};
            axes.forEach((axis, key) => {
                axisById[key] = axis;
            });

            this.drawGridAndAxes(ctx, { leftAxes, rightAxes, chartLeft, chartRight, chartTop, chartBottom, chartHeight, chartWidth }, axisById);
            this.drawBars(ctx, datasets, labels, axisById, chartLeft, chartTop, chartWidth, chartHeight, chartBottom);
            this.drawLines(ctx, datasets, labels, axisById, chartLeft, chartTop, chartWidth, chartHeight, chartBottom);
            this.drawXAxis(ctx, labels, chartLeft, chartTop, chartWidth, chartBottom);
            this.drawLegend(ctx, datasets, chartLeft, topPadding - legendHeight + 10);

            ctx.restore();
        }

        drawGridAndAxes(ctx, layout, axisById) {
            const { leftAxes, rightAxes, chartLeft, chartRight, chartTop, chartBottom } = layout;
            ctx.save();
            ctx.strokeStyle = '#d0d0d0';
            ctx.fillStyle = '#4a4a4a';
            ctx.lineWidth = 1;
            ctx.font = '12px sans-serif';

            const drawAxis = (axis, alignLeft) => {
                const lineX = axis.lineX;
                ctx.beginPath();
                ctx.strokeStyle = '#888';
                ctx.moveTo(lineX, chartTop);
                ctx.lineTo(lineX, chartBottom);
                ctx.stroke();

                axis.ticks.forEach((tick) => {
                    const y = axis.scale(tick, chartTop, chartBottom);
                    if (y === null) return;
                    ctx.strokeStyle = '#ccc';
                    const gridEnabled = axis.config?.grid?.drawOnChartArea !== false;
                    if (gridEnabled) {
                        ctx.beginPath();
                        ctx.moveTo(chartLeft, y);
                        ctx.lineTo(chartRight, y);
                        ctx.stroke();
                    }
                    ctx.save();
                    ctx.fillStyle = '#4a4a4a';
                    ctx.textAlign = alignLeft ? 'right' : 'left';
                    ctx.textBaseline = 'middle';
                    const labelX = alignLeft ? lineX - 8 : lineX + 8;
                    ctx.fillText(formatTick(tick), labelX, y);
                    ctx.restore();
                });

                const title = axis.config?.title?.text;
                if (title) {
                    ctx.save();
                    ctx.translate(lineX + (alignLeft ? -36 : 36), (chartTop + chartBottom) / 2);
                    ctx.rotate(-Math.PI / 2);
                    ctx.textAlign = 'center';
                    ctx.fillText(title, 0, 0);
                    ctx.restore();
                }
            };

            leftAxes.forEach(axis => drawAxis(axis, true));
            rightAxes.forEach(axis => drawAxis(axis, false));
            ctx.restore();
        }

        drawXAxis(ctx, labels, chartLeft, chartTop, chartWidth, chartBottom) {
            const labelCount = labels.length;
            ctx.save();
            ctx.strokeStyle = '#888';
            ctx.fillStyle = '#4a4a4a';
            ctx.lineWidth = 1;
            ctx.font = '12px sans-serif';
            ctx.beginPath();
            ctx.moveTo(chartLeft, chartBottom);
            ctx.lineTo(chartLeft + chartWidth, chartBottom);
            ctx.stroke();
            if (labelCount === 0) {
                ctx.restore();
                return;
            }
            const categoryWidth = chartWidth / labelCount;
            for (let i = 0; i < labelCount; i++) {
                const x = chartLeft + categoryWidth * (i + 0.5);
                ctx.textAlign = 'center';
                ctx.textBaseline = 'top';
                const text = String(labels[i]);
                ctx.fillText(text, x, chartBottom + 10);
            }
            ctx.restore();
        }

        drawBars(ctx, datasets, labels, axisById, chartLeft, chartTop, chartWidth, chartHeight, chartBottom) {
            const barDatasets = datasets.filter(ds => ds._resolvedType === 'bar');
            if (!barDatasets.length) return;
            const labelCount = labels.length;
            const categoryWidth = chartWidth / Math.max(labelCount, 1);
            const barCount = barDatasets.length;
            const groupWidth = categoryWidth * 0.7;
            const barWidth = groupWidth / Math.max(barCount, 1);
            barDatasets.forEach((dataset, order) => {
                const color = resolveColor(dataset, dataset._index, 'backgroundColor');
                const borderColor = dataset.borderColor || color;
                const borderWidth = dataset.borderWidth || 0;
                const axis = axisById[dataset._yAxis];
                if (!axis) return;
                (dataset.data || []).forEach((rawValue, index) => {
                    const value = toNumber(rawValue);
                    if (value === null) return;
                    const center = chartLeft + categoryWidth * (index + 0.5);
                    const x = center - groupWidth / 2 + order * barWidth;
                    const y = axis.scale(value, chartTop, chartBottom);
                    const zeroY = axis.scale(0, chartTop, chartBottom);
                    const topY = Math.min(y, zeroY);
                    const height = Math.abs((zeroY ?? chartBottom) - y);
                    ctx.fillStyle = color;
                    ctx.fillRect(x, topY, barWidth - 4, height);
                    if (borderWidth > 0) {
                        ctx.save();
                        ctx.strokeStyle = borderColor;
                        ctx.lineWidth = borderWidth;
                        ctx.strokeRect(x, topY, barWidth - 4, height);
                        ctx.restore();
                    }
                });
            });
        }

        drawLines(ctx, datasets, labels, axisById, chartLeft, chartTop, chartWidth, chartHeight, chartBottom) {
            const lineDatasets = datasets.filter(ds => ds._resolvedType !== 'bar');
            if (!lineDatasets.length) return;
            const labelCount = labels.length;
            const categoryWidth = chartWidth / Math.max(labelCount, 1);
            lineDatasets.forEach((dataset) => {
                const axis = axisById[dataset._yAxis];
                if (!axis) return;
                const color = dataset.borderColor || resolveColor(dataset, dataset._index, 'borderColor');
                const tension = Math.min(Math.max(Number(dataset.tension) || 0, 0), 0.8);
                const dataPoints = (dataset.data || []).map((rawValue, index) => {
                    const value = toNumber(rawValue);
                    if (value === null) return null;
                    const x = chartLeft + categoryWidth * (index + 0.5);
                    const y = axis.scale(value, chartTop, chartBottom);
                    return { x, y, value };
                });
                ctx.save();
                ctx.strokeStyle = color;
                ctx.lineWidth = dataset.borderWidth || 2;
                if (Array.isArray(dataset.borderDash)) {
                    ctx.setLineDash(dataset.borderDash);
                } else {
                    ctx.setLineDash([]);
                }
                ctx.beginPath();
                let started = false;
                let lastPoint = null;
                dataPoints.forEach((point) => {
                    if (!point) {
                        started = false;
                        lastPoint = null;
                        return;
                    }
                    if (!started) {
                        ctx.moveTo(point.x, point.y);
                        started = true;
                    } else if (tension > 0 && lastPoint) {
                        const cp1x = lastPoint.x + (point.x - lastPoint.x) * tension;
                        const cp1y = lastPoint.y;
                        const cp2x = point.x - (point.x - lastPoint.x) * tension;
                        const cp2y = point.y;
                        ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, point.x, point.y);
                    } else {
                        ctx.lineTo(point.x, point.y);
                    }
                    lastPoint = point;
                });
                ctx.stroke();

                if (dataset.fill) {
                    ctx.lineTo(chartLeft + chartWidth, axis.scale(0, chartTop, chartBottom));
                    ctx.lineTo(chartLeft, axis.scale(0, chartTop, chartBottom));
                    ctx.closePath();
                    const fillColor = dataset.backgroundColor || color + '33';
                    ctx.fillStyle = fillColor;
                    ctx.fill();
                }
                ctx.restore();
            });
        }

        drawLegend(ctx, datasets, startX, y) {
            if (!datasets.some(ds => ds.label)) {
                return;
            }
            ctx.save();
            ctx.font = '13px sans-serif';
            ctx.textBaseline = 'middle';
            let x = startX;
            const gap = 16;
            datasets.forEach((dataset, index) => {
                const label = dataset.label;
                if (!label) return;
                const color = resolveColor(dataset, dataset._index, dataset._resolvedType === 'bar' ? 'backgroundColor' : 'borderColor');
                ctx.fillStyle = color;
                ctx.fillRect(x, y, 12, 12);
                ctx.fillStyle = '#333';
                ctx.textAlign = 'left';
                ctx.fillText(label, x + 18, y + 6);
                x += ctx.measureText(label).width + 18 + gap;
            });
            ctx.restore();
        }
    }

    window.Chart = ChartInstance;
})();
