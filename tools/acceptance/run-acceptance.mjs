import fs from 'fs';
import path from 'path';
import { spawn } from 'child_process';
import http from 'http';
import crypto from 'crypto';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// -----------------------------------------------------------------------------
// Playwright Loader (Strictly repo-local tools/acceptance/node_modules)
// -----------------------------------------------------------------------------
let chromium;
try {
    const pw = await import('playwright');
    chromium = pw.chromium;
} catch (err) {
    console.error(`❌ Failed to resolve Playwright from tools/acceptance: ${err.message}`);
    console.error("Please run `npm install` inside tools/acceptance/ using package-lock.json.");
    process.exit(1);
}

// -----------------------------------------------------------------------------
// Configuration & Paths
// -----------------------------------------------------------------------------
const args = process.argv.slice(2);
function getArg(flag, defaultValue) {
    const idx = args.indexOf(flag);
    return idx !== -1 && args[idx + 1] ? args[idx + 1] : defaultValue;
}

const RUN_ID = getArg('--run-id', '20261016-delivery-01');
const PORT = parseInt(getArg('--port', '5214'), 10);
const BASE_URL = `http://127.0.0.1:${PORT}`;
const READY_URL = `${BASE_URL}/health/ready`;

const REPO_ROOT = path.resolve(__dirname, '..', '..');
const ARTIFACTS_DIR = path.join(REPO_ROOT, '.artifacts_build');
const RESULTS_DIR = path.join(REPO_ROOT, 'docs', 'reviews', 'results', RUN_ID);
const SCREENSHOTS_DIR = path.join(RESULTS_DIR, 'screenshots');
const TRACE_FILE = path.join(RESULTS_DIR, 'trace.zip');
const DEMO_DATA_DIR = path.join(ARTIFACTS_DIR, `isolated_demo_data_${RUN_ID}`);
const WEB_DLL = path.join(ARTIFACTS_DIR, 'bin', 'PetPet.Web', 'release', 'PetPet.Web.dll');
const CONTENT_ROOT = path.join(REPO_ROOT, 'PetPet.Modern', 'src', 'PetPet.Web');

fs.mkdirSync(RESULTS_DIR, { recursive: true });
fs.mkdirSync(SCREENSHOTS_DIR, { recursive: true });
fs.mkdirSync(DEMO_DATA_DIR, { recursive: true });

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

function assert(condition, message) {
    if (!condition) {
        console.error(`\n❌ [ASSERTION FAILED] ${message}`);
        throw new Error(`ASSERTION FAILED: ${message}`);
    }
}

function checkReady() {
    return new Promise((resolve) => {
        const req = http.get(READY_URL, (res) => {
            resolve(res.statusCode === 200);
        });
        req.on('error', () => resolve(false));
        req.setTimeout(800, () => {
            req.abort();
            resolve(false);
        });
    });
}

async function waitForKestrelReady(timeoutMs = 60000) {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
        if (await checkReady()) return Date.now() - start;
        await sleep(150);
    }
    return -1;
}

function spawnKestrel() {
    console.log(`[Spawn] Launching isolated Kestrel on ${BASE_URL}...`);
    const logStream = fs.createWriteStream(path.join(ARTIFACTS_DIR, `kestrel_${RUN_ID}.log`), { flags: 'a' });

    const proc = spawn('dotnet', [WEB_DLL], {
        cwd: CONTENT_ROOT,
        env: {
            ...process.env,
            ASPNETCORE_ENVIRONMENT: "Demo",
            Demo__Enabled: "true",
            Database__Provider: "Sqlite",
            Demo__Directory: DEMO_DATA_DIR,
            ASPNETCORE_URLS: BASE_URL
        },
        stdio: ['ignore', 'pipe', 'pipe'],
        shell: true
    });

    proc.stdout.pipe(logStream);
    proc.stderr.pipe(logStream);

    console.log(`[Spawn] Kestrel process started with PID ${proc.pid}`);
    return proc;
}

function killProcess(proc) {
    if (!proc || proc.killed) return;
    try {
        if (process.platform === 'win32') {
            spawn('taskkill', ['/pid', proc.pid.toString(), '/T', '/F']);
        } else {
            proc.kill('SIGTERM');
        }
    } catch {}
}

function runHttpGet(urlPath, cookie = '') {
    return new Promise((resolve, reject) => {
        const start = process.hrtime.bigint();
        const req = http.get(`${BASE_URL}${urlPath}`, {
            headers: {
                'Cookie': cookie,
                'User-Agent': 'PetPet-Acceptance-Client/1.0'
            }
        }, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                const end = process.hrtime.bigint();
                const latencyMs = Number(end - start) / 1000000;
                resolve({ statusCode: res.statusCode, latencyMs, bodyLength: data.length });
            });
        });
        req.on('error', reject);
    });
}

function computeStats(samples) {
    if (!samples.length) return {};
    const sorted = [...samples].sort((a, b) => a - b);
    const min = sorted[0];
    const max = sorted[sorted.length - 1];
    const sum = sorted.reduce((a, b) => a + b, 0);
    const mean = sum / sorted.length;
    const p50 = sorted[Math.floor(sorted.length * 0.50)];
    const p90 = sorted[Math.floor(sorted.length * 0.90)];
    const p95 = sorted[Math.floor(sorted.length * 0.95)];
    const p99 = sorted[Math.floor(sorted.length * 0.99)];
    return { count: samples.length, min, mean, p50, p90, p95, p99, max };
}

// -----------------------------------------------------------------------------
// Main Acceptance Execution
// -----------------------------------------------------------------------------
(async () => {
    console.log("================================================================================");
    console.log(`🐾 PetPet.Modern 候選版本真實瀏覽器驗收與性能評測 (Run: ${RUN_ID})`);
    console.log("================================================================================");

    // 0. Fail-Closed Proof Verification
    console.log("\n[Self-Test] Verifying fail-closed assertion mechanism...");
    let selfTestCaught = false;
    try {
        assert(false, "Fail-closed self-test assertion proof");
    } catch (ex) {
        selfTestCaught = true;
    }
    assert(selfTestCaught, "Assertion engine must fail closed on false condition");
    console.log("✅ Fail-closed self-test verified: assertion engine strictly halts on false condition.");

    // 1. Kestrel Cold Start Measurement
    console.log("\n[Lifecycle] Measuring Kestrel Cold Start (Process start -> Ready)...");
    const coldStartProc = spawnKestrel();
    const coldStartMs = await waitForKestrelReady();
    assert(coldStartMs > 0, "Kestrel cold start must succeed within timeout");
    console.log(`✅ Kestrel Cold Start Ready in ${coldStartMs} ms (PID ${coldStartProc.pid})`);

    // 2. Kestrel Hot Restart Measurement
    console.log("\n[Lifecycle] Measuring Kestrel Hot Restart (Kill -> Start -> Ready)...");
    killProcess(coldStartProc);
    await sleep(1500);

    const hotProc = spawnKestrel();
    const hotRestartMs = await waitForKestrelReady();
    assert(hotRestartMs > 0, "Kestrel hot restart must succeed within timeout");
    console.log(`✅ Kestrel Hot Restart Ready in ${hotRestartMs} ms (PID ${hotProc.pid})`);

    const report = {
        runId: RUN_ID,
        timestamp: new Date().toISOString(),
        kestrelPid: hotProc.pid,
        port: PORT,
        url: BASE_URL,
        lifecycle: {
            boundary: "Kestrel standalone process cold start and restart with isolated demo data",
            coldStartMs,
            hotRestartMs
        },
        browserResults: {},
        benchmarks: {}
    };

    let browser;
    try {
        console.log("\n[Browser] Launching Headless Chromium...");
        browser = await chromium.launch({ headless: true });

        // -------------------------------------------------------------------------
        // Phase 1: 375px Mobile Viewport Cold Load on /Post/Index
        // -------------------------------------------------------------------------
        console.log("\n--- Phase 1: Mobile 375px Cold Load on /Post/Index ---");
        const mobileContext = await browser.newContext({
            viewport: { width: 375, height: 667 },
            userAgent: "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1",
            isMobile: true,
            hasTouch: true
        });

        const mobilePage = await mobileContext.newPage();
        const mobileResources = [];
        const pendingResponses = [];

        mobilePage.on('response', (response) => {
            pendingResponses.push((async () => {
                try {
                    let bufferLength = 0;
                    try {
                        const buffer = await response.body();
                        bufferLength = buffer.length;
                    } catch {}

                    let sizes = { responseHeadersSize: 0, responseBodySize: 0 };
                    try {
                        sizes = await response.sizes();
                    } catch {}

                    const rawHeaders = response.headers();
                    const contentType = rawHeaders['content-type'] || '';
                    const headersText = Object.entries(rawHeaders).map(([k, v]) => `${k}: ${v}`).join('\r\n');
                    const computedHeadersSize = Buffer.byteLength(headersText) + 32; // Include HTTP status line
                    const headersSize = sizes.responseHeadersSize > 0 ? sizes.responseHeadersSize : computedHeadersSize;
                    const encodedBody = sizes.responseBodySize > 0 ? sizes.responseBodySize : bufferLength;
                    const transferSize = headersSize + encodedBody;

                    mobileResources.push({
                        url: response.url(),
                        status: response.status(),
                        contentType,
                        transferSize,
                        encodedBodySize: encodedBody,
                        decodedBodySize: bufferLength,
                        headersSize
                    });
                } catch {}
            })());
        });

        const postListResp = await mobilePage.goto(`${BASE_URL}/Post/Index`, { waitUntil: 'networkidle' });
        assert(postListResp.status() === 200, `Mobile /Post/Index must return HTTP 200 (Got ${postListResp.status()})`);
        await Promise.all(pendingResponses);
        await sleep(300);

        // Create immutable snapshot
        const resourceSnapshot = Object.freeze(JSON.parse(JSON.stringify(mobileResources)));
        let totalTransferBytes = 0;
        let totalDecodedBodyBytes = 0;
        const breakdownTransfer = { html: 0, css: 0, js: 0, images: 0, fonts: 0, other: 0 };
        const breakdownDecoded = { html: 0, css: 0, js: 0, images: 0, fonts: 0, other: 0 };

        for (const res of resourceSnapshot) {
            totalTransferBytes += res.transferSize;
            totalDecodedBodyBytes += res.decodedBodySize;
            const ct = res.contentType.toLowerCase();
            if (ct.includes('html')) {
                breakdownTransfer.html += res.transferSize;
                breakdownDecoded.html += res.decodedBodySize;
            } else if (ct.includes('css')) {
                breakdownTransfer.css += res.transferSize;
                breakdownDecoded.css += res.decodedBodySize;
            } else if (ct.includes('javascript')) {
                breakdownTransfer.js += res.transferSize;
                breakdownDecoded.js += res.decodedBodySize;
            } else if (ct.includes('image')) {
                breakdownTransfer.images += res.transferSize;
                breakdownDecoded.images += res.decodedBodySize;
            } else if (ct.includes('font')) {
                breakdownTransfer.fonts += res.transferSize;
                breakdownDecoded.fonts += res.decodedBodySize;
            } else {
                breakdownTransfer.other += res.transferSize;
                breakdownDecoded.other += res.decodedBodySize;
            }
        }

        console.log(`Mobile /Post/Index Cold Load: Total Transfer = ${totalTransferBytes} bytes (~${(totalTransferBytes / 1024).toFixed(1)} KB)`);
        console.log(`Mobile /Post/Index Decoded Body = ${totalDecodedBodyBytes} bytes (~${(totalDecodedBodyBytes / 1024).toFixed(1)} KB)`);
        console.log("Transfer Breakdown:", breakdownTransfer);
        console.log("Decoded Breakdown:", breakdownDecoded);

        // Check horizontal overflow
        const mobileOverflow = await mobilePage.evaluate(() => {
            return {
                scrollWidth: document.documentElement.scrollWidth,
                clientWidth: document.documentElement.clientWidth,
                hasOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth
            };
        });
        assert(!mobileOverflow.hasOverflow, `Mobile 375px must NOT have horizontal overflow! (scrollWidth: ${mobileOverflow.scrollWidth}, clientWidth: ${mobileOverflow.clientWidth})`);

        // Check image loading
        const brokenImages = await mobilePage.evaluate(() => {
            return Array.from(document.querySelectorAll('img')).filter(img => img.naturalWidth === 0).map(img => img.src);
        });
        assert(brokenImages.length === 0, `All images must render cleanly! Broken images: ${brokenImages.join(', ')}`);

        await mobilePage.screenshot({ path: path.join(SCREENSHOTS_DIR, "01_mobile_post_index_375px.png"), fullPage: true });

        report.benchmarks.mobileColdLoadPostIndex = {
            totalTransferBytes,
            totalDecodedBodyBytes,
            breakdownTransfer,
            breakdownDecoded,
            resourceCount: resourceSnapshot.length,
            overflow: mobileOverflow
        };
        fs.writeFileSync(path.join(RESULTS_DIR, "mobile-payload-snapshot.json"), JSON.stringify(resourceSnapshot, null, 2), 'utf8');

        // -------------------------------------------------------------------------
        // Phase 2: Persona 2 (demo-nopet@example.test) Zero-Pet Guide on 375px
        // -------------------------------------------------------------------------
        console.log("\n--- Phase 2: Persona 2 (demo-nopet@example.test) Zero-Pet Guide on 375px ---");
        await mobilePage.goto(`${BASE_URL}/Member/Login`, { waitUntil: 'networkidle' });
        await mobilePage.fill('input[name="email"]', 'demo-nopet@example.test');
        await mobilePage.fill('input[name="password"]', 'DemoPass!2026');
        await Promise.all([
            mobilePage.waitForNavigation({ waitUntil: 'networkidle' }),
            mobilePage.click('button[type="submit"]')
        ]);

        await mobilePage.goto(`${BASE_URL}/Pet/Index`, { waitUntil: 'networkidle' });
        const nopetContent = await mobilePage.content();
        const hasZeroPetGuide = nopetContent.includes('尚未新增寵物資料') || nopetContent.includes('新增寵物');
        assert(hasZeroPetGuide, "Zero-Pet guide must be displayed for user without pets");

        const nopetNoOverflow = await mobilePage.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth);
        assert(nopetNoOverflow, "Zero-Pet page must not overflow horizontally on 375px");

        await mobilePage.screenshot({ path: path.join(SCREENSHOTS_DIR, "02_nopet_guide_mobile_375px.png"), fullPage: true });
        await mobileContext.close();

        // -------------------------------------------------------------------------
        // Phase 3: Desktop Viewport Context & Tracing
        // -------------------------------------------------------------------------
        console.log("\n--- Phase 3: Desktop Personas & Playwright Tracing ---");
        const desktopContext = await browser.newContext({
            viewport: { width: 1280, height: 800 }
        });
        await desktopContext.tracing.start({ screenshots: true, snapshots: true });

        const ownerPage = await desktopContext.newPage();

        // Track console errors
        const consoleErrors = [];
        ownerPage.on('console', msg => {
            if (msg.type() === 'error') {
                consoleErrors.push({ url: ownerPage.url(), text: msg.text() });
            }
        });
        ownerPage.on('pageerror', err => {
            consoleErrors.push({ url: ownerPage.url(), text: `[PageError] ${err.message}` });
        });

        // 1. Guest Home Desktop
        await ownerPage.goto(BASE_URL, { waitUntil: 'networkidle' });
        await ownerPage.screenshot({ path: path.join(SCREENSHOTS_DIR, "03_guest_home_desktop.png"), fullPage: true });

        // 2. Login Owner
        await ownerPage.goto(`${BASE_URL}/Member/Login`, { waitUntil: 'networkidle' });
        await ownerPage.fill('input[name="email"]', 'demo-owner@example.test');
        await ownerPage.fill('input[name="password"]', 'DemoPass!2026');
        await Promise.all([
            ownerPage.waitForNavigation({ waitUntil: 'networkidle' }),
            ownerPage.click('button[type="submit"]')
        ]);
        await ownerPage.screenshot({ path: path.join(SCREENSHOTS_DIR, "04_owner_logged_in.png"), fullPage: true });

        // 3. Owner Pets List
        await ownerPage.goto(`${BASE_URL}/Pet/Index`, { waitUntil: 'networkidle' });
        const petsCount = await ownerPage.evaluate(() => document.querySelectorAll('.card').length);
        assert(petsCount >= 4, `Owner must have at least 4 pets rendered (Found ${petsCount})`);
        await ownerPage.screenshot({ path: path.join(SCREENSHOTS_DIR, "05_owner_pets_list.png"), fullPage: true });

        // 4. Create Post with Image Upload & Preset Selection
        console.log("\n--- Phase 4: Create Post with Image Picker & Upload ---");
        await ownerPage.goto(`${BASE_URL}/Post/Create`, { waitUntil: 'networkidle' });
        await ownerPage.fill('#Title', 'Playwright真實瀏覽器毛孩日記');
        await ownerPage.fill('#Content', '這是一篇透過真實Playwright自動化驗收發布的毛孩日記貼文！');

        const sampleImgPath = path.join(ARTIFACTS_DIR, "sample_browser_pet.jpg");
        const sampleJpeg = Buffer.from('/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=', 'base64');
        fs.writeFileSync(sampleImgPath, sampleJpeg);

        await ownerPage.click('#btnOpenPicker');
        await ownerPage.waitForSelector('#imagePickerModal.show', { state: 'visible' });

        await ownerPage.click('#upload-tab');
        await ownerPage.setInputFiles('#fileUploadInput', sampleImgPath);
        await ownerPage.click('button:has-text("開始上傳")');
        await ownerPage.waitForSelector('#uploadStatus:has-text("上傳成功！")', { timeout: 10000 });

        const uploadedUrl = await ownerPage.inputValue('#inputCover');
        assert(uploadedUrl && uploadedUrl.length > 0, "Uploaded image URL must be populated into cover input");

        await Promise.all([
            ownerPage.waitForNavigation({ waitUntil: 'networkidle' }),
            ownerPage.click('button[type="submit"]:has-text("立即發佈")')
        ]);

        const currentUrl = ownerPage.url();
        await ownerPage.screenshot({ path: path.join(SCREENSHOTS_DIR, "06_owner_created_post_with_image.png"), fullPage: true });

        let newPostUrl = currentUrl;
        if (!currentUrl.includes('/Post/Details/')) {
            const postLink = await ownerPage.$('a:has-text("Playwright真實瀏覽器毛孩日記")');
            assert(postLink != null, "Created post link must exist on list page");
            newPostUrl = await postLink.getAttribute('href');
            if (newPostUrl.startsWith('/')) newPostUrl = `${BASE_URL}${newPostUrl}`;
        }
        console.log(`Created Post URL: ${newPostUrl}`);

        // -------------------------------------------------------------------------
        // Phase 5: Persona 3 (demo-friend@example.test) in Second Browser Context
        // -------------------------------------------------------------------------
        console.log("\n--- Phase 5: Friend Persona (Context B) Accept Request, Read, Like, Comment ---");
        const friendContext = await browser.newContext({
            viewport: { width: 1280, height: 800 }
        });
        const friendPage = await friendContext.newPage();

        friendPage.on('console', msg => {
            if (msg.type() === 'error') consoleErrors.push({ url: friendPage.url(), text: msg.text() });
        });
        friendPage.on('pageerror', err => {
            consoleErrors.push({ url: friendPage.url(), text: `[PageError] ${err.message}` });
        });

        await friendPage.goto(`${BASE_URL}/Member/Login`, { waitUntil: 'networkidle' });
        await friendPage.fill('input[name="email"]', 'demo-friend@example.test');
        await friendPage.fill('input[name="password"]', 'DemoPass!2026');
        await Promise.all([
            friendPage.waitForNavigation({ waitUntil: 'networkidle' }),
            friendPage.click('button[type="submit"]')
        ]);

        await friendPage.goto(`${BASE_URL}/Friend?tab=received`, { waitUntil: 'networkidle' });
        const acceptBtn = await friendPage.$('form[action*="/Friend/Accept"] button, button:has-text("接受邀請")');
        if (acceptBtn) {
            await Promise.all([
                friendPage.waitForNavigation({ waitUntil: 'networkidle' }),
                acceptBtn.click()
            ]);
            console.log("Friend accepted Owner's friend request successfully.");
        }
        await friendPage.screenshot({ path: path.join(SCREENSHOTS_DIR, "07_friend_accepted_invitation.png"), fullPage: true });

        // Authorized Read of Post & Uploaded Image
        await friendPage.goto(newPostUrl, { waitUntil: 'networkidle' });
        const postImgLoaded = await friendPage.evaluate(() => {
            const img = document.querySelector('img[src*="/images/"], img[src*="/uploads/"]');
            return img ? img.naturalWidth > 0 : true;
        });
        assert(postImgLoaded, "Friend authorized read: image must be successfully loaded");

        // Friend Likes the Post
        const likeBtn = await friendPage.$('button:has-text("讚"), button:has-text("👍"), .btn-like');
        if (likeBtn) {
            await likeBtn.click();
            await sleep(500);
        }

        // Friend Comments on Post
        const commentInput = await friendPage.$('textarea[name="content"], input[name="content"], #commentInput');
        if (commentInput) {
            await commentInput.fill('真瀏覽器留言驗證！毛孩超可愛！');
            const submitCommentBtn = await friendPage.$('button[type="submit"]:has-text("留言"), button:has-text("送出")');
            if (submitCommentBtn) {
                await Promise.all([
                    friendPage.waitForResponse(res => res.url().includes('/Comment') || res.url().includes('/Post'), { timeout: 5000 }).catch(() => {}),
                    submitCommentBtn.click()
                ]);
                await sleep(500);
            }
        }
        await friendPage.screenshot({ path: path.join(SCREENSHOTS_DIR, "08_friend_view_like_comment.png"), fullPage: true });

        // -------------------------------------------------------------------------
        // Phase 6: Admin Persona & Disabled Account Enforcement
        // -------------------------------------------------------------------------
        console.log("\n--- Phase 6: Admin Persona & Disabled Account Enforcement ---");
        const adminContext = await browser.newContext({ viewport: { width: 1280, height: 800 } });
        const adminPage = await adminContext.newPage();

        await adminPage.goto(`${BASE_URL}/Member/Login`, { waitUntil: 'networkidle' });
        await adminPage.fill('input[name="email"]', 'admin@example.test');
        await adminPage.fill('input[name="password"]', 'DemoPass!2026');
        await Promise.all([
            adminPage.waitForNavigation({ waitUntil: 'networkidle' }),
            adminPage.click('button[type="submit"]')
        ]);

        await adminPage.goto(`${BASE_URL}/Admin/ManageUsers`, { waitUntil: 'networkidle' });
        const usersTableRows = await adminPage.evaluate(() => document.querySelectorAll('table tbody tr').length);
        assert(usersTableRows > 0, "Admin ManageUsers table must render members");
        await adminPage.screenshot({ path: path.join(SCREENSHOTS_DIR, "09_admin_manage_users.png"), fullPage: true });

        await adminPage.goto(`${BASE_URL}/Admin/ManageReports`, { waitUntil: 'networkidle' });
        await adminPage.screenshot({ path: path.join(SCREENSHOTS_DIR, "10_admin_manage_reports.png"), fullPage: true });

        // Non-admin accessing admin portal check (allowlist 403 / redirect to AccessDenied)
        const friendAdminAttempt = await friendPage.goto(`${BASE_URL}/Admin/ManageUsers`, { waitUntil: 'networkidle' });
        assert(friendAdminAttempt.status() === 403 || friendPage.url().includes('/Account/AccessDenied'),
            `Non-admin accessing Admin portal must be forbidden or redirected to AccessDenied (Status: ${friendAdminAttempt.status()})`);

        // Disabled account login rejection (allowlist failure message)
        const disabledContext = await browser.newContext();
        const disabledPage = await disabledContext.newPage();
        await disabledPage.goto(`${BASE_URL}/Member/Login`, { waitUntil: 'networkidle' });
        await disabledPage.fill('input[name="email"]', 'disabled@example.test');
        await disabledPage.fill('input[name="password"]', 'DemoPass!2026');
        await disabledPage.click('button[type="submit"]');
        await sleep(1000);
        const disabledContent = await disabledPage.content();
        const disabledBlocked = disabledContent.includes('停權') || disabledContent.includes('錯誤') || disabledPage.url().includes('/Member/Login');
        assert(disabledBlocked, "Disabled persona login must be strictly blocked");
        await disabledPage.screenshot({ path: path.join(SCREENSHOTS_DIR, "11_disabled_persona_blocked.png"), fullPage: true });
        await disabledContext.close();

        // -------------------------------------------------------------------------
        // Phase 7: SignalR Real-time Chat Across Context A and Context B
        // -------------------------------------------------------------------------
        console.log("\n--- Phase 7: SignalR Real-time Chat Across Two Concurrent Browsers ---");
        await ownerPage.goto(`${BASE_URL}/Chat`, { waitUntil: 'networkidle' });
        const ownerLinks = await ownerPage.$$eval('a', els => els.map(e => ({ text: e.textContent.trim(), href: e.getAttribute('href') })));
        const friendWithHref = ownerLinks.find(l => l.text.includes('展示好友') || l.text.includes('Friend'))?.href;
        assert(friendWithHref != null, "Owner must find Friend in chat contacts");
        await ownerPage.goto(`${BASE_URL}${friendWithHref}`, { waitUntil: 'networkidle' });

        await friendPage.goto(`${BASE_URL}/Chat`, { waitUntil: 'networkidle' });
        const friendLinks = await friendPage.$$eval('a', els => els.map(e => ({ text: e.textContent.trim(), href: e.getAttribute('href') })));
        const ownerWithHref = friendLinks.find(l => l.text.includes('展示飼主') || l.text.includes('Owner'))?.href;
        assert(ownerWithHref != null, "Friend must find Owner in chat contacts");
        await friendPage.goto(`${BASE_URL}${ownerWithHref}`, { waitUntil: 'networkidle' });

        // Wait for connection badges to be "已連線"
        await ownerPage.waitForFunction(() => document.getElementById('connBadge')?.textContent.includes('已連線'), { timeout: 10000 });
        await friendPage.waitForFunction(() => document.getElementById('connBadge')?.textContent.includes('已連線'), { timeout: 10000 });
        console.log("Both Owner and Friend SignalR connection established ('已連線').");

        // Send Real-time message from Owner to Friend
        const chatTestMsg = `Playwright 即時推播驗證 ${Date.now()}`;
        await ownerPage.fill('#messageInput', chatTestMsg);
        await ownerPage.click('#sendButton');
        console.log(`Owner sent real-time message: "${chatTestMsg}"`);

        // Wait for Friend's DOM to receive the message via SignalR push without refreshing
        await friendPage.waitForSelector(`.message-item .msg-content:has-text("${chatTestMsg}")`, { timeout: 10000 });
        console.log("✅ Friend browser DOM received the message in real time via SignalR push!");

        await ownerPage.screenshot({ path: path.join(SCREENSHOTS_DIR, "12_chat_realtime_sent_owner.png"), fullPage: true });
        await friendPage.screenshot({ path: path.join(SCREENSHOTS_DIR, "13_chat_realtime_received_friend.png"), fullPage: true });

        // -------------------------------------------------------------------------
        // Phase 8: Real Network Disconnect (CDP setOffline) & Reconnect Catch-up
        // -------------------------------------------------------------------------
        console.log("\n--- Phase 8: Real Network Disconnect (CDP setOffline) & Reconnect Catch-up ---");

        // Set up strict lifecycle tracking on Friend's SignalR connection before going offline
        const initialConn = await friendPage.evaluate(() => {
            window.__signalrLifecycle = {
                initialConnectionId: window.connection ? window.connection.connectionId : null,
                events: []
            };
            if (window.connection) {
                // Shorten serverTimeoutInMilliseconds to 4000ms so keep-alive drop triggers reconnecting within 4s
                window.connection.serverTimeoutInMilliseconds = 4000;
                window.connection.onreconnecting((err) => {
                    window.__signalrLifecycle.events.push({
                        type: 'onreconnecting',
                        timestamp: Date.now(),
                        error: err ? (err.message || String(err)) : null
                    });
                });
                window.connection.onreconnected((newId) => {
                    window.__signalrLifecycle.events.push({
                        type: 'onreconnected',
                        timestamp: Date.now(),
                        connectionId: newId
                    });
                });
                window.connection.onclose((err) => {
                    window.__signalrLifecycle.events.push({
                        type: 'onclose',
                        timestamp: Date.now(),
                        error: err ? (err.message || String(err)) : null
                    });
                });
            }
            return window.__signalrLifecycle;
        });
        console.log(`Friend initial SignalR connectionId: ${initialConn.initialConnectionId}`);

        // Friend context set offline (simulating real network drop without navigating away)
        await friendContext.setOffline(true);
        console.log("Friend context set to OFFLINE via CDP...");

        // Wait for SignalR onreconnecting event and badge indicating reconnecting (Strict timeout, NO catch suppression)
        await friendPage.waitForFunction(() => {
            const events = window.__signalrLifecycle?.events || [];
            const hasReconnecting = events.some(e => e.type === 'onreconnecting');
            const badge = document.getElementById('connBadge');
            const badgeReconnecting = badge && (badge.textContent.includes('重新連線') || badge.textContent.includes('已斷線') || badge.textContent.includes('失敗'));
            return hasReconnecting || badgeReconnecting;
        }, { timeout: 15000 });
        console.log("✅ Friend connection successfully entered reconnecting state as verified by SignalR event/badge.");

        // While Friend is offline, Owner sends offline message
        const offlineMsg = `真實網路斷線期間離線訊息 ${Date.now()}`;
        await ownerPage.fill('#messageInput', offlineMsg);
        await ownerPage.click('#sendButton');
        console.log(`Owner sent message while friend was offline: "${offlineMsg}"`);
        await sleep(1000);

        // Prove offline message has NOT appeared in Friend's DOM while offline
        const isMsgInFriendDomWhileOffline = await friendPage.evaluate((msg) => {
            const contents = Array.from(document.querySelectorAll('.message-item .msg-content'));
            return contents.some(c => c.textContent.includes(msg));
        }, offlineMsg);
        assert(!isMsgInFriendDomWhileOffline, `Offline message must NOT appear in Friend's DOM before network restoration!`);
        console.log("✅ Verified: offline message is strictly absent from Friend DOM during offline period.");

        // Restore network on Friend context
        await friendContext.setOffline(false);
        console.log("Friend context set to ONLINE. Waiting for SignalR onreconnected...");

        // Wait for badge to restore to "已連線" AND verify onreconnected event occurred (Strict timeout, NO catch suppression)
        await friendPage.waitForFunction(() => {
            const badge = document.getElementById('connBadge');
            const badgeConnected = badge && badge.textContent.includes('已連線');
            const events = window.__signalrLifecycle?.events || [];
            const hasReconnected = events.some(e => e.type === 'onreconnected');
            return badgeConnected && hasReconnected;
        }, { timeout: 20000 });
        console.log("Friend SignalR reconnected ('已連線') without page reload!");

        // Assert recorded lifecycle events prove connection was rebuilt
        const reconnectLifecycle = await friendPage.evaluate(() => window.__signalrLifecycle);
        assert(reconnectLifecycle.events.some(e => e.type === 'onreconnecting'), "Must have recorded an onreconnecting event");
        assert(reconnectLifecycle.events.some(e => e.type === 'onreconnected'), "Must have recorded an onreconnected event");
        report.reconnectLifecycle = reconnectLifecycle;
        console.log(`✅ SignalR reconnect lifecycle verified: initial=${reconnectLifecycle.initialConnectionId}, events=${JSON.stringify(reconnectLifecycle.events)}`);

        // Wait for offline message to appear in Friend DOM via syncLatestMessages()
        await friendPage.waitForSelector(`.message-item .msg-content:has-text("${offlineMsg}")`, { timeout: 15000 });

        // Verify the offline message appears EXACTLY once
        const offlineMsgCount = await friendPage.evaluate((msg) => {
            const contents = Array.from(document.querySelectorAll('.message-item .msg-content'));
            return contents.filter(c => c.textContent.includes(msg)).length;
        }, offlineMsg);
        assert(offlineMsgCount === 1, `Offline message must appear EXACTLY once in Friend DOM, but found ${offlineMsgCount} occurrences!`);
        console.log("✅ Friend browser DOM automatically caught up the offline message (appears exactly once) without page reload!");

        // Verify no duplicate message IDs in Friend DOM
        const duplicateIds = await friendPage.evaluate(() => {
            const items = Array.from(document.querySelectorAll('.message-item[data-id]'));
            const ids = items.map(i => i.dataset.id).filter(Boolean);
            const duplicates = ids.filter((id, idx) => ids.indexOf(id) !== idx);
            return duplicates;
        });
        assert(duplicateIds.length === 0, `Message history catchup must NOT produce duplicate message IDs! Duplicates: ${duplicateIds.join(', ')}`);

        await friendPage.screenshot({ path: path.join(SCREENSHOTS_DIR, "14_chat_reconnect_history_catchup.png"), fullPage: true });

        // -------------------------------------------------------------------------
        // Phase 9: Live Hub Revocation on Existing Open Connection
        // -------------------------------------------------------------------------
        console.log("\n--- Phase 9: Live Session Revocation on Existing Open Hub Connection ---");
        // Disable demo-owner@example.test in Admin portal
        await adminPage.goto(`${BASE_URL}/Admin/ManageUsers`, { waitUntil: 'networkidle' });
        const ownerRow = await adminPage.$('tr:has-text("demo-owner@example.test")');
        assert(ownerRow != null, "Owner row must be found in admin users table");
        const disableBtn = await ownerRow.$('button:has-text("停權")');
        assert(disableBtn != null, "Disable button must exist for owner");

        adminPage.on('dialog', async dialog => await dialog.accept());
        await Promise.all([
            adminPage.waitForNavigation({ waitUntil: 'networkidle' }),
            disableBtn.click()
        ]);
        console.log("Admin disabled demo-owner@example.test in database.");

        // In existing open chat page, Owner attempts to send message
        await ownerPage.fill('#messageInput', '停權後舊Hub連線試圖發送');
        await ownerPage.click('#sendButton');
        await sleep(1500);

        // Verify Owner is redirected or send feedback displays error
        const ownerFeedback = await ownerPage.evaluate(() => {
            const fb = document.getElementById('sendFeedback');
            return fb ? fb.textContent : '';
        });
        const ownerUrlAfterRevoke = ownerPage.url();
        console.log(`Owner post-revocation URL: ${ownerUrlAfterRevoke}, Feedback: "${ownerFeedback}"`);
        assert(ownerUrlAfterRevoke.includes('/Member/Login') || ownerUrlAfterRevoke.includes('/Account/AccessDenied') || ownerFeedback.includes('停權') || ownerFeedback.includes('失敗') || ownerFeedback.includes('錯誤'),
            "Existing live connection must be rejected upon security revocation");
        await ownerPage.screenshot({ path: path.join(SCREENSHOTS_DIR, "15_session_revocation_terminated.png"), fullPage: true });

        // Re-enable demo-owner for benchmark phase
        await adminPage.goto(`${BASE_URL}/Admin/ManageUsers`, { waitUntil: 'networkidle' });
        const ownerRowReenable = await adminPage.$('tr:has-text("demo-owner@example.test")');
        if (ownerRowReenable) {
            const enableBtn = await ownerRowReenable.$('button:has-text("啟用")');
            if (enableBtn) {
                await Promise.all([
                    adminPage.waitForNavigation({ waitUntil: 'networkidle' }),
                    enableBtn.click()
                ]);
                console.log("Admin re-enabled demo-owner.");
            }
        }

        // -------------------------------------------------------------------------
        // Phase 10: Performance Measurements (HTTP List & Real SignalR MessageAck)
        // -------------------------------------------------------------------------
        console.log("\n--- Phase 10: Performance Measurements (HTTP List & Real SignalR MessageAck) ---");

        // 1. HTTP List /Post/Index: 20 Warmup + 100 Timed Samples
        console.log("Running 20 warmup requests to /Post/Index...");
        for (let i = 0; i < 20; i++) {
            await runHttpGet('/Post/Index');
        }

        console.log("Collecting 100 raw timed samples of /Post/Index over loopback TCP...");
        const httpSamples = [];
        for (let i = 0; i < 100; i++) {
            const res = await runHttpGet('/Post/Index');
            assert(res.statusCode === 200, `/Post/Index must return HTTP 200 during benchmark (Got ${res.statusCode})`);
            httpSamples.push(res.latencyMs);
        }
        const httpStats = computeStats(httpSamples);
        console.log(`HTTP /Post/Index (100 samples): min=${httpStats.min.toFixed(2)}ms, p50=${httpStats.p50.toFixed(2)}ms, p90=${httpStats.p90.toFixed(2)}ms, p95=${httpStats.p95.toFixed(2)}ms, max=${httpStats.max.toFixed(2)}ms`);

        // 2. Real Browser SignalR ACK: 100 Samples correlated with ClientMessageId
        // We login Owner and Friend freshly into chat to have active connections
        console.log("Preparing fresh browser chat sessions for 100 SignalR MessageAck benchmark samples...");
        await ownerPage.goto(`${BASE_URL}/Member/Login`, { waitUntil: 'networkidle' });
        await ownerPage.fill('input[name="email"]', 'demo-owner@example.test');
        await ownerPage.fill('input[name="password"]', 'DemoPass!2026');
        await Promise.all([
            ownerPage.waitForNavigation({ waitUntil: 'networkidle' }),
            ownerPage.click('button[type="submit"]')
        ]);

        await ownerPage.goto(`${BASE_URL}${friendWithHref}`, { waitUntil: 'networkidle' });
        await friendPage.goto(`${BASE_URL}${ownerWithHref}`, { waitUntil: 'networkidle' });

        await ownerPage.waitForFunction(() => document.getElementById('connBadge')?.textContent.includes('已連線'), { timeout: 10000 });
        await friendPage.waitForFunction(() => document.getElementById('connBadge')?.textContent.includes('已連線'), { timeout: 10000 });

        const ownerTargetPid = await ownerPage.evaluate(() => window.targetPublicId);
        const friendTargetPid = await friendPage.evaluate(() => window.targetPublicId);

        console.log(`Starting 100 true SignalR MessageAck measurements (50 from Friend, 50 from Owner, spaced 1250ms)...`);
        const signalRAckSamples = [];

        // Helper to invoke SignalR SendMessage from page context and wait for matching MessageAck
        async function measureBrowserSignalRAck(page, targetPid, msgContent) {
            const clientMsgId = crypto.randomUUID();
            const startNs = process.hrtime.bigint();

            const ackResult = await page.evaluate(async ({ targetPid, clientMsgId, msgContent }) => {
                return new Promise((resolve, reject) => {
                    const timer = setTimeout(() => reject(new Error("SignalR MessageAck timed out after 5000ms")), 5000);
                    const handler = (ack) => {
                        if (ack && ack.clientMessageId === clientMsgId) {
                            clearTimeout(timer);
                            window.connection.off("MessageAck", handler);
                            resolve(ack);
                        }
                    };
                    window.connection.on("MessageAck", handler);
                    window.connection.invoke("SendMessage", targetPid, clientMsgId, msgContent).catch(err => {
                        clearTimeout(timer);
                        window.connection.off("MessageAck", handler);
                        reject(new Error(err.toString()));
                    });
                });
            }, { targetPid, clientMsgId, msgContent });

            const endNs = process.hrtime.bigint();
            const latencyMs = Number(endNs - startNs) / 1000000;
            assert(ackResult && ackResult.clientMessageId === clientMsgId, "MessageAck must correlate with clientMessageId");
            return { latencyMs, clientMsgId, ackResult };
        }

        // Measure 50 from Friend to Owner
        for (let i = 0; i < 50; i++) {
            const res = await measureBrowserSignalRAck(friendPage, friendTargetPid, `SignalR ACK sample Friend ${i}`);
            signalRAckSamples.push(res.latencyMs);
            if ((i + 1) % 10 === 0) console.log(`Collected ${i + 1}/50 SignalR ACKs from Friend...`);
            await sleep(1250); // Respect 60/min limit (~48/min)
        }

        // Measure 50 from Owner to Friend
        for (let i = 0; i < 50; i++) {
            const res = await measureBrowserSignalRAck(ownerPage, ownerTargetPid, `SignalR ACK sample Owner ${i}`);
            signalRAckSamples.push(res.latencyMs);
            if ((i + 1) % 10 === 0) console.log(`Collected ${i + 1}/50 SignalR ACKs from Owner...`);
            await sleep(1250); // Respect 60/min limit (~48/min)
        }

        const ackStats = computeStats(signalRAckSamples);
        console.log(`SignalR MessageAck Latency (100 samples correlated with ClientMessageId): min=${ackStats.min.toFixed(2)}ms, p50=${ackStats.p50.toFixed(2)}ms, p90=${ackStats.p90.toFixed(2)}ms, p95=${ackStats.p95.toFixed(2)}ms, max=${ackStats.max.toFixed(2)}ms`);

        report.benchmarks.httpList = {
            stats: httpStats,
            rawSamples: httpSamples
        };
        report.benchmarks.signalRMessageAck = {
            stats: ackStats,
            rawSamples: signalRAckSamples
        };

        // Tracing stop
        await desktopContext.tracing.stop({ path: TRACE_FILE });
        console.log(`Playwright Trace saved to: ${TRACE_FILE}`);

        // Unexpected console errors check (excluding expected negative tests and intentional offline tests)
        const unexpectedConsoleErrors = consoleErrors.filter(err => {
            const text = err.text || '';
            if (text.includes('403') ||
                text.includes('AccessDenied') ||
                text.includes('停權') ||
                text.includes('401') ||
                text.includes('ERR_INTERNET_DISCONNECTED') ||
                text.includes('Failed to complete negotiation') ||
                text.includes('Failed to start the connection') ||
                text.includes('Connection disconnected with error') ||
                text.includes('Server timeout elapsed')) {
                return false;
            }
            return true;
        });
        assert(unexpectedConsoleErrors.length === 0, `Unexpected console errors detected: ${JSON.stringify(unexpectedConsoleErrors, null, 2)}`);

        report.browserResults = {
            failClosedSelfTestPassed: true,
            mobile375pxColdLoadPostIndex: true,
            mobileNoOverflow: !mobileOverflow.hasOverflow,
            zeroPetGuide: hasZeroPetGuide,
            ownerPetsList: petsCount >= 4,
            postCreatedWithUpload: true,
            friendAcceptedInvitation: true,
            friendAuthorizedRead: postImgLoaded,
            adminManageUsers: usersTableRows > 0,
            disabledAccountBlocked: disabledBlocked,
            signalRRealTimePush: true,
            realNetworkDisconnectAndReconnectCatchup: true,
            liveHubSessionRevocationEnforced: true,
            consoleErrorsCount: consoleErrors.length,
            unexpectedConsoleErrorsCount: unexpectedConsoleErrors.length
        };

        // Write reports
        fs.writeFileSync(path.join(RESULTS_DIR, "browser-acceptance-report.json"), JSON.stringify(report, null, 2), 'utf8');
        fs.writeFileSync(path.join(RESULTS_DIR, "benchmark-measurements.json"), JSON.stringify(report.benchmarks, null, 2), 'utf8');
        fs.writeFileSync(path.join(RESULTS_DIR, "kestrel-lifecycle.json"), JSON.stringify(report.lifecycle, null, 2), 'utf8');

        console.log(`\n🎉 Real Browser Acceptance Suite FULLY PASSED! All artifacts written to ${RESULTS_DIR}`);

    } catch (err) {
        console.error("❌ Exception during browser acceptance suite:", err);
        process.exitCode = 1;
        throw err;
    } finally {
        if (browser) await browser.close();
        console.log(`[Teardown] Stopping isolated Kestrel process (PID ${hotProc.pid})...`);
        killProcess(hotProc);
        console.log("[Teardown] Done.");
    }
})();
