// Loads the published smoke app in a real browser and waits in real time for Blazor to render.
// Headless --dump-dom with --virtual-time-budget captured the page before the native .wasm finished compiling.
import { chromium } from 'playwright-core';

const [url, executablePath] = process.argv.slice(2);
const failurePattern = /Unhandled exception rendering component|crit: Microsoft\.AspNetCore\.Components\.WebAssembly|TypeInitialization_Type|DllNotFound|EntryPointNotFound/;
const failures = [];

const browser = await chromium.launch({ executablePath, headless: true });
try {
    const page = await browser.newPage();
    page.on('pageerror', error => failures.push(`Page error: ${error.message}`));
    page.on('requestfailed', request => failures.push(`Request failed: ${request.url()} (${request.failure()?.errorText})`));
    page.on('console', message => {
        if (message.type() === 'error' || failurePattern.test(message.text())) {
            failures.push(`Console ${message.type()}: ${message.text()}`);
        }
    });

    // Wait for Blazor's own ready marker rather than the load event; slower runners need well over 30 seconds for the native .wasm.
    await page.goto(url, { waitUntil: 'commit', timeout: 60000 });
    await page.waitForSelector('[data-aot-smoke-ready="true"]', { timeout: 120000 });
    await page.waitForLoadState('networkidle');

    if (failurePattern.test(await page.content())) {
        failures.push('The rendered page reports an unhandled Blazor exception.');
    }
}
catch (error) {
    failures.push(`The smoke app did not reach its ready marker: ${error.message}`);
}
finally {
    await browser.close();
}

if (failures.length > 0) {
    console.error(failures.join('\n'));
    process.exit(1);
}

console.log('Browser smoke app rendered without errors.');
