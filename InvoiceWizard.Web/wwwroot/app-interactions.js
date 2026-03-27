window.invoiceWizardUi = window.invoiceWizardUi || {
    ensureInitialized() {
        if (window.__invoiceWizardUiInitialized) {
            return;
        }

        window.__invoiceWizardUiInitialized = true;
        const modalState = new WeakMap();

        document.addEventListener("touchstart", event => {
            const modal = event.target.closest(".modal-card");
            if (!modal) {
                return;
            }

            const touch = event.touches[0];
            modalState.set(modal, {
                startY: touch.clientY,
                closed: false,
                active: modal.scrollTop <= 0
            });
        }, { passive: true });

        document.addEventListener("touchmove", event => {
            const modal = event.target.closest(".modal-card");
            if (!modal) {
                return;
            }

            const state = modalState.get(modal);
            if (!state || state.closed || !state.active || modal.scrollTop > 0) {
                return;
            }

            const touch = event.touches[0];
            const deltaY = touch.clientY - state.startY;
            if (deltaY < 90) {
                return;
            }

            state.closed = true;
            const backdrop = modal.closest(".modal-backdrop");
            if (backdrop) {
                backdrop.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
            }
        }, { passive: true });
    },

    clickById(id) {
        document.getElementById(id)?.click();
    }
};
