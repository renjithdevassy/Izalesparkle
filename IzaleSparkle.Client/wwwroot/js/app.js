// ================================================================
//  IZALE SPARKLE — Mobile-First PWA JS  |  .NET 10 Blazor WASM
// ================================================================

// ── CUSTOM CURSOR (desktop only) ─────────────────────────────
window.initCursor = function () {
    if (window.matchMedia('(pointer:coarse)').matches) return;
    if (document.getElementById('izale-cursor')) return;

    const dot  = Object.assign(document.createElement('div'), { id: 'izale-cursor' });
    const ring = Object.assign(document.createElement('div'), { id: 'izale-cursor-ring' });
    dot.style.cssText  = 'position:fixed;width:10px;height:10px;border-radius:50%;background:#C8973A;pointer-events:none;z-index:9999;transform:translate(-50%,-50%);box-shadow:0 0 8px rgba(200,151,58,.5);transition:transform .15s';
    ring.style.cssText = 'position:fixed;width:36px;height:36px;border-radius:50%;border:1.5px solid #C8973A;pointer-events:none;z-index:9998;transform:translate(-50%,-50%);transition:width .35s,height .35s,opacity .3s';
    document.body.append(dot, ring);

    let mx = 0, my = 0, rx = 0, ry = 0;
    document.addEventListener('mousemove', e => { mx = e.clientX; my = e.clientY; dot.style.left = mx + 'px'; dot.style.top = my + 'px'; });
    (function loop() { rx += (mx-rx)*.14; ry += (my-ry)*.14; ring.style.left=rx+'px'; ring.style.top=ry+'px'; requestAnimationFrame(loop); })();

    let lastSp = 0;
    document.addEventListener('mousemove', e => {
        const now = Date.now();
        if (now - lastSp < 90) return;
        lastSp = now;
        const sp = document.createElement('div');
        sp.style.cssText = `position:fixed;left:${e.clientX}px;top:${e.clientY}px;width:5px;height:5px;border-radius:50%;background:#E4B655;pointer-events:none;z-index:9990;transform:translate(-50%,-50%);animation:sparkleFade .8s ease forwards`;
        document.body.appendChild(sp);
        setTimeout(() => sp.remove(), 800);
    });
    document.addEventListener('mouseover', e => {
        if (e.target.closest('a,button,input,select,textarea,.product-card'))
            { ring.style.width='54px'; ring.style.height='54px'; ring.style.opacity='.45'; }
    });
    document.addEventListener('mouseout', e => {
        if (e.target.closest('a,button,input,select,textarea,.product-card'))
            { ring.style.width='36px'; ring.style.height='36px'; ring.style.opacity='1'; }
    });
};

// ── SCROLL REVEAL ─────────────────────────────────────────────
window.initScrollReveal = function () {
    // Disconnect any previous observer so repeated calls (after navigation) don't pile up.
    if (window._revealObs) window._revealObs.disconnect();
    const obs = new IntersectionObserver(entries => {
        entries.forEach(e => { if (e.isIntersecting) { e.target.classList.add('visible'); obs.unobserve(e.target); } });
    }, { threshold: 0.06, rootMargin: '0px 0px -40px 0px' });
    // Only observe elements that haven't been revealed yet.
    document.querySelectorAll('.reveal:not(.visible),.reveal-left:not(.visible),.reveal-right:not(.visible)')
        .forEach(el => obs.observe(el));
    window._revealObs = obs;
};

// ── NAVBAR SCROLL SHADOW ──────────────────────────────────────
window.initNavScroll = function () {
    const update = () => document.querySelector('.navbar')?.classList.toggle('scrolled', window.scrollY > 40);
    window.addEventListener('scroll', update, { passive: true });
    update();
};

// ── STICKY BAR (product detail) ───────────────────────────────
window.initStickyBar = function (anchorId) {
    const anchor = document.getElementById(anchorId);
    const bar    = document.querySelector('.sticky-bar');
    if (!anchor || !bar) return;
    const obs = new IntersectionObserver(([entry]) => {
        bar.classList.toggle('visible', !entry.isIntersecting);
    }, { threshold: 0, rootMargin: '-80px 0px 0px 0px' });
    obs.observe(anchor);
};

// ── TOUCH SWIPE (product image gallery) ──────────────────────
window.initTouchSwipe = function (elementId, dotnet) {
    const el = document.getElementById(elementId);
    if (!el) return;
    let startX = 0, startY = 0;
    el.addEventListener('touchstart', e => {
        startX = e.touches[0].clientX;
        startY = e.touches[0].clientY;
    }, { passive: true });
    el.addEventListener('touchend', e => {
        const dx = e.changedTouches[0].clientX - startX;
        const dy = e.changedTouches[0].clientY - startY;
        if (Math.abs(dx) < 30 || Math.abs(dy) > Math.abs(dx)) return; // ignore vertical swipes
        if (dx < 0) dotnet.invokeMethodAsync('SwipeLeft');
        else        dotnet.invokeMethodAsync('SwipeRight');
    }, { passive: true });
};

// ── TOAST ─────────────────────────────────────────────────────
window.showToastJS = function (msg) {
    let t = document.getElementById('global-toast');
    if (!t) { t = Object.assign(document.createElement('div'), { id: 'global-toast' }); document.body.appendChild(t); }
    t.textContent = msg;
    t.classList.add('show');
    clearTimeout(t._timer);
    t._timer = setTimeout(() => t.classList.remove('show'), 2800);
};

// ── SCROLL TO ELEMENT ─────────────────────────────────────────
window.scrollToElement = function (selector) {
    document.querySelector(selector)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
};

// ── ELEMENT RECT (zoom) ───────────────────────────────────────
window.getElementRect = function (id) {
    const el = document.getElementById(id);
    if (!el) return { width: 500, height: 500, left: 0, top: 0 };
    const r = el.getBoundingClientRect();
    return { width: r.width, height: r.height, left: r.left, top: r.top };
};

// ── PULL-TO-REFRESH PREVENTION (iOS) ─────────────────────────
window.preventPullToRefresh = function () {
    let startY = 0;
    document.addEventListener('touchstart', e => { startY = e.touches[0].clientY; }, { passive: true });
    document.addEventListener('touchmove', e => {
        if (window.scrollY === 0 && e.touches[0].clientY > startY) e.preventDefault();
    }, { passive: false });
};

// ── HAPTIC FEEDBACK (if available) ───────────────────────────
window.hapticLight  = () => navigator.vibrate?.( 10);
window.hapticMedium = () => navigator.vibrate?.( 30);
window.hapticHeavy  = () => navigator.vibrate?.([50, 30, 50]);

// ── SHARE API ────────────────────────────────────────────────
window.nativeShare = async function (title, text, url) {
    if (!navigator.share) return false;
    try { await navigator.share({ title, text, url }); return true; }
    catch { return false; }
};

// ── CLIPBOARD ────────────────────────────────────────────────
window.copyToClipboard = function (text) {
    return navigator.clipboard?.writeText(text).then(() => true).catch(() => false) ?? Promise.resolve(false);
};

// ── PWA INSTALL PROMPT ────────────────────────────────────────
window.deferredInstallPrompt = null;

window.addPwaInstallListener = function (dotnet) {
    window.addEventListener('pwa-installable', () => dotnet.invokeMethodAsync('OnPwaInstallable'));
    if (window.deferredInstallPrompt) dotnet.invokeMethodAsync('OnPwaInstallable');
};

window.triggerPwaInstall = async function () {
    if (!window.deferredInstallPrompt) return;
    window.deferredInstallPrompt.prompt();
    await window.deferredInstallPrompt.userChoice;
    window.deferredInstallPrompt = null;
};

// ── SW UPDATE ────────────────────────────────────────────────
window.addSwUpdateListener = function (dotnet) {
    window.addEventListener('sw-update-available', () => dotnet.invokeMethodAsync('OnSwUpdateAvailable'));
};

// ── RE-OBSERVE after Blazor navigation ───────────────────────
window.refreshAfterRender = function () { window.initScrollReveal(); };

// ── INIT ─────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    window.initCursor();
    window.initScrollReveal();
    window.initNavScroll();
    window.preventPullToRefresh();
});

// Capture install prompt early
window.addEventListener('beforeinstallprompt', e => {
    e.preventDefault();
    window.deferredInstallPrompt = e;
    window.dispatchEvent(new CustomEvent('pwa-installable'));
});
window.addEventListener('appinstalled', () => { window.deferredInstallPrompt = null; });
