(() => {
    const messageMenus = [...document.querySelectorAll(
        "[data-message-notifications]")];
    const userNotificationMenus = [...document.querySelectorAll(
        "[data-user-notifications]")];
    if ((messageMenus.length === 0 &&
        userNotificationMenus.length === 0) || !window.signalR) {
        return;
    }

    const activeChat = document.querySelector("[data-session-chat]");
    const activeBookingId = Number(activeChat?.dataset.bookingId || 0);
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/session")
        .withAutomaticReconnect()
        .build();

    const formatCount = (count) => count > 99 ? "99+" : String(count);

    const updateCount = (menu, count) => {
        menu.dataset.unreadCount = String(count);
        const countBadge = menu.querySelector(
            "[data-message-notification-count]");
        const summary = menu.querySelector(
            "[data-message-notification-summary]");
        const empty = menu.querySelector(
            "[data-message-notification-empty]");
        const trigger = menu.querySelector(
            "[data-message-notification-trigger]");

        countBadge.textContent = formatCount(count);
        countBadge.hidden = count === 0;
        summary.textContent = `${count} unread`;
        empty.hidden = count > 0;
        trigger.setAttribute(
            "aria-label",
            `Messages, ${count} unread`);
    };

    if (activeBookingId) {
        messageMenus.forEach((menu) => {
            const visibleMessages = [...menu.querySelectorAll(
                `[data-booking-id="${activeBookingId}"]`)];
            visibleMessages.forEach((message) => message.remove());
            const count = Math.max(
                0,
                Number(menu.dataset.unreadCount || 0) -
                    visibleMessages.length);
            updateCount(menu, count);
        });
    }

    const createNotification = (notification) => {
        const link = document.createElement("a");
        link.className = "message-notification-item is-new";
        link.href = notification.openUrl;
        link.dataset.messageId = notification.sessionMessageId;
        link.dataset.bookingId = notification.bookingId;

        const sender = document.createElement("span");
        sender.className = "message-notification-sender";
        sender.textContent = notification.senderName;
        const preview = document.createElement("span");
        preview.className = "message-notification-preview";
        preview.textContent = notification.messageText;
        const time = document.createElement("time");
        const sentAt = new Date(notification.sentAt);
        time.dateTime = sentAt.toISOString();
        time.textContent = sentAt.toLocaleString([], {
            day: "2-digit",
            month: "short",
            hour: "2-digit",
            minute: "2-digit"
        });
        link.append(sender, preview, time);
        return link;
    };

    const displayNotification = (notification) => {
        if (Number(notification.bookingId) === activeBookingId) {
            return;
        }

        messageMenus.forEach((menu) => {
            const list = menu.querySelector(
                "[data-message-notification-list]");
            if (list.querySelector(
                `[data-message-id="${notification.sessionMessageId}"]`)) {
                return;
            }

            list.prepend(createNotification(notification));
            while (list.children.length > 10) {
                list.lastElementChild.remove();
            }

            const count = Number(menu.dataset.unreadCount || 0) + 1;
            updateCount(menu, count);
        });

        const visibleMenu = messageMenus.find(
            (menu) => menu.offsetParent !== null);
        const trigger = visibleMenu?.querySelector(
            "[data-message-notification-trigger]");
        if (trigger && window.bootstrap) {
            bootstrap.Dropdown.getOrCreateInstance(trigger).show();
        }
    };

    connection.on("ReceiveMessageNotification", displayNotification);

    const updateUserNotificationCount = (menu, count) => {
        menu.dataset.unreadCount = String(count);
        const countBadge = menu.querySelector(
            "[data-user-notification-count]");
        const summary = menu.querySelector(
            "[data-user-notification-summary]");
        const empty = menu.querySelector(
            "[data-user-notification-empty]");
        const trigger = menu.querySelector(
            "[data-user-notification-trigger]");

        countBadge.textContent = formatCount(count);
        countBadge.hidden = count === 0;
        summary.textContent = `${count} unread`;
        empty.hidden = count > 0;
        trigger.setAttribute(
            "aria-label",
            `Notifications, ${count} unread`);
    };

    const createUserNotification = (notification) => {
        const link = document.createElement("a");
        link.className = "message-notification-item is-new";
        link.href = notification.openUrl;
        link.dataset.userNotificationId =
            notification.userNotificationId;

        const title = document.createElement("span");
        title.className = "message-notification-sender";
        title.textContent = notification.title;
        const preview = document.createElement("span");
        preview.className = "message-notification-preview";
        preview.textContent = notification.message;
        const time = document.createElement("time");
        const createdAt = new Date(notification.createdAt);
        time.dateTime = createdAt.toISOString();
        time.textContent = createdAt.toLocaleString([], {
            day: "2-digit",
            month: "short",
            hour: "2-digit",
            minute: "2-digit"
        });
        link.append(title, preview, time);
        return link;
    };

    const displayUserNotification = (notification) => {
        userNotificationMenus.forEach((menu) => {
            const list = menu.querySelector(
                "[data-user-notification-list]");
            if (list.querySelector(
                `[data-user-notification-id="${notification.userNotificationId}"]`)) {
                return;
            }

            list.prepend(createUserNotification(notification));
            while (list.children.length > 10) {
                list.lastElementChild.remove();
            }

            const count = Number(menu.dataset.unreadCount || 0) + 1;
            updateUserNotificationCount(menu, count);
        });

        const visibleMenu = userNotificationMenus.find(
            (menu) => menu.offsetParent !== null);
        const trigger = visibleMenu?.querySelector(
            "[data-user-notification-trigger]");
        if (trigger && window.bootstrap) {
            bootstrap.Dropdown.getOrCreateInstance(trigger).show();
        }
    };

    connection.on(
        "ReceiveUserNotification",
        displayUserNotification);

    const startConnection = async () => {
        try {
            await connection.start();
        } catch {
            window.setTimeout(startConnection, 3000);
        }
    };

    startConnection();
})();
