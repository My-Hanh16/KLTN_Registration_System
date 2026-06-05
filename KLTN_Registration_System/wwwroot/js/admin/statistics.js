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

    function bindTimeGreeting() {
        const greeting = document.getElementById('timeGreeting');
        if (!greeting) return;

        const hour = new Date().getHours();
        if (hour < 11) {
            greeting.textContent = 'Xin chào buổi sáng';
        } else if (hour < 14) {
            greeting.textContent = 'Xin chào buổi trưa';
        } else if (hour < 18) {
            greeting.textContent = 'Xin chào buổi chiều';
        } else {
            greeting.textContent = 'Xin chào buổi tối';
        }
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
        gradient.addColorStop(0, 'rgba(37,99,235,0.24)');
        gradient.addColorStop(0.5, 'rgba(96,165,250,0.08)');
        gradient.addColorStop(1, 'rgba(255,255,255,0)');

        const allLabels = data.monthLabels || [];
        const allCounts = data.monthCounts || [];
        const sliceRange = range => {
            const start = Math.max(0, allLabels.length - range);
            return {
                labels: allLabels.slice(start),
                counts: allCounts.slice(start)
            };
        };
        const initialRange = sliceRange(7);
        const numericData = allCounts.filter(v => v !== null && v !== undefined);

        const chart = new Chart(canvas, {
            type: 'line',
            data: {
                labels: initialRange.labels,
                datasets: [{
                    label: 'Sinh viên đã đăng ký',
                    data: initialRange.counts,
                    borderColor: '#2563eb',
                    backgroundColor: gradient,
                    fill: true,
                    tension: 0.46,
                    cubicInterpolationMode: 'monotone',
                    spanGaps: false,
                    borderWidth: 4,
                    borderCapStyle: 'round',
                    borderJoinStyle: 'round',
                    pointBackgroundColor: '#fff',
                    pointBorderColor: '#2563eb',
                    pointHoverBackgroundColor: '#2563eb',
                    pointHoverBorderColor: '#fff',
                    pointBorderWidth: 0,
                    pointHoverBorderWidth: 3,
                    pointRadius: 0,
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
                            color: 'rgba(148,163,184,0.10)',
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
                        grid: {
                            display: true,
                            color: 'rgba(148,163,184,0.18)',
                            drawTicks: false
                        }
                    }
                }
            }
        });

        document.querySelectorAll('[data-line-range]').forEach(button => {
            button.addEventListener('click', () => {
                const range = Number(button.dataset.lineRange || 7);
                const next = sliceRange(range);
                chart.data.labels = next.labels;
                chart.data.datasets[0].data = next.counts;
                chart.update();

                document.querySelectorAll('[data-line-range]').forEach(item => {
                    item.classList.toggle('active', item === button);
                });
            });
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

    function shortenFacultyName(name) {
        const normalized = (name || '').trim().toLocaleLowerCase('vi-VN');
        const map = {
            'công nghệ thông tin': 'CNTT',
            'dien tu vien thong': 'DTVT',
            'điện tử viễn thông': 'ĐTVT',
            'kinh tế': 'KT',
            'ngoại ngữ': 'NN',
            'báo chí': 'BC'
        };

        if (map[normalized]) return map[normalized];

        const words = (name || '')
            .trim()
            .split(/\s+/)
            .filter(Boolean);

        return words.length > 1
            ? words.map(word => word[0]).join('').toUpperCase()
            : (name || '');
    }

    function initMajorChart(data) {
        const canvas = document.getElementById('majorChart');
        if (!canvas) return;

        const fullLabels = data.majorLabels || [];
        const shortLabels = fullLabels.map(shortenFacultyName);
        const gradient = canvas.getContext('2d').createLinearGradient(0, 0, 0, 300);
        gradient.addColorStop(0, '#6366f1');
        gradient.addColorStop(1, '#4f46e5');

        new Chart(canvas, {
            type: 'bar',
            data: {
                labels: shortLabels,
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
                    tooltip: {
                        ...tooltipDefaults,
                        callbacks: {
                            ...(tooltipDefaults.callbacks || {}),
                            title: items => fullLabels[items[0]?.dataIndex] || items[0]?.label || ''
                        }
                    }
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
        bindTimeGreeting();
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
