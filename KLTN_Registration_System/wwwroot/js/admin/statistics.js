(function () {
    const CHART_PRIMARY = '#4f46e5';
    const CHART_GRAY = '#e2e8f0';

    const tooltipDefaults = {
        backgroundColor: '#1e293b',
        padding: 12,
        titleFont: { size: 13, family: 'Inter' },
        bodyFont: { size: 12, family: 'Inter' },
        cornerRadius: 10,
        displayColors: false
    };

    function readData() {
        const el = document.getElementById('statisticsChartData');
        if (!el) return {};

        try {
            return JSON.parse(el.textContent || '{}');
        } catch {
            return {};
        }
    }

    function showChartLoadError() {
        document.querySelectorAll('.canvas-wrap').forEach(el => {
            el.innerHTML = '<div class="chart-empty">Không tải được thư viện biểu đồ. Vui lòng tải lại trang.</div>';
        });
    }

    function bindFilterSubmit() {
        document.querySelectorAll('.js-auto-submit').forEach(select => {
            select.addEventListener('change', () => {
                select.closest('form')?.submit();
            });
        });
    }

    function applyProgressBars() {
        document.querySelectorAll('.stat-progress-fill[data-progress]').forEach(bar => {
            const value = Math.min(100, Math.max(0, Number(bar.dataset.progress || 0)));
            bar.style.width = value + '%';
        });
    }

    function initPieChart(data) {
        const canvas = document.getElementById('pieChart');
        if (!canvas) return;

        new Chart(canvas, {
            type: 'doughnut',
            data: {
                labels: ['Đã đăng ký', 'Chưa đăng ký'],
                datasets: [{
                    data: [data.registeredStudents || 0, data.unregisteredStudents || 0],
                    backgroundColor: [CHART_PRIMARY, CHART_GRAY],
                    borderWidth: 0,
                    hoverOffset: 6
                }]
            },
            options: {
                cutout: '72%',
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: { ...tooltipDefaults }
                }
            }
        });
    }

    function initLineChart(data) {
        const canvas = document.getElementById('lineChart');
        if (!canvas) return;

        const ctx = canvas.getContext('2d');
        const gradient = ctx.createLinearGradient(0, 0, 0, 300);
        gradient.addColorStop(0, 'rgba(79,70,229,0.28)');
        gradient.addColorStop(0.45, 'rgba(99,102,241,0.10)');
        gradient.addColorStop(1, 'rgba(255,255,255,0)');

        const lineData = data.monthCounts || [];
        const numericData = lineData.filter(v => v !== null && v !== undefined);

        new Chart(canvas, {
            type: 'line',
            data: {
                labels: data.monthLabels || [],
                datasets: [{
                    label: 'Sinh viên đã đăng ký',
                    data: lineData,
                    borderColor: CHART_PRIMARY,
                    backgroundColor: gradient,
                    fill: true,
                    tension: 0.38,
                    cubicInterpolationMode: 'monotone',
                    spanGaps: false,
                    borderWidth: 3,
                    pointBackgroundColor: '#fff',
                    pointBorderColor: '#fff',
                    pointHoverBackgroundColor: CHART_PRIMARY,
                    pointHoverBorderColor: '#fff',
                    pointBorderWidth: 0,
                    pointHoverBorderWidth: 3,
                    pointRadius: ctx => ctx.raw == null ? 0 : 3,
                    pointHoverRadius: 6
                }]
            },
            options: {
                maintainAspectRatio: false,
                animation: { duration: 900, easing: 'easeOutQuart' },
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        ...tooltipDefaults,
                        callbacks: {
                            title: items => `Ngày ${items[0].label}`,
                            label: item => item.raw == null
                                ? 'Chưa tới ngày này'
                                : `${item.raw} sinh viên đã đăng ký`
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        suggestedMax: Math.max(1, ...numericData) + 1,
                        border: { display: false },
                        ticks: {
                            precision: 0,
                            color: '#94a3b8',
                            font: { size: 11, weight: 700 },
                            padding: 8
                        },
                        grid: {
                            color: 'rgba(148,163,184,0.14)',
                            drawTicks: false
                        }
                    },
                    x: {
                        border: { display: false },
                        ticks: {
                            color: '#64748b',
                            font: { size: 11, weight: 700 },
                            maxRotation: 0,
                            autoSkip: true,
                            maxTicksLimit: 8
                        },
                        grid: { display: false }
                    }
                }
            }
        });
    }

    function initTopicStatusChart(data) {
        const canvas = document.getElementById('topicStatusChart');
        if (!canvas) return;

        new Chart(canvas, {
            type: 'doughnut',
            data: {
                labels: data.topicStatusLabels || [],
                datasets: [{
                    data: data.topicStatusCounts || [],
                    backgroundColor: ['#16a34a', '#f59e0b', '#ef4444'],
                    borderColor: '#fff',
                    borderWidth: 4,
                    hoverOffset: 6
                }]
            },
            options: {
                cutout: '68%',
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: { ...tooltipDefaults }
                }
            }
        });
    }

    function initMajorChart(data) {
        const canvas = document.getElementById('majorChart');
        if (!canvas) return;

        const gradient = canvas.getContext('2d').createLinearGradient(0, 0, 0, 300);
        gradient.addColorStop(0, '#6366f1');
        gradient.addColorStop(1, '#4f46e5');

        new Chart(canvas, {
            type: 'bar',
            data: {
                labels: data.majorLabels || [],
                datasets: [{
                    label: 'Số đề tài',
                    data: data.majorCounts || [],
                    backgroundColor: gradient,
                    borderRadius: 8,
                    borderSkipped: false,
                    barThickness: 36
                }]
            },
            options: {
                maintainAspectRatio: false,
                animation: { duration: 1200, easing: 'easeOutQuart' },
                plugins: {
                    legend: { display: false },
                    tooltip: { ...tooltipDefaults }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: { precision: 0 },
                        grid: { color: '#f1f5f9' }
                    },
                    x: { grid: { display: false } }
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        bindFilterSubmit();
        applyProgressBars();

        if (typeof Chart === 'undefined') {
            showChartLoadError();
            return;
        }

        const data = readData();
        initPieChart(data);
        initLineChart(data);
        initTopicStatusChart(data);
        initMajorChart(data);
    });
})();
