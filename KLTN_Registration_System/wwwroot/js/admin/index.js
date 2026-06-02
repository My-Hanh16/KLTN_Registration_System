(function () {
    function readChartData() {
        const el = document.getElementById('registrationLineChartData');
        if (!el) return { labels: [], counts: [] };

        try {
            return JSON.parse(el.textContent || '{}');
        } catch {
            return { labels: [], counts: [] };
        }
    }

    function initRegistrationLineChart() {
        const canvas = document.getElementById('registrationLineChart');
        if (!canvas || !window.Chart) return;

        const data = readChartData();
        const ctx = canvas.getContext('2d');
        const chartGradient = ctx.createLinearGradient(0, 0, 0, 400);
        chartGradient.addColorStop(0, 'rgba(37, 99, 235, 0.2)');
        chartGradient.addColorStop(1, 'rgba(255, 255, 255, 0)');

        new Chart(ctx, {
            type: 'line',
            data: {
                labels: data.labels || [],
                datasets: [{
                    label: 'Đăng ký',
                    data: data.counts || [],
                    borderColor: '#2563eb',
                    borderWidth: 4,
                    fill: true,
                    backgroundColor: chartGradient,
                    tension: 0.4,
                    pointRadius: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false }
                },
                scales: {
                    y: {
                        display: false,
                        beginAtZero: true
                    },
                    x: {
                        grid: { display: false },
                        border: { display: false },
                        ticks: {
                            font: { size: 10, weight: 'bold' },
                            color: '#94a3b8'
                        }
                    }
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', initRegistrationLineChart);
})();
