(function () {
    let deferredPrompt = null;

    async function registerServiceWorker() {
        if (!("serviceWorker" in navigator)) {
            return;
        }

        try {
            await navigator.serviceWorker.register("/service-worker.js");
        } catch {
            // Keep the app usable even if service worker registration fails.
        }
    }

    window.addEventListener("beforeinstallprompt", event => {
        event.preventDefault();
        deferredPrompt = event;
    });

    window.addEventListener("appinstalled", () => {
        deferredPrompt = null;
    });

    function isIos() {
        return /iphone|ipad|ipod/i.test(window.navigator.userAgent);
    }

    function isStandalone() {
        return window.matchMedia("(display-mode: standalone)").matches || window.navigator.standalone === true;
    }

    window.invoiceWizardPwa = {
        install: async function () {
            if (isStandalone()) {
                return "installed";
            }

            if (deferredPrompt) {
                await deferredPrompt.prompt();
                const choice = await deferredPrompt.userChoice;
                deferredPrompt = null;
                return choice && choice.outcome === "accepted" ? "accepted" : "prompted";
            }

            if (isIos()) {
                return "ios";
            }

            return "unavailable";
        }
    };

    registerServiceWorker();
})();
