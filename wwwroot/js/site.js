document.addEventListener('DOMContentLoaded', () => {
    // 1. Theme Toggle Logic
    const themeToggle = document.getElementById('theme-toggle');
    const themeIcon = document.getElementById('theme-icon');
    const htmlElement = document.documentElement;

    // Load saved theme immediately
    const savedTheme = localStorage.getItem('theme') || 'dark';
    htmlElement.setAttribute('data-theme', savedTheme);
    updateThemeIcon(savedTheme);

    if (themeToggle) {
        themeToggle.addEventListener('click', () => {
            const currentTheme = htmlElement.getAttribute('data-theme');
            const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
            
            htmlElement.setAttribute('data-theme', newTheme);
            localStorage.setItem('theme', newTheme);
            updateThemeIcon(newTheme);
        });
    }

    function updateThemeIcon(theme) {
        if (!themeIcon) return;
        if (theme === 'dark') {
            themeIcon.classList.replace('bi-sun', 'bi-moon-stars');
        } else {
            themeIcon.classList.replace('bi-moon-stars', 'bi-sun');
        }
    }

    // 2. Notification Polling (Only if bell exists)
    const notiCountBadge = document.getElementById('noti-count');
    const notiList = document.getElementById('noti-list');
    const notiBell = document.getElementById('notificationDropdown');

    if (notiBell) {
        const updateNotifications = async () => {
            try {
                const response = await fetch('/api/notifications');
                if (response.ok) {
                    const notifications = await response.json();
                    
                    if (notifications.length > 0) {
                        notiCountBadge.textContent = notifications.length;
                        notiCountBadge.style.display = 'block';
                        
                        notiList.innerHTML = notifications.map(n => `
                            <li>
                                <div class="notification-item">
                                    <p class="mb-1 small">${n.message}</p>
                                    <span class="text-muted" style="font-size: 0.7rem;">${new Date(n.timestamp).toLocaleString()}</span>
                                </div>
                            </li>
                        `).join('');
                    } else {
                        notiCountBadge.style.display = 'none';
                        notiList.innerHTML = '<li class="p-3 text-center text-muted small">No new notifications</li>';
                    }
                }
            } catch (error) {
                console.error('Error fetching notifications:', error);
            }
        };

        // Initial fetch
        updateNotifications();
        
        // Mark as read when dropdown is opened
        notiBell.addEventListener('click', async () => {
            if (notiCountBadge.style.display !== 'none') {
                try {
                    await fetch('/api/notifications/mark-read', { method: 'POST' });
                    setTimeout(() => {
                        notiCountBadge.style.display = 'none';
                    }, 2000);
                } catch (e) {
                    console.error('Error marking notifications as read', e);
                }
            }
        });

        // Poll every 30 seconds
        setInterval(updateNotifications, 30000);
    }
});
