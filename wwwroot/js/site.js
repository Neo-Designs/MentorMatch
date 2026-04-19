(function () {
    'use strict';

    const htmlEl = document.documentElement;

    document.addEventListener('DOMContentLoaded', () => {
        initTheme();
        initNotifications();
    });

    // 1. Theme Logic (Sync with _Layout script)
    function initTheme() {
        const themeToggle = document.getElementById('theme-toggle');
        const themeIcon = document.getElementById('theme-icon');
        if (!themeToggle) return;

        themeToggle.addEventListener('click', () => {
            const current = htmlEl.getAttribute('data-theme');
            const next = current === 'dark' ? 'light' : 'dark';
            
            htmlEl.setAttribute('data-theme', next);
            localStorage.setItem('theme', next);
            updateThemeIcon(next, themeIcon);
        });
    }

    function updateThemeIcon(theme, icon) {
        if (!icon) return;
        if (theme === 'dark') {
            icon.classList.replace('bi-sun', 'bi-moon-stars');
        } else {
            icon.classList.replace('bi-moon-stars', 'bi-sun');
        }
    }

    // 2. Notifications Logic (SignalR + Fetch)
    function initNotifications() {
        const badge = document.getElementById('notificationBadge');
        const list = document.getElementById('notificationList');
        const dropdown = document.getElementById('notificationDropdown');
        if (!badge) return;

        // Initialize SignalR Connection
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/notificationHub")
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveNotification", (notification) => {
            // Instant update when server pushes
            fetchNotifications();
            // Optional: Show a toast here if desired
        });

        connection.start().catch(err => console.error("SignalR Connection Error: ", err));

        // Initial fetch for history
        fetchNotifications();

        // Mark as read when dropdown is opened
        dropdown.addEventListener('show.bs.dropdown', () => {
            if (!badge.classList.contains('d-none')) {
                markAllRead();
            }
        });

        window.fetchNotifications = fetchNotifications; // Global access if needed
        window.markAllRead = markAllRead; 
    }

    async function fetchNotifications() {
        const badge = document.getElementById('notificationBadge');
        const list = document.getElementById('notificationList');
        if (!badge || !list) return;

        try {
            const response = await fetch('/api/notifications');
            if (!response.ok) return;
            const data = await response.json();

            let htmlBuffer = '';

            // Header for dropdown
            const unreadCount = data.filter(n => !n.isRead).length;
            if (unreadCount === 0) {
                badge.classList.add('d-none');
            } else {
                badge.textContent = unreadCount;
                badge.classList.remove('d-none');
            }

            htmlBuffer += `
                <li>
                    <div class="d-flex justify-content-between align-items-center px-3 py-2 border-bottom border-white border-opacity-10">
                        <span class="fw-bold small text-success">Notifications</span>
                        ${unreadCount > 0 
                            ? '<button class="btn btn-sm btn-link text-decoration-none small p-0" onclick="markAllRead()">Mark all read</button>' 
                            : '<span class="small text-muted">All caught up</span>'}
                    </div>
                </li>`;

            // Notification Items
            data.forEach(n => {
                const isUnread = !n.isRead;
                htmlBuffer += `
                    <li>
                        <a class="dropdown-item py-3 text-wrap border-bottom border-white border-opacity-10 ${isUnread ? 'bg-success bg-opacity-10' : ''}" href="${n.linkUrl || '#'}">
                            <div class="d-flex align-items-center mb-1">
                                <small class="d-block fw-bold text-white">${generateSafeHtml(n.title ?? 'Notification')}</small>
                                ${isUnread ? '<span class="badge bg-success ms-2" style="font-size: 0.6rem;">NEW</span>' : ''}
                            </div>
                            <span class="small text-white-50">${generateSafeHtml(n.message)}</span>
                            <div class="mt-1" style="font-size: 0.65rem; color: rgba(255,255,255,0.3)">${new Date(n.timestamp).toLocaleString()}</div>
                        </a>
                    </li>`;
            });

            list.innerHTML = htmlBuffer;

        } catch (error) {
            console.error('Error fetching notifications:', error);
        }
    }

    async function markAllRead() {
        const badge = document.getElementById('notificationBadge');
        try {
            await fetch('/api/notifications/mark-read', { method: 'POST' });
            if (badge) badge.classList.add('d-none');
            // Refresh list to remove highlight
            await fetchNotifications();
        } catch (error) {
            console.error('Error marking read:', error);
        }
    }

    function generateSafeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

})();
