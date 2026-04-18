// MentorMatch — site.js
// ---------------------------------------------------------------------------
// Global client-side behaviours:
//   1. Theme toggle (syncs with the no-flash script in _Layout.cshtml)
//   2. Active nav-link highlighting based on current URL
//   3. OS color-scheme change listener (only if user hasn't explicitly chosen)
//   4. Notification polling (only renders if the bell exists)
//   5. Reduced-motion aware micro-interactions
// ---------------------------------------------------------------------------

(function () {
    'use strict';

    const htmlEl = document.documentElement;
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    document.addEventListener('DOMContentLoaded', () => {
        initThemeToggle();
        initActiveNavLink();
        initOsThemeListener();
        initNotifications();
    });

    // -----------------------------------------------------------------------
    // 1. Theme toggle
    // -----------------------------------------------------------------------
    function initThemeToggle() {
        const themeToggle = document.getElementById('theme-toggle');
        const themeIcon = document.getElementById('theme-icon');
        if (!themeToggle) return;

        // The no-flash script in _Layout.cshtml already set data-theme before paint.
        // We just need to sync icon + aria-pressed with that initial state.
        const initialTheme = htmlEl.getAttribute('data-theme') || 'dark';
        syncToggleUi(themeToggle, themeIcon, initialTheme);

        themeToggle.addEventListener('click', () => {
            const current = htmlEl.getAttribute('data-theme') === 'light' ? 'light' : 'dark';
            const next = current === 'dark' ? 'light' : 'dark';

            htmlEl.setAttribute('data-theme', next);
            try { localStorage.setItem('theme', next); } catch (_) { /* private mode / quota */ }
            syncToggleUi(themeToggle, themeIcon, next);
        });
    }

    function syncToggleUi(btn, icon, theme) {
        btn.setAttribute('aria-pressed', theme === 'light' ? 'true' : 'false');
        if (!icon) return;
        // bi-sun in light mode, bi-moon-stars in dark mode
        if (theme === 'light') {
            icon.classList.remove('bi-moon-stars');
            icon.classList.add('bi-sun');
        } else {
            icon.classList.remove('bi-sun');
            icon.classList.add('bi-moon-stars');
        }
    }

    // -----------------------------------------------------------------------
    // 2. Active nav-link highlighting
    //    Adds .active to the nav-link whose href best matches current path.
    // -----------------------------------------------------------------------
    function initActiveNavLink() {
        const path = window.location.pathname.replace(/\/+$/, '').toLowerCase() || '/';
        const links = document.querySelectorAll('.navbar-nav .nav-link[href]');
        let bestMatch = null;
        let bestLength = -1;

        links.forEach(link => {
            let href;
            try {
                href = new URL(link.href, window.location.origin).pathname.replace(/\/+$/, '').toLowerCase() || '/';
            } catch (_) { return; }

            // Skip '#' and 'javascript:' anchors
            if (!href || href === '#') return;

            if (path === href || (href !== '/' && path.startsWith(href + '/'))) {
                if (href.length > bestLength) {
                    bestMatch = link;
                    bestLength = href.length;
                }
            }
        });

        if (bestMatch) {
            bestMatch.classList.add('active');
            bestMatch.setAttribute('aria-current', 'page');
        }
    }

    // -----------------------------------------------------------------------
    // 3. OS theme listener
    //    If the user has NOT set a saved theme, follow their OS preference
    //    live (so flipping OS dark/light updates the app without a reload).
    // -----------------------------------------------------------------------
    function initOsThemeListener() {
        if (!window.matchMedia) return;
        const mql = window.matchMedia('(prefers-color-scheme: light)');

        const handler = (e) => {
            let saved = null;
            try { saved = localStorage.getItem('theme'); } catch (_) {}
            if (saved) return; // user made an explicit choice — don't override
            const next = e.matches ? 'light' : 'dark';
            htmlEl.setAttribute('data-theme', next);
            const btn = document.getElementById('theme-toggle');
            const icon = document.getElementById('theme-icon');
            if (btn) syncToggleUi(btn, icon, next);
        };

        if (mql.addEventListener) mql.addEventListener('change', handler);
        else if (mql.addListener) mql.addListener(handler); // older browsers
    }

    // -----------------------------------------------------------------------
    // 4. Notification polling
    //    Only runs if the bell element is present in the DOM.
    // -----------------------------------------------------------------------
    function initNotifications() {
        const notiBell = document.getElementById('notificationDropdown');
        const notiCountBadge = document.getElementById('noti-count');
        const notiList = document.getElementById('noti-list');
        if (!notiBell) return;

        const escapeHtml = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[c]));

        const formatTime = (t) => {
            const d = new Date(t);
            return isNaN(d.getTime()) ? '' : d.toLocaleString();
        };

        const updateNotifications = async () => {
            try {
                const response = await fetch('/api/notifications', { credentials: 'same-origin' });
                if (!response.ok) return;
                const notifications = await response.json();

                if (Array.isArray(notifications) && notifications.length > 0) {
                    if (notiCountBadge) {
                        notiCountBadge.textContent = String(notifications.length);
                        notiCountBadge.style.display = 'block';
                    }
                    if (notiList) {
                        notiList.innerHTML = notifications.map(n => `
                            <li>
                                <div class="notification-item">
                                    <p class="mb-1 small">${escapeHtml(n.message)}</p>
                                    <span class="text-muted" style="font-size: 0.7rem;">${escapeHtml(formatTime(n.timestamp))}</span>
                                </div>
                            </li>
                        `).join('');
                    }
                } else {
                    if (notiCountBadge) notiCountBadge.style.display = 'none';
                    if (notiList) {
                        notiList.innerHTML = '<li class="p-3 text-center text-muted small">No new notifications</li>';
                    }
                }
            } catch (err) {
                // Swallow: no net, 401 after logout, etc.
                console.error('Error fetching notifications:', err);
            }
        };

        // Initial fetch
        updateNotifications();

        // Mark as read when the dropdown is opened
        notiBell.addEventListener('click', async () => {
            if (!notiCountBadge || notiCountBadge.style.display === 'none') return;
            try {
                const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
                const headers = {};
                if (tokenEl) headers['RequestVerificationToken'] = tokenEl.value;

                await fetch('/api/notifications/mark-read', {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers
                });
                const hideAfter = prefersReducedMotion ? 0 : 2000;
                setTimeout(() => { notiCountBadge.style.display = 'none'; }, hideAfter);
            } catch (e) {
                console.error('Error marking notifications as read', e);
            }
        });

        // Poll every 30 seconds
        setInterval(updateNotifications, 30000);
    }
})();
