// ScanFetch Monitor Web Application
class ScanFetchMonitor {
    constructor() {
        this.apiUrl = window.location.origin;
        this.eventSource = null;
        this.isConnected = false;
        this.startTime = Date.now();
        this.events = [];
        this.logs = [];
        this.maxEvents = 100;
        this.maxLogs = 500;
        
        this.init();
    }

    init() {
        this.setupEventListeners();
        this.connectSSE();
        this.loadInitialData();
        this.startUptimeCounter();
        this.requestNotificationPermission();
    }

    // Setup UI event listeners
    setupEventListeners() {
        document.getElementById('startBtn').addEventListener('click', () => this.startScanFetch());
        document.getElementById('stopBtn').addEventListener('click', () => this.stopScanFetch());
        document.getElementById('restartBtn').addEventListener('click', () => this.restartScanFetch());
        document.getElementById('refreshBtn').addEventListener('click', () => this.loadInitialData());
        document.getElementById('clearEventsBtn').addEventListener('click', () => this.clearEvents());
        document.getElementById('clearLogsBtn').addEventListener('click', () => this.clearLogs());
        document.getElementById('logLevelFilter').addEventListener('change', (e) => this.filterLogs(e.target.value));
    }

    // Request browser notification permission
    requestNotificationPermission() {
        if ('Notification' in window && Notification.permission === 'default') {
            Notification.requestPermission();
        }
    }

    // Show browser notification
    showNotification(title, body, type = 'info') {
        if ('Notification' in window && Notification.permission === 'granted') {
            const icon = type === 'error' ? '🔴' : type === 'warning' ? '🟡' : '🔵';
            new Notification(`${icon} ${title}`, {
                body: body,
                icon: '/favicon.ico',
                badge: '/favicon.ico',
                tag: 'scanfetch-alert',
                requireInteraction: type === 'error'
            });
        }
    }

    // Connect to SSE stream
    connectSSE() {
        this.updateConnectionStatus('connecting', 'Подключение...');
        
        this.eventSource = new EventSource(`${this.apiUrl}/api/events`);
        
        this.eventSource.onopen = () => {
            this.isConnected = true;
            this.updateConnectionStatus('connected', 'Подключено');
            this.addLog('info', 'Подключено к серверу событий');
        };
        
        this.eventSource.onmessage = (event) => {
            try {
                const data = JSON.parse(event.data);
                this.handleEvent(data);
            } catch (e) {
                console.error('Failed to parse event:', e);
            }
        };
        
        this.eventSource.onerror = () => {
            this.isConnected = false;
            this.updateConnectionStatus('disconnected', 'Отключено');
            this.addLog('error', 'Соединение с сервером потеряно');
            
            // Reconnect after 5 seconds
            setTimeout(() => {
                if (!this.isConnected) {
                    this.connectSSE();
                }
            }, 5000);
        };
    }

    // Handle incoming SSE event
    handleEvent(event) {
        this.addEvent(event);
        
        // Show notification for errors and disconnections
        if (event.type === 'ScannerError') {
            this.showNotification('Ошибка сканера', `${event.scanner}: ${event.message}`, 'error');
            this.addLog('error', `[${event.scanner}] ${event.message}`);
        } else if (event.type === 'ScannerDisconnected') {
            this.showNotification('Сканер отключён', `${event.scanner}: ${event.message}`, 'warning');
            this.addLog('warning', `[${event.scanner}] ${event.message}`);
        } else if (event.type === 'ScannerConnected') {
            this.addLog('info', `[${event.scanner}] Подключён`);
        }
    }

    // Update connection status indicator
    updateConnectionStatus(status, text) {
        const dot = document.getElementById('connectionDot');
        const textEl = document.getElementById('connectionText');
        
        dot.className = `status-dot ${status}`;
        textEl.textContent = text;
    }

    // Load initial data
    async loadInitialData() {
        await this.loadScanners();
        await this.loadErrors();
    }

    // Load scanners status
    async loadScanners() {
        try {
            const response = await fetch(`${this.apiUrl}/api/status`);
            const data = await response.json();
            
            if (data.scanners) {
                this.renderScanners(data.scanners);
                this.updateStatistics(data.scanners);
            }
        } catch (e) {
            console.error('Failed to load scanners:', e);
            this.addLog('error', 'Не удалось загрузить статус сканеров');
        }
    }

    // Load error history
    async loadErrors() {
        try {
            const response = await fetch(`${this.apiUrl}/api/errors?count=50`);
            const data = await response.json();
            
            if (data.errors) {
                data.errors.reverse().forEach(event => this.addEvent(event, false));
            }
        } catch (e) {
            console.error('Failed to load errors:', e);
        }
    }

    // Render scanners grid
    renderScanners(scanners) {
        const container = document.getElementById('scannersContainer');
        
        if (scanners.length === 0) {
            container.innerHTML = '<div class="empty-state">Нет настроенных сканеров</div>';
            return;
        }
        
        container.innerHTML = scanners.map(scanner => `
            <div class="scanner-card">
                <div class="scanner-header">
                    <div class="scanner-name">${this.escapeHtml(scanner.name)}</div>
                    <div class="scanner-status ${scanner.connected ? 'connected' : 'disconnected'}">
                        ${scanner.connected ? 
                            '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/></svg> Подключён' :
                            '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/></svg> Отключён'
                        }
                    </div>
                </div>
                <div class="scanner-info">
                    <div><strong>Роль:</strong> ${this.escapeHtml(scanner.role)}</div>
                    <div><strong>Адрес:</strong> ${this.escapeHtml(scanner.ip)}:${scanner.port}</div>
                    ${scanner.remoteEndpoint ? `<div><strong>Удалённый:</strong> ${this.escapeHtml(scanner.remoteEndpoint)}</div>` : ''}
                </div>
            </div>
        `).join('');
    }

    // Update statistics
    updateStatistics(scanners) {
        const connected = scanners.filter(s => s.connected).length;
        const disconnected = scanners.filter(s => !s.connected).length;
        const errors = this.events.filter(e => e.type === 'ScannerError').length;
        
        document.getElementById('statConnected').textContent = connected;
        document.getElementById('statDisconnected').textContent = disconnected;
        document.getElementById('statErrors').textContent = errors;
        document.getElementById('errorCount').textContent = errors;
    }

    // Add event to list
    addEvent(event, updateStats = true) {
        this.events.unshift(event);
        if (this.events.length > this.maxEvents) {
            this.events.pop();
        }
        
        const container = document.getElementById('eventsContainer');
        const isEmpty = container.querySelector('.empty-state');
        if (isEmpty) {
            container.innerHTML = '';
        }
        
        const eventType = event.type === 'ScannerError' ? 'error' : 
                         event.type === 'ScannerDisconnected' ? 'warning' : 'info';
        
        const eventEl = document.createElement('div');
        eventEl.className = `event-item ${eventType}`;
        eventEl.innerHTML = `
            <div class="event-header">
                <span class="event-type">${this.escapeHtml(event.type)}</span>
                <span class="event-time">${this.formatTimestamp(event.timestamp)}</span>
            </div>
            <div class="event-message">${this.escapeHtml(event.message)}</div>
            ${event.scanner ? `<div class="event-scanner">Сканер: ${this.escapeHtml(event.scanner)}</div>` : ''}
        `;
        
        container.insertBefore(eventEl, container.firstChild);
        
        // Auto-scroll
        if (document.getElementById('autoScrollEvents').checked) {
            container.scrollTop = 0;
        }
        
        // Limit displayed events
        while (container.children.length > this.maxEvents) {
            container.removeChild(container.lastChild);
        }
        
        if (updateStats) {
            this.loadScanners(); // Reload to update stats
        }
    }

    // Add log entry
    addLog(level, message) {
        this.logs.unshift({ level, message, timestamp: new Date().toISOString() });
        if (this.logs.length > this.maxLogs) {
            this.logs.pop();
        }
        
        const container = document.getElementById('logsContainer');
        const isEmpty = container.querySelector('.empty-state');
        if (isEmpty) {
            container.innerHTML = '';
        }
        
        const logEl = document.createElement('div');
        logEl.className = `log-item ${level}`;
        logEl.dataset.level = level;
        logEl.innerHTML = `
            <span class="log-timestamp">[${this.formatTime(new Date())}]</span>
            <span class="log-message">${this.escapeHtml(message)}</span>
        `;
        
        container.insertBefore(logEl, container.firstChild);
        
        // Auto-scroll
        if (document.getElementById('autoScrollLogs').checked) {
            container.scrollTop = 0;
        }
        
        // Limit displayed logs
        while (container.children.length > this.maxLogs) {
            container.removeChild(container.lastChild);
        }
    }

    // Filter logs by level
    filterLogs(level) {
        const logs = document.querySelectorAll('.log-item');
        logs.forEach(log => {
            if (level === 'all' || log.dataset.level === level) {
                log.style.display = '';
            } else {
                log.style.display = 'none';
            }
        });
    }

    // Clear events
    clearEvents() {
        this.events = [];
        document.getElementById('eventsContainer').innerHTML = '<div class="empty-state">Нет событий</div>';
        document.getElementById('errorCount').textContent = '0';
    }

    // Clear logs
    clearLogs() {
        this.logs = [];
        document.getElementById('logsContainer').innerHTML = '<div class="empty-state">Нет логов</div>';
    }

    // Start ScanFetch (placeholder - implement server-side control)
    async startScanFetch() {
        this.addLog('info', 'Команда запуска отправлена');
        this.showNotification('ScanFetch', 'Запуск приложения...', 'info');
        // TODO: Implement server-side endpoint
    }

    // Stop ScanFetch (placeholder - implement server-side control)
    async stopScanFetch() {
        this.addLog('warning', 'Команда остановки отправлена');
        this.showNotification('ScanFetch', 'Остановка приложения...', 'warning');
        // TODO: Implement server-side endpoint
    }

    // Restart ScanFetch
    async restartScanFetch() {
        await this.stopScanFetch();
        setTimeout(() => this.startScanFetch(), 2000);
    }

    // Start uptime counter
    startUptimeCounter() {
        setInterval(() => {
            const uptime = Date.now() - this.startTime;
            document.getElementById('statUptime').textContent = this.formatUptime(uptime);
        }, 1000);
    }

    // Format uptime
    formatUptime(ms) {
        const seconds = Math.floor(ms / 1000);
        const hours = Math.floor(seconds / 3600);
        const minutes = Math.floor((seconds % 3600) / 60);
        const secs = seconds % 60;
        return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(secs).padStart(2, '0')}`;
    }

    // Format timestamp
    formatTimestamp(timestamp) {
        const date = new Date(timestamp);
        return date.toLocaleString('ru-RU', {
            day: '2-digit',
            month: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit'
        });
    }

    // Format time
    formatTime(date) {
        return date.toLocaleTimeString('ru-RU', {
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit'
        });
    }

    // Escape HTML
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', () => {
    window.monitor = new ScanFetchMonitor();
});
