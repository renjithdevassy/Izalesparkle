'use strict';

const API = 'https://izalesparkle.com';

// ── STATE ─────────────────────────────────────────────────────────
const state = {
    token: localStorage.getItem('izale_tv_token') || null,
    currentPage: 'dashboard',
    orders: [],
    knownOrderIds: new Set(JSON.parse(localStorage.getItem('izale_known_orders') || '[]')),
    focusGroup: 'login',   // login | sidebar | content
    focusIndex: 0,
    orderScrollIndex: 0,
    pollTimer: null,
};

// ── HELPERS ───────────────────────────────────────────────────────
const $ = id => document.getElementById(id);
const fmt = n => '£' + Number(n).toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtDate = d => new Date(d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });

// GET via XHR with token in query string — no custom headers, so no CORS preflight
// (the TV's old browser fails preflighted requests).
function api(path) {
    return new Promise(function(resolve, reject) {
        const sep = path.indexOf('?') >= 0 ? '&' : '?';
        const url = API + path + (state.token ? sep + 'access_token=' + encodeURIComponent(state.token) : '');
        const xhr = new XMLHttpRequest();
        xhr.open('GET', url, true);
        xhr.timeout = 10000;
        xhr.onload = function() {
            if (xhr.status < 200 || xhr.status >= 300) return reject(new Error(xhr.status));
            try { resolve(JSON.parse(xhr.responseText)); }
            catch(e) { reject(e); }
        };
        xhr.onerror = function() { reject(new Error('network')); };
        xhr.ontimeout = function() { reject(new Error('timeout')); };
        xhr.send();
    });
}

// ── CLOCK ─────────────────────────────────────────────────────────
function startClock() {
    const el = $('sidebar-time');
    function tick() {
        const now = new Date();
        el.textContent = now.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })
            + '\n' + now.toLocaleDateString('en-GB', { day: '2-digit', month: 'short' });
    }
    tick();
    setInterval(tick, 30000);
}

// ── NOTIFICATION ──────────────────────────────────────────────────
function showNotification(title, body) {
    $('notif-title').textContent = title;
    $('notif-body').textContent = body;
    const el = $('notification');
    el.classList.add('show');
    // Play a subtle beep via AudioContext
    try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.frequency.value = 880;
        gain.gain.setValueAtTime(0.3, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.6);
        osc.start();
        osc.stop(ctx.currentTime + 0.6);
    } catch(e) {}
    setTimeout(() => el.classList.remove('show'), 5000);
}

// ── STATUS BADGE ──────────────────────────────────────────────────
function badge(status) {
    const map = {
        Pending: 'pending', Processing: 'processing',
        Shipped: 'shipped', Delivered: 'delivered', Cancelled: 'cancelled'
    };
    const cls = map[status] || 'pending';
    return `<span class="badge badge-${cls}">${status}</span>`;
}

// ── LOGIN ─────────────────────────────────────────────────────────
function showStatus(msg, color) {
    if (window.dbg) dbg(msg);
    const el = $('login-error');
    el.textContent = msg;
    el.style.color = color || '#C9A84C';
    el.style.fontSize = '30px';
    el.style.fontWeight = '700';
    el.style.marginTop = '20px';
    el.style.display = 'block';
}

async function doLogin() {
    const email = 'renjithdevassy@gmail.com';
    const pass  = 'Renjithanoopy1!';
    showStatus('Connecting to server...', '#C9A84C');
    const xhr = new XMLHttpRequest();
    xhr.open('POST', API + '/api/auth/tv-login', true);
    xhr.setRequestHeader('Content-Type', 'text/plain');
    xhr.timeout = 15000;
    xhr.onload = function() {
        showStatus('Response: ' + xhr.status + ' (' + xhr.responseText.length + ' chars)', '#C9A84C');
        let data;
        try { data = JSON.parse(xhr.responseText); }
        catch(pe) {
            showStatus('Empty body. Headers: ' + xhr.getAllResponseHeaders().replace(/\r?\n/g, ' | ').substring(0, 400), '#E74C3C');
            return;
        }
        if (data.success && data.token) {
            showStatus('Login OK! Loading...', '#2ECC71');
            state.token = data.token;
            localStorage.setItem('izale_tv_token', data.token);
            setTimeout(enterMain, 800);
        } else {
            showStatus('Login denied: ' + (data.message || JSON.stringify(data).substring(0, 80)), '#E74C3C');
        }
    };
    xhr.onerror = function() { showStatus('Network error (XHR)', '#E74C3C'); };
    xhr.ontimeout = function() { showStatus('Server timeout', '#E74C3C'); };
    xhr.send(JSON.stringify({ email: email, password: pass }));
}

function logout() {
    state.token = null;
    localStorage.removeItem('izale_tv_token');
    clearInterval(state.pollTimer);
    showScreen('login-screen');
    state.focusGroup = 'login';
    loginFocusables[0].classList.add('focused');
    state.focusIndex = 0;
}

// ── SCREENS ───────────────────────────────────────────────────────
function showScreen(id) {
    document.querySelectorAll('.screen').forEach(s => s.classList.remove('active'));
    $(id).classList.add('active');
}

function showPage(name) {
    document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
    $(`${name}-page`).classList.add('active');
    document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
    var navEl = document.querySelector(`[data-page="${name}"]`);
    if (navEl) navEl.classList.add('active');
    state.currentPage = name;
    if (name === 'dashboard') loadDashboard();
    if (name === 'orders') loadOrders();
}

// ── DASHBOARD ─────────────────────────────────────────────────────
async function loadDashboard() {
    try {
        const res = await api('/api/admin/dashboard');
        const d = res.data;
        $('stat-revenue').textContent = fmt(d.totalRevenue);
        $('stat-orders').textContent  = d.totalOrders;
        $('stat-pending').textContent = d.pendingOrders;
        $('stat-today').textContent   = fmt(d.todayRevenue);
        renderRecentOrders(d.recentOrders);
    } catch(e) {
        $('recent-orders-wrap').innerHTML = '<div class="loading" style="color:#E74C3C">Failed to load dashboard</div>';
    }
}

function renderRecentOrders(orders) {
    if (!orders || orders.length === 0) {
        $('recent-orders-wrap').innerHTML = '<div class="loading">No orders yet</div>';
        return;
    }
    const rows = orders.slice(0, 6).map(o => `
        <tr>
            <td>${o.orderNumber}</td>
            <td>${o.customerEmail}</td>
            <td>${fmt(o.total)}</td>
            <td>${badge(o.status)}</td>
            <td>${fmtDate(o.createdAt)}</td>
        </tr>`).join('');
    $('recent-orders-wrap').innerHTML = `
        <table class="orders-table">
            <thead><tr>
                <th>Order #</th><th>Customer</th><th>Total</th><th>Status</th><th>Date</th>
            </tr></thead>
            <tbody>${rows}</tbody>
        </table>`;
}

// ── ORDERS PAGE ───────────────────────────────────────────────────
async function loadOrders() {
    $('orders-list-wrap').innerHTML = '<div class="loading"><div class="spinner"></div> Loading...</div>';
    try {
        const res = await api('/api/admin/orders');
        state.orders = (res.data && res.data.orders) || res.data || [];
        renderOrdersTable();
    } catch(e) {
        $('orders-list-wrap').innerHTML = '<div class="loading" style="color:#E74C3C">Failed to load orders</div>';
    }
}

function renderOrdersTable() {
    const orders = state.orders;
    if (!orders.length) {
        $('orders-list-wrap').innerHTML = '<div class="loading">No orders found</div>';
        return;
    }
    const rows = orders.map((o, i) => `
        <tr data-index="${i}" tabindex="0" class="order-row">
            <td>${o.orderNumber}</td>
            <td>${o.customerEmail}</td>
            <td>${fmt(o.total)}</td>
            <td>${badge(o.status)}</td>
            <td>${o.paymentMethod || 'Bank Transfer'}</td>
            <td>${fmtDate(o.createdAt)}</td>
        </tr>`).join('');
    $('orders-list-wrap').innerHTML = `
        <table class="orders-table" id="orders-table">
            <thead><tr>
                <th>Order #</th><th>Customer</th><th>Total</th><th>Status</th><th>Payment</th><th>Date</th>
            </tr></thead>
            <tbody>${rows}</tbody>
        </table>`;
    // Attach click listeners
    document.querySelectorAll('.order-row').forEach(row => {
        row.addEventListener('click', () => openOrderDetail(parseInt(row.dataset.index)));
    });
}

// ── ORDER DETAIL ──────────────────────────────────────────────────
function openOrderDetail(index) {
    const o = state.orders[index];
    if (!o) return;
    $('detail-number').textContent = o.orderNumber;
    $('detail-date').textContent   = fmtDate(o.createdAt);
    $('detail-status').innerHTML   = badge(o.status);

    const items = (o.items || []).map(i => `
        <div class="item-row">
            <div class="item-name">${i.productName} × ${i.quantity}${i.size ? ' — ' + i.size : ''}</div>
            <div class="item-price">${fmt(i.lineTotal)}</div>
        </div>`).join('');

    $('detail-grid').innerHTML = `
        <div class="detail-section">
            <h3>Customer & Shipping</h3>
            <div class="detail-row"><span class="label">Email</span><span class="value">${o.customerEmail}</span></div>
            <div class="detail-row"><span class="label">Name</span><span class="value">${o.shipFirstName || ''} ${o.shipLastName || ''}</span></div>
            <div class="detail-row"><span class="label">Address</span><span class="value">${o.shipLine1 || ''}, ${o.shipCity || ''}</span></div>
            <div class="detail-row"><span class="label">Postcode</span><span class="value">${o.shipPostcode || ''}</span></div>
            <div class="detail-row"><span class="label">Payment</span><span class="value">${o.paymentMethod || 'Bank Transfer'}</span></div>
        </div>
        <div class="detail-section">
            <h3>Order Summary</h3>
            <div class="items-list">${items}</div>
            <div class="detail-row" style="margin-top:16px"><span class="label">Subtotal</span><span class="value">${fmt(o.subtotal)}</span></div>
            <div class="detail-row"><span class="label">Shipping</span><span class="value">${fmt(o.shipping)}</span></div>
            <div class="detail-row"><span class="label">VAT</span><span class="value">${fmt(o.vat)}</span></div>
            ${o.discount > 0 ? `<div class="detail-row"><span class="label">Discount</span><span class="value" style="color:#2ECC71">-${fmt(o.discount)}</span></div>` : ''}
            <div class="detail-row" style="border-top:1px solid #2A2A3E;margin-top:10px;padding-top:10px">
                <span class="label" style="font-size:26px;font-weight:700">Total</span>
                <span class="value" style="font-size:30px;color:#C9A84C">${fmt(o.total)}</span>
            </div>
        </div>`;

    showPage('detail');
    state.prevPage = state.currentPage;
}

// ── POLLING FOR NEW ORDERS ────────────────────────────────────────
async function pollOrders() {
    try {
        const res = await api('/api/admin/orders');
        const orders = (res.data && res.data.orders) || res.data || [];
        const newOrders = orders.filter(o => !state.knownOrderIds.has(o.id));
        if (newOrders.length > 0 && state.knownOrderIds.size > 0) {
            newOrders.forEach(o => {
                showNotification(
                    `New Order — ${o.orderNumber}`,
                    `${o.customerEmail} · ${fmt(o.total)}`
                );
            });
        }
        orders.forEach(o => state.knownOrderIds.add(o.id));
        localStorage.setItem('izale_known_orders', JSON.stringify([...state.knownOrderIds]));
        // Refresh current page data silently
        if (state.currentPage === 'orders') {
            state.orders = orders;
            renderOrdersTable();
        }
        if (state.currentPage === 'dashboard') loadDashboard();
    } catch(e) {}
}

// ── ENTER MAIN APP ────────────────────────────────────────────────
function enterMain() {
    showScreen('main-screen');
    state.focusGroup = 'sidebar';
    state.focusIndex = 0;
    updateSidebarFocus();
    startClock();
    loadDashboard();
    // Poll every 30 seconds
    state.pollTimer = setInterval(pollOrders, 30000);
    // Initial poll to seed known IDs
    pollOrders();
}

// ── KEYBOARD / REMOTE NAVIGATION ──────────────────────────────────
// Samsung TV remote key codes
const KEY = {
    UP: 38, DOWN: 40, LEFT: 37, RIGHT: 39,
    ENTER: 13, BACK: 10009,
    RED: 403, GREEN: 404, YELLOW: 405, BLUE: 406,
    NUM0: 48, NUM1: 49, NUM2: 50
};

// Login focusables
const loginFocusables = () => [
    $('login-email'), $('login-password'), $('login-btn')
];

const sidebarItems = () => document.querySelectorAll('.nav-item[data-nav="sidebar"]');

function updateSidebarFocus() {
    sidebarItems().forEach((el, i) => {
        el.classList.toggle('focused', i === state.focusIndex);
    });
}

function updateLoginFocus() {
    const items = loginFocusables();
    items.forEach((el, i) => {
        el.classList.toggle('focused', i === state.focusIndex);
    });
}

// Order row focus for orders page
let orderFocusIndex = 0;
function updateOrderFocus() {
    const rows = document.querySelectorAll('.order-row');
    rows.forEach((r, i) => r.classList.toggle('focused', i === orderFocusIndex));
    if (rows[orderFocusIndex]) {
        rows[orderFocusIndex].scrollIntoView({ block: 'nearest' });
    }
}

document.addEventListener('keydown', e => {
    const k = e.keyCode;

    // ── LOGIN SCREEN ──
    if (state.focusGroup === 'login') {
        const items = loginFocusables();
        if (k === KEY.DOWN) {
            if (items[state.focusIndex]) items[state.focusIndex].classList.remove('focused');
            state.focusIndex = Math.min(state.focusIndex + 1, items.length - 1);
            updateLoginFocus();
            if (items[state.focusIndex]) items[state.focusIndex].focus();
        } else if (k === KEY.UP) {
            if (items[state.focusIndex]) items[state.focusIndex].classList.remove('focused');
            state.focusIndex = Math.max(state.focusIndex - 1, 0);
            updateLoginFocus();
            if (items[state.focusIndex]) items[state.focusIndex].focus();
        } else if (k === KEY.ENTER) {
            if (state.focusIndex === 2) doLogin();
            else { state.focusIndex++; updateLoginFocus(); if (items[state.focusIndex]) items[state.focusIndex].focus(); }
        }
        return;
    }

    // ── MAIN SCREEN ──
    if (k === KEY.BACK) {
        if (state.currentPage === 'detail') {
            showPage(state.prevPage || 'orders');
            state.focusGroup = 'content';
            return;
        }
        if (state.focusGroup === 'content') {
            state.focusGroup = 'sidebar';
            updateSidebarFocus();
            return;
        }
    }

    if (state.focusGroup === 'sidebar') {
        const items = sidebarItems();
        if (k === KEY.DOWN) {
            state.focusIndex = Math.min(state.focusIndex + 1, items.length - 1);
            updateSidebarFocus();
        } else if (k === KEY.UP) {
            state.focusIndex = Math.max(state.focusIndex - 1, 0);
            updateSidebarFocus();
        } else if (k === KEY.RIGHT || k === KEY.ENTER) {
            const focused = items[state.focusIndex];
            if (focused && focused.id === 'nav-logout') { logout(); return; }
            const page = focused ? focused.dataset.page : null;
            if (page) { showPage(page); state.focusGroup = 'content'; orderFocusIndex = 0; }
        }
        return;
    }

    if (state.focusGroup === 'content') {
        if (k === KEY.LEFT) {
            state.focusGroup = 'sidebar';
            updateSidebarFocus();
            return;
        }
        // Order row navigation
        if (state.currentPage === 'orders') {
            const rows = document.querySelectorAll('.order-row');
            if (k === KEY.DOWN) {
                orderFocusIndex = Math.min(orderFocusIndex + 1, rows.length - 1);
                updateOrderFocus();
            } else if (k === KEY.UP) {
                orderFocusIndex = Math.max(orderFocusIndex - 1, 0);
                updateOrderFocus();
            } else if (k === KEY.ENTER) {
                openOrderDetail(orderFocusIndex);
            }
        }
    }
});

// ── BUTTON LISTENERS ──────────────────────────────────────────────
$('login-btn').addEventListener('click', doLogin);
$('nav-logout').addEventListener('click', logout);

document.querySelectorAll('.nav-item[data-page]').forEach(el => {
    el.addEventListener('click', () => showPage(el.dataset.page));
});

// ── TIZEN REMOTE REGISTRATION ─────────────────────────────────────
function registerTizenKeys() {
    if (window.tizen && tizen.tvinputdevice) {
        try {
            tizen.tvinputdevice.registerKey('Back');
            tizen.tvinputdevice.registerKey('ColorF0Red');
            tizen.tvinputdevice.registerKey('ColorF1Green');
        } catch(e) {}
    }
}

// ── INIT ──────────────────────────────────────────────────────────
window.addEventListener('load', () => {
    if (window.dbg) dbg('load event fired, token=' + (state.token ? 'yes' : 'no'));
    registerTizenKeys();
    if (state.token) {
        if (window.dbg) dbg('entering main with saved token');
        enterMain();
    } else {
        showScreen('login-screen');
        $('login-error').textContent = 'Signing in...';
        $('login-error').style.color = '#C9A84C';
        doLogin();
    }
});
